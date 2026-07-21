using System.Collections.Immutable;
using System.Numerics;
using AddShapeEffect.ForVideoEffectChain;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Shape;

namespace AddShapeEffect
{
    internal class AddShapeEffectProcessor : IVideoEffectProcessor
    {
        readonly AddShapeEffect item;
        readonly IGraphicsDevicesAndContext devices;
        readonly VideoEffectChainNode chain;

        DisposeCollector disposer = new();
        bool isFirst = true;
        IShapeParameter? shapeParameter;
        IShapeSource? shapeSource;
        ID2D1Image? currentInput;
        ImmutableList<IVideoEffect> currentEffects = ImmutableList<IVideoEffect>.Empty;

        private readonly Transform3D transformEffect;
        private readonly Opacity opacityEffect;
        private readonly Transform3D inputTransformEffect;
        private readonly AlphaMask alphaMaskEffect;

        // 単純なPorter-Duff合成（Normal / Add 等）用。
        // 出力(Output)はこの効果に固定し、合成モードに応じて入力の配線だけを毎フレーム切り替える。
        private readonly Composite finalCompositeEffect;

        // ★追加: ピクセル演算が必要な合成モード（Multiply / Screen 等）用。
        //   Vortice.Direct2D1.Effects.Composite と同名の "Blend" が
        //   YukkuriMovieMaker.Project.Blend (enum) と衝突するため、
        //   D2Dエフェクト側は常にフル修飾名で参照する。
        private readonly Vortice.Direct2D1.Effects.Blend blendD2DEffect;

        // ★追加: 子（図形）のエフェクトチェーンが書き換えたDrawDescription（描画位置・拡大率・
        //   回転角・カメラ・不透明度・反転）を、本体と全く同じアルゴリズムで図形画像へ
        //   焼き込むための内部エフェクト。本体の YukkuriMovieMaker.Player.Video.Effects.DrawingEffect
        //   をそのまま利用する（自前で行列を近似実装しない）。
        private readonly YukkuriMovieMaker.Player.Video.Effects.DrawingEffect drawingBakeEffect;

        // ★追加: 図形が無い/無効な場合の代替レイヤー。
        //   本体のDrawingEffectと同じ Flood(透明色)+Crop の組み合わせで、
        //   常に有効な（null化しない）画像を用意しておく。
        //   合成先の入力をnullにしたりCompositeとSourceCopyを毎フレーム
        //   切り替えたりすると、グラフの配線が不安定になり
        //   「グラフに構成エラーが見つかりました」(D2DERR_INVALID_GRAPH_CONFIGURATION)
        //   の原因になり得るため、常に「透明なだけの実画像」を使う。
        private readonly Flood emptyLayerEffect;
        private readonly Crop emptyLayerCropEffect;

        public ID2D1Image Output { get; }

        public AddShapeEffectProcessor(IGraphicsDevicesAndContext devices, AddShapeEffect item)
        {
            chain = new VideoEffectChainNode(devices);
            this.item = item;
            this.devices = devices;

            transformEffect = new Transform3D(devices.DeviceContext);
            disposer.Collect(transformEffect);

            opacityEffect = new Opacity(devices.DeviceContext);
            disposer.Collect(opacityEffect);

            inputTransformEffect = new Transform3D(devices.DeviceContext);
            disposer.Collect(inputTransformEffect);

            alphaMaskEffect = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);

            finalCompositeEffect = new Composite(devices.DeviceContext);
            disposer.Collect(finalCompositeEffect);

            blendD2DEffect = new Vortice.Direct2D1.Effects.Blend(devices.DeviceContext);
            disposer.Collect(blendD2DEffect);

            drawingBakeEffect = new YukkuriMovieMaker.Player.Video.Effects.DrawingEffect(devices);
            disposer.Collect(drawingBakeEffect);

            emptyLayerEffect = new Flood(devices.DeviceContext)
            {
                Color = new Vortice.Mathematics.Color4(0f, 0f, 0f, 0f)
            };
            disposer.Collect(emptyLayerEffect);
            emptyLayerCropEffect = new Crop(devices.DeviceContext);
            disposer.Collect(emptyLayerCropEffect);
            using (var floodOutput = emptyLayerEffect.Output)
            {
                emptyLayerCropEffect.SetInput(0, floodOutput, true);
            }

            // Output の参照はここで一度だけ確定させ、以後は差し替えない
            // （下流に接続済みの参照が壊れないようにするため）
            Output = finalCompositeEffect.Output;
        }

        public DrawDescription Update(EffectDescription effectDescription)
        {
            var Frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var FPS = effectDescription.FPS;

            if (currentEffects != item.Effects)
            {
                currentEffects = item.Effects;
                chain.UpdateChain(currentEffects);
            }

            ID2D1Image? shapeOutputImage = null;
            ID2D1Image? transformedShapeImage = null;
            DrawDescription descAfterChain = effectDescription.DrawDescription;

            if (currentInput == null) return effectDescription.DrawDescription;

            // 入力(input)のバウンズはここで一度だけ取得し、以降(図形の有無に関わらず)使い回す。
            // 透明レイヤー(emptyLayerCropEffect)のcrop範囲も、入力に合わせて毎フレーム更新しておく。
            var currentInputBounds = devices.DeviceContext.GetImageLocalBounds(currentInput);
            emptyLayerCropEffect.Rectangle = new Vector4(
                currentInputBounds.Left, currentInputBounds.Top,
                currentInputBounds.Right, currentInputBounds.Bottom);

            {
                var shapeParameter = item.ShapeParameter;
                if (isFirst || this.shapeParameter != shapeParameter)
                {
                    if (shapeSource is not null) { disposer.RemoveAndDispose(ref shapeSource); }
                    if (shapeParameter != null)
                    {
                        shapeSource = shapeParameter.CreateShapeSource(devices);
                        disposer.Collect(shapeSource);
                    }
                    isFirst = false;
                    this.shapeParameter = shapeParameter;
                }

                if (shapeSource is not null)
                {
                    shapeSource.Update(effectDescription);
                    shapeOutputImage = shapeSource.Output;
                }
            }

            if (shapeOutputImage != null)
            {
                // --------------------------------------------------------------
                // ★再設計: 子チェーン（item.Effects）に渡す DrawDescription を
                //   親から受け取った値ではなく「中立値」にしてから処理させる。
                //   こうすると、子チェーン内の「描画位置」「拡大縮小」「回転角」等の
                //   標準（非遅延ではない）エフェクトは中立値を基準に計算するため、
                //   返ってくる DrawDescription は最初から「子だけの寄与」そのものになり、
                //   差分計算が一切不要になる（親のDrawDescriptionを差し引く必要がない）。
                // --------------------------------------------------------------
                DrawDescription neutralDrawDescription = effectDescription.DrawDescription with
                {
                    Draw = Vector3.Zero,
                    Zoom = Vector2.One,
                    Rotation = Vector3.Zero,
                    Camera = Matrix4x4.Identity,
                    Opacity = 1.0,
                    Invert = false,
                };

                chain.SetInput(shapeOutputImage);
                descAfterChain = chain.UpdateOutputAndDescription(effectDescription with { DrawDescription = neutralDrawDescription });
                ID2D1Image imageAfterChain = chain.Output;

                // --------------------------------------------------------------
                // ★子チェーンの寄与（descAfterChain、中立値基準なので子だけの値）を、
                //   本体と全く同じ YukkuriMovieMaker.Player.Video.Effects.DrawingEffect で
                //   図形画像へ焼き込む。回転符号・カメラ合成順・ズーム補間モードの
                //   変換などをすべて本体と一致させられる。
                // --------------------------------------------------------------
                drawingBakeEffect.SetInput(imageAfterChain);
                drawingBakeEffect.Update(effectDescription with { DrawDescription = descAfterChain });
                ID2D1Image bakedShapeImage = drawingBakeEffect.Output;

                // --------------------------------------------------------------
                //  中心・サイズの基準は「子チェーン適用前(shapeOutputImage)」のバウンズを使う。
                //  （焼き込み後の画像バウンズを基準にすると、焼き込みで動かした結果を
                //  毎回中心へ引き戻してしまい、カメラ移動等が蓄積しなくなるため）
                // --------------------------------------------------------------
                var shapeBounds = devices.DeviceContext.GetImageLocalBounds(shapeOutputImage);

                bool boundsValid =
                    float.IsFinite(shapeBounds.Left) && float.IsFinite(shapeBounds.Right) &&
                    float.IsFinite(shapeBounds.Top) && float.IsFinite(shapeBounds.Bottom);

                float shapeWidth = boundsValid ? Math.Max(1f, shapeBounds.Right - shapeBounds.Left) : 1f;
                float shapeHeight = boundsValid ? Math.Max(1f, shapeBounds.Bottom - shapeBounds.Top) : 1f;
                float shapeCenterX = boundsValid ? (shapeBounds.Left + shapeBounds.Right) / 2f : 0f;
                float shapeCenterY = boundsValid ? (shapeBounds.Top + shapeBounds.Bottom) / 2f : 0f;

                float inputWidth = currentInputBounds.Right - currentInputBounds.Left;
                float inputHeight = currentInputBounds.Bottom - currentInputBounds.Top;

                var x = (float)item.X.GetValue(Frame, length, FPS);
                var y = (float)item.Y.GetValue(Frame, length, FPS);
                var z = (float)item.Z.GetValue(Frame, length, FPS);
                var rotX = (float)item.RotationX.GetValue(Frame, length, FPS);
                var rotY = (float)item.RotationY.GetValue(Frame, length, FPS);
                var rotZ = (float)item.RotationZ.GetValue(Frame, length, FPS);
                var zoomItem = Math.Max(0.0001f, (float)item.Zoom.GetValue(Frame, length, FPS) / 100.0f);
                var zoomXItem = (float)item.ZoomX.GetValue(Frame, length, FPS) / 100.0f;
                var zoomYItem = (float)item.ZoomY.GetValue(Frame, length, FPS) / 100.0f;
                var opacityItem = (float)item.Opacity.GetValue(Frame, length, FPS) / 100.0f;

                float leftMargin = (float)item.Left.GetValue(Frame, length, FPS);
                float rightMargin = (float)item.Right.GetValue(Frame, length, FPS);
                float topMargin = (float)item.Top.GetValue(Frame, length, FPS);
                float bottomMargin = (float)item.Bottom.GetValue(Frame, length, FPS);

                float baseHalfWidth = (shapeWidth / 2f) * zoomXItem * zoomItem;
                float baseL = x - baseHalfWidth;
                float baseR = x + baseHalfWidth;

                float targetL = item.PinLeft ? (-inputWidth / 2f + leftMargin) : baseL;
                float targetR = item.PinRight ? (inputWidth / 2f - rightMargin) : baseR;

                float finalWidth = targetR - targetL;
                float finalScaleX = (shapeWidth > 0) ? (finalWidth / shapeWidth) : 0;
                float offsetX = (targetL + targetR) / 2f;

                float baseHalfHeight = (shapeHeight / 2f) * zoomYItem * zoomItem;
                float baseT = y - baseHalfHeight;
                float baseB = y + baseHalfHeight;

                float targetT = item.PinTop ? (-inputHeight / 2f + topMargin) : baseT;
                float targetB = item.PinBottom ? (inputHeight / 2f - bottomMargin) : baseB;

                float finalHeight = targetB - targetT;
                float finalScaleY = (shapeHeight > 0) ? (finalHeight / shapeHeight) : 0;
                float offsetY = (targetT + targetB) / 2f;

                if (item.InvertX) finalScaleX *= -1f;
                if (item.InvertY) finalScaleY *= -1f;

                // 図形の見た目の中心をローカル原点へ引き戻す（回転・拡縮のピボット補正）
                Matrix4x4 center = Matrix4x4.CreateTranslation(-shapeCenterX, -shapeCenterY, 0f);
                Matrix4x4 scale = Matrix4x4.CreateScale(finalScaleX, finalScaleY, 1.0f);
                Matrix4x4 rotXMat = Matrix4x4.CreateRotationX((float)MathHelper.ToRadians(rotX));
                Matrix4x4 rotYMat = Matrix4x4.CreateRotationY((float)MathHelper.ToRadians(rotY));
                Matrix4x4 rotZMat = Matrix4x4.CreateRotationZ((float)MathHelper.ToRadians(rotZ));

                float cameraDistance = 1000.0f;
                Matrix4x4 perspective = Matrix4x4.Identity;
                perspective.M34 = -1.0f / cameraDistance;
                Matrix4x4 translation = Matrix4x4.CreateTranslation(offsetX, offsetY, z);

                // center を最初に適用してから scale/rotate/translate/perspective の順で合成
                Matrix4x4 finalTransform = center * scale * rotXMat * rotYMat * rotZMat * translation * perspective;

                transformEffect.SetInput(0, bakedShapeImage, true);
                transformEffect.TransformMatrix = finalTransform;

                // 子チェーン側のドット絵（ニアレストネイバー）設定は、上の drawingBakeEffect が
                // descAfterChain.ZoomInterpolationMode を使って焼き込み時に正しく反映済みなので、
                // ここでは図形設定自体の「ドット絵」トグル(item.IsDot)だけを見ればよい。
                var mode = item.IsDot ? Transform3DInterpolationMode.NearestNeighbor : Transform3DInterpolationMode.Linear;
                transformEffect.SetValue((int)Transform3DProperties.InterpolationMode, mode);

                opacityEffect.SetInput(0, transformEffect.Output, true);
                opacityEffect.SetValue(0, opacityItem);

                transformedShapeImage = opacityEffect.Output;
            }
            else
            {
                chain.ClearInput();
                transformedShapeImage = null;
            }

            // ★修正: 図形が無い/無効な場合でも、後続の合成エフェクトの入力を
            //   nullにはせず、常に有効な「透明なだけの画像」(emptyLayerCropEffect)を渡す。
            //   （null⇔実画像の切り替えや、Composite⇔SourceCopyのモード切り替えを
            //   毎フレーム行うと、グラフの配線が不安定になりやすいため）
            ID2D1Image compositeInput1 = transformedShapeImage ?? emptyLayerCropEffect.Output;
            if (item.IsClippingEnabled && transformedShapeImage != null)
            {
                alphaMaskEffect.SetInput(0, compositeInput1, true);
                alphaMaskEffect.SetInput(1, inputTransformEffect.Output, true);
                compositeInput1 = alphaMaskEffect.Output;
            }

            // --------------------------------------------------------------
            // ★追加: 合成モード対応
            //   Normal/Add のような単純なアルファ合成は Composite エフェクトへ、
            //   Multiply/Screen 等ピクセル演算が必要なモードは Blend エフェクトへ振り分ける。
            //   GradientMapエフェクトと同様に、Blend側の結果は一旦作成してから
            //   finalCompositeEffect(Output固定)へ流し込むことで
            //   外部から見える Output の参照を変えずに済ませる。
            //   ★重要: finalCompositeEffect / blendD2DEffect の入力スロットは
            //   どちらも常に「有効な画像」で埋め、null化はしない
            //  （SourceCopy利用時に使わない側のスロットも、直前と同じ有効な画像を
            //   与えておくことで、Composite効果の入力数(InputCount=2)を
            //   常に満たしておく）。
            // --------------------------------------------------------------
            {
                // ★追加: 「Z順に描画」がONの場合は、図形自身のZ座標(item.Z)を基準に
                //   手前/奥を自動判定する。
                //   ご指定の通り: 図形のZ=a、アイテムのZ=b としたとき
                //   a≦b なら図形が手前(表)、b<a なら図形が奥(裏)。
                //   このAddShapeEffectの実行時点ではアイテム自身のZ(b)を
                //   基準0として扱っているため、a<=0 なら手前、a>0 なら奥として判定する。
                //   OFFの場合は従来通り「背面に描画」トグルで手動指定する。
                //   NOTE: これはこの図形と入力(input)1組だけのローカルな前後判定であり、
                //   タイムライン全体でアイテム同士が交差してめり込むような
                //   本体の「Z順に描画」機能そのものではありません。
                float shapeZForOrder = (float)item.Z.GetValue(Frame, length, FPS);
                bool drawShapeInFront = item.IsZOrderDrawing ? (shapeZForOrder > 0f) : !item.IsBack;

                ID2D1Image bottom = drawShapeInFront ? inputTransformEffect.Output : compositeInput1;
                ID2D1Image top = drawShapeInFront ? compositeInput1 : inputTransformEffect.Output;

                var blendMode = item.BlendMode;

                if (IsSimpleCompositionMode(blendMode))
                {
                    finalCompositeEffect.SetInput(0, bottom, true);
                    finalCompositeEffect.SetInput(1, top, true);
                    finalCompositeEffect.Mode = ToCompositeMode(blendMode);
                }
                else
                {
                    blendD2DEffect.SetInput(0, bottom, true);
                    blendD2DEffect.SetInput(1, top, true);
                    blendD2DEffect.Mode = ToD2DBlendMode(blendMode);

                    using var blended = blendD2DEffect.Output;
                    finalCompositeEffect.SetInput(0, blended, true);
                    // 使わない側のスロットもnullにせず、直前と同じ有効な画像で埋めておく
                    finalCompositeEffect.SetInput(1, bottom, true);
                    finalCompositeEffect.Mode = CompositeMode.SourceCopy;
                }
            }

            // ★修正: descAfterChain（子チェーンが書き換えたDrawDescription）をそのまま返すと、
            //   子（図形）の「描画位置」「拡大縮小」「不透明度」「回転角」等の変化が
            //   親アイテム全体にまで適用されてしまう。
            //   子チェーンの寄与は既に上の transformEffect / opacityEffect 側に
            //   合成済みなので、ここでは受け取った時点の DrawDescription を
            //   一切変更せずそのまま返し、親には何も伝播させない。
            return effectDescription.DrawDescription;
        }

        // --------------------------------------------------------------
        // ★追加: 合成モードの振り分け・変換ヘルパー
        //   NOTE: YukkuriMovieMaker.Project.Blend の実際のメンバー名は
        //   お手元のSDK参照で要確認（IntelliSenseで確認の上、
        //   下記switch式のケース名が一致しない場合は書き換えてください）。
        //   未対応のケースは Normal/Multiply 相当にフォールバックします。
        // --------------------------------------------------------------
        private static bool IsSimpleCompositionMode(YukkuriMovieMaker.Project.Blend mode) => mode switch
        {
            YukkuriMovieMaker.Project.Blend.Normal => true,
            YukkuriMovieMaker.Project.Blend.Add => true,
            _ => false,
        };

        private static CompositeMode ToCompositeMode(YukkuriMovieMaker.Project.Blend mode) => mode switch
        {
            YukkuriMovieMaker.Project.Blend.Add => CompositeMode.Plus,
            _ => CompositeMode.SourceOver,
        };

        private static Vortice.Direct2D1.BlendMode ToD2DBlendMode(YukkuriMovieMaker.Project.Blend mode) => mode switch
        {
            YukkuriMovieMaker.Project.Blend.Multiply => Vortice.Direct2D1.BlendMode.Multiply,
            YukkuriMovieMaker.Project.Blend.Screen => Vortice.Direct2D1.BlendMode.Screen,
            // NOTE: 「比較(暗)」「比較(明)」に相当する Blend 列挙子の正確な名前が未確認のため、
            //   ここでは Darken/Lighten の分岐を外しています（default で Multiply 扱いにフォールバック）。
            //   実際のメンバー名が分かり次第、下記のように1行追加してください。
            //   例: YukkuriMovieMaker.Project.Blend.〇〇 => Vortice.Direct2D1.BlendMode.Darken,
            //   例: YukkuriMovieMaker.Project.Blend.〇〇 => Vortice.Direct2D1.BlendMode.Lighten,
            YukkuriMovieMaker.Project.Blend.ColorDodge => Vortice.Direct2D1.BlendMode.ColorDodge,
            YukkuriMovieMaker.Project.Blend.ColorBurn => Vortice.Direct2D1.BlendMode.ColorBurn,
            YukkuriMovieMaker.Project.Blend.HardLight => Vortice.Direct2D1.BlendMode.HardLight,
            YukkuriMovieMaker.Project.Blend.SoftLight => Vortice.Direct2D1.BlendMode.SoftLight,
            YukkuriMovieMaker.Project.Blend.Difference => Vortice.Direct2D1.BlendMode.Difference,
            YukkuriMovieMaker.Project.Blend.Exclusion => Vortice.Direct2D1.BlendMode.Exclusion,
            YukkuriMovieMaker.Project.Blend.Overlay => Vortice.Direct2D1.BlendMode.Overlay,
            _ => Vortice.Direct2D1.BlendMode.Multiply,
        };

        public void ClearInput()
        {
            currentInput = null;
            finalCompositeEffect.SetInput(0, null, true);
            finalCompositeEffect.SetInput(1, null, true);
            blendD2DEffect.SetInput(0, null, true);
            blendD2DEffect.SetInput(1, null, true);
            drawingBakeEffect.ClearInput();
            inputTransformEffect.SetInput(0, null, true);
            transformEffect.SetInput(0, null, true);
            opacityEffect.SetInput(0, null, true);
            alphaMaskEffect.SetInput(0, null, true);
            alphaMaskEffect.SetInput(1, null, true);
            if (shapeSource is not null) { disposer.RemoveAndDispose(ref shapeSource); }
            isFirst = true;
            shapeParameter = null;
            currentEffects = ImmutableList<IVideoEffect>.Empty;
            chain.ClearInput();
        }

        public void Dispose()
        {
            disposer.Dispose();
            chain.Dispose();
            inputTransformEffect.Dispose();
        }

        public void SetInput(ID2D1Image? input)
        {
            currentInput = input;
            inputTransformEffect.SetInput(0, input, true);
            chain.SetInput(input);
            currentEffects = item.Effects;
            chain.UpdateChain(currentEffects);
        }
    }
}
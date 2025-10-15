using System.Numerics;
using AddShapeEffect.ForVideoEffectChain;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
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

        private readonly Transform3D transformEffect;
        private readonly Crop cropEffect;
        private readonly Opacity opacityEffect;

        private readonly AffineTransform2D inputTransformEffect;

        private readonly AlphaMask alphaMaskEffect;
        private readonly Composite finalCompositeEffect;


        public ID2D1Image Output { get; }

        public AddShapeEffectProcessor(IGraphicsDevicesAndContext devices, AddShapeEffect item)
        {
            chain = new VideoEffectChainNode(devices);
            this.item = item;
            this.devices = devices;

            transformEffect = new Transform3D(devices.DeviceContext);
            disposer.Collect(transformEffect);

            cropEffect = new Crop(devices.DeviceContext);
            cropEffect.SetInput(0, transformEffect.Output, true);
            disposer.Collect(cropEffect);

            opacityEffect = new Opacity(devices.DeviceContext);
            opacityEffect.SetInput(0, cropEffect.Output, true);
            disposer.Collect(opacityEffect);

            alphaMaskEffect = new AlphaMask(devices.DeviceContext);
            disposer.Collect(alphaMaskEffect);

            inputTransformEffect = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(inputTransformEffect);

            finalCompositeEffect = new Composite(devices.DeviceContext);
            disposer.Collect(finalCompositeEffect);

            Output = finalCompositeEffect.Output;
            disposer.Collect(Output);

        }
        public DrawDescription Update(EffectDescription effectDescription)
        {
            long Frame = effectDescription.ItemPosition.Frame;
            long length = effectDescription.ItemDuration.Frame;
            int FPS = effectDescription.FPS;

            ID2D1Image? shapeOutputImage = null;
            ID2D1Image? transformedShapeImage = null;
            ID2D1Image? maskedShapeImage = null;
            DrawDescription descAfterChain = effectDescription.DrawDescription;

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
                    ID2D1Image current = shapeSource.Output;

                    shapeOutputImage = current;
                }
            }
            if (shapeOutputImage != null)
            {
                chain.SetInput(shapeOutputImage);

                ID2D1Image imageAfterChain = chain.Output;

                var x = (float)item.X.GetValue(Frame, length, FPS);
                var y = (float)item.Y.GetValue(Frame, length, FPS);
                var z = (float)item.Z.GetValue(Frame, length, FPS);
                var rotX = (float)item.RotationX.GetValue(Frame, length, FPS);
                var rotY = (float)item.RotationY.GetValue(Frame, length, FPS);
                var rotZ = (float)item.RotationZ.GetValue(Frame, length, FPS);
                var zoomItem = (float)item.Zoom.GetValue(Frame, length, FPS) / 100.0f;
                var zoomXItem = (float)item.ZoomX.GetValue(Frame, length, FPS) / 100.0f;
                var zoomYItem = (float)item.ZoomY.GetValue(Frame, length, FPS) / 100.0f;
                var opacityItem = (float)item.Opacity.GetValue(Frame, length, FPS) / 100.0f;

                DrawDescription initialDesc;
                initialDesc = new(
                    new Vector3(x, y, z),
                    new Vector2(0, 0),
                    new Vector2(zoomItem * zoomXItem, zoomItem * zoomYItem),
                    new Vector3(rotX, rotY, rotZ),
                    Matrix4x4.Identity,
                    InterpolationMode.Linear,
                    opacityItem,
                    false,
                    []
                );

                chain.UpdateChain(item.Effects);
                DrawDescription newDescription = chain.UpdateOutputAndDescription(effectDescription, initialDesc);

                float finalOpacity = (float)newDescription.Opacity;


                if (item.InvertX) zoomXItem *= -1f;
                if (item.InvertY) zoomYItem *= -1f;
                Matrix4x4 scale = Matrix4x4.CreateScale(
                    newDescription.Zoom.X,
                    newDescription.Zoom.Y,
                    1.0f);
                Matrix4x4 rotXMat = Matrix4x4.CreateRotationX((float)MathHelper.ToRadians(newDescription.Rotation.X));
                Matrix4x4 rotYMat = Matrix4x4.CreateRotationY((float)MathHelper.ToRadians(newDescription.Rotation.Y));
                Matrix4x4 rotZMat = Matrix4x4.CreateRotationZ((float)MathHelper.ToRadians(newDescription.Rotation.Z));

                float cameraDistance = 1000.0f;
                Matrix4x4 perspective = Matrix4x4.Identity;
                perspective.M34 = -1.0f / cameraDistance;

                Matrix4x4 translation = Matrix4x4.CreateTranslation(newDescription.Draw);

                Matrix4x4 finalTransform =
                    scale *
                    rotXMat *
                    rotYMat *
                    rotZMat *
                    translation *
                    newDescription.Camera *
                    perspective;

                transformEffect.SetInput(0, imageAfterChain, true);
                transformEffect.TransformMatrix = finalTransform;

                opacityEffect.SetValue(0, finalOpacity);
                Apply(devices.DeviceContext);

                transformedShapeImage = opacityEffect.Output;
            }
            else
            {
                chain.ClearChain();
                transformedShapeImage = null;
            }
            ID2D1Image? compositeInput1 = transformedShapeImage;
            if (item.IsClippingEnabled && compositeInput1 != null)
            {
                alphaMaskEffect.SetInput(0, compositeInput1, true);
                alphaMaskEffect.SetInput(1, finalCompositeEffect.GetInput(0), true);
                maskedShapeImage = alphaMaskEffect.Output;
                compositeInput1 = maskedShapeImage;
            }
            if (compositeInput1 != null)
            {
                finalCompositeEffect.SetInput(1, compositeInput1, true);
                finalCompositeEffect.Mode = CompositeMode.SourceOver;
            }
            else
            {
                finalCompositeEffect.SetInput(1, null, true);
                finalCompositeEffect.Mode = CompositeMode.SourceCopy;
            }

            return descAfterChain;
        }

        public void ClearInput()
        {
            finalCompositeEffect.SetInput(0, null, true);
            finalCompositeEffect.SetInput(1, null, true);

            inputTransformEffect.SetInput(0, null, true);

            alphaMaskEffect.SetInput(0, null, true);
            alphaMaskEffect.SetInput(1, null, true);
            if (shapeSource is not null) { disposer.RemoveAndDispose(ref shapeSource); }

            isFirst = true;
            shapeParameter = null;
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
            inputTransformEffect.SetInput(0, input, true);

            finalCompositeEffect.SetInput(0, inputTransformEffect.Output, true);

            chain.SetInput(input);
            chain.UpdateChain(item.Effects);
        }

        #region SafeTransform3DHelper
        const float D3D11_FTOI_INSTRUCTION_MAX_INPUT = 2.1474836E+09f;
        const float D3D11_FTOI_INSTRUCTION_MIN_INPUT = -2.1474836E+09f;

        void Apply(ID2D1DeviceContext deviceContext)
        {
            //transform3dエフェクトの出力画像1pxあたりの入力サイズが4096pxを超えるとエラーになる
            //エラー時には出力サイズがD3D11_FTOI_INSTRUCTION_MAX_INPUTになるため、cropエフェクトを使用し入力サイズを4096pxに制限する

            //一旦cropエフェクトの範囲を初期化する
            cropEffect.Rectangle = new Vector4(float.MinValue, float.MinValue, float.MaxValue, float.MaxValue);
            var renderBounds = deviceContext.GetImageLocalBounds(transformEffect.Output);
            if (renderBounds.Left == D3D11_FTOI_INSTRUCTION_MIN_INPUT
                || renderBounds.Top == D3D11_FTOI_INSTRUCTION_MIN_INPUT
                || renderBounds.Right == D3D11_FTOI_INSTRUCTION_MAX_INPUT
                || renderBounds.Bottom == D3D11_FTOI_INSTRUCTION_MAX_INPUT)
            {
                //エラーの場合にのみ入力サイズを制限する
                cropEffect.Rectangle = new Vector4(-2048, -2048, 2048, 2048);
            }
        }
        #endregion
    }
}

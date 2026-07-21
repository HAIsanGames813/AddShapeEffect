using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Shape;

namespace AddShapeEffect
{
    [VideoEffect("図形貼り付け", ["装飾"], [], isAviUtlSupported: false)]
    internal class AddShapeEffect : VideoEffectBase
    {
        public override string Label => "図形貼り付け";

        [Display(GroupName = "描画", Name = "X座標")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Y座標")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Z座標")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "不透明度")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = "描画", Name = "拡大率")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Zoom { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "描画", Name = "拡大率X")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ZoomX { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "描画", Name = "拡大率Y")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ZoomY { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "描画", Name = "X軸回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Y軸回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Z軸回転")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationZ { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "ドット絵")]
        [ToggleSlider]
        public bool IsDot { get => isDot; set => Set(ref isDot, value); }
        bool isDot = false;

        [Display(GroupName = "描画", Name = "左右反転")]
        [ToggleSlider]
        public bool InvertX { get => invertX; set => Set(ref invertX, value); }
        bool invertX = false;

        [Display(GroupName = "描画", Name = "上下反転")]
        [ToggleSlider]
        public bool InvertY { get => invertY; set => Set(ref invertY, value); }
        bool invertY = false;

        // ------------------------------------------------------------
        // ★追加: Z順に描画
        // NOTE: これはタイムライン全体のアイテム間Z順合成（本体の「Z順に描画」機能、
        //   4.55で交差表示が改善されるとされているもの）そのものではありません。
        //   本エフェクトが扱えるのは「この図形」と「入力(input)」1組の前後関係だけなので、
        //   ここでは図形自身のZ座標(下の「Z座標」)が0以下なら手前、0より大きければ奥、
        //   という簡易的なローカル判定に留めています。ONの間は「背面に描画」より
        //   こちらが優先されます。複数アイテムが実際に交差してめり込むような
        //   本格的な描画までは再現できません。
        // ------------------------------------------------------------
        [Display(GroupName = "描画", Name = "Z順に描画")]
        [ToggleSlider]
        public bool IsZOrderDrawing { get => isZOrderDrawing; set => Set(ref isZOrderDrawing, value); }
        bool isZOrderDrawing = false;

        [Display(GroupName = "描画", Name = "背面に描画")]
        [ToggleSlider]
        public bool IsBack { get => isBack; set => Set(ref isBack, value); }
        bool isBack = false;

        // ------------------------------------------------------------
        // ★追加: 合成モード（グラデーションマップエフェクトのBlendModeを参考に追加）
        // NOTE: YukkuriMovieMaker.Project.Blend は本体の合成モードと共通の enum です。
        //       [EnumComboBox] は本体UIでenumドロップダウンに使われる属性名の想定です。
        //       手元のSDK(YukkuriMovieMaker.Controls.dll)でビルドしCS0246/属性未検出になる場合は、
        //       実際に使われているenum用の属性名に置き換えてください。
        // ------------------------------------------------------------
        [Display(GroupName = "合成", Name = "合成モード")]
        [EnumComboBox]
        public YukkuriMovieMaker.Project.Blend BlendMode { get => blendMode; set => Set(ref blendMode, value); }
        YukkuriMovieMaker.Project.Blend blendMode = YukkuriMovieMaker.Project.Blend.Normal;

        [Display(GroupName = "サイズ追従", Name = "左端に固定")]
        [ToggleSlider]
        public bool PinLeft { get => pinLeft; set => Set(ref pinLeft, value); }
        bool pinLeft = false;

        [Display(GroupName = "サイズ追従", Name = "右端に固定")]
        [ToggleSlider]
        public bool PinRight { get => pinRight; set => Set(ref pinRight, value); }
        bool pinRight = false;

        [Display(GroupName = "サイズ追従", Name = "上端に固定")]
        [ToggleSlider]
        public bool PinTop { get => pinTop; set => Set(ref pinTop, value); }
        bool pinTop = false;

        [Display(GroupName = "サイズ追従", Name = "下端に固定")]
        [ToggleSlider]
        public bool PinBottom { get => pinBottom; set => Set(ref pinBottom, value); }
        bool pinBottom = false;

        [Display(GroupName = "余白", Name = "左")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Left { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "余白", Name = "右")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Right { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "余白", Name = "上")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Top { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "余白", Name = "下")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Bottom { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "図形", Name = "クリッピング")]
        [ToggleSlider]
        public bool IsClippingEnabled { get => isClippingEnabled; set => Set(ref isClippingEnabled, value); }
        bool isClippingEnabled = false;

        [Display(GroupName = "図形", Name = "種類")]
        [ShapeTypeComboBox]
        public Type ShapeType { get => shapeType; set => Set(ref shapeType, value); }
        Type shapeType = PluginLoader.GetPrimaryPluginType<IShapePlugin>();

        private Type? oldShapeType;

        [Display(GroupName = "図形", AutoGenerateField = true)]
        public IShapeParameter ShapeParameter { get => shapeParameter; set => Set(ref shapeParameter, value); }
        IShapeParameter shapeParameter = new RectangleShapeParameter(null);

        [Display(GroupName = "図形のエフェクト", Name = "")]
        [VideoEffectSelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public ImmutableList<IVideoEffect> Effects { get => effects; set => Set(ref effects, value); }
        ImmutableList<IVideoEffect> effects = [];

        public override void BeginEdit()
        {
            oldShapeType = ShapeType;
            base.BeginEdit();
        }

        public override async ValueTask EndEditAsync()
        {
            if (ShapeParameter is null || oldShapeType != ShapeType)
            {
                ShapeParameter = ShapeFactory
                    .GetPlugin(ShapeType)
                    .CreateShapeParameter(ShapeParameter?.GetSharedData());
            }
            await base.EndEditAsync();
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new AddShapeEffectProcessor(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => [X, Y, Z, Opacity, Zoom, ZoomX, ZoomY, RotationX, RotationY, RotationZ, Left, Right, Top, Bottom, ShapeParameter, .. Effects];
    }
}
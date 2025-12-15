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

        [Display(GroupName = "描画", Name = "X座標", Description = "横方向の描画位置")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Y座標", Description = "縦方向の描画位置")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Z座標", Description = "奥行きの描画位置")]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "不透明度", Description = "不透明度")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = "描画", Name = "拡大率", Description = "全体の拡大率")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Zoom { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "描画", Name = "拡大率X", Description = "横方向の拡大率")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ZoomX { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "描画", Name = "拡大率Y", Description = "縦方向の拡大率")]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ZoomY { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = "描画", Name = "X軸回転", Description = "X軸に対する回転角")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Y軸回転", Description = "Y軸に対する回転角")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "Z軸回転", Description = "Z軸に対する回転角")]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationZ { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "描画", Name = "左右反転", Description = "左右反転")]
        [ToggleSlider]
        public bool InvertX { get => invertX; set => Set(ref invertX, value); }
        bool invertX = false;

        [Display(GroupName = "描画", Name = "上下反転", Description = "上下反転")]
        [ToggleSlider]
        public bool InvertY { get => invertY; set => Set(ref invertY, value); }
        bool invertY = false;

        [Display(GroupName = "図形", Name = "クリッピング", Description = "図形を下のアイテムの形状で切り抜きます")]
        [ToggleSlider]
        public bool IsClippingEnabled { get => isClippingEnabled; set => Set(ref isClippingEnabled, value); }
        bool isClippingEnabled = false;

        [Display(GroupName = "図形", Name = "種類", Description = "図形の種類")]
        [ShapeTypeComboBox]
        public Type ShapeType { get => shapeType; set => Set(ref shapeType, value); }
        Type shapeType = PluginLoader.GetPrimaryPluginType<IShapePlugin>();

        private Type? oldShapeType;

        [Display(GroupName = "図形", AutoGenerateField = true)]
        public IShapeParameter ShapeParameter { get => shapeParameter; set => Set(ref shapeParameter, value); }
        IShapeParameter shapeParameter = new RectangleShapeParameter(null);

        [Display(GroupName = "図形のエフェクト", Name = "", Description = "図形にかける映像エフェクト")]
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
            => [X, Y, Z, Opacity, Zoom, ZoomX, ZoomY, RotationX, RotationY, RotationZ, ShapeParameter, .. Effects];
    }
}
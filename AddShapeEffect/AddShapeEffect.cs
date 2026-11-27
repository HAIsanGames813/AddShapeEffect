using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using YukkuriMovieMaker.Project.Items;
using YukkuriMovieMaker.Shape;

namespace AddShapeEffect
{
    [VideoEffect(nameof(Texts.Paste_shape), [VideoEffectCategories.Decoration], ["図形", "Shape", "貼り付け", "Paste", "追加", "Add"], isAviUtlSupported: false, ResourceType = typeof(Texts))]
    internal class AddShapeEffect : VideoEffectBase
    {
        public override string Label => Texts.Paste_shape;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.X_coordinate), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Y_coordinate), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Z_coordinate), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Z { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Opacity), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Opacity { get; } = new Animation(100, 0, 100);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Scale_factor), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation Zoom { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Scale_factor_x), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ZoomX { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Scale_factor_y), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 100)]
        public Animation ZoomY { get; } = new Animation(100, 0, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.X_axis_rotation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Y_axis_rotation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Z_axis_rotation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation RotationZ { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Pixel_art), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsDot { get => isDot; set => Set(ref isDot, value); }
        bool isDot = false;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Flip_horizontal), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool InvertX { get => invertX; set => Set(ref invertX, value); }
        bool invertX = false;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Flip_vertical), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool InvertY { get => invertY; set => Set(ref invertY, value); }
        bool invertY = false;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Bring_to_front), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsAlwaysFront { get => isAlwaysFront; set => Set(ref isAlwaysFront, value); }
        bool isAlwaysFront = false;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Draw_behind), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsBack { get => isBack; set => Set(ref isBack, value); }
        bool isBack = false;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Draw_in_z_order), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsZOrderDrawing { get => isZOrderDrawing; set => Set(ref isZOrderDrawing, value); }
        bool isZOrderDrawing = false;

        [Display(GroupName = nameof(Texts.Draw), Name = nameof(Texts.Blend_mode), ResourceType = typeof(Texts))]
        [EnumComboBox]
        public YukkuriMovieMaker.Project.Blend BlendMode { get => blendMode; set => Set(ref blendMode, value); }
        YukkuriMovieMaker.Project.Blend blendMode = YukkuriMovieMaker.Project.Blend.Normal;

        [Display(GroupName = nameof(Texts.Follow_size), Name = nameof(Texts.Pin_to_left), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinLeft { get => pinLeft; set => Set(ref pinLeft, value); }
        bool pinLeft = false;

        [Display(GroupName = nameof(Texts.Follow_size), Name = nameof(Texts.Pin_to_right), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinRight { get => pinRight; set => Set(ref pinRight, value); }
        bool pinRight = false;

        [Display(GroupName = nameof(Texts.Follow_size), Name = nameof(Texts.Pin_to_top), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinTop { get => pinTop; set => Set(ref pinTop, value); }
        bool pinTop = false;

        [Display(GroupName = nameof(Texts.Follow_size), Name = nameof(Texts.Pin_to_bottom), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool PinBottom { get => pinBottom; set => Set(ref pinBottom, value); }
        bool pinBottom = false;

        [Display(GroupName = nameof(Texts.Margin), Name = nameof(Texts.Left), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Left { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Margin), Name = nameof(Texts.Right), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Right { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Margin), Name = nameof(Texts.Top), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Top { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Margin), Name = nameof(Texts.Bottom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Bottom { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = nameof(Texts.Shape), Name = nameof(Texts.Clipping), ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsClippingEnabled { get => isClippingEnabled; set => Set(ref isClippingEnabled, value); }
        bool isClippingEnabled = false;

        [Display(GroupName = nameof(Texts.Shape), Name = nameof(Texts.Type), ResourceType = typeof(Texts))]
        [ShapeTypeComboBox]
        public Type ShapeType { get => shapeType; set => Set(ref shapeType, value); }
        Type shapeType = PluginLoader.GetPrimaryPluginType<IShapePlugin>();

        private Type? oldShapeType;

        [Display(GroupName = nameof(Texts.Shape), AutoGenerateField = true, ResourceType = typeof(Texts))]
        public IShapeParameter? ShapeParameter { get => shapeParameter; set => Set(ref shapeParameter, value); }
        IShapeParameter? shapeParameter = new RectangleShapeParameter(null);

        [Display(GroupName = nameof(Texts.Shape_effect), ResourceType = typeof(Texts))]
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
        public override IEnumerable<string> GetFiles() => [.. Effects.OfType<IFileItem>().Select(x => x.GetFiles()).SelectMany(x => x)];

        public override void ReplaceFile(string from, string to)
        {
            foreach (IFileItem item in Effects.OfType<IFileItem>())
                item.ReplaceFile(from, to);
        }

        public override IEnumerable<TimelineResource> GetResources()
        {
            foreach (TimelineResource tr in base.GetResources())
                yield return tr;

            foreach (IVideoEffect effect in Effects.OfType<IVideoEffect>())
            {
                foreach (TimelineResource tr_1 in effect.GetResources())
                    yield return tr_1;

                if (ContentId.TryParses(effect.Remark, out ContentId[]? contentIds))
                    foreach (ContentId contentId in contentIds)
                        yield return TimelineResource.CreateFromContentId(contentId);

                if (TimelineResource.TryCreateFromVideoEffect(effect, out TimelineResource? resource))
                    yield return resource;
            }
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            var animatables = new List<IAnimatable> { X, Y, Z, Opacity, Zoom, ZoomX, ZoomY, RotationX, RotationY, RotationZ, Left, Right, Top, Bottom };
            if (ShapeParameter is not null)
            {
                animatables.Add(ShapeParameter);
            }
            animatables.AddRange(Effects);
            return animatables;
        }
    }
}
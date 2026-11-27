using AddShapeEffect.ForVideoEffectChain;
using System.Collections.Immutable;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;
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
        private readonly Composite compositeEffect;
        private readonly Vortice.Direct2D1.Effects.Blend blendEffect;
        private readonly AffineTransform2D outputPassthrough;
        private readonly DrawingEffect drawingBakeEffect;

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

            compositeEffect = new Composite(devices.DeviceContext);
            disposer.Collect(compositeEffect);

            blendEffect = new Vortice.Direct2D1.Effects.Blend(devices.DeviceContext);
            disposer.Collect(blendEffect);

            outputPassthrough = new AffineTransform2D(devices.DeviceContext);
            disposer.Collect(outputPassthrough);

            drawingBakeEffect = new DrawingEffect(devices);
            disposer.Collect(drawingBakeEffect);

            Output = outputPassthrough.Output;
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

            var currentInputBounds = devices.DeviceContext.GetImageLocalBounds(currentInput);

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
                descAfterChain = chain.UpdateOutput(effectDescription, neutralDrawDescription);
                ID2D1Image imageAfterChain = chain.Output;

                drawingBakeEffect.SetInput(imageAfterChain);
                drawingBakeEffect.Update(effectDescription with { DrawDescription = descAfterChain });
                ID2D1Image bakedShapeImage = drawingBakeEffect.Output;

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

                Matrix4x4 center = Matrix4x4.CreateTranslation(-shapeCenterX, -shapeCenterY, 0f);
                Matrix4x4 scale = Matrix4x4.CreateScale(finalScaleX, finalScaleY, 1.0f);
                Matrix4x4 rotXMat = Matrix4x4.CreateRotationX((float)MathHelper.ToRadians(rotX));
                Matrix4x4 rotYMat = Matrix4x4.CreateRotationY((float)MathHelper.ToRadians(rotY));
                Matrix4x4 rotZMat = Matrix4x4.CreateRotationZ((float)MathHelper.ToRadians(rotZ));

                float cameraDistance = 1000.0f;
                Matrix4x4 perspective = Matrix4x4.Identity;
                perspective.M34 = -1.0f / cameraDistance;
                Matrix4x4 translation = Matrix4x4.CreateTranslation(offsetX, offsetY, z);

                Matrix4x4 finalTransform = center * scale * rotXMat * rotYMat * rotZMat * translation * perspective;

                transformEffect.SetInput(0, bakedShapeImage, true);
                transformEffect.TransformMatrix = finalTransform;

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
            ID2D1Image? compositeInput1 = transformedShapeImage;
            if (item.IsClippingEnabled && compositeInput1 != null)
            {
                alphaMaskEffect.SetInput(0, compositeInput1, true);
                alphaMaskEffect.SetInput(1, inputTransformEffect.Output, true);
                compositeInput1 = alphaMaskEffect.Output;
            }

            if (compositeInput1 != null)
            {
                bool drawShapeInFront;
                if (item.IsAlwaysFront)
                {
                    drawShapeInFront = true;
                }
                else if (item.IsZOrderDrawing)
                {
                    float shapeZForOrder = (float)item.Z.GetValue(Frame, length, FPS);
                    drawShapeInFront = shapeZForOrder > 0f;
                }
                else
                {
                    drawShapeInFront = !item.IsBack;
                }

                ID2D1Image bg = drawShapeInFront ? inputTransformEffect.Output : compositeInput1;
                ID2D1Image fg = drawShapeInFront ? compositeInput1 : inputTransformEffect.Output;
                var blendMode = item.BlendMode;
                if (blendMode.IsCompositionEffect())
                {
                    compositeEffect.Mode = blendMode.ToD2DCompositionMode();
                    compositeEffect.SetInput(0, bg, true);
                    compositeEffect.SetInput(1, fg, true);
                    var composited = compositeEffect.Output;
                    outputPassthrough.SetInput(0, composited, true);
                    composited.Dispose();
                }
                else
                {
                    blendEffect.Mode = blendMode.ToD2DBlendMode();
                    blendEffect.SetInput(0, bg, true);
                    blendEffect.SetInput(1, fg, true);
                    var blended = blendEffect.Output;
                    outputPassthrough.SetInput(0, blended, true);
                    blended.Dispose();
                }
            }
            else
            {
                outputPassthrough.SetInput(0, inputTransformEffect.Output, true);
            }
            return effectDescription.DrawDescription;
        }

        public void ClearInput()
        {
            currentInput = null;
            outputPassthrough.SetInput(0, null, true);
            compositeEffect.SetInput(0, null, true);
            compositeEffect.SetInput(1, null, true);
            blendEffect.SetInput(0, null, true);
            blendEffect.SetInput(1, null, true);
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
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using AddShapeEffect.ForVideoEffectChain;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Settings;








namespace AddShapeEffect
{
    internal class AddShapeEffectProcessor : IVideoEffectProcessor
    {
        readonly AddShapeEffect item;
        readonly IGraphicsDevicesAndContext devices;
        readonly VideoEffectChainNode chain;
        int oldLenOfEffects;


        DisposeCollector disposer = new();

        bool isFirst = true;
        IShapeParameter? shapeParameter;

        IShapeSource? shapeSource;

        private readonly Transform3D transformEffect;

        private readonly Opacity opacityEffect;

        private readonly Transform3D inputTransformEffect;

        private readonly AlphaMask alphaMaskEffect;
        private readonly Composite finalCompositeEffect;


        public ID2D1Image Output { get; }

        public AddShapeEffectProcessor(IGraphicsDevicesAndContext devices, AddShapeEffect item)
        {
            chain = new VideoEffectChainNode(devices);
            oldLenOfEffects = item.Effects.Count;
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
            inputTransformEffect.TransformMatrix = Matrix4x4.Identity;

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
                DrawDescription initialDesc;
                    initialDesc = new(
                        effectDescription.DrawDescription.Draw,
                        new System.Numerics.Vector2(0, 0),
                        new System.Numerics.Vector2(1f, 1f),
                        new System.Numerics.Vector3(0, 0, 0),
                        effectDescription.DrawDescription.Camera,
                        effectDescription.DrawDescription.ZoomInterpolationMode,
                        1.0f,
                        false,
                        effectDescription.DrawDescription.Controllers
                    );

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
                float finalOpacity = opacityItem;

                if (item.InvertX) zoomXItem *= -1f;
                if (item.InvertY) zoomYItem *= -1f;
                Matrix4x4 scale = Matrix4x4.CreateScale(
                    zoomXItem * zoomItem,
                    zoomYItem * zoomItem,
                    1.0f);
                Matrix4x4 rotXMat = Matrix4x4.CreateRotationX((float)MathHelper.ToRadians(rotX));
                Matrix4x4 rotYMat = Matrix4x4.CreateRotationY((float)MathHelper.ToRadians(rotY));
                Matrix4x4 rotZMat = Matrix4x4.CreateRotationZ((float)MathHelper.ToRadians(rotZ));

                float cameraDistance = 1000.0f;
                Matrix4x4 perspective = Matrix4x4.Identity;
                perspective.M34 = -1.0f / cameraDistance;

                Matrix4x4 translation = Matrix4x4.CreateTranslation(x, y, z);

                Matrix4x4 finalTransform =
                    scale *
                    rotXMat *
                    rotYMat *
                    rotZMat *
                    translation *
                    perspective;

                transformEffect.SetInput(0, imageAfterChain, true);
                transformEffect.TransformMatrix = finalTransform;

                opacityEffect.SetInput(0, transformEffect.Output, true);

                opacityEffect.SetValue(0, finalOpacity);

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
    }
}

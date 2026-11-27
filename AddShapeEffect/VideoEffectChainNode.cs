using System.Collections.Immutable;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace AddShapeEffect.ForVideoEffectChain
{
    public class VideoEffectChainNode
    {
        readonly IGraphicsDevicesAndContext devices;
        readonly AffineTransform2D outTrans;
        readonly AffineTransform2D inTrans;
        readonly ID2D1Bitmap empty;
        readonly FrameAndLength fl = new();
        List<(IVideoEffect effect, IVideoEffectProcessor processor)> Chain = [];

        readonly ID2D1Image output;
        public ID2D1Image Output => output;

        public VideoEffectChainNode(IGraphicsDevicesAndContext devices)
        {
            this.devices = devices;
            empty = devices.DeviceContext.CreateEmptyBitmap();
            inTrans = new AffineTransform2D(devices.DeviceContext);
            inTrans.SetInput(0, empty, true);
            outTrans = new AffineTransform2D(devices.DeviceContext);
            inTrans.SetInput(0, inTrans.Output, true);
            output = outTrans.Output;
        }

        public void UpdateChain(ImmutableList<IVideoEffect> effects)
        {
            var disposedIndex = from tuple in Chain
                                where !effects.Contains(tuple.effect)
                                select Chain.IndexOf(tuple) into i
                                orderby i descending
                                select i;
            foreach (int index in disposedIndex)
            {
                IVideoEffectProcessor processor = Chain[index].processor;
                processor.ClearInput();
                processor.Dispose();
                Chain.RemoveAt(index);
            }

            List<IVideoEffect> keeped = Chain.Select((e_ep) => e_ep.effect).ToList();
            List<(IVideoEffect effect, IVideoEffectProcessor processor)> newChain = new(effects.Count);
            foreach (var effect in effects)
            {
                int index = keeped.IndexOf(effect);
                newChain.Add(index < 0 ? (effect, effect.CreateVideoEffect(devices)) : Chain[index]);
            }

            Chain = newChain;
        }

        public void SetInput(ID2D1Image? input)
        {
            inTrans.SetInput(0, input ?? empty, true);
        }

        public void ClearChain()
        {
            foreach (var (_, processor) in Chain)
            {
                processor.ClearInput();
                processor.Dispose();
            }
            Chain.Clear();
            outTrans.SetInput(0, inTrans.Output, true);
        }

        public void ClearInput()
        {
            inTrans.SetInput(0, empty, true);
            outTrans.SetInput(0, inTrans.Output, true);

            foreach (var (_, processor) in Chain)
            {
                processor.ClearInput();
                processor.Dispose();
            }

            Chain.Clear();
        }

        public DrawDescription UpdateOutput(EffectDescription effectDescription, DrawDescription drawDescription)
        {
            fl.UpdateFrom(effectDescription);
            DrawDescription desc = new(
                drawDescription.Draw,
                drawDescription.CenterPoint,
                drawDescription.Zoom,
                drawDescription.Rotation,
                drawDescription.Camera,
                drawDescription.ZoomInterpolationMode,
                drawDescription.Opacity,
                drawDescription.Invert,
                drawDescription.Controllers
                );

            ID2D1Image image = inTrans.Output;

            foreach ((IVideoEffect effect, IVideoEffectProcessor processor) in Chain)
            {
                if (effect.IsEnabled)
                {
                    IVideoEffectProcessor item = processor;
                    item.SetInput(image);
                    TimelineItemSourceDescription timeLineItemSourceDescription =
                        new(effectDescription, fl.Frame, fl.Length, effectDescription.Layer);
                    EffectDescription ed =
                        new(timeLineItemSourceDescription, desc, effectDescription.InputIndex, effectDescription.InputCount, effectDescription.GroupIndex, effectDescription.GroupCount);
                    desc = item.Update(ed);
                    image = item.Output;
                }
            }

            outTrans.SetInput(0, image, true);
            return desc;
        }

        public void Dispose()
        {
            outTrans.SetInput(0, null, true);
            outTrans.Dispose();
            inTrans.SetInput(0, null, true);
            inTrans.Dispose();
            empty.Dispose();
            output.Dispose();

            Chain.ForEach(i =>
            {
                i.processor.ClearInput();
                i.processor.Dispose();
            });
        }
    }
}
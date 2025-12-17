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
        readonly AffineTransform2D transform;
        readonly ID2D1Bitmap empty;
        readonly FrameAndLength fl = new();
        ID2D1Image? input;
        bool isEmpty;
        List<(IVideoEffect effect, IVideoEffectProcessor processor)> Chain = [];

        ID2D1Image output;
        public ID2D1Image Output => isEmpty ? empty : output;

        public VideoEffectChainNode(IGraphicsDevicesAndContext devices)
        {
            this.devices = devices;
            transform = new AffineTransform2D(devices.DeviceContext);
            output = transform.Output;
            empty = devices.DeviceContext.CreateEmptyBitmap();
            isEmpty = true;
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
            this.input = input;
            if (input == null)
            {
                isEmpty = true;
                transform.SetInput(0, null, true);
                return;
            }
            else
            {
                if (Chain.Count > 0)
                {
                    Chain.First().processor.SetInput(input);
                    transform.SetInput(0, Chain.Last().processor.Output, true);
                }
                else
                {
                    transform.SetInput(0, input, true);
                }
                isEmpty = false;
            }
        }

        public void ClearChain()
        {
            foreach (var (_, processor) in Chain)
            {
                processor.ClearInput();
                processor.Dispose();
            }
            Chain.Clear();
            transform.SetInput(0, input, true);
        }

        public void ClearInput()
        {
            input = null;
            transform.SetInput(0, null, true);
            foreach (var (_, processor) in Chain)
            {
                processor.ClearInput();
                processor.Dispose();
            }
            Chain.Clear();
        }

        // 引数を EffectDescription に変更し、内部で安全にプロパティを解決
        public DrawDescription UpdateOutputAndDescription(EffectDescription effectDescription)
        {
            if (input == null)
            {
                isEmpty = true;
                transform.SetInput(0, null, true);
                return effectDescription.DrawDescription;
            }

            ID2D1Image? image = input;
            DrawDescription desc = effectDescription.DrawDescription;

            foreach (var (effect, processor) in Chain)
            {
                if (effect.IsEnabled)
                {
                    processor.SetInput(image);
                    // 個別のエフェクトを更新。元の effectDescription をそのまま渡すのが最も安全
                    desc = processor.Update(effectDescription with { DrawDescription = desc });
                    image = processor.Output;
                }
            }

            transform.SetInput(0, image, true);
            isEmpty = false;
            return desc;
        }

        public void Dispose()
        {
            transform.SetInput(0, null, true);
            transform.Dispose();
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
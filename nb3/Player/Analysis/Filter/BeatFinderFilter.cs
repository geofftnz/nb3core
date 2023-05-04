using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nb3.Common;
using nb3.Player.Analysis.Filter.Nodes;

namespace nb3.Player.Analysis.Filter
{
    public class BeatFinderFilter : SpectrumFilterBase, ISpectrumFilter
    {
        private const int NUMOUTPUTS = 6;
        private const int BUFFERLEN = 2880 * 4;

        public enum FilterOutputs
        {
            Current = 0,
            Bpm,
            Correlation,
            Out,
            Trigger,
            Trigger4
        }
        public int OutputOffset { get; set; }
        public int OutputSlotCount { get { return NUMOUTPUTS; } }
        private float[] output = new float[NUMOUTPUTS];

        public float Threshold { get; set; } = 0.2f;
        public float Decay { get; set; } = 0.02f;
        public float Release { get; set; } = 0.2f;

        private RingBuffer<float> buffer = new RingBuffer<float>(BUFFERLEN);

        private int freqStart, freqCount;
        private float lowpassCoeff;
        private float out2 = 0f;
        private float blend = 0f;
        private float avg = 0f;
        private float peak = 0f;
        private float floor = 1f;

        private Random rand = new Random();
        private HysteresisPulse hysteresis = new HysteresisPulse(0.6f, 0.4f);
        private RisingEdgeTimer edge1 = new RisingEdgeTimer();
        private RisingEdgeDividerTimer edge2 = new RisingEdgeDividerTimer(4);

        private const int OVERSAMPLE = 8;  // amount to expand/oversample the signal before correlating

        private const int initial_offset = 45 * OVERSAMPLE;        // start around 60bpm
        private int offset = initial_offset;
        private const int CORRELATIONWIDTH = 360 * OVERSAMPLE;

        public BeatFinderFilter(string name = "BEAT1", int freq_start = 0, int freq_count = 128, float lowpass_coeff = 0.98f) : base(name, "cur", "bpm", "fit", "out","trig","trig4")
        {
            freqStart = freq_start;
            freqCount = freq_count;
            lowpassCoeff = lowpass_coeff;

        }

        public float[] GetValues(FilterParameters frame)
        {
            float current = 0f;

            // calculate the overall signal level
            //for (int i = freqStart; i < freqStart + freqCount; i++)
            //{
            //    current += frame.Spectrum2DB[i];
            //}
            //current /= freqCount;

            for (int i = 0; i < 8; i++)
            {
                current += frame.Spectrum2DB[i];
            }
            for (int i = 128; i < 256; i++)
            {
                current += frame.Spectrum2DB[i];
            }
            current /= 8 + 128;

            //current = MathExt.Interpolate(current, blend, 0.5f);

            output[(int)FilterOutputs.Current] = current;

            // add sample to ringbuffer
            buffer.Add(current);


            // loop over a reasonable range of inter-beat times.
            // 60bpm == 180 frames. 1/4 note: 45 frames.
            // 180bpm == 60 frames. 1/4 note: 15 frames.

            // oversample our buffer to improve the resolution of matching
            // TODO: Do this on-the-fly?
            var rbuffer = buffer.Last().Take(BUFFERLEN).ToArray().CubicResample(OVERSAMPLE);

            // whole notes
            //int min = 60 * resample;
            //int max = 180 * resample;
            int min = 15 * OVERSAMPLE;
            int max = 45 * OVERSAMPLE;

            /*
            // quarter notes
            int min = 15 * OVERSAMPLE;
            int max = 45 * OVERSAMPLE;

            var best = GetCorrelations(rbuffer, min, max, 180 * OVERSAMPLE).ToList()
                .OrderByDescending(c =>
                    c.correlation  // correlation between current & offset history
                    * (1f / (1 + c.frames * 0.2f))  // bias towards smaller offset
                    )
                .First();
            
            float bpm = 10800f / (Math.Max(1, best.frames) * 4f / OVERSAMPLE);
            */

            // get the correlation based on our current offset
            double current_correlation = Correlation(new Span<float>(rbuffer, 0, CORRELATIONWIDTH), new Span<float>(rbuffer, offset, CORRELATIONWIDTH));

            var next_offset = offset;

            // random movement
            var next = GetCorrelation(rbuffer, offset + rand.Next(21) - 10, CORRELATIONWIDTH);
            if (next.correlation > current_correlation)
            {
                next_offset = (offset*7 + next.offset) / 8;
            }
            if (next_offset < min) next_offset = initial_offset;
            if (next_offset > max) next_offset = initial_offset;
            offset = next_offset;



            float bpm = 10800f / (Math.Max(1, offset) * 4f / OVERSAMPLE);

            //avg = avg * lowpassCoeff + current * (1f - lowpassCoeff);
            output[(int)FilterOutputs.Bpm] = bpm / 180f;
            output[(int)FilterOutputs.Correlation] = (float)Math.Max(0.0, current_correlation);

            // accumulate the last few samples
            blend = 0f;
            for (int i = 0; i < 4; i++)
            {
                blend += rbuffer[i * offset];
            }
            blend /= 8f;


            avg = avg * lowpassCoeff + (1f - lowpassCoeff) * blend;
            //out2 -= avg;
            peak *= 0.99f;
            floor += 0.001f;

            floor = Math.Min(floor, avg);
            avg -= floor;

            peak = Math.Max(peak, avg);
            out2 = peak > 0.0f ? avg / peak : avg;

            output[(int)FilterOutputs.Out] = out2;

            float h = hysteresis.Get(out2);
            output[(int)FilterOutputs.Trigger] = edge1.Get(h);
            output[(int)FilterOutputs.Trigger4] = edge2.Get(h);


            return output;
        }

        public static IEnumerable<(int frames, double correlation)> GetCorrelations(float[] buffer, int min, int max, int length)
        {
            for (int i = min; i <= max; i++)
            {
                yield return (i, Correlation(
                    new Span<float>(buffer, 0, length),
                    new Span<float>(buffer, i, length)
                    ));
            }
        }

        public static (int offset, double correlation) GetCorrelation(float[] buffer, int offset, int length)
        {
            return (offset, Correlation(new Span<float>(buffer, 0, length), new Span<float>(buffer, offset, length)));
        }

        public static double Correlation(Span<float> x, Span<float> y)
        {
            if (x.Length != y.Length)
            {
                throw new ArgumentException("Arrays must have the same length.");
            }

            int n = x.Length;
            float sum_x = 0.0f;
            float sum_y = 0.0f;
            float sum_x_sq = 0.0f;
            float sum_y_sq = 0.0f;
            float sum_xy = 0.0f;

            for (int i = 0; i < n; i++)
            {
                sum_x += x[i];
                sum_y += y[i];
                sum_x_sq += x[i] * x[i];
                sum_y_sq += y[i] * y[i];
                sum_xy += x[i] * y[i];
            }

            double numerator = n * sum_xy - sum_x * sum_y;
            double denominator = Math.Sqrt((n * sum_x_sq - sum_x * sum_x) * (n * sum_y_sq - sum_y * sum_y));

            if (denominator == 0.0)
            {
                return 0.0; // if denominator is zero, correlation is undefined
            }

            return numerator / denominator;
        }

        public void Reset()
        {
        }
    }
}

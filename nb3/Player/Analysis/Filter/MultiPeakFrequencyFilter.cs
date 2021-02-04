using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nb3.Common;

namespace nb3.Player.Analysis.Filter
{
    public class MultiPeakFrequencyFilter : SpectrumFilterBase, ISpectrumFilter
    {
        private const int NUMPEAKS = 5;
        private const int NUMOUTPUTS = NUMPEAKS * 2;

        public enum FilterOutputs
        {
            FrequencyOfs = 0,
            LevelOfs,
        }

        public int OutputOffset { get; set; }
        public int OutputSlotCount { get { return NUMOUTPUTS; } }
        private float[] output = new float[NUMOUTPUTS];

        public int FreqStart { get; set; }
        public int FreqCount { get; set; }
        public float PeakSelectionThreshold { get; set; } = 0.02f;
        public float HighFreqFallOff { get; set; } = 0.1f;


        private float[] LowPassSpectrum = new float[Globals.SPECTRUMRES];
        private float[] SmoothSpectrum = new float[Globals.SPECTRUMRES];

        private float[] FilterKernel = new float[] { 0.0093f, 0.028002f, 0.065984f, 0.121703f, 0.175713f, 0.198596f, 0.175713f, 0.121703f, 0.065984f, 0.028002f, 0.0093f };

        public MultiPeakFrequencyFilter(string name = "MPFF", int freq_start = 0, int freq_count = 8) : base(name, "Freq1", "Level1", "Freq2", "Level2", "Freq3", "Level3", "Freq4", "Level4", "Freq5", "Level5")
        {
            FreqStart = freq_start;
            FreqCount = freq_count;
        }

        private void GenerateLowPassSpectrum(float[] s, float k)
        {
            for (int i = 0; i < LowPassSpectrum.Length; i++)
            {
                LowPassSpectrum[i] = LowPassSpectrum[i] * k + (1f - k) * s[i];
            }

        }

        private void GenerateSmoothSpectrum(float[] s, float[] k)
        {
            int filterSize = k.Length;
            int filterRadius = filterSize / 2;

            for (int i = 0; i < SmoothSpectrum.Length; i++)
            {
                float total = 0f;

                for (int j = 0; j < filterSize; j++)
                {
                    int si = i + j - filterRadius;
                    if (si < 0) si = 0;
                    if (si >= s.Length) si = s.Length - 1;

                    total += s[si] * k[j];
                }
                SmoothSpectrum[i] = total;
            }
        }

        public float[] GetValues(FilterParameters frame)
        {
            // temporal smoothing
            GenerateLowPassSpectrum(frame.SpectrumDB, 0.4f);

            // Generate the noise floor
            GenerateSmoothSpectrum(LowPassSpectrum, FilterKernel);
            // TODO: remove the above and replace it with a variable-width kernel below


            // Subtract the temporally-smoothed spectrum from the noise floor across our frequency range.
            // extract peaks above the threshold
            var peaks = Enumerable.Range(FreqStart, FreqCount)
                .Select(i => new Tuple<int, float>(i, LowPassSpectrum[i] - SmoothSpectrum[i]))  // find peaks
                .Select(s => new Tuple<int, float>(s.Item1, s.Item2 * (1f - ((float)s.Item1 * HighFreqFallOff) / (float)FreqCount)))  // apply high-frequency falloff
                .Where(s => s.Item2 > PeakSelectionThreshold)  // get peaks above threshold
                .OrderByDescending(s => s.Item2) // rank
                .Take(NUMPEAKS)
                .ToList();

            // output peaks as Frequency,Level pairs
            int outputIndex = 0;
            for (int i = 0; i < NUMPEAKS; i++)
            {
                if (i < peaks.Count)
                {
                    output[outputIndex++] = ((float)peaks[i].Item1 / (float)Globals.SPECTRUMRES);  // freq
                    output[outputIndex++] = peaks[i].Item2;  // level
                }
                else
                {
                    output[outputIndex++] = 0f;  // freq
                    output[outputIndex++] = 0f;  // level
                }
            }

            return output;
        }

        public void Reset()
        {
        }
    }
}

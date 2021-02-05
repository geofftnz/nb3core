using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nb3.Common;

namespace nb3.Player.Analysis.Filter
{
    /// <summary>
    /// Process:
    /// 
    /// - Spectrum, in dB
    /// - 32-tap moving average (box filter) == noise floor
    /// - 3-tap moving average, or gaussian filter == peak source
    /// - subtract: peak source - noise floor
    /// - peak extraction: step through linearly looking for /-\ points (also consider /--\, but unlikely given floats)
    /// - Allocation:
    ///   - For all active slots, ordered by strength descending:
    ///     - Attempt to find a peak matching current - nearest frequency. Match if found and remove peak from list.
    ///   - For all inactive slots, allocate strongest remaining peaks
    ///   - For all active, unmatched slots, decay strength & deactivate if below threshold.
    /// 
    /// </summary>
    public class MultiPeakFrequencyFilter : SpectrumFilterBase, ISpectrumFilter
    {
        private const int NUMPEAKS = 5;
        private const int NUMOUTPUTS = NUMPEAKS * 2;

        public enum FilterOutputs
        {
            FrequencyOfs = 0,
            LevelOfs,
        }

        private class TrackedPeak
        {
            public bool IsActive { get; set; }
            public float Frequency { get; set; }
            public float Level { get; set; }
        }

        private TrackedPeak[] trackedPeaks = new TrackedPeak[NUMPEAKS];

        public int OutputOffset { get; set; }
        public int OutputSlotCount { get { return NUMOUTPUTS; } }
        private float[] output = new float[NUMOUTPUTS];

        public int FreqStart { get; set; }
        public int FreqCount { get; set; }

        /// <summary>
        /// How strong a peak has to be above the noise floor.
        /// </summary>
        public float PeakSelectionThreshold { get; set; } = 0.05f;
        public float HighFreqFallOff { get; set; } = 0.5f;

        /// <summary>
        /// How much temporal smoothing to apply. 0 = none, <1 = all.
        /// </summary>
        public float TemporalSmoothing { get; set; } = 0.5f;

        /// <summary>
        /// Amount to bias the detected peak level towards the absolute level (from the relative level above noise floor)
        /// </summary>
        public float AbsoluteLevelBias { get; set; } = 0.5f;

        /// <summary>
        /// How far away a peak can be to match to an existing tracked peak.
        /// </summary>
        public float PeakTrackingFrequencyThreshold { get; set; } = 0.02f;

        /// <summary>
        /// How weak a peak can be relative to the current tracked peak to be considered a continuation.
        /// </summary>
        public float PeakTrackingLevelThreshold { get; set; } = 0.75f;

        /// <summary>
        /// Amount to decay tracked peak level on no match
        /// </summary>
        public float PeakTrackingUnmatchedDecay { get; set; } = 0.2f;

        /// <summary>
        /// Level below which to deactivate a peak.
        /// </summary>
        public float PeakTrackingExtinctionThreshold { get; set; } = 0.05f;


        private float[] NoiseFloor = new float[Globals.SPECTRUMRES];
        private float[] SmoothSpectrum = new float[Globals.SPECTRUMRES];
        private float[] DiffSpectrum = new float[Globals.SPECTRUMRES];

        //private float[] FilterKernel = new float[] { 0.0093f, 0.028002f, 0.065984f, 0.121703f, 0.175713f, 0.198596f, 0.175713f, 0.121703f, 0.065984f, 0.028002f, 0.0093f };
        private float[] FilterKernel = new float[] { 0.05f, 0.2f, 0.5f, 0.2f, 0.05f };

        public MultiPeakFrequencyFilter(string name = "MPFF", int freq_start = 0, int freq_count = 8) : base(name, "Freq1", "Level1", "Freq2", "Level2", "Freq3", "Level3", "Freq4", "Level4", "Freq5", "Level5")
        {
            FreqStart = freq_start;
            FreqCount = freq_count;

            for (int i = 0; i < NUMPEAKS; i++)
            {
                trackedPeaks[i] = new TrackedPeak { IsActive = false };
            }
        }


        /*private void GenerateLowPassSpectrum(float[] s, float k)
        {
            for (int i = 0; i < NoiseFloor.Length; i++)
            {
                NoiseFloor[i] = NoiseFloor[i] * k + (1f - k) * s[i];
            }
        }*/

        private void GenerateNoiseFloor(float[] s, int radius)
        {
            float scale = .5f / radius;
            for (int i = 0; i < NoiseFloor.Length; i++)
            {
                float sum = 0f;
                for (int j = 1; j < radius; j++)
                {
                    sum += s[Math.Max(0, i - j)];
                    sum += s[Math.Min(NoiseFloor.Length - 1, i + j)];
                }
                NoiseFloor[i] = sum * scale;
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
                SmoothSpectrum[i] = SmoothSpectrum[i] * TemporalSmoothing + (1f - TemporalSmoothing) * total;
            }
        }

        private class DetectedPeak
        {
            public float Frequency { get; set; }
            public float Level { get; set; }
            public bool IsAllocated { get; set; }
        }

        private IEnumerable<DetectedPeak> GetPeaks(float[] s, float[] original)
        {
            for (int i = 1; i < s.Length - 1; i++)
            {
                if (s[i] > s[i - 1] && s[i] > s[i + 1])  // local maximum
                {
                    yield return new DetectedPeak
                    {
                        Frequency = (float)i / (float)Globals.SPECTRUMRES,
                        Level = s[i] * (1f - AbsoluteLevelBias) + AbsoluteLevelBias * original[i], 
                        IsAllocated = false
                    };
                }
            }
        }


        public float[] GetValues(FilterParameters frame)
        {
            // temporal smoothing
            //GenerateLowPassSpectrum(frame.SpectrumDB, 0.4f);

            // Generate the noise floor, wide box filter
            GenerateNoiseFloor(frame.SpectrumDB, 16);

            // Generate smooth spectrum by applying a small gaussian (or similar) kernel.
            GenerateSmoothSpectrum(frame.SpectrumDB, FilterKernel);

            // Subtract, clamp to zero.
            for (int i = 0; i < DiffSpectrum.Length; i++)
            {
                DiffSpectrum[i] = Math.Max(0f, SmoothSpectrum[i] - NoiseFloor[i]);
            }

            float freq_start = (float)FreqStart / Globals.SPECTRUMRES;
            float freq_end = (float)(FreqStart + FreqCount) / Globals.SPECTRUMRES;

            var peaks = GetPeaks(DiffSpectrum, SmoothSpectrum)
                .Where(p => p.Frequency >= freq_start && p.Frequency < freq_end)  // frequency range
                .Where(p => p.Level > PeakSelectionThreshold)  // level threshold
                .Select(p => { p.Level *= 1f - p.Frequency * HighFreqFallOff; return p; })  // high freq falloff
                .OrderByDescending(p => p.Level)
                .Take(NUMPEAKS * 4)   // grab more than we need.
                .ToList();

            // - Allocation:
            //   - For all active slots, ordered by strength descending:
            //     - Attempt to find a peak matching current - nearest frequency. Match if found and remove peak from list.
            foreach (var trackedPeak in trackedPeaks.Where(tp => tp.IsActive).OrderByDescending(tp => tp.Level))
            {
                // find closest peak by frequency, within selection threshold
                var matchingPeak = peaks
                    .Where(p => !p.IsAllocated)
                    .Where(p => Math.Abs(trackedPeak.Frequency - p.Frequency) < PeakTrackingFrequencyThreshold)
                    .Where(p => p.Level >= trackedPeak.Level * PeakTrackingLevelThreshold)
                    .OrderBy(p => Math.Abs(trackedPeak.Frequency - p.Frequency))
                    .FirstOrDefault();

                if (matchingPeak != null) // found one
                {
                    trackedPeak.Frequency = matchingPeak.Frequency;
                    trackedPeak.Level = trackedPeak.Level * 0.5f + 0.5f * matchingPeak.Level;  // blend level
                    matchingPeak.IsAllocated = true;
                }
                else  // nothing matching
                {
                    // decay level
                    trackedPeak.Level *= PeakTrackingUnmatchedDecay;

                    // deactivate if extinct
                    if (trackedPeak.Level < PeakTrackingExtinctionThreshold)
                    {
                        trackedPeak.IsActive = false;
                    }
                }
            }

            //   - For all inactive slots, allocate strongest remaining peaks
            foreach (var trackedPeak in trackedPeaks.Where(tp => !tp.IsActive))
            {
                // find strongest unallocated peak
                var matchingPeak = peaks
                    .Where(p => !p.IsAllocated)
                    .OrderByDescending(p => p.Level)
                    .FirstOrDefault();

                if (matchingPeak != null)
                {
                    // new tracked peak
                    trackedPeak.IsActive = true;
                    trackedPeak.Frequency = matchingPeak.Frequency;
                    trackedPeak.Level = matchingPeak.Level;
                    matchingPeak.IsAllocated = true;
                }
            }

            // output peaks as Frequency,Level pairs
            int outputIndex = 0;
            foreach (var trackedPeak in trackedPeaks)
            {
                output[outputIndex++] = trackedPeak.IsActive ? trackedPeak.Frequency : 0f;
                output[outputIndex++] = trackedPeak.IsActive ? trackedPeak.Level : 0f;
            }

            /*
            // Subtract the temporally-smoothed spectrum from the noise floor across our frequency range.
            // extract peaks above the threshold
            var peaks = Enumerable.Range(FreqStart, FreqCount)
                .Select(i => new Tuple<int, float>(i, NoiseFloor[i] - SmoothSpectrum[i]))  // find peaks
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
            }*/

            return output;
        }

        public void Reset()
        {
        }
    }
}

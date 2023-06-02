using NAudio.Dsp;
using nb3.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis
{
    /// <summary>
    /// Represents a single sample of audio analysis to be passed to the renderer.
    /// </summary>
    public class AudioAnalysisSample
    {
        public int Samples { get; set; } = 1;
        public double SampleSeconds { get; set; } = 1.0 / Globals.AUDIOFRAMERATE;  // Number of seconds covered by this frame
        public float[] Spectrum { get; set; } = null;
        public float[] Spectrum2 { get; set; } = null;
        public float[] AudioData { get; set; } = null;

        public AudioAnalysisSample()
        {

        }
        public AudioAnalysisSample(float[] spectrum, float[] spectrum2, float[] audioData, int samples, double sampleSeconds)
        {
            Spectrum = spectrum;
            Spectrum2 = spectrum2;
            AudioData = audioData;
            Samples = samples;
            SampleSeconds = sampleSeconds;
        }

        public void CopyTo(AudioAnalysisSample dest)
        {
            dest.Samples = Samples;
            dest.SampleSeconds = SampleSeconds;
            dest.Spectrum ??= new float[Spectrum.Length];
            dest.Spectrum2 ??= new float[Spectrum2.Length];
            dest.AudioData ??= new float[AudioData.Length];

            Spectrum?.CopyTo(dest.Spectrum, 0);
            Spectrum2?.CopyTo(dest.Spectrum2, 0);
            AudioData?.CopyTo(dest.AudioData, 0);
        }
    }
}

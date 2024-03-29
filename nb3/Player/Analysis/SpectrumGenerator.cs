﻿using NAudio.Dsp;
using NAudio.Wave;
using nb3.Common;
using nb3.Player.Analysis;
using nb3.Player.Analysis.Filter;
using nb3.Player.Analysis.LoudnessWeighting;
using nb3.Vis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis
{
    /// <summary>
    /// Generates FFT spectrums from audio.
    /// Based heavily on the SampleAggregator from the NAudio samples.
    /// 
    /// Main difference is that it generates FFTs more frequently, using a ring buffer.
    /// 
    /// Target is some sort of hybrid spectrum calculated from overlaid FFTs of different sizes, in order to get good time
    /// resolution for high frequencies and good frequency resolution for low frequencies.
    /// 
    /// Note: This executes on NAudio's playback thread.
    /// </summary>
    public class SpectrumGenerator : ISampleProvider
    {
        /// <summary>
        /// 
        /// </summary>
        private ISampleProvider source;
        private int channels;

        //private const int fftSize = Globals.SPECTRUMRES * 2;
        private const int targetFrameRate = Globals.AUDIOFRAMERATE;

        private int frameInterval;
        private int sampleCounter = 0;
        //private const int outputResolution = Globals.SPECTRUMRES;
        //private const int outputResolution2 = Globals.SPECTRUM2RES;

        private const int MAXCHANNELS = 2;
        //private const int BUFFERLEN = 8192;

        private BufferedFFT[] fft = new BufferedFFT[MAXCHANNELS];
        private BufferedFFT fft2;

        //private ILoudnessWeighting loudnessWeighting;

        private SpectrumAnalyser analyser = new SpectrumAnalyser();

        // hacky exposing of filter output name list
        public List<string> FilterOutputNames => analyser.OutputNames;
        public List<Tuple<string, FilterParameter>> FilterParameters => analyser.GetFilterParameters().ToList();

        public event EventHandler<FftEventArgs> SpectrumReady;
        public WaveFormat WaveFormat => source.WaveFormat;

        public int FrameInterval { get { return frameInterval; } }

        public SpectrumGenerator(ISampleProvider source)
        {
            this.source = source;
            this.channels = source.WaveFormat.Channels;
            this.frameInterval = source.WaveFormat.SampleRate / targetFrameRate;
            //this.loudnessWeighting = new ITU_T_468_Weighting(source.WaveFormat.SampleRate / 2);
            //this.loudnessWeighting = new A_Weighting(source.WaveFormat.SampleRate);

            for (int i = 0; i < MAXCHANNELS; i++)
            {
                fft[i] = new BufferedFFT(Globals.SPECTRUMRES, new ITU_T_468_Weighting(source.WaveFormat.SampleRate / 2));
            }

            fft2 = new BufferedFFT(Globals.SPECTRUM2RES, new ITU_T_468_Weighting(source.WaveFormat.SampleRate / 2));
        }


        public int Read(float[] buffer, int offset, int count)
        {
            if (source == null)
                return 0;


            int samplesRead = 0;

            try
            {
                samplesRead = source.Read(buffer, offset, count);
            }
            catch (NullReferenceException)  // TODO: FIX FILTHY HACK
            {
                return 0;
            }

            for (int i = 0; i < samplesRead; i += channels)
            {
                // add to ring buffer
                AddSample(buffer, i, channels);
            }

            return samplesRead;
        }

        private void AddSample(float[] samples, int offset, int channels)
        {
            // mono source - copy to both channels
            if (channels == 1)
            {
                fft[0].Add(samples[offset]);
                fft[1].Add(samples[offset]);

                fft2.Add(samples[offset]);
            }
            else
            {
                fft[0].Add(samples[offset]);
                fft[1].Add(samples[offset + 1]);

                fft2.Add((samples[offset] + samples[offset + 1]) * 0.5f);
            }

            sampleCounter++;
            if (sampleCounter > frameInterval)
            {
                float[] f = new float[Globals.SPECTRUMRES * MAXCHANNELS];

                for (int i = 0; i < MAXCHANNELS; i++)
                {
                    fft[i].GenerateTo(f, i, Globals.SPECTRUMRES, MAXCHANNELS);
                }


                float[] f2 = new float[Globals.SPECTRUM2RES];
                fft2.GenerateTo(f2, 0, Globals.SPECTRUM2RES);


                var analysisSample = new AudioAnalysisSample(f, f2, new float[Globals.AUDIODATASIZE], frameInterval, frameInterval / WaveFormat.SampleRate);

                analyser.Process(analysisSample);

                SpectrumReady?.Invoke(this, new FftEventArgs(analysisSample));

                sampleCounter = 0;
            }
        }

        private IEnumerable<float> MixChannels(float[] samples, int count, int MAXCHANNELS)
        {
            float total = 0f;
            int c = MAXCHANNELS;
            for (int i = 0; i < count; i++)
            {
                total += samples[i];
                if (--c == 0)
                {
                    yield return total;
                    c = MAXCHANNELS;
                    total = 0f;
                }
            }
        }

        private IEnumerable<float> Resample(float[] src, int count, Func<float, float> remap)
        {
            int srcmax = src.Length - 1;
            int destmax = count - 1;
            float[] offset = new float[] { -0.7f, -0.3f, 0.0f, 0.3f, 0.7f };

            for (int i = 0; i <= destmax; i++)
            {
                float total = 0f;

                for (int j = 0; j < offset.Length; j++)
                {
                    float di = ((float)i + offset[j]) / (float)destmax;
                    int si = (int)(Math.Max(0f, Math.Min(1f, remap(Math.Max(0f, Math.Min(1f, di))))) * srcmax);
                    total += src[si];
                }

                yield return total / (float)offset.Length;
            }

        }

    }


    public class FftEventArgs : EventArgs
    {
        [DebuggerStepThrough]
        public FftEventArgs(AudioAnalysisSample sample)
        {
            Sample = sample;
        }

        public AudioAnalysisSample Sample { get; set; }
    }

}

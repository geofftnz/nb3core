﻿using NAudio.Utils;
using nb3.Common;
using nb3.Player.Analysis.Filter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis
{
    /// <summary>
    /// SpectrumAnalyser
    /// 
    /// Takes an AudioAnalysisSample containing a spectrum and populates the AudioData array
    /// </summary>
    public class SpectrumAnalyser
    {
        private const int SPECTRUMHISTORY = 16;
        private RingBuffer<float[]> SpectrumHistory = new RingBuffer<float[]>(SPECTRUMHISTORY);
        private FilterParameters filterParams = new FilterParameters();
        public List<ISpectrumFilter> Filters { get; private set; } = new List<ISpectrumFilter>();
        private int maxFilterOutputIndex = 0;

        public List<string> OutputNames { get; } = new List<string>();

        public SpectrumAnalyser()
        {
            filterParams.Spectrum = new float[Globals.SPECTRUMRES];
            filterParams.SpectrumDB = new float[Globals.SPECTRUMRES];
            filterParams.Spectrum2 = new float[Globals.SPECTRUM2RES];
            filterParams.Spectrum2DB = new float[Globals.SPECTRUM2RES];
            filterParams.SpectrumHistory = SpectrumHistory;

            // The order of these is important as they define where filter outputs end up in the analysis data texture.
            AddFilter(new BeatFinderFilter("BEAT1", 0, Globals.SPECTRUM2RES, 0.25f));
            //AddFilter(new BeatFinderFilter("BEAT2", 0, Globals.SPECTRUM2RES, 0.25f) { FourBeatCorrelation = true });

            AddFilter(new PeakFrequencyFilter("PFF", 12, Globals.SPECTRUMRES / 3 - 12));
            //AddFilter(new MultiPeakFrequencyFilter("MPFF", 12, Globals.SPECTRUMRES / 3 - 12));
            AddFilter(new MultiPeakFrequencyFilter("MPFF", 0, Globals.SPECTRUMRES - 20));  // 0 was 12
            AddFilter(new KickDrumFilter3("KD3", 0, 6));
            AddFilter(new KickDrumFilter2("KD2A", 0, 4));
            AddFilter(new KickDrumFilter2("KD2B", 0, 8));
            AddFilter(new DistributionFilter("DF"));
            AddFilter(new KickDrumFilter("KD1"));
            AddFilter(new BroadbandTransientFilter("HH1", (f, i) => f.Spectrum2[i], 96, 224, MathExt.Flat(2)) { TriggerHigh = 0.5f, TriggerLow = 0.45f, MaxGain = 4f });  // 256 spectrum
            AddFilter(new BroadbandTransientFilter("HH2", (f, i) => f.Spectrum2[i], 96, 224, MathExt.Flat(2)) { TriggerHigh = 0.8f, TriggerLow = 0.75f, MaxGain = 4f });  // 256 spectrum
            AddFilter(new BroadbandTransientFilter("BD", (f, i) => f.Spectrum[i], 4, 16, MathExt.Flat(4)) { TriggerHigh = 0.5f, TriggerLow = 0.45f, MaxGain = 6f, AGCDecay = 0.999f });  // 1024 spectrum
            AddFilter(new BroadbandTransientFilter("SN", (f, i) => f.Spectrum2[i], 64, 224, MathExt.LinearDecay(16)) { TriggerHigh = 0.8f, TriggerLow = 0.75f, MaxGain = 8f, AGCDecay = 0.999f });  // 256 spectrum


            //AddFilter(new BroadbandTransientFilter("HH2", (f, i) => f.Spectrum2DB[i], 128, 96, Enumerable.Range(0, 16).Select(i => i < 8 ? 0.05f : -0.02f).ToArray()) { TriggerHigh = 0.4f, TriggerLow = 0.35f });  // 256 spectrum

            //AddFilter(new BroadbandTransientFilter("CL1", 8, 16) { Threshold = 0.15f });
            //AddFilter(new BroadbandTransientFilter("CL2", 16, 16) { Threshold = 0.15f });
            //AddFilter(new NeuralNetworkFilter("NN01"));
        }

        public void Process(AudioAnalysisSample frame)
        {
            SpectrumHistory.Add(frame.Spectrum);
            filterParams.Frame = frame;

            // generate downmixed spectrums
            for (int i = 0; i < Globals.SPECTRUMRES; i++)
            {
                filterParams.Spectrum[i] = (frame.Spectrum[i * 2] + frame.Spectrum[i * 2 + 1]) * 0.5f;
                //filterParams.SpectrumDB[i] = (float)Decibels.LinearToDecibels(filterParams.Spectrum[i]);

                filterParams.SpectrumDB[i] = filterParams.Spectrum[i].NormDB();
                //s.rgb = 20.0 * log(s.rgb);
                //s.rgb = max(vec3(0.0), vec3(1.0) + ((s.rgb + vec3(20.0)) / vec3(200.0)));

            }

            for (int i = 0; i < Globals.SPECTRUM2RES; i++)
            {
                filterParams.Spectrum2[i] = frame.Spectrum2[i];
                filterParams.Spectrum2DB[i] = frame.Spectrum2[i].NormDB();
            }

            // run filters
            foreach (var filter in Filters)
            {
                var results = filter.GetValues(filterParams);
                for (int i = 0; i < filter.OutputSlotCount; i++)
                {
                    frame.AudioData[filter.OutputOffset + i] = results[i];
                }
            }
        }

        private void AddFilter(ISpectrumFilter filter)
        {
            if (maxFilterOutputIndex + filter.OutputSlotCount >= Globals.AUDIODATASIZE)
                throw new InvalidOperationException("SpectrumAnalyser out of slots for filter.");

            // get output labels
            for (int i = 0; i < filter.OutputSlotCount; i++)
            {
                OutputNames.Add(filter.GetOutputName(i));
            }

            filter.OutputOffset = maxFilterOutputIndex;
            maxFilterOutputIndex += filter.OutputSlotCount;
            Filters.Add(filter);
        }

        public IEnumerable<Tuple<string,FilterParameter>> GetFilterParameters()
        {
            return Filters.SelectMany(f => f.Parameters().Select(p => new Tuple<string, FilterParameter>(f.Name, p)));
        }
    }
}

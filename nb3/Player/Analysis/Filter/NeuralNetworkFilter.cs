using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nb3.Common;
using NeuralNetwork;
using NeuralNetwork.Nodes;

namespace nb3.Player.Analysis.Filter
{
    public class NeuralNetworkFilter : SpectrumFilterBase, ISpectrumFilter
    {
        private const int HISTORY_SIZE = 1;
        private const int NUMOUTPUTS = 3;
        public int OutputOffset { get; set; }
        public int OutputSlotCount { get { return NUMOUTPUTS; } }

        public enum Outputs
        {
            TrainingTarget,
            Output,
            Error
        }


        private float[] output = new float[NUMOUTPUTS];

        private Network neuralNetwork = new Network();
        private INetworkRunContext context;
        private float avgError = 0f;

        private ISpectrumFilter trainingFilter = new BroadbandTransientFilter("BD", (f, i) => f.Spectrum[i], 0, 12, MathExt.Flat(4)) { TriggerHigh = 0.5f, TriggerLow = 0.45f, MaxGain = 6f };

        public NeuralNetworkFilter(string name) : base(name, "TRAIN", "OUT", "ERR")
        {
            IActivationFunction activationFunction = new NeuralNetwork.Nodes.Activations.Tanh();
            neuralNetwork.SetInputs(Globals.SPECTRUMRES * HISTORY_SIZE);
            neuralNetwork.AddLayer(activationFunction, 4);
            neuralNetwork.AddLayer(activationFunction, 1);
            neuralNetwork.LearningRate = 0.25f;
            neuralNetwork.Momentum = 0.2f;
            context = neuralNetwork.GetNewContext();
        }

        public float[] GetValues(FilterParameters frame)
        {
            // get training output
            float target = trainingFilter.GetValues(frame)[(int)BroadbandTransientFilter.Outputs.Level];
            if (target < 0.98f) target = 0f;

            // train
            context.Set(frame.SpectrumDB.Take(context.InputCount));
            context.Target[0] = target;
            output[(int)Outputs.TrainingTarget] = target;
            neuralNetwork.Train(context);
            neuralNetwork.Update();
            float error = context.TotalError;
            avgError = avgError * 0.95f + 0.05f * error;
            output[(int)Outputs.Error] = avgError * 4.0f;

            // fill context with current spectrum frame
            context.Set(frame.SpectrumDB.Take(context.InputCount));

            neuralNetwork.Run(context);

            output[(int)Outputs.Output] = context.Outputs.First();
            return output;
        }

        public void Reset()
        {

        }


    }
}

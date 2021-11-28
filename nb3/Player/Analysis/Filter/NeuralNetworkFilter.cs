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
        private float avgError = 0f;
        private INetworkRunContext runContext;
        private INetworkRunContext trainingContext;
        private int trainingRuns = 0;

        private ISpectrumFilter trainingFilter = new BroadbandTransientFilter("BD", (f, i) => f.Spectrum[i], 0, 12, MathExt.Flat(4)) { TriggerHigh = 0.5f, TriggerLow = 0.45f, MaxGain = 6f };

        public NeuralNetworkFilter(string name) : base(name, "TRAIN", "OUT", "ERR")
        {
            IActivationFunction activationFunction = new NeuralNetwork.Nodes.Activations.Tanh();
            neuralNetwork.SetInputs(Globals.SPECTRUMRES * HISTORY_SIZE);
            neuralNetwork.AddLayer(activationFunction, 4);
            neuralNetwork.AddLayer(activationFunction, 1);
            //neuralNetwork.LearningRate = 0.02f;
            neuralNetwork.Momentum = 0.25f;
            
            runContext = neuralNetwork.GetNewContext();
            trainingContext = neuralNetwork.GetNewContext();
        }

        public float[] GetValues(FilterParameters frame)
        {
            // training
            trainingRuns++;

            if (trainingRuns < 10000)
            {
                float target = trainingFilter.GetValues(frame)[(int)BroadbandTransientFilter.Outputs.Edge];
                if (target < 0.95f) target = 0f;
                output[(int)Outputs.TrainingTarget] = target;
                neuralNetwork.LearningRate = target > 0.5f ? 0.05f : 0.01f;
                avgError = avgError * 0.99f + 0.01f * TrainNetwork(trainingContext, frame.SpectrumDB, target);
                output[(int)Outputs.Error] = avgError * 4.0f;
            }
            else
            {
                output[(int)Outputs.TrainingTarget] = 0.5f;
                output[(int)Outputs.Error] = 0f;
            }

            // fill context with current spectrum frame
            runContext.Set(frame.SpectrumDB.Take(runContext.InputCount));

            neuralNetwork.Run(runContext);

            output[(int)Outputs.Output] = runContext.Outputs.First();
            return output;
        }

        private float TrainNetwork(INetworkRunContext context, IEnumerable<float> inputs, float target)
        {
            context.Set(inputs.Take(context.InputCount));
            context.Target[0] = target;
            neuralNetwork.Train(context);
            neuralNetwork.Update();
            return context.TotalError;
        }

        public void Reset()
        {

        }


    }
}

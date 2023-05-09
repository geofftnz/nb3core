using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis.Filter.Nodes
{
    public class StepwiseAccumulator : IFilterNode
    {
        private float _threshold = 1f;
        private int _counter = 0;
        private int _counterScale = 256;

        public StepwiseAccumulator(float threshold = 1f, int counterScale = 256)
        {
            _threshold = threshold;
            _counterScale = counterScale;
        }

        public float Get(float input)
        {
            if (input >= _threshold)
            {
                _counter++;
                _counter %= _counterScale;
            }
            return (float)_counter / (float)_counterScale;
        }
    }
}

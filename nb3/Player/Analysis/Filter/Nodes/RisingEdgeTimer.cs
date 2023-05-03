using nb3.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis.Filter.Nodes
{
    public class RisingEdgeTimer : IFilterNode
    {
        public float Threshold { get; set; }
        public float Decay { get; set; }

        private float previous = 0f;
        private float value = 0f;
        

        public RisingEdgeTimer(float threshold = 0.5f, float decay = 0.01f)
        {
            Threshold = threshold;
            Decay = decay;
        }

        public float Get(float input)
        {
            value = Math.Max(0.0f,value-Decay);
            if (previous < Threshold && input>= Threshold)
            {
                value = 1.0f;
            }
            previous = input;
            return value;
        }
    }
}

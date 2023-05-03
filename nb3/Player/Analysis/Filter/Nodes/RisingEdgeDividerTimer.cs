using nb3.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis.Filter.Nodes
{
    public class RisingEdgeDividerTimer : IFilterNode
    {
        public float Threshold { get; set; }
        public float Decay { get; set; }
        public int Divide { get; set; }

        private float previous = 0f;
        private float value = 0f;
        private int counter = 0;
        

        public RisingEdgeDividerTimer(int divide = 4, float threshold = 0.5f, float decay = 0.01f)
        {
            Threshold = threshold;
            Decay = decay;
            Divide = divide;
        }

        public float Get(float input)
        {
            value = Math.Max(0.0f,value-Decay);
            if (previous < Threshold && input>= Threshold)
            {
                if (counter == 0)
                {
                    value = 1.0f;
                }
                counter = (counter + 1) % Divide;
            }
            previous = input;
            return value;
        }

        public void Reset()
        {
            counter = 0;
        }
    }
}

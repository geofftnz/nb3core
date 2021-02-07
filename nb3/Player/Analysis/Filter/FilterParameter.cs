using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Player.Analysis.Filter
{
    public class FilterParameter
    {
        public string Name { get; set; }
        public Func<float> GetValue;
        public Action<float> SetValue { get; set; }
        public float Delta { get; set; }
    }
}

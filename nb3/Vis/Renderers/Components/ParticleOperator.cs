using OpenTKExtensions.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Vis.Renderers.Components
{
    public class ParticleOperator : OperatorComponentBase
    {

        public ParticleOperator() : base("particles/operator.vert.glsl", "particles/operator.frag.glsl")
        {

        }

    }
}

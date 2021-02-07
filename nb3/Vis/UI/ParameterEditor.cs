using OpenTK.Mathematics;
using OpenTKExtensions.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Vis.UI
{
    public class ParameterEditor : GameComponentBase, IRenderable, ITransformable
    {
        public Matrix4 ViewMatrix { get; set; }
        public Matrix4 ModelMatrix { get; set; }
        public Matrix4 ProjectionMatrix { get; set; }

        public bool Visible { get; set; }
        public int DrawOrder { get; set; }




        public void Render(IFrameRenderData frameData)
        {
            
        }
    }
}

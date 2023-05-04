using nb3.Vis.Renderers.Components;
using OpenTKExtensions.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Vis.Renderers
{
    public class BasicShaderRenderer : CompositeGameComponent, IRenderable, IUpdateable, IReloadable, IKeyboardControllable, IResizeable
    {

        private BasicShaderHost shaderHost;

        public BasicShaderRenderer(string vertexShaderFileName, string fragmentShaderFileName)
        {
            components.Add(shaderHost = new BasicShaderHost(vertexShaderFileName, fragmentShaderFileName));
        }
        public BasicShaderRenderer(string fragmentShaderFileName)
            : this(@"shaderhost.glsl|vert", fragmentShaderFileName)
        {
        }

        public BasicShaderRenderer()
            : this(@"shaderhost.glsl|vert", @"effects/pulse.glsl|effect")
        {
        }

    }
}

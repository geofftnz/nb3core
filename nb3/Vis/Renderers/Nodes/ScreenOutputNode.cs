using OpenTKExtensions.Framework;
using OpenTKExtensions.Framework.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;

namespace nb3.Vis.Renderers.Nodes
{
    public class ScreenOutputNode : RenderGraphNodeBase
    {
        public ScreenOutputNode() : base()
        {
            IsRoot = true;
            IsFinalOutput = true;

            ChildComponent = new OperatorComponentBase("PostProcess/output.vert.glsl", "PostProcess/output.frag.glsl")
            {
                IsFinalOutput = true,
                TextureBinds = () =>
                {
                    (_input["tex"] as GraphNodeTexturePort).InternalValue.Bind(TextureUnit.Texture0);
                },
                SetShaderUniforms = (sp) =>
                {
                    if (sp != null)
                    {
                        sp.SetUniform("inputTex", 0);
                    }
                }
            };

            _input.Add("tex", new GraphNodeTexturePort()
            {
                Name = "tex",
                Target = TextureTarget.Texture2D,
                Format = PixelFormat.Rgba
            });

        }

        protected override void AssignInputs()
        {
            (_input["tex"] as GraphNodeTexturePort).InternalValue.Bind(TextureUnit.Texture0);
        }

        protected override void AssignOutputs()
        {
            // no outputs
        }
    }
}

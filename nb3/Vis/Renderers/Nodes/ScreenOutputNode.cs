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
                TextureBinds = (fd) =>
                {
                    
                    (fd as FrameData)?.GlobalTextures.SpectrumTex.Bind(TextureUnit.Texture0);
                    (fd as FrameData)?.GlobalTextures.Spectrum2Tex.Bind(TextureUnit.Texture1);
                    (fd as FrameData)?.GlobalTextures.AudioDataTex.Bind(TextureUnit.Texture2);
                    (_input["tex"] as GraphNodeTexturePort).InternalValue.Bind(TextureUnit.Texture3);
                },
                SetShaderUniforms = (sp,fd) =>
                {
                    var frameData = fd as FrameData;
                    if (sp != null)
                    {
                        sp
                        .SetUniform("time", (float)frameData.Time)
                        .SetUniform("spectrumTex", 0)
                        .SetUniform("spectrum2Tex", 1)
                        .SetUniform("audioDataTex", 2)
                        .SetUniform("inputTex", 3)
                        .SetUniform("currentPosition", frameData.GlobalTextures.SamplePositionRelative)
                        .SetUniform("currentPositionEst", frameData.GlobalTextures.EstimatedSamplePositionRelative);
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

        protected override void AssignInputs(IFrameRenderData frameData)
        {
            (_input["tex"] as GraphNodeTexturePort).InternalValue.Bind(TextureUnit.Texture0);
        }

        protected override void AssignOutputs(IFrameRenderData frameData)
        {
            // no outputs
        }
    }
}

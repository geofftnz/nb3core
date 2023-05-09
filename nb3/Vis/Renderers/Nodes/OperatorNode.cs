using OpenTKExtensions.Framework;
using OpenTKExtensions.Framework.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTKExtensions.Resources;

namespace nb3.Vis.Renderers.Nodes
{
    public class OperatorNode : RenderGraphNodeBase, IResizeable
    {
        public OperatorNode(string fragmentShader, SizeInheritance inheritSize = SizeInheritance.Inherit) : base(false, inheritSize)
        {
            SetOutput(0, new TextureSlotParam(TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.HalfFloat, false,
                TextureParameter.Create(TextureParameterName.TextureMagFilter, TextureMagFilter.Linear),
                TextureParameter.Create(TextureParameterName.TextureMinFilter, TextureMinFilter.Linear),
                TextureParameter.Create(TextureParameterName.TextureWrapS, TextureWrapMode.ClampToEdge),
                TextureParameter.Create(TextureParameterName.TextureWrapT, TextureWrapMode.ClampToEdge)
                ));

            _output.Add("tex", new GraphNodeTexturePort()
            {
                Target = TextureTarget.Texture2D,
                Format = PixelFormat.Rgba,
                Name = "tex"
            });


            ChildComponent = new OperatorComponentBase("Common/simple.vert", fragmentShader)
            {
                TextureBinds = (fd) =>
                {
                    
                    (fd as FrameData)?.GlobalTextures.SpectrumTex.Bind(TextureUnit.Texture0);
                    (fd as FrameData)?.GlobalTextures.Spectrum2Tex.Bind(TextureUnit.Texture1);
                    (fd as FrameData)?.GlobalTextures.AudioDataTex.Bind(TextureUnit.Texture2);
                },
                SetShaderUniforms = (sp,fd) =>
                {
                    var frameData = fd as FrameData;
                    if (sp != null)
                    {
                        sp
                        .SetUniform("time", (float)frameData.Time)
                        .SetUniform("aspectRatio", AspectRatio)
                        .SetUniform("spectrumTex", 0)
                        .SetUniform("spectrum2Tex", 1)
                        .SetUniform("audioDataTex", 2)
                        .SetUniform("input0Tex", 3)
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
            (_input["tex"] as GraphNodeTexturePort).InternalValue.Bind(TextureUnit.Texture3);
        }

        protected override void AssignOutputs(IFrameRenderData frameData)
        {
            _output["tex"].Value = GetTexture(0);
        }
    }
}

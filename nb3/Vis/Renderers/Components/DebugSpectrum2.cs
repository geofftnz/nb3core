using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTKExtensions;
using OpenTKExtensions.Framework;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
using NLog;
using OpenTK.Mathematics;

namespace nb3.Vis.Renderers.Components
{

    /// <summary>
    /// Renders the current state of the audio spectrum buffer. This will be a 1024x1024 1-channel fp32 texture.
    /// 
    /// The texture will be supplied from the common texture set.
    /// 
    /// </summary>
    public class DebugSpectrum2 : OperatorComponentBase, IRenderable, IReloadable, ITransformable
    {
        public Matrix4 ViewMatrix { get; set; } = Matrix4.Identity;
        public Matrix4 ModelMatrix { get; set; } = Matrix4.Identity;
        public Matrix4 ProjectionMatrix { get; set; } = Matrix4.Identity;

        public DebugSpectrum2() : base(@"DebugSpectrum.glsl|vert", @"DebugSpectrum.glsl|waterfall2_frag")
        {
            TextureBinds = (fd) =>
            {
                var frameData = fd as FrameData;
                if (frameData != null)
                {
                    frameData.GlobalTextures.Spectrum2Tex.Bind(TextureUnit.Texture0);
                }
            };

            SetShaderUniforms = (sp, fd) =>
            {
                var frameData = fd as FrameData;
                if (frameData != null && sp != null)
                {
                    sp
                    .SetUniform("time", (float)frameData.Time)
                    .SetUniform("spectrumTex", 0)
                    .SetUniform("projectionMatrix", ProjectionMatrix)
                    .SetUniform("modelMatrix", ModelMatrix)
                    .SetUniform("viewMatrix", ViewMatrix)
                    .SetUniform("currentPosition", frameData.GlobalTextures.SamplePositionRelative)
                    .SetUniform("currentPositionEst", frameData.GlobalTextures.EstimatedSamplePositionRelative);
                }
            };
        }

        public override void Render(IFrameRenderData renderData, IFrameBufferTarget target)
        {
            base.Render(renderData, target);
        }
    }
}

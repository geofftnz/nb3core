﻿using System;
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
    public class DebugSpectrumWaterfall : OperatorComponentBase, IRenderable, IReloadable, ITransformable
    {
        public Matrix4 ViewMatrix { get; set; } = Matrix4.Identity;
        public Matrix4 ModelMatrix { get; set; } = Matrix4.Identity;
        public Matrix4 ProjectionMatrix { get; set; } = Matrix4.Identity;

        public DebugSpectrumWaterfall() : base(@"DebugSpectrum.glsl|vert", @"DebugSpectrum.glsl|waterfall_frag")
        {
            TextureBinds = (fd) =>
            {
                var frameData = fd as FrameData;
                if (frameData != null)
                {
                    frameData.GlobalTextures.SpectrumTex.Bind(TextureUnit.Texture0);
                    frameData.GlobalTextures.AudioDataTex.Bind(TextureUnit.Texture1);
                }
            };

            SetShaderUniforms = (sp,fd) =>
            {
                var frameData = fd as FrameData;
                if (frameData != null && sp != null)
                {
                    sp
                    .SetUniform("spectrumTex", 0)
                    .SetUniform("audioDataTex", 1)
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

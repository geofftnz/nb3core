using nb3.Vis.Renderers.Components;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTKExtensions.Components.ParticleSystem.Models;
using OpenTKExtensions.Components.ParticleSystem.Renderers;
using OpenTKExtensions.Components.ParticleSystem.Targets;
using OpenTKExtensions.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Vis.Renderers
{
    public class ParticleRenderer : CompositeGameComponent, IRenderable, IUpdateable, IReloadable, IKeyboardControllable, ITransformable, IResizeable
    {
        private const int particleArrayWidth = 2048;
        private const int particleArrayHeight = 2048;

        private DefaultParticleModel model;
        private IParticleRenderTarget renderTarget;
        private ColourParticleRenderer renderer;
        private ParticleOperator operator1;

        public Matrix4 ViewMatrix { get; set; }
        public Matrix4 ModelMatrix { get; set; }
        public Matrix4 ProjectionMatrix { get; set; }

        /// <summary>
        /// Framedata snooped from Render()
        /// </summary>
        private FrameData frameData;
        private float deltaTime = 0f;

        public ParticleRenderer()
        {
            // create the model, which contains the textures that represent the particles.
            components.Add(model = new DefaultParticleModel(particleArrayWidth, particleArrayHeight));

            // create the render target, which is used to write to the model
            components.Add(renderTarget = new PosColRenderTarget(particleArrayWidth, particleArrayHeight)
            {
                DrawOrder = 1,
                IsFinalOutput = false,
                SetBuffers = (rt) =>
                {
                    rt.SetOutput(0, model.ParticlePositionWrite);
                    rt.SetOutput(1, model.ParticleColourWrite);
                }
            }) ;

            // Add operator(s) to renderTarget. The operators write to the render target to alter the particles.
            renderTarget.Add(operator1 = new ParticleOperator()
            {
                IsFinalOutput = true,
                TextureBinds = () =>
                {
                    if (frameData != null)
                    {
                        frameData.GlobalTextures.SpectrumTex.Bind(TextureUnit.Texture0);
                        frameData.GlobalTextures.AudioDataTex.Bind(TextureUnit.Texture1);
                        model.ParticlePositionRead.Bind(TextureUnit.Texture2);
                        model.ParticlePositionPrevious.Bind(TextureUnit.Texture3);
                        model.ParticleColourRead.Bind(TextureUnit.Texture4);
                    }
                },
                SetShaderUniforms = (sp) =>
                {
                    if (sp != null && frameData != null)
                    {
                        sp
                        .SetUniform("time", (float)(frameData.Time))
                        .SetUniform("deltaTime", deltaTime)
                        .SetUniform("spectrumTex", 0)
                        .SetUniform("audioDataTex", 1)
                        .SetUniform("particlePosTex", 2)
                        .SetUniform("particlePosPrevTex", 3)
                        .SetUniform("particleColTex", 4)
                        .SetUniform("currentPosition", frameData.GlobalTextures.SamplePositionRelative)
                        .SetUniform("currentPositionEst", frameData.GlobalTextures.EstimatedSamplePositionRelative);
                    }
                }
            }) ;

            // create the renderer, which renders GL_POINTS using vertices that point to texels in the model textures.
            components.Add(renderer = new ColourParticleRenderer("Particles/particles_col.vert.glsl", "Particles/particles_col.frag.glsl", particleArrayWidth, particleArrayHeight)
            {
                DrawOrder = 2,
                IsFinalOutput = true,
                ParticlePositionTextureFunc = () => model.ParticlePositionWrite,
                ParticleColourTextureFunc = () => model.ParticleColourWrite
            }) ;

            ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f), 16f / 9f, 0.0001f, 10f);
            ViewMatrix = Matrix4.LookAt(new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 0f), new Vector3(0f, 1f, 0f));
            ModelMatrix = Matrix4.Identity;
        }

        public override void Resize(int width, int height)
        {
            float aspect = (float)width / (float)(height > 0 ? height : width);
            ProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(60f), aspect, 0.0001f, 10f);

            renderer.ProjectionMatrix = ProjectionMatrix;
            renderer.ViewMatrix = ViewMatrix;
            renderer.ModelMatrix = ModelMatrix;

            base.Resize(width, height);
        }

        public override void Render(IFrameRenderData frameData, IFrameBufferTarget target)
        {
            var fd = frameData as FrameData;
            if (this.frameData != null && fd != null)
            {
                deltaTime = (float)(this.frameData.Time - fd.Time);
            }
            this.frameData = fd;

            base.Render(frameData, target);
            model.SwapBuffers(); // TODO: this should happen automatically - move it to the particle system.
        }
    }
}

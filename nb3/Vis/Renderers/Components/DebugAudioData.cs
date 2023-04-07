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
using OpenTKExtensions.Text;
using nb3.Common;
using NLog.Layouts;
using OpenTK.Mathematics;

namespace nb3.Vis.Renderers.Components
{

    /// <summary>
    /// Renders the current state of the audio spectrum buffer. This will be a 1024x1024 1-channel fp32 texture.
    /// 
    /// The texture will be supplied from the common texture set.
    /// 
    /// </summary>
    public class DebugAudioData : OperatorComponentBase, IRenderable, IReloadable, ITransformable
    {
        public Matrix4 ViewMatrix { get; set; } = Matrix4.Identity;
        public Matrix4 ModelMatrix { get; set; } = Matrix4.Identity;
        public Matrix4 ProjectionMatrix { get; set; } = Matrix4.Identity;

        // temporary component collection support until OperatorComponentBase can be changed to derive from a composite component.
        protected GameComponentCollection components = new GameComponentCollection();

        private List<string> filterOutputNames = new List<string>();
        protected TextManager textManager;

        public DebugAudioData(Font font, List<string> outputNames) : base(@"DebugAudioData.glsl|vert", @"DebugAudioData.glsl|frag")
        {
            TextureBinds = (fd) =>
            {
                var frameData = fd as FrameData;
                if (frameData != null)
                {
                    frameData.GlobalTextures.AudioDataTex.Bind(TextureUnit.Texture0);
                }
            };

            SetShaderUniforms = (sp,fd) =>
            {
                var frameData = fd as FrameData;
                if (frameData != null && sp != null)
                {
                    sp
                    .SetUniform("audioDataTex", 0)
                    .SetUniform("projectionMatrix", ProjectionMatrix)
                    .SetUniform("modelMatrix", ModelMatrix)
                    .SetUniform("viewMatrix", ViewMatrix)
                    .SetUniform("currentPosition", frameData.GlobalTextures.SamplePositionRelative)
                    .SetUniform("currentPositionEst", frameData.GlobalTextures.EstimatedSamplePositionRelative);
                }
            };

            Loading += DebugAudioData_Loading;
            Unloading += DebugAudioData_Unloading;


            if (outputNames != null)
            {
                filterOutputNames.AddRange(outputNames);
            }

            components.Add(textManager = new TextManager("tm", font) { DrawOrder = 2 });
        }

        private void DebugAudioData_Unloading(object sender, EventArgs e)
        {
            components.Unload();
        }

        private void DebugAudioData_Loading(object sender, EventArgs e)
        {
            components.Load();

            float rowsize = 2f / Globals.AUDIODATASIZE;
            int i = 0;

            foreach (var s in filterOutputNames)
            {
                var tb = new TextBlock($"F{i:000}", $"{i:000} {s}", new Vector3(0.0f, 0.0f + (i + .5f) * rowsize, 0.0f), 0.07f / 1024f, new Vector4(1f, 1f, 1f, .2f));
                textManager.AddOrUpdate(tb);

                var tb2 = new TextBlock($"FV{i:000}", $"value", new Vector3(0.1f, 0.0f + (i + .5f) * rowsize, 0.0f), 0.07f / 1024f, new Vector4(1f, 1f, 1f, .2f));
                textManager.AddOrUpdate(tb2);

                i++;
            }
        }

        public override void Render(IFrameRenderData renderData, IFrameBufferTarget target)
        {
            base.Render(renderData, target);  // We render our component (line graphs), then we render the text over the top (below).


            // update value display
            for(int i = 0; i < filterOutputNames.Count; i++)
            {
                textManager.Blocks[$"FV{i:000}"].Text = $"{(renderData as FrameData)?.GlobalTextures.LastSample.AudioData[i]:0.000}";
            }


            textManager.ModelMatrix = Matrix4.CreateScale(2f, ModelMatrix.Row1.Y, 1f);
            textManager.ViewMatrix = ViewMatrix;
            textManager.ProjectionMatrix = ProjectionMatrix;
            textManager.Refresh();

            //components.Do<ITransformable>(c => { c.ViewMatrix = ViewMatrix; c.ProjectionMatrix = ProjectionMatrix; }); // TODO: temp hack until operatorcomponentbase is derived from compositecomponent

            // TODO: this needs to be simplified. See note above about OperatorComponentBase / CompositeComponent etc
            if (target != null)
            {
                components.RenderToTarget(renderData, target);
            }
            else
            {
                components.Render(renderData);
            }

        }

        private void LayoutLabels()
        {
            //if (_doneTextLayout) return;


        }
    }
}

using OpenTKExtensions.Framework;
using OpenTKExtensions;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using nb3.Vis.Renderers.Components;
using OpenTK.Input;
using OpenTKExtensions.Input;
using OpenTKExtensions.Text;
using NLog.Filters;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Windowing.Common;
using nb3.Vis.UI;

namespace nb3.Vis.Renderers
{
    public class AnalysisDebugRenderer : CompositeGameComponent, IRenderable, IUpdateable, IReloadable, IResizeable, IKeyboardControllable
    {
        private DebugSpectrumWaterfall waterfall;
        private DebugSpectrum2 waterfall2;
        private DebugSpectrum spectrum;
        private DebugAudioData datagraphs;

        private Matrix4 projection = Matrix4.Identity;
        private Matrix4 view = Matrix4.Identity;
        private Matrix4 waterfallModel = Matrix4.Identity;
        private Matrix4 waterfall2Model = Matrix4.Identity;
        private Matrix4 spectrumModel = Matrix4.Identity;
        private Matrix4 audioDataModel = Matrix4.Identity;

        private ParameterEditor parameterEditor;
        private KeyboardActionManager keyboardActions;

        private float ypos = 0.0f;
        private float ypostarget = 0.0f;
        private const float yshift = 0.05f;


        private Matrix4 GetLayout(float size, ref float offset)
        {
            // model vertices are -1 -> 1 (zero-centered), so we need to bring them into the 0-1 range. (0.5-centered)
            // we also apply a vertical scale factor for the size of the component.
            return Matrix4.CreateScale(0.5f, 0.5f, 1.0f) * Matrix4.CreateTranslation(0.5f, 0.5f, 0.0f) * Matrix4.CreateScale(1f, size, 1f);
        }

        public AnalysisDebugRenderer(Font font, Player.Player player)
        {
            projection = Matrix4.CreateOrthographicOffCenter(0f, 1f, 1f, 0f, 0.0f, 10f);  // 0,0 in top left

            //waterfallModel = Matrix4.CreateScale(1.0f, 0.5f, 1.0f) * Matrix4.CreateTranslation(0.0f, 0.5f, 0.0f);
            //spectrumModel = Matrix4.CreateScale(1.0f, 0.2f, 1.0f) * Matrix4.CreateTranslation(0.0f, 0.3f, 0.0f);
            //waterfall2Model = Matrix4.CreateScale(1.0f, 0.3f, 1.0f) * Matrix4.CreateTranslation(0.0f, 0.0f, 0.0f);
            //audioDataModel = Matrix4.CreateScale(1.0f, 20.0f, 1.0f) * Matrix4.CreateTranslation(0.0f, -20.0f, 0.0f);

            float offset = 1.0f;
            waterfallModel = GetLayout(0.5f, ref offset);
            spectrumModel = GetLayout(0.2f, ref offset);
            waterfall2Model = GetLayout(0.2f, ref offset);
            audioDataModel = GetLayout(10.0f, ref offset);  // 20

            int drawOrder = 1;
            components.Add(waterfall = new DebugSpectrumWaterfall() { DrawOrder = drawOrder++, ModelMatrix = waterfallModel, ProjectionMatrix = projection });
            components.Add(spectrum = new DebugSpectrum() { DrawOrder = drawOrder++, ModelMatrix = spectrumModel, ProjectionMatrix = projection });
            components.Add(waterfall2 = new DebugSpectrum2() { DrawOrder = drawOrder++, ModelMatrix = waterfall2Model, ProjectionMatrix = projection });
            components.Add(datagraphs = new DebugAudioData(font, player.FilterOutputNames) { DrawOrder = drawOrder++, ModelMatrix = audioDataModel, ProjectionMatrix = projection });
            components.Add(keyboardActions = new KeyboardActionManager() { KeyboardPriority = 100 }, 1);

            components.Add(parameterEditor = new ParameterEditor(font) { DrawOrder = drawOrder++ });

            keyboardActions.Add(Keys.Up, 0, () => { ypostarget += yshift; });
            keyboardActions.Add(Keys.Down, 0, () => { ypostarget -= yshift; });

            //parameterEditor.AddParameter("p1", () => 123.4567f, (f) => { });
            //parameterEditor.AddParameter("p2", () => 0.007f, (f) => { });

            // copy parameters over
            foreach (var p in player.FilterParameters)
            {
                parameterEditor.AddParameter($"{p.Item1}_{p.Item2.Name}", p.Item2.GetValue, p.Item2.SetValue, p.Item2.Delta);
            }

        }

        public override bool ProcessKeyDown(KeyboardKeyEventArgs e)
        {
            return base.ProcessKeyDown(e);
        }

        public override void Update(IFrameUpdateData frameData)
        {
            ypos = ypos * 0.9f + 0.1f * ypostarget;
            if (Math.Abs(ypos - ypostarget) < 0.0005)
                ypos = ypostarget;

            UpdateView();
            base.Update(frameData);
        }

        private void UpdateView()
        {
            view = Matrix4.CreateTranslation(0f, ypos, 0f);

            float y = ypos;
            //components.Do<ITransformable>(c => c.ViewMatrix = view);
            // set the view matrices of our components
            foreach (var c in components.OfType<ITransformable>())
            {
                c.ViewMatrix = Matrix4.CreateTranslation(0f, y, 0f);
                y += c.ModelMatrix.Row1.Y * 2.0f; // offset by y scale, undoing half-scaling from model
            }


        }

    }
}

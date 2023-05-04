using OpenTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using System.Threading;
using OpenTKExtensions;
using OpenTKExtensions.Framework;
using OpenTKExtensions.Text;
using System.Diagnostics;
using OpenTKExtensions.Filesystem;
using System.Collections.Concurrent;
using OpenTKExtensions.Input;
using OpenTK.Input;
using nb3.Player.Analysis;
using OpenTKExtensions.Resources;
using nb3.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using NLog;
using OpenTKExtensions.Framework.Graph;
using nb3.Vis.Renderers.Nodes;

namespace nb3.Vis
{
    public class VisHost : GameWindow
    {
        private const string SHADERPATH = @"../../../Res/Shaders;Res/Shaders";

        private GameComponentCollection components = new GameComponentCollection();
        private Font font;
        private TextManager text;
        private KeyboardActionManager keyboardActions;
        private ComponentSwitcher switcher;

        private TextBlock title = new TextBlock("t0", "NeuralBeat3 ©2016-2020 Geoff Thornburrow", new Vector3(0.0f, 0.05f, 0f), 0.0005f, new Vector4(1f, 0.8f, 0.2f, 1f));
        private Matrix4 overlayProjection;
        private Matrix4 overlayModelview;

        private FrameData frameData = new FrameData();
        private GlobalTextures globalTextures = new GlobalTextures();
        private Stopwatch timer = new Stopwatch();
        private OpenTKExtensions.Components.FrameCounter frameCounter;
        private FileSystemPoller shaderUpdatePoller = new FileSystemPoller(SHADERPATH.Split(';')[0]);
        private double lastShaderPollTime = 0.0;

        private object threadLock = new object();


        private ConcurrentQueue<AudioAnalysisSample> sampleQueue = new ConcurrentQueue<AudioAnalysisSample>();

        private float[] tempSpectrum = new float[Globals.SPECTRUMRES]; // this will be coming in from the analysis side.

        private object playerPropertyLock = new object();
        private Player.Player _player = null;
        public Player.Player Player
        {
            get
            {
                lock (playerPropertyLock)
                    return _player;
            }
            set
            {
                lock (playerPropertyLock)
                {
                    //TODO: unhook existing events?
                    _player = value;

                    if (_player != null)
                    {
                        _player.SpectrumReady += (s, e) => { AddSample(e.Sample); };
                        //_player.PlayerStart += OnPlayerStart;
                    }

                }
            }
        }

        public ConcurrentQueue<AudioAnalysisSample> SampleQueue
        {
            get
            {
                return sampleQueue;
            }

            set
            {
                sampleQueue = value;
            }
        }

        private int lastTracksPlayed = 0;


        public VisHost(Player.Player player)
            : base(
                  GameWindowSettings.Default,
                  //new GameWindowSettings {
                  //    IsMultiThreaded = false,
                  //    UpdateFrequency = 120
                  //},
                  new NativeWindowSettings
                  {
                      Size = new Vector2i(1600, 900),
                      APIVersion = new Version(4, 5),
                      Profile = ContextProfile.Compatability,
                      Flags = ContextFlags.Default,
                      Title = "NeuralBeat3",
                      API = ContextAPI.OpenGL
                  })
        {
            Player = player;


            //TargetRenderFrequency = 144f;
            //this.RenderFrequency = 144.0;
            VSync = VSyncMode.On;

            UpdateFrame += VisHost_UpdateFrame;
            RenderFrame += VisHost_RenderFrame;
            Load += VisHost_Load;
            Unload += VisHost_Unload;
            Resize += VisHost_Resize;
            Closed += VisHost_Closed;
            Closing += VisHost_Closing;

            //Keyboard.KeyDown += Keyboard_KeyDown;
            //Keyboard.KeyUp += Keyboard_KeyUp;
            this.KeyDown += Keyboard_KeyDown;
            this.KeyUp += Keyboard_KeyUp;

            // set default shader loader
            ShaderProgram.DefaultLoader = new OpenTKExtensions.Loaders.MultiPathFileSystemLoader(SHADERPATH);


            // framedata setup
            frameData.GlobalTextures = globalTextures;

            // create components
            //components.Add(font = new Font(@"res\font\calibrib.ttf_sdf.2048.png", @"res\font\calibrib.ttf_sdf.2048.txt"), 1);
            components.Add(font = new Font(@"res\font\lucon.ttf_sdf.1024.png", @"res\font\lucon.ttf_sdf.1024.txt"), 1);
            components.Add(text = new TextManager("texmgr", font), 2);
            components.Add(keyboardActions = new KeyboardActionManager() { KeyboardPriority = int.MaxValue }, 1);
            components.Add(globalTextures);
            components.Add(frameCounter = new OpenTKExtensions.Components.FrameCounter(font));
            components.Add(switcher = new ComponentSwitcher() { KeyForward = new KeySpec(Keys.Tab), KeyBackward = new KeySpec(Keys.Tab, KeyModifiers.Shift) });

            switcher.Add(new Renderers.AnalysisDebugRenderer(font, Player));
            switcher.Add(new Renderers.BasicShaderRenderer());
            switcher.Add(new Renderers.BasicShaderRenderer("effects/spiral.glsl|effect"));
            switcher.Add(new Renderers.ParticleRenderer());

            var graph = new ComponentGraph();
            graph.Add(new ParticleNode() { Name = "particles" });
            graph.Add(new ScreenOutputNode() { Name = "output" }); 
            graph.AddEdge(new NodePortReference() { Node = "particles", Port = "tex" }, new NodePortReference() { Node = "output", Port = "tex" });

            switcher.Add(graph);


            var graph2 = new ComponentGraph();
            graph2.Add(new ParticleNode() { Name = "particles"});
            graph2.Add(new OperatorNode("effects/kaleidoscope.frag") { Name = "kaleidoscope" });
            graph2.Add(new ScreenOutputNode() { Name = "output" });
            graph2.AddEdge(new NodePortReference() { Node = "particles", Port = "tex" }, new NodePortReference() { Node = "kaleidoscope", Port = "tex0" });
            graph2.AddEdge(new NodePortReference() { Node = "kaleidoscope", Port = "tex" }, new NodePortReference() { Node = "output", Port = "tex" });

            switcher.Add(graph2);


            //Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        }

        private void Keyboard_KeyUp(KeyboardKeyEventArgs e)
        {
            components.ProcessKeyUp(e);
        }

        private void Keyboard_KeyDown(KeyboardKeyEventArgs e)
        {
            //e.Modifiers &= ~KeyModifiers.NumLock;
            //this.keyboardActions.ProcessKeyDown(e.Key, e.Modifiers);
            components.ProcessKeyDown(e);
        }

        private void VisHost_Closing(System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = false;
        }

        private void VisHost_Closed()
        {

        }

        private void VisHost_Resize(ResizeEventArgs e)
        {
            GL.Viewport(0, 0, e.Width, e.Height);
            components.Resize(e.Width, e.Height);

            SetProjection(e.Size);

            //SetProjection(new Vector2i(e.Width, e.Height));
            //components.Resize(e.Width, e.Height);
        }

        private void VisHost_Unload()
        {
            components.Unload();
        }

        private void VisHost_Load()
        {
            //Keyboard.KeyRepeat = true;

            components.Load();
            SetProjection(ClientSize);
            timer.Start();
            InitKeyboard();
        }

        private void InitKeyboard()
        {
            // winamp-style controls
            keyboardActions.Add(Keys.Left, 0, () => { Player?.Skip(-5); });
            keyboardActions.Add(Keys.Right, 0, () => { Player?.Skip(5); });
            //keyboardActions.Add(Key.Z, 0, () => {  });  //TODO: previous in playlist
            keyboardActions.Add(Keys.X, 0, () => { Player?.Play(); });
            keyboardActions.Add(Keys.C, 0, () => { Player?.TogglePause(); });
            keyboardActions.Add(Keys.V, 0, () => { Player?.Stop(); });
            //keyboardActions.Add(Key.B, 0, () => {  });  //TODO: next in playlist
        }

        private void VisHost_RenderFrame(FrameEventArgs e)
        {
            //lock (this.threadLock)
            //{
            double time = timer.Elapsed.TotalSeconds;
            frameData.DeltaRenderTime = time - frameData.RenderTime;
            frameData.RenderTime = time;

            if (shaderUpdatePoller.HasChanges)
            {
                components.Reload();
                shaderUpdatePoller.Reset();
            }

            //text.AddOrUpdate(title);

            GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
            GL.ClearDepth(1.0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);


            components.Render(frameData);



            //GL.Disable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.Blend);

            //text.Render();

            //GL.Finish();

            SwapBuffers();
            Thread.Sleep(0);
            //}
        }

        private void VisHost_UpdateFrame(FrameEventArgs e)
        {
            //lock (this.threadLock)
            //{
            double time = timer.Elapsed.TotalSeconds;
            frameData.DeltaTime = time - frameData.Time;
            frameData.Time = time;

            // poll for shader changes
            // TODO: make poll time a parameter
            if (frameData.Time - lastShaderPollTime > 2.0)
            {
                shaderUpdatePoller.Poll();
                lastShaderPollTime = frameData.Time;
            }

            if (Player != null && Player.TracksPlayed != lastTracksPlayed)
            {
                globalTextures.Reset();
                Player?.WaveFormat.Maybe(wf => globalTextures.SampleRate = wf.SampleRate);
                lastTracksPlayed = Player.TracksPlayed;
            }

            AudioAnalysisSample sample;

            while (SampleQueue.TryDequeue(out sample))
            {
                globalTextures.PushSample(sample);
            }

            components.Update(frameData);

            Thread.Sleep(0);
            //}
        }

        private void SetProjection(Vector2i size)
        {
            SetOverlayProjection(size);
            //SetGBufferCombineProjection();
        }

        private void SetOverlayProjection(Vector2i size)
        {
            float aspect = size.Y > 0 ? ((float)size.X / (float)size.Y) : 1f;

            overlayProjection = Matrix4.CreateOrthographicOffCenter(0.0f, aspect, 1.0f, 0.0f, 0.0f, 10.0f);
            overlayModelview = Matrix4.Identity;// * Matrix4.CreateTranslation(0.0f, 0.0f, -1.0f);

            //text.Projection = overlayProjection;
            //text.Modelview = overlayModelview;
        }

        public void AddSample(AudioAnalysisSample sample)
        {
            SampleQueue.Enqueue(sample);
        }

        //private void OnPlayerStart(object sender, Player.PlayerStartEventArgs e)
        //{
        //    globalTextures.SampleRate = e.SampleRate;
        //}

    }
}

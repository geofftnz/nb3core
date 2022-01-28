using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTKExtensions.Framework;
using OpenTKExtensions.Input;
using OpenTKExtensions.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Vis.UI
{

    /// <summary>
    /// Provides the ability to tweak arbitrary parameters
    /// </summary>
    public class ParameterEditor : CompositeGameComponent, IRenderable, ITransformable, IKeyboardControllable
    {
        public const int NUMLINES = 9;

        public class EditorEntry
        {
            public string Name { get; set; }

            public Func<float> GetValue;
            public Action<float> SetValue { get; set; }
            public float Delta { get; set; }

            public TextBlock LabelBlock { get; set; }

            public bool IsSelected { get; set; }
        }

        public Matrix4 ViewMatrix { get; set; }
        public Matrix4 ModelMatrix { get; set; }
        public Matrix4 ProjectionMatrix { get; set; }

        private TextManager textManager;

        private List<EditorEntry> entries = new List<EditorEntry>();

        private KeyboardActionManager keys = new KeyboardActionManager() { CaptureAllKeypresses = true };

        private Vector3 currentPos = new Vector3(0f, 0.14f, 0f);
        private Vector3 offset = new Vector3(0f, 0.03f, 0f);
        private float textSize = 0.0005f;

        private int selectedIndex = -1;

        private EditorEntry Current => (selectedIndex >= 0 && selectedIndex < entries.Count) ? entries[selectedIndex] : null;

        public Vector4 ColourSelected { get; set; } = new Vector4(1f, 1f, 1f, 1f);
        public Vector4 ColourDeselected { get; set; } = new Vector4(1f, 1f, 1f, .3f);

        // key config
        public KeySpec KeyToggleVisible { get; set; } = new KeySpec(Keys.F12);

        public KeySpec KeySelectNext { get; set; } = new KeySpec(Keys.Down);
        public KeySpec KeySelectPrev { get; set; } = new KeySpec(Keys.Up);
        public KeySpec KeyIncrease { get; set; } = new KeySpec(Keys.Right);
        public KeySpec KeyDecrease { get; set; } = new KeySpec(Keys.Left);
        public KeySpec KeySmallIncrease { get; set; } = new KeySpec(Keys.Right, KeyModifiers.Shift);
        public KeySpec KeySmallDecrease { get; set; } = new KeySpec(Keys.Left, KeyModifiers.Shift);

        public ParameterEditor(Font font)
        {
            components.Add(textManager = new TextManager("txtmgr", font));
            components.Add(keys);

            textManager.Add(new TextBlock("title", "Filter Parameters", new Vector3(0f, 0.1f, 0f), 0.0005f, new Vector4(1f, 1f, 1f, 1f)));
            textManager.Visible = true;

            Visible = false;

            SetupKeys();
        }

        private void SetupKeys()
        {
            keys.Clear();

            keys.Add(KeySelectNext, () => { selectedIndex = Math.Min(selectedIndex + 1, entries.Count - 1); UpdateLabels(); });
            keys.Add(KeySelectPrev, () => { selectedIndex = (selectedIndex > 0) ? selectedIndex - 1 : selectedIndex; UpdateLabels(); });

            keys.Add(KeyIncrease, () =>
            {
                Current?.SetValue(Current.GetValue() + Current.Delta);
                UpdateLabels();
            });
            keys.Add(KeySmallIncrease, () =>
            {
                Current?.SetValue(Current.GetValue() + Current.Delta * 0.1f);
                UpdateLabels();
            });
            keys.Add(KeyDecrease, () =>
            {
                Current?.SetValue(Current.GetValue() - Current.Delta);
                UpdateLabels();
            });
            keys.Add(KeySmallDecrease, () =>
            {
                Current?.SetValue(Current.GetValue() - Current.Delta * 0.1f);
                UpdateLabels();
            });
        }

        public void AddParameter(string name, Func<float> getValue, Action<float> setValue, float delta = 0.001f)
        {
            if (entries.Any(e => e.Name == name))
            {
                throw new InvalidOperationException($"ParameterEditor: key {name} already exists.");
            }

            // set initial index if this is the first entry
            if (!entries.Any())
            {
                selectedIndex = 0;
            }

            var a = new EditorEntry();
            a.Name = name;
            a.GetValue = getValue;
            a.SetValue = setValue;
            a.Delta = delta;
            a.IsSelected = false;
            a.LabelBlock = new TextBlock(name, "", currentPos, textSize, ColourDeselected);

            entries.Add(a);

            currentPos += offset;

            UpdateLabels();
        }

        private void UpdateLabels()
        {
            int index = -1;
            foreach (var entry in entries)
            {
                index++;

                // update isSelected
                entry.IsSelected = index == selectedIndex;

                // format value
                entry.LabelBlock.Text = $"{entry.Name:16}: {entry.GetValue():0.0000}";

                // set colour
                entry.LabelBlock.Colour = entry.IsSelected ? ColourSelected : ColourDeselected;

                textManager.AddOrUpdate(entry.LabelBlock);
            }
        }

        public override void Render(IFrameRenderData frameData, IFrameBufferTarget target)
        {
            base.Render(frameData, target);
        }

        public override bool ProcessKeyDown(KeyboardKeyEventArgs e)
        {
            // always listen to our toggle-visible key
            if ((e.Modifiers & (KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Control)) == KeyToggleVisible.Modifiers && e.Key == KeyToggleVisible.Key)
            {
                Visible = !Visible;
                return false;
            }
            else
            {
                return Visible && base.ProcessKeyDown(e);
            }
        }

        public override bool ProcessKeyUp(KeyboardKeyEventArgs e)
        {
            return Visible && base.ProcessKeyUp(e);
        }

    }
}

using OpenTKExtensions.Framework.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK.Graphics.OpenGL4;
using OpenTKExtensions.Resources;
using OpenTKExtensions.Framework;

namespace nb3.Vis.Renderers.Nodes
{
    public class ParticleNode : RenderGraphNodeBase
    {
        public ParticleNode() : base(false, SizeInheritance.Inherit)
        {
            ChildComponent = new ParticleRenderer();
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

        }

        protected override void AssignInputs(IFrameRenderData frameData)
        {
            // no inputs
        }

        protected override void AssignOutputs(IFrameRenderData frameData)
        {
            _output["tex"].Value = GetTexture(0);
        }
    }
}

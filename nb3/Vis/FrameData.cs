using OpenTKExtensions;
using OpenTKExtensions.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Vis
{
    public class FrameData : IFrameRenderData, IFrameUpdateData
    {
        /// <summary>
        /// Time according to the update process, in seconds.
        /// </summary>
        public double Time { get; set; }
        /// <summary>
        /// Time since last update, in seconds.
        /// </summary>
        public double DeltaTime { get; set; }

        /// <summary>
        /// Time according to frames rendered, in seconds.
        /// </summary>
        public double RenderTime { get; set; }

        /// <summary>
        /// Time since last frame, in seconds.
        /// </summary>
        public double DeltaRenderTime { get; set; }

        public GlobalTextures GlobalTextures { get; set; }
    }
}

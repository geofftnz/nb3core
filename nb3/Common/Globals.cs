using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Common
{
    public class Globals
    {
        public const int SPECTRUMRES = 1024;
        public const int SPECTRUM2RES = 256;
        public const int AUDIODATASIZE = 256;

        public static int SpectrumFrequencyIndex(int frequency, int samplerate = 44100) => Math.Clamp((frequency * SPECTRUMRES) / (samplerate / 2), 0, SPECTRUMRES - 1);
    }
}

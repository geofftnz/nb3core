using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nb3.Common
{
    public static class MathExt
    {
        /// <summary>
        /// Converts a linear value to dB, then scales it so that 0dB == 1.0 and -100dB == 0.0
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static double NormDB(this double a)
        {
            return Math.Max(0.0, 1.0 + (20.0 * Math.Log10(a)) / 100.0);
        }
        public static float NormDB(this float a)
        {
            return (float)((double)a).NormDB();
        }

        public static float Mix(this float x, float a, float b)
        {
            return a * (1f - x) + x * b;
        }

        public static float[] Flat(int n, float sum_to = 1f)
        {
            return Enumerable.Range(0, n).Select(i => sum_to / n).ToArray();
        }
        public static float[] LinearDecay(int n, float sum_to = 1f)
        {
            var x = Enumerable.Range(0, n).Select(i => (float)(n - i)).ToArray();
            var total = x.Sum();
            return x.Select(i => (i * sum_to) / total).ToArray();
        }

        // ChatGPT
        public static float[] CubicResample(this float[] input, int factor)
        {
            int n = input.Length;
            int m = n * factor;
            float[] output = new float[m];

            for (int i = 0; i < m; i++)
            {
                float x = i / (float)factor;
                int j = (int)x;
                float a = x - j;

                float y0 = input[Math.Max(0, j - 1)];
                float y1 = input[j];
                float y2 = input[Math.Min(n - 1, j + 1)];
                float y3 = input[Math.Min(n - 1, j + 2)];

                float a0 = y3 - y2 - y0 + y1;
                float a1 = y0 - y1 - a0;
                float a2 = y2 - y0;
                float a3 = y1;

                output[i] = ((a0 * a * a * a) + (a1 * a * a) + (a2 * a) + a3);
            }

            return output;
        }

        public static float Interpolate(float from, float to, float x)
        {
            return to * x + (1f - x) * from;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using sut=nb3.Player.Analysis;

namespace nb3test.Player.Analysis
{
    public class RingBuffer
    {
        public sut.RingBuffer<T> CreateRingBuffer<T>(int size)
        {
            return new sut.RingBuffer<T>(size);
        }


        [Fact]
        public void add_and_read_items()
        {
            var rb = CreateRingBuffer<int>(4);

            rb.Add(1);
            rb.Add(2);
            rb.Add(3);
            rb.Add(4);
            rb.Add(5);

            var contents = rb.Last().Take(4).ToArray();

            Assert.Equal(4, contents.Length);
            Assert.Equal(5, contents[0]);
            Assert.Equal(4, contents[1]);
            Assert.Equal(3, contents[2]);
            Assert.Equal(2, contents[3]);
        }

        [Fact]
        public void read_via_index_operator()
        {
            var rb = CreateRingBuffer<int>(4);

            rb.Add(1);
            rb.Add(2);
            rb.Add(3);
            rb.Add(4);
            rb.Add(5);

            Assert.Equal(5, rb[0]);
            Assert.Equal(4, rb[1]);
            Assert.Equal(3, rb[2]);
            Assert.Equal(2, rb[3]);

            Assert.Throws<IndexOutOfRangeException>(() => rb[4]);
            Assert.Throws<IndexOutOfRangeException>(() => rb[-1]);

        }

    }
}

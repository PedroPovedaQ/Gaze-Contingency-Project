using System;
using System.Threading;

namespace SpatialVoice
{
    /// <summary>Single network producer, single Unity audio-thread consumer. Bounded SPSC
    /// ring; no locks, allocations or Unity calls in Read. Signed PCM16 little endian.</summary>
    public sealed class PcmStreamBuffer
    {
        readonly float[] m_Samples;
        long m_Written, m_Read;
        int m_TrailingByte = -1, m_Complete, m_Underruns;
        public PcmStreamBuffer(int capacity) { if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity)); m_Samples = new float[capacity]; }
        public int Count => (int)(Interlocked.Read(ref m_Written) - Interlocked.Read(ref m_Read));
        public bool Complete => Volatile.Read(ref m_Complete) != 0;
        public bool Drained => Complete && Count == 0;
        public int Underruns => Volatile.Read(ref m_Underruns);

        public void Write(byte[] pcm)
        {
            if (Complete) throw new InvalidOperationException("PCM stream already ended.");
            int samples = (pcm.Length + (m_TrailingByte >= 0 ? 1 : 0)) / 2;
            long written = Interlocked.Read(ref m_Written), read = Interlocked.Read(ref m_Read);
            if (written - read + samples > m_Samples.Length) throw new InvalidOperationException("PCM capacity exceeded.");
            int i = 0;
            if (m_TrailingByte >= 0 && pcm.Length > 0)
            {
                m_Samples[written++ % m_Samples.Length] = (short)(m_TrailingByte | pcm[i++] << 8) / 32768f;
                m_TrailingByte = -1;
            }
            while (i + 1 < pcm.Length)
            {
                m_Samples[written++ % m_Samples.Length] = (short)(pcm[i] | pcm[i + 1] << 8) / 32768f;
                i += 2;
            }
            if (i < pcm.Length) m_TrailingByte = pcm[i];
            Interlocked.Exchange(ref m_Written, written);
        }
        public void Finish()
        {
            if (m_TrailingByte >= 0) throw new FormatException("Truncated PCM16 sample.");
            Volatile.Write(ref m_Complete, 1);
        }
        public void Read(float[] destination)
        {
            long read = Interlocked.Read(ref m_Read), written = Interlocked.Read(ref m_Written);
            int count = (int)Math.Min(destination.Length, written - read);
            for (int i = 0; i < count; i++) destination[i] = m_Samples[read++ % m_Samples.Length];
            Array.Clear(destination, count, destination.Length - count);
            Interlocked.Exchange(ref m_Read, read);
            if (count < destination.Length && !Complete) Interlocked.Increment(ref m_Underruns);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Serialization
{
    sealed class InvalidWireTypeException : Exception
    {
        public InvalidWireTypeException (byte type, ByteInputStream stream)
        {
        }
    }

    sealed class ByteInputStream
    {
        readonly byte[] m_buffer;
        int m_pos;
        int m_end;

        private ByteInputStream (byte[] buffer, int pos, int end)
        {
            m_buffer = buffer.Coerce();
            m_end = end.Clamp(0, buffer.Length);
            m_pos = pos.Clamp(0, m_end);
        }

        public static ByteInputStream FromByteArray (byte[] buffer)
        {
            return new ByteInputStream (buffer, 0, buffer.Length);
        }

        public int Position
        {
            get
            {
                return m_pos;
            }
        }

        public int Remaining
        {
            get
            {
                return m_end - m_pos;
            }
        }

        public bool ReadByte (out byte v)
        {
            if (m_pos < m_end)
            {
                v = m_buffer[m_pos];
                ++m_pos;
                return true;
            }
            else
            {
                v = 0;
                return false;
            }
        }

        public bool ReadArray (byte[] a, int begin, int length)
        {
            if (a == null)
            {
                return false;
            }

            begin = begin.Coerce ();

            var end = begin + length;
            if (end > a.Length)
            {
                return false;
            }

            if (Remaining < length)
            {
                return false;
            }

            if (length < 1)
            {
                return true;
            }

            for (var iter = begin; iter < end; ++iter)
            {
                a[iter] = m_buffer[m_pos++];
            }

            return true;
        }

        public bool ReadBuffer (int length, out ByteInputStream v)
        {
            length = length.Coerce ();
            var end = m_pos + length;
            if (end <= m_end)
            {
                v = new ByteInputStream (m_buffer, m_pos, end);
                m_pos = end;
                return true;
            }
            else
            {
                v = null;
                return false;
            }
        }

    }

    sealed class ByteOutputStream
    {
        readonly List<byte> m_buffer = new List<byte> (64);

        private ByteOutputStream ()
        {
        }

        public void WriteByte (byte v)
        {
            m_buffer.Add (v);
        }

        public static ByteOutputStream Create ()
        {
            return new ByteOutputStream ();
        }

        public byte[] ToArray ()
        {
            return m_buffer.ToArray ();
        }
    }

    static partial class ProtobufWireTypeSerializers
    {
        public static bool ReadKey (this ByteInputStream stream, out UInt64 tag, out byte type)
        {
            tag = 0;
            type = 0;

            if (!stream.ReadVarint (ref tag))
            {
                return false;
            }

            type = (byte)(tag & 0x7);
            tag >>= 3;

            return true;
        }

        public static void WriteKey (this ByteOutputStream stream, UInt64 tag, byte type)
        {
            var x = (UInt64)type & 0x7;
            var v = tag << 3 | x;

            stream.WriteVarint (v);
        }

        public static bool SkipValue (this ByteInputStream stream, byte type)
        {
            switch (type)
            {
                case 0:
                    {
                        UInt64 value = 0;
                        return stream.ReadVarint (ref value);
                    }
                case 1:
                    {
                        UInt64 value = 0;
                        return stream.Read64bit (ref value);
                    }
                case 2:
                    {
                        ByteInputStream value = null;
                        return stream.ReadLengthDelimited (ref value);
                    }
                case 5:
                    {
                        UInt32 value = 0;
                        return stream.Read32bit (ref value);
                    }
                case 3:
                case 4:
                default:
                    throw new InvalidWireTypeException (type, stream);
            }
        }

        public static bool SkipKeyValue (this ByteInputStream stream)
        {
            UInt64 tag;
            byte type;

            if (!stream.ReadKey (out tag, out type))
            {
                return false;
            }

            return stream.SkipValue (type);
        }

        public static bool SkipKeyValues (this ByteInputStream stream)
        {
            while (stream.SkipKeyValue ());
            return false;
        }


        // Type: 0
        public static bool ReadVarint (this ByteInputStream stream, ref UInt64 v)
        {
            v = 0;
            byte b;
            int shift = 0;

            while (stream.ReadByte(out b))
            {
                v |= ((UInt64)(b & 0x7F)) << shift;
                shift += 7;
                if ((b & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;

        }

        // Type: 0
        public static void WriteVarint (this ByteOutputStream stream, UInt64 v)
        {
            while ((v & ~0x7FUL) != 0)
            {
                stream.WriteByte ((byte)(0x80UL | (v & 0x7FUL)));
                v >>= 7;
            }

            stream.WriteByte ((byte)(v & 0x7FUL));
        }

        // Type: 1
        public static bool Read64bit (this ByteInputStream stream, ref UInt64 v)
        {
            v = 0;
            byte b;
            int shift = 0;
            int iter = 0;

            for (; iter < 8 && stream.ReadByte(out b); ++iter)
            {
                v |= ((UInt64)(b & 0xFF)) << shift;
                shift += 8;
            }

            return iter == 8;
        }

        // Type: 1
        public static void Write64bit (this ByteOutputStream stream, UInt64 v)
        {
            for (var iter = 0; iter < 8; ++iter)
            {
                stream.WriteByte ((byte)((v & 0xFFUL)));
                v >>= 8;
            }
        }

        // Type: 2
        public static bool ReadLengthDelimited (this ByteInputStream stream, ref ByteInputStream value)
        {
            UInt64 length = 0;
            if (!stream.ReadVarint (ref length))
            {
                return false;
            }

            var ilength = (int)length;

            return stream.ReadBuffer(ilength, out value);
        }

        // Type: 2
        public static void WriteLengthDelimited (this ByteOutputStream stream, ByteInputStream value)
        {
            stream.WriteVarint ((UInt64)value.Remaining);
            byte b;
            while (value.ReadByte(out b))
            {
                stream.WriteByte (b);
            }
        }

        // Type: 5
        public static bool Read32bit (this ByteInputStream stream, ref UInt32 v)
        {
            v = 0;
            byte b;
            int shift = 0;
            int iter = 0;

            for (; iter < 4 && stream.ReadByte(out b); ++iter)
            {
                v |= ((UInt32)(b & 0xFF)) << shift;
                shift += 8;
            }

            return iter == 4;
        }

        // Type: 5
        public static void Write32bit (this ByteOutputStream stream, UInt32 v)
        {
            for (var iter = 0; iter < 4; ++iter)
            {
                stream.WriteByte ((byte)((v & 0xFFU)));
                v >>= 8;
            }
        }

    }

    static partial class ProtobufTypeSerializers
    {
        [StructLayout (LayoutKind.Explicit)]
        struct DoubleConverter
        {
            [FieldOffset (0)]
            public UInt64 uint64Val;

            [FieldOffset (0)]
            public Double doubleVal;
        }

        [StructLayout (LayoutKind.Explicit)]
        struct SingleConverter
        {
            [FieldOffset (0)]
            public UInt32 uint32Val;

            [FieldOffset (0)]
            public Single singleVal;
        }

        static void Convert (double from, ref UInt64 to)
        {
            // TODO: Endianess
            DoubleConverter converter = new DoubleConverter ();
            converter.doubleVal = from;
            to = converter.uint64Val;
        }

        static void Convert (UInt64 from, ref double to)
        {
            // TODO: Endianess
            DoubleConverter converter = new DoubleConverter ();
            converter.uint64Val = from;
            to = converter.doubleVal;
        }

        static void Convert (float from, ref UInt32 to)
        {
            // TODO: Endianess
            SingleConverter converter = new SingleConverter ();
            converter.singleVal = from;
            to = converter.uint32Val;
        }

        static void Convert (UInt32 from, ref float to)
        {
            // TODO: Endianess
            SingleConverter converter = new SingleConverter ();
            converter.uint32Val = from;
            to = converter.singleVal;
        }

        static void Convert (bool from, ref UInt64 to)
        {
            to = from ? 1UL : 0UL;
        }

        static void Convert (UInt64 from, ref bool to)
        {
            to = from != 0UL;
        }
    
        static readonly Encoding g_encoding = Encoding.UTF8;

        [ThreadStatic]
        static byte[] g_decodingBytes = new byte[16];

        static void Convert (ByteInputStream from, ref string to)
        {
            var remaining = from.Remaining;
            if (remaining > g_decodingBytes.Length)
            {
                Array.Resize(ref g_decodingBytes, remaining*2);
            }

            if (!from.ReadArray(g_decodingBytes, 0, remaining))
            {
                to = "";
            }

            to = g_encoding.GetString(g_decodingBytes, 0, remaining);
        }
    
        static void Convert (string from, ref ByteInputStream to)
        {
            from = from.Coerce();

            var bytes = g_encoding.GetBytes (from);

            to = ByteInputStream.FromByteArray (bytes);
        }
    
        static void Convert (ByteInputStream from, ref Byte[] to)
        {
            var remaining = from.Remaining;

            to = new byte[remaining];

            from.ReadArray(to, 0, remaining);
        }

        static void Convert (Byte[] from, ref ByteInputStream to)
        {
            to = ByteInputStream.FromByteArray (from);
        }
    }

}

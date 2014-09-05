using System;


namespace Serialization
{
    static partial class ProtobufTypes
    {


        public static bool ReadInt32 (this ByteInputStream stream, byte type, out Int32 v)
        {
            v = default (Int32);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            v = (Int32) wv;

            return true;
            
        }

        public static void WriteInt32 (this ByteOutputStream stream, UInt64 tag, Int32 v)
        {
            stream.WriteKey (tag, 0);
            UInt64 wv = (UInt64)v;

            stream.WriteVarint (wv);
        }



        public static bool ReadInt64 (this ByteInputStream stream, byte type, out Int64 v)
        {
            v = default (Int64);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            v = (Int64) wv;

            return true;
            
        }

        public static void WriteInt64 (this ByteOutputStream stream, UInt64 tag, Int64 v)
        {
            stream.WriteKey (tag, 0);
            UInt64 wv = (UInt64)v;

            stream.WriteVarint (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out UInt32 v)
        {
            return stream.ReadUInt32 (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, UInt32 v)
        {
            stream.WriteUInt32 (tag, v);
        }

        public static bool ReadUInt32 (this ByteInputStream stream, byte type, out UInt32 v)
        {
            v = default (UInt32);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            v = (UInt32) wv;

            return true;
            
        }

        public static void WriteUInt32 (this ByteOutputStream stream, UInt64 tag, UInt32 v)
        {
            stream.WriteKey (tag, 0);
            UInt64 wv = (UInt64)v;

            stream.WriteVarint (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out UInt64 v)
        {
            return stream.ReadUInt64 (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, UInt64 v)
        {
            stream.WriteUInt64 (tag, v);
        }

        public static bool ReadUInt64 (this ByteInputStream stream, byte type, out UInt64 v)
        {
            v = default (UInt64);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            v = (UInt64) wv;

            return true;
            
        }

        public static void WriteUInt64 (this ByteOutputStream stream, UInt64 tag, UInt64 v)
        {
            stream.WriteKey (tag, 0);
            UInt64 wv = (UInt64)v;

            stream.WriteVarint (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out Int32 v)
        {
            return stream.ReadSInt32 (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, Int32 v)
        {
            stream.WriteSInt32 (tag, v);
        }

        public static bool ReadSInt32 (this ByteInputStream stream, byte type, out Int32 v)
        {
            v = default (Int32);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            var xx = wv;
            var yy = xx >> 1;
            var ww = (xx & 0x1) == 0 ? yy : ~yy;

            v = (Int32) ww;

            return true;
            
        }

        public static void WriteSInt32 (this ByteOutputStream stream, UInt64 tag, Int32 v)
        {
            stream.WriteKey (tag, 0);
            var xx = (UInt64)v;
            var yy = xx << 1;
            var wv = (xx & 0x80000000U) == 0 ? yy : ~yy;

            stream.WriteVarint (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out Int64 v)
        {
            return stream.ReadSInt64 (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, Int64 v)
        {
            stream.WriteSInt64 (tag, v);
        }

        public static bool ReadSInt64 (this ByteInputStream stream, byte type, out Int64 v)
        {
            v = default (Int64);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            var xx = wv;
            var yy = xx >> 1;
            var ww = (xx & 0x1) == 0 ? yy : ~yy;

            v = (Int64) ww;

            return true;
            
        }

        public static void WriteSInt64 (this ByteOutputStream stream, UInt64 tag, Int64 v)
        {
            stream.WriteKey (tag, 0);
            var xx = (UInt64)v;
            var yy = xx << 1;
            var wv = (xx & 0x8000000000000000UL) == 0 ? yy : ~yy;

            stream.WriteVarint (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out bool v)
        {
            return stream.ReadBool (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, bool v)
        {
            stream.WriteBool (tag, v);
        }

        public static bool ReadBool (this ByteInputStream stream, byte type, out bool v)
        {
            v = default (bool);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            Convert (wv, ref v);

            return true;
            
        }

        public static void WriteBool (this ByteOutputStream stream, UInt64 tag, bool v)
        {
            stream.WriteKey (tag, 0);
            var wv = default (UInt64);
            Convert (v, ref wv);

            stream.WriteVarint (wv);
        }



        public static bool ReadEnum (this ByteInputStream stream, byte type, out Int32 v)
        {
            v = default (Int32);

            if (type != 0)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.ReadVarint (ref wv))
            {
                return false;
            }

            v = (Int32) wv;

            return true;
            
        }

        public static void WriteEnum (this ByteOutputStream stream, UInt64 tag, Int32 v)
        {
            stream.WriteKey (tag, 0);
            UInt64 wv = (UInt64)v;

            stream.WriteVarint (wv);
        }



        public static bool ReadFixed64 (this ByteInputStream stream, byte type, out UInt64 v)
        {
            v = default (UInt64);

            if (type != 1)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.Read64bit (ref wv))
            {
                return false;
            }

            v = (UInt64) wv;

            return true;
            
        }

        public static void WriteFixed64 (this ByteOutputStream stream, UInt64 tag, UInt64 v)
        {
            stream.WriteKey (tag, 1);
            UInt64 wv = (UInt64)v;

            stream.Write64bit (wv);
        }



        public static bool ReadSFixed64 (this ByteInputStream stream, byte type, out Int64 v)
        {
            v = default (Int64);

            if (type != 1)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.Read64bit (ref wv))
            {
                return false;
            }

            var xx = wv;
            var yy = xx >> 1;
            var ww = (xx & 0x1) == 0 ? yy : ~yy;

            v = (Int64) ww;

            return true;
            
        }

        public static void WriteSFixed64 (this ByteOutputStream stream, UInt64 tag, Int64 v)
        {
            stream.WriteKey (tag, 1);
            var xx = (UInt64)v;
            var yy = xx << 1;
            var wv = (xx & 0x8000000000000000UL) == 0 ? yy : ~yy;

            stream.Write64bit (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out Double v)
        {
            return stream.ReadDouble (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, Double v)
        {
            stream.WriteDouble (tag, v);
        }

        public static bool ReadDouble (this ByteInputStream stream, byte type, out Double v)
        {
            v = default (Double);

            if (type != 1)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt64 wv = default (UInt64);

            if (!stream.Read64bit (ref wv))
            {
                return false;
            }

            Convert (wv, ref v);

            return true;
            
        }

        public static void WriteDouble (this ByteOutputStream stream, UInt64 tag, Double v)
        {
            stream.WriteKey (tag, 1);
            var wv = default (UInt64);
            Convert (v, ref wv);

            stream.Write64bit (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out String v)
        {
            return stream.ReadString (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, String v)
        {
            stream.WriteString (tag, v);
        }

        public static bool ReadString (this ByteInputStream stream, byte type, out String v)
        {
            v = default (String);

            if (type != 2)
            {
                stream.SkipValue (type);
                return false;
            }

            ByteInputStream wv = default (ByteInputStream);

            if (!stream.ReadLengthDelimited (ref wv))
            {
                return false;
            }

            Convert (wv, ref v);

            return true;
            
        }

        public static void WriteString (this ByteOutputStream stream, UInt64 tag, String v)
        {
            stream.WriteKey (tag, 2);
            var wv = default (ByteInputStream);
            Convert (v, ref wv);

            stream.WriteLengthDelimited (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out Byte[] v)
        {
            return stream.ReadBytes (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, Byte[] v)
        {
            stream.WriteBytes (tag, v);
        }

        public static bool ReadBytes (this ByteInputStream stream, byte type, out Byte[] v)
        {
            v = default (Byte[]);

            if (type != 2)
            {
                stream.SkipValue (type);
                return false;
            }

            ByteInputStream wv = default (ByteInputStream);

            if (!stream.ReadLengthDelimited (ref wv))
            {
                return false;
            }

            Convert (wv, ref v);

            return true;
            
        }

        public static void WriteBytes (this ByteOutputStream stream, UInt64 tag, Byte[] v)
        {
            stream.WriteKey (tag, 2);
            var wv = default (ByteInputStream);
            Convert (v, ref wv);

            stream.WriteLengthDelimited (wv);
        }



        public static bool ReadFixed32 (this ByteInputStream stream, byte type, out UInt32 v)
        {
            v = default (UInt32);

            if (type != 5)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt32 wv = default (UInt32);

            if (!stream.Read32bit (ref wv))
            {
                return false;
            }

            v = (UInt32) wv;

            return true;
            
        }

        public static void WriteFixed32 (this ByteOutputStream stream, UInt64 tag, UInt32 v)
        {
            stream.WriteKey (tag, 5);
            UInt32 wv = (UInt32)v;

            stream.Write32bit (wv);
        }



        public static bool ReadSFixed32 (this ByteInputStream stream, byte type, out Int32 v)
        {
            v = default (Int32);

            if (type != 5)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt32 wv = default (UInt32);

            if (!stream.Read32bit (ref wv))
            {
                return false;
            }

            var xx = wv;
            var yy = xx >> 1;
            var ww = (xx & 0x1) == 0 ? yy : ~yy;

            v = (Int32) ww;

            return true;
            
        }

        public static void WriteSFixed32 (this ByteOutputStream stream, UInt64 tag, Int32 v)
        {
            stream.WriteKey (tag, 5);
            var xx = (UInt32)v;
            var yy = xx << 1;
            var wv = (xx & 0x80000000U) == 0 ? yy : ~yy;

            stream.Write32bit (wv);
        }


        public static bool Read (this ByteInputStream stream, byte type, out Single v)
        {
            return stream.ReadFloat (type, out v);
        }

        public static void Write (this ByteOutputStream stream, UInt64 tag, Single v)
        {
            stream.WriteFloat (tag, v);
        }

        public static bool ReadFloat (this ByteInputStream stream, byte type, out Single v)
        {
            v = default (Single);

            if (type != 5)
            {
                stream.SkipValue (type);
                return false;
            }

            UInt32 wv = default (UInt32);

            if (!stream.Read32bit (ref wv))
            {
                return false;
            }

            Convert (wv, ref v);

            return true;
            
        }

        public static void WriteFloat (this ByteOutputStream stream, UInt64 tag, Single v)
        {
            stream.WriteKey (tag, 5);
            var wv = default (UInt32);
            Convert (v, ref wv);

            stream.Write32bit (wv);
        }

    }
}


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serialization;
using System.Reflection;

namespace ManualLabor
{

    class Invoice
    {
        // Tag: 1
        public long     Id             { get; set; }
        // Tag: 2
        public string   PublicId       { get; set; }
        // Tag: 600
        // Google protobuf doesn't support decimals
        // Representing an amount as a double is out of the question obviously
        // Next best thing is 64bit int
        public long     TotalAmount    { get; set; }
        // Tag: 601
        public long     Amount         { get; set; }

        public override string ToString()
        {
            return new 
            {
                Invoice     = true  ,
                Id                  ,
                PublicId            ,
                TotalAmount         ,
                Amount              ,
            }.ToString ();
        }
    }


    class InvoiceLine
    {
        // 1
        public long Id             { get; set; }
        // 2
        public int  Ordinal       { get; set; }
        // 700
        public long TotalAmount    { get; set; }
        // 701
        public long Amount         { get; set; }

        public override string ToString()
        {
            return new 
            {
                InvoiceLine = true  ,
                Id                  ,
                Ordinal             ,
                TotalAmount         ,
                Amount              ,
            }.ToString ();
        }
    }

    static partial class ProtobufSerializers
    {
        public static void WriteMessage (this ByteOutputStream stream, Invoice value)
        {
            stream.Write(1      , value.Id          );
            stream.Write(2      , value.PublicId    );
            stream.Write(600    , value.TotalAmount );
            stream.Write(601    , value.Amount      );
        }

        public static bool ReadMessage (this ByteInputStream stream, out Invoice value)
        {
            value = new Invoice ();

            UInt64 tag;
            byte type;

            while (stream.ReadKey (out tag, out type))
            {
                switch (tag)
                {
                    case 1:
                        {
                            long v;
                            if (stream.Read (type, out v))
                            {
                                value.Id = v;
                            }
                            else
                            {
                                Logger.TraceError ("Invalid Invoice.Id value");
                                return false;
                            }
                        }
                        break;
                    case 2:
                        {
                            string v;
                            if (stream.Read (type, out v))
                            {
                                value.PublicId = v;
                            }
                            else
                            {
                                Logger.TraceError ("Invalid Invoice.Id value");
                                return false;
                            }
                        }
                        break;
                    case 600:
                        {
                            long v;
                            if (stream.Read (type, out v))
                            {
                                value.TotalAmount = v;
                            }
                            else
                            {
                                Logger.TraceError ("Invalid Invoice.TotalAmount value");
                                return false;
                            }
                        }
                        break;
                    case 601:
                        {
                            long v;
                            if (stream.Read (type, out v))
                            {
                                value.Amount = v;
                            }
                            else
                            {
                                Logger.TraceError ("Invalid Invoice.Amount value");
                                return false;
                            }
                        }
                        break;
                    default:
                        stream.SkipValue (type);
                        break;
                }
            }

            return true;
        }

        public static void WriteMessage (this ByteOutputStream stream, UInt64 tag, Invoice value)
        {
            stream.WriteKey (tag, 2);
            stream.WriteMessage (value);
        }

        public static bool Read (this ByteInputStream stream, byte type, out Invoice value)
        {
            if (type != 2)
            {
                stream.SkipValue (type);
            }

            return stream.ReadMessage (out value);
        }

    }

}

namespace ReflectionDoesIt
{
    [AttributeUsage (AttributeTargets.Property)]
    sealed class TagAttribute : Attribute
    {
        public readonly UInt64 Tag;

        public TagAttribute (UInt64 tag)
        {
            Tag = tag;
        }
    }

    class Invoice
    {
        [Tag(1)]
        public long     Id             { get; set; }

        [Tag(2)]
        public string   PublicId       { get; set; }

        // Google protobuf doesn't support decimals
        // Representing an amount as a double is out of the question obviously
        // Next best thing is 64bit int
        [Tag(600)]
        public long     TotalAmount    { get; set; }

        [Tag(601)]
        public long     Amount         { get; set; }

        public override string ToString()
        {
            return new 
            {
                Invoice     = true  ,
                Id                  ,
                PublicId            ,
                TotalAmount         ,
                Amount              ,
            }.ToString ();
        }
    }

    static partial class ProtobufSerializers
    {
        static Dictionary<UInt64, PropertyInfo> GetTaggedProperties<T> ()
            where T : class, new()
        {
            var type        = typeof (T);
            // TODO: LINQ creates many small enumerator instances, can be solved by caching
            var tags        = type
                .GetProperties ()                   // TODO: Slow
                .Select (p => new { Tag = p.GetCustomAttribute<TagAttribute> (), Property = p})  
                .Where (t => t.Tag != null)
                .Where (t => t.Property.CanWrite && t.Property.CanRead)   // TODO: Should be reported
                // TODO: Duplicated tags will throw run time exception for something that is known in compile time
                .ToDictionary (t => t.Tag.Tag, t => t.Property) 
                ;

            return tags;
        }

        static Dictionary<Type, MethodInfo> GetReaders ()
        {
            var type = typeof (ProtobufTypeSerializers);

            var readers = type
                .GetMethods () 
                .Where (mi => mi.IsStatic)
                .Where (mi => mi.Name == "Read")
                .Select (mi => 
                    {
                        if (mi.ReturnType != typeof (bool))
                        {
                            return null;
                        }

                        var ps = mi.GetParameters ();
                        if (ps.Length != 3)
                        {
                            return null;
                        }

                        if (ps[0].ParameterType != typeof (ByteInputStream))
                        {
                            return null;
                        }

                        if (ps[1].ParameterType != typeof (byte))
                        {
                            return null;
                        }

                        if (!ps[2].IsOut)
                        {
                            return null;
                        }

                        var pt = ps[2].ParameterType;

                        return new { Type = pt, MethodInfo = mi};  
                    })
                .Where (t => t != null)
                .ToDictionary (t => t.Type, t => t.MethodInfo);

            return readers;
        }

        static Dictionary<Type, MethodInfo> GetWriters ()
        {
            var type = typeof (ProtobufTypeSerializers);

            var writers = type
                .GetMethods () 
                .Where (mi => mi.IsStatic)
                .Where (mi => mi.Name == "Write")
                .Select (mi => 
                    {
                        if (mi.ReturnType != typeof (void))
                        {
                            return null;
                        }

                        var ps = mi.GetParameters ();
                        if (ps.Length != 3)
                        {
                            return null;
                        }

                        if (ps[0].ParameterType != typeof (ByteOutputStream))
                        {
                            return null;
                        }

                        if (ps[1].ParameterType != typeof (UInt64))
                        {
                            return null;
                        }

                        return new { Type = ps[2].ParameterType, MethodInfo = mi};  
                    })
                .Where (t => t != null)
                .ToDictionary (t => t.Type, t => t.MethodInfo);

            return writers;
        }

        static readonly Dictionary<Type, MethodInfo> g_readers = GetReaders ();
        static readonly Dictionary<Type, MethodInfo> g_writers = GetWriters ();

        public static void WriteMessage<T> (this ByteOutputStream stream, T value)
            where T : class, new()
        {
            // TODO: Slow
            // TODO: Random ordering
            // TODO: Creates an enumerator object
            // TODO: Several virtual calls
            foreach (var tagged in GetTaggedProperties<T>() )
            {
                var tag = tagged.Key;
                var p = tagged.Value;

                var propertyType = p.PropertyType;
                // TODO: Dictionary lookup fast but not as fast as native field access
                var writer = g_writers.Lookup (propertyType);
                if (writer == null)
                {
                    // TODO: Run time error for something that is known in compile time.
                    throw new Exception ("Non supported type: " + propertyType.Name);
                }

                // TODO: .GetValue very slow compared to native getter (about 100x slower)
                // TODO: Property value is boxed unnecessarily
                var v = p.GetValue (value, null);
                // TODO: .Invoke very slow compared to native call (about 100x slower)
                // TODO: Creates an array object
                // TODO: Boxes tag
                writer.Invoke (null, new object [] {stream, tag, v});
            }
        }

        public static bool ReadMessage<T> (this ByteInputStream stream, out T value)
            where T : class, new()
        {
            // TODO: Slow
            // TODO: Creates a dictionary object
            var tagged = GetTaggedProperties<T>();

            value = new T ();

            UInt64 tag;
            byte type;

            while (stream.ReadKey (out tag, out type))
            {
                // TODO: Dictionary lookup slower than switch
                var pi = tagged.Lookup (tag);

                if (pi != null)
                {
                    var propertyType = pi.PropertyType;

                    var reader = g_readers.Lookup (propertyType.MakeByRefType ());
                    if (reader == null)
                    {
                        // TODO: Run time error for something that is known in compile time.
                        throw new Exception ("Non supported type: " + propertyType.Name);
                    }

                    // TODO: Creates an array object
                    // TODO: Unboxes bool
                    // TODO: Boxes type
                    // TODO: Creates new array object
                    var args = new object[] {stream, type, null};
                    var result = (bool)reader.Invoke (null, args);

                    if (result)
                    {
                        // TODO: .SetValue very slow compared to native setter (about 100x slower)
                        // TODO: Property value is boxed unnecessarily
                        pi.SetValue(value, args[2]);
                    }
                    else
                    {
                        Logger.TraceError ("Invalid value for " + pi.DeclaringType.Name + "." + pi.Name);
                        return false;
                    }
                }
                else
                {
                    stream.SkipValue (type);
                }
            }

            return true;
        }
    }

}

namespace Serialization
{
//    using ManualLabor;
    using ReflectionDoesIt;

    class Program
    {
        static void Main(string[] args)
        {
            var inv = new Invoice 
            {
                Id          = 1001  ,
                PublicId    = "TEST",
                TotalAmount = 100   ,
                Amount      = 75    ,
            };

            Console.WriteLine ("Input: {0}", inv);

            var output = ByteOutputStream.Create ();
            output.Write (998, 0);      // Unknown field
            output.WriteMessage (inv);
            output.Write (999, 0);      // Unknown field



            Invoice inv2;
            var input = ByteInputStream.FromByteArray (output.ToArray ());
            var result = input.ReadMessage (out inv2);

            Console.WriteLine ("Result: {0}, Output: {1}", result, inv);

            Console.WriteLine ("Done!");
            Console.ReadKey ();
        }

    }
}

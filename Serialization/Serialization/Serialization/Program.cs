using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Serialization;

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
namespace Serialization
{
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

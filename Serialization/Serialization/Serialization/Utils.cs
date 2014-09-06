using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace Serialization
{   
    static class ArrayUtils<T>
    {
        public static readonly T[] EmptyArray = new T[0];
    }

    static class Logger
    {
        public static void TraceError (string message, params object[] args)
        {
        }
    }

    static class Utils
    {
        public static int Coerce (this int i)
        {
            return i > 0 ? i : 0;
        }

        public static string Coerce (this string i)
        {
            return i != null ? i : "";
        }

        public static T[] Coerce<T> (this T[] i)
        {
            return i != null ? i : ArrayUtils<T>.EmptyArray;
        }

        public static int Clamp (this int v, int from, int to)
        {
            if (v < from)
            {
                return from;
            }

            if (v > to)
            {
                return to;
            }

            return v;
        }

        public static TValue Lookup<TKey, TValue> (this Dictionary<TKey, TValue> dict, TKey key, TValue defaultTo = default (TValue))
        {
            if (dict == null)
            {
                return defaultTo;
            }

            TValue v;
            if (dict.TryGetValue (key, out v))
            {
                return v;
            }
            else
            {
                return defaultTo;
            }
        }

    }
}

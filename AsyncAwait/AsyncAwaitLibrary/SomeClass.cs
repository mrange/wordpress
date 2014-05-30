// ----------------------------------------------------------------------------------------------
// Copyright (c) Mårten Rånge.
// ----------------------------------------------------------------------------------------------
// This source code is subject to terms and conditions of the Microsoft Public License. A 
// copy of the license can be found in the License.html file at the root of this distribution. 
// If you cannot locate the  Microsoft Public License, please send an email to 
// dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
//  by the terms of the Microsoft Public License.
// ----------------------------------------------------------------------------------------------
// You must not remove this notice, or any other, from this software.
// ----------------------------------------------------------------------------------------------

using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AsyncAwaitLibrary
{
    public class SomeClass
    {
        public static void TraceThreadId ([CallerMemberName] string caller = null)
        {
            Trace.WriteLine (string.Format (
                "{0}, thread id: {1}", 
                caller,
                Thread.CurrentThread.ManagedThreadId));
        }

        public static async Task<string> ReadSomeTextAsync(string fileName, bool continueOnCapturedContext)
        {
            TraceThreadId ();

            var context = SynchronizationContext.Current;
            Trace.WriteLine (string.Format (
                "ReadSomeTextAsync, SynchronizationContext: {0}"        , 
                context != null ? context.GetType ().FullName : "Null"  ));

            try
            {
                using (var sr = new StreamReader(fileName))
                {
                    var result = await sr.ReadToEndAsync().ConfigureAwait (continueOnCapturedContext);
                    return result;
                }
            }
            finally
            {
                TraceThreadId ();
            }
        }
    }
}

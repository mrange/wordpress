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

using AsyncAwaitLibrary;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace AsyncAwait
{
    static class Program
    {
        static void ReadText ()
        {
            SomeClass.TraceThreadId ();

            var readTask    = SomeClass.ReadSomeTextAsync ("SomeText.txt");
            var text        = readTask.Result;

            Console.WriteLine("Read {0} characters", text.Length);

            SomeClass.TraceThreadId ();
        }

        static int readingFiles = 0;

        static async Task<string> ReadTask ()
        {
            SomeClass.TraceThreadId ();

            ++readingFiles;

            var text = await SomeClass
                .ReadSomeTextAsync("SomeText.txt");

            --readingFiles;

            SomeClass.TraceThreadId ();

            return text;
        }

        static void RaceCondition ()
        {
            SomeClass.TraceThreadId ();

            var task = ReadTask ();

            SomeClass.TraceThreadId ();
        }

        [STAThread]
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
/*
            SynchronizationContext.SetSynchronizationContext (null);
            ReadText ();

            using (var context = new WindowsFormsSynchronizationContext ())
            {
                SynchronizationContext.SetSynchronizationContext (context);
                ReadText ();
            }

            using (var context = new WindowsFormsSynchronizationContext ())
            {
                SynchronizationContext.SetSynchronizationContext (context);
                ReadText ();
            }
*/
            {
                var context     = new DispatcherSynchronizationContext ();
                SynchronizationContext.SetSynchronizationContext (context);
                ReadText ();
            }

            SynchronizationContext.SetSynchronizationContext (null);

            RaceCondition ();

            Console.ReadKey ();
        }
    }
}

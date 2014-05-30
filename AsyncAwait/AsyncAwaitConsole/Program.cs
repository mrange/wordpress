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

using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Threading;

namespace AsyncAwait
{
    static class Program
    {
        delegate SynchronizationContext SynchronizationContextDelegate ();

        [STAThread]
        static void Main(string[] args)
        {
            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;

            SynchronizationContextDelegate defaultContext       = () => null;
            SynchronizationContextDelegate windowsFormsContext  = () => new WindowsFormsSynchronizationContext ();
            SynchronizationContextDelegate dispatcherContext    = () => new DispatcherSynchronizationContext ();

            Info ("Starting test run...");

            // Starting test runs with different variants of 
            // synchronization contexts and continueOnCapturedContext

            RunTestCase (defaultContext     , continueOnCapturedContext:true    );
            RunTestCase (defaultContext     , continueOnCapturedContext:false   );
            RunTestCase (windowsFormsContext, continueOnCapturedContext:true    );
            RunTestCase (windowsFormsContext, continueOnCapturedContext:false   );
            RunTestCase (dispatcherContext  , continueOnCapturedContext:true    );
            RunTestCase (dispatcherContext  , continueOnCapturedContext:false   );

            Info ("Test run done");
        }

        static void RunTestCase (SynchronizationContextDelegate contextCreator, bool continueOnCapturedContext)
        {
            var thread = 
                new Thread (() => TestCase (contextCreator, continueOnCapturedContext))
                {
                    Name                = "Test case"   ,
                    IsBackground        = true          ,
                };

            thread.SetApartmentState (ApartmentState.STA);
            
            thread.Start ();

            var completed = thread.Join (TimeSpan.FromSeconds (1));
            if (!completed)
            {
                Error ("Detected dead-lock");
                thread.Interrupt ();
            }

        }

        static void TestCase (SynchronizationContextDelegate contextCreator, bool continueOnCapturedContext)
        {
            var previous = SynchronizationContext.Current;

            var context = contextCreator ();
            SynchronizationContext.SetSynchronizationContext (context);

            var description = context != null ? context.GetType ().Name : "Null" ;
            Info (
                "ContinueOnCapturedContext={0} and context={1}".FormatWith (
                    continueOnCapturedContext   , 
                    description                 ));

            try 
            {
                var task = ReadTextTask (continueOnCapturedContext);

                // Awaiting a result on async task in this way may cause a dead-lock
                // depending on the synchronization context and continueOnCapturedContext
                var text = task.Result;
            }
            catch (Exception exc)
            {
                Info ("Thread threw exception: {0}".FormatWith (exc.Message));
            }
            finally
            {
                var disposable = context as IDisposable;
                if (disposable != null)
                {
                    try
                    {
                        disposable.Dispose ();
                    }
                    catch (Exception exc)
                    {
                        Error ("Caught exception during dispose: {0}", exc.Message);
                    }
                }

                SynchronizationContext.SetSynchronizationContext (null);
            }

        }

        static async Task<string> ReadStreamAsync (string fileName, bool continueOnCapturedContext)
        {
            using (var sr = new StreamReader(fileName))
            {
                var result = await sr.ReadToEndAsync().ConfigureAwait (continueOnCapturedContext);
                return result;
            }
        }

        static int readingFiles = 0;

        static async Task<string> ReadTextTask (bool continueOnCapturedContext)
        {
            var before = Thread.CurrentThread.ManagedThreadId;

            // Tracks how many files are being read concurrently
            // If the continuation is run by a different thread this can cause race conditions
            ++readingFiles;

            var text = await ReadStreamAsync ("SomeText.txt", continueOnCapturedContext).ConfigureAwait (continueOnCapturedContext);

            --readingFiles;

            var after = Thread.CurrentThread.ManagedThreadId;
            if (before != after)
            {
                Error ("Race condition detected");
            }

            return text;
        }

        static object consoleLock = new object ();

        static void Info (string message, [CallerMemberName] string memberName = null)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine ("{0}: {1}", memberName, message);
            }
        }

        static void Error (string message, [CallerMemberName] string memberName = null)
        {
            lock (consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine ("{0}: {1}", memberName, message);
            }
        }

        static string FormatWith (this string format, params object[] args)
        {
            return string.Format (CultureInfo.InvariantCulture, format, args);
        }
    }
}

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
using System.Threading.Tasks;
using System.Windows;

namespace AsyncAwaitUI
{
    public partial class MainWindow
    {
        int readingFiles = 0;

        public MainWindow()
        {
            InitializeComponent();
        }

        void Clicked_DeadLock(object sender, EventArgs args)
        {
            SomeClass.TraceThreadId ();

            var readTask    = SomeClass.ReadSomeTextAsync("SomeText.txt");
            var text        = readTask.Result;

            

            SomeClass.TraceThreadId ();
        }

        async Task<string> ReadTask ()
        {
            SomeClass.TraceThreadId ();

            ++readingFiles;

            var text = await SomeClass.ReadSomeTextAsync("SomeText.txt");

            --readingFiles;

            SomeClass.TraceThreadId ();

            return text;
        }

        async void Clicked_NoDeadLock (object sender, EventArgs args)
        {
            var text = await ReadTask ();
        }
    }
}

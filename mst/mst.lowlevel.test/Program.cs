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
using System.Threading;

namespace mst.lowlevel.test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Please focus the application");

            for (var iter = 5; iter > 0; --iter)
            {
                Console.WriteLine("{0}...", iter);
                Thread.Sleep(1000);
            }

            //Keyboard.Send ("Testing");
            Mouse.LeftClickAndHold(200,200);
            Mouse.ReleaseLeft(400,400);
        }
    }
}

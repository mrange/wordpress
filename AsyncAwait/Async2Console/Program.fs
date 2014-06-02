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

open System
open System.IO
open System.Threading

open mrange


let createStream =
    async2 {
        return new StreamReader ("SomeText.txt")
    }

let simple =
    async2 {
        use! x = createStream
        use mtx = new Mutex ()
        do! Async2.AwaitWaitHandle mtx
        return 1
    }

[<EntryPoint>]
let main argv = 
    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    let comp (v : 'T)   : unit = 
        printfn "Operation completed: %A" v
    let exe (ex : exn)  : unit = 
        printfn "Exception was raised: %A" ex
    let canc (cr : CancelReason) : unit = 
        printfn "Operation cancelled: %A" cr

    Async2.Start simple comp exe canc
    0

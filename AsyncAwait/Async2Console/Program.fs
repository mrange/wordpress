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

// TODO: 
// Start child task
// 

let readText (fileName : string) =
    async2 {
        use stream = new StreamReader (fileName)
        let! text = Async2.AwaitTask <| stream.ReadToEndAsync ()
        return text
    }

let composite =
    async2 {
        let! x = Async2.StartChild <| readText "SomeText.txt" 
        let! y = Async2.StartChild <| readText "SomeText.txt" 

        let! xx = x
        let! yy = y

        return xx + yy
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

    Async2.Start composite comp exe canc
    0

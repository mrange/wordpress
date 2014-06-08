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

let readText (fileName : string) =
    async2 {
        use stream = new StreamReader (fileName)
        let! text = Async2.AwaitTask <| stream.ReadToEndAsync ()
        return text
    }

let composite (sc : SynchronizationContext) =
    async2 {
        let! x = Async2.StartChild <| readText "SomeText.txt" 
        let! y = Async2.StartChild <| readText "SomeOtherText.txt" 

        let! xx = x
        let! yy = y

        let zz = xx + " " + yy

        do! Async2.AwaitSwitchToContext sc <| fun () -> printfn "Result: %s" zz 

        return zz
    }
(*
let forExpression =
    async2 {
        for i in 0..9 do
            let! zz = composite
            return zz
        return ""
    }
*)
[<EntryPoint>]
let main argv = 
    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    ignore <| Async2Test.runTestCases ()

(*
    let comp (v : 'T)   : unit = 
        printfn "Operation completed: %A" v
    let exe (ex : exn)  : unit = 
        printfn "Exception was raised: %A" ex.Message
    let canc (cr : CancelReason) : unit = 
        printfn "Operation cancelled: %A" cr

    let t = Async2.StartNewThread ApartmentState.STA (composite SynchronizationContext.Current) comp exe canc
    t.Join ()
    *)
    0

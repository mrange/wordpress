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

namespace mrange

module Async2Test =

    open System

    exception TestException 
    let cancelObject = "Cancelled"

    let mutable errors = 0

    let inline print (cc : ConsoleColor) (label : string) (msg : string) =
        let color = Console.ForegroundColor
        try
            Console.ForegroundColor <- cc
            Console.WriteLine ("{0}: {1}", label, msg)
        finally
            Console.ForegroundColor <- color

    let info msg = 
        print ConsoleColor.White    "INFO " msg

    let error msg = 
        errors <- errors + 1
        print ConsoleColor.Red      "ERROR" msg

    let startTestRun 
        (nm: string) 
        (a : Async2<'T>) 
        ncomp nexe ncanc 
        ecomp eexe ecanc 
        ccomp cexe ccanc 
        = 
        let n = async2 {
            let! v = a            

            return v
        }   
        let e = async2 {
            let! v = a            

            raise TestException ()

            return v
        }   
        let c = async2 {
            let! v = a            
            do! Async2.Cancel cancelObject
            return v
        }  
        info <| sprintf "Running test case: %s" nm
        Async2.Start n ncomp nexe ncanc
        Async2.Start e ecomp eexe ecanc
        Async2.Start c ccomp cexe ccanc

    let ncomp   (n : string) vv v               = if v <> vv then error <| sprintf "%s - normal completion, expected %A, got %A" n vv v
    let nexe    (n : string) (ex : exn)         = error <| sprintf "%s - normal exception   , unexpected %A" n ex
    let ncanc   (n : string) (cr : CancelReason)= error <| sprintf "%s - normal cancel      , unexpected %A" n cr
    let ecomp   (n : string) v                  = error <| sprintf "%s - exception value    , unexpected %A" n v
    let eexe    (n : string) (ex : exn)         = match ex with 
                                                  | :? TestException -> ()
                                                  | _ -> error <| sprintf "%s - exception exception, unexpected %A" n ex
    let ecanc   (n : string) (cr : CancelReason)= error <| sprintf "%s - exception cancel   , unexpected %A" n cr
    let ccomp   (n : string) v                  = error <| sprintf "%s - cancel value       , unexpected %A" n v
    let cexe    (n : string) (ex : exn)         = error <| sprintf "%s - cancel exception   , unexpected %A" n ex
    let ccanc   (n : string) (cr : CancelReason)= match cr with 
                                                  | CancelReason.UserCancelled o when obj.ReferenceEquals (o, cancelObject) -> ()
                                                  | _ -> error <| sprintf "%s - cancel cancel      , unexpected %A" n cr
        
    let startTestRun_CheckValue (n : string) (a : Async2<'T>) (v : 'T) =
        startTestRun 
            (n : string) 
            a
            (ncomp n v)
            (nexe n)
            (ncanc n)
            (ecomp n)
            (eexe n)
            (ecanc n)
            (ccomp n)
            (cexe n)
            (ccanc n)

    let testBind () = 
        let a = async2 {
            return 1
        }
        let b = async2 {
            return 2
        }
        let c = async2 {
            let! aa = a 
            let! bb = b
            return aa + bb
        }
        startTestRun_CheckValue "bind" c 3

//    let testCombine () = 

    let testDelay () = 
        let a = async2 {
            return 1
        }
        let i = ref 0
        let b = Async2.Delay <| fun () -> 
                i := !i + 1
                a
        startTestRun_CheckValue "delay" b 1
        let expected = 3
        if !i <> expected then
            error <| sprintf "Expected to invoke delay %d times but was invoked %d times" expected !i

    let testReturn () = 
        let a = async2 {
            return 1
        }
        startTestRun_CheckValue "return" a 1

    let testReturnFrom () = 
        let a = async2 {
            return 1
        }
        let b = async2 {
            return! a
        }
        startTestRun_CheckValue "return from" b 1

    let testZero () = 
        let x = 0
        let a = async2 {
            if x = 1 then
                return 1
        }
        startTestRun_CheckValue "zero" a 0

    let runTestCases () = 
        testBind ()
        testDelay ()
        testReturn ()
        testReturnFrom ()
        testZero ()
        if errors > 0 then
            error "Error(s) detected"

        errors = 0

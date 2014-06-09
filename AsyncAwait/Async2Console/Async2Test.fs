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
    open System.Collections.Generic
    open System.Linq

    type DisposeChainer(d : IDisposable, a : unit->unit) =
        interface IDisposable with
            member x.Dispose () = 
                    a ()
                    d.Dispose ()
    
    type DisposeDetectorEnumerator<'T>(e : IEnumerator<'T>, a : unit->unit) =
        interface IEnumerator<'T> with
            member x.Current        = e.Current
            member x.Dispose ()     = a (); e.Dispose ()
            member x.MoveNext ()    = e.MoveNext ()
            member x.Reset ()       = e.Reset ()

            member x.Current : obj  = upcast e.Current

    type DisposeDetectorEnumerable<'T>(e : IEnumerable<'T>, a : unit->unit) =
        
        let create () = new DisposeDetectorEnumerator<_> (e.GetEnumerator (), a)

        interface IEnumerable<'T> with
            member x.GetEnumerator () : IEnumerator<'T> = 
                upcast create ()
            member x.GetEnumerator () : System.Collections.IEnumerator = 
                upcast create ()

    type IEnumerable<'T> with
        member x.DisposeDetector (a : unit->unit) : IEnumerable<'T> = upcast DisposeDetectorEnumerable (x, a)

    exception TestException 
    exception LoopException 

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


    type TestContinuations<'T> = 
        {
            name    : string
            ncomp   : 'T          -> unit
            nexe    : exn         -> unit
            ncanc   : CancelReason-> unit
            ecomp   : 'T          -> unit
            eexe    : exn         -> unit
            ecanc   : CancelReason-> unit
            ccomp   : 'T          -> unit
            cexe    : exn         -> unit
            ccanc   : CancelReason-> unit
        }

    let userException<'T when 'T :> exn> n (ex : exn) = 
        match ex with 
        | :? 'T -> ()
        | _ -> error <| sprintf "%s - exception exception, unexpected %A" n ex

    let userCancel n (co : obj) cr =
        match cr with 
        | CancelReason.UserCancelled o when obj.ReferenceEquals (o, co) -> ()
        | _ -> error <| sprintf "%s - cancel cancel      , unexpected %A" n cr

    let failure (n : string) =     
        {
            name  = n
            ncomp = fun v  -> error <| sprintf "%s - normal value       , unexpected %A" n v
            nexe  = fun ex -> error <| sprintf "%s - normal exception   , unexpected %A" n ex
            ncanc = fun cr -> error <| sprintf "%s - normal cancel      , unexpected %A" n cr
            ecomp = fun v  -> error <| sprintf "%s - exception value    , unexpected %A" n v
            eexe  = userException<TestException> n
            ecanc = fun cr -> error <| sprintf "%s - exception cancel   , unexpected %A" n cr
            ccomp = fun v  -> error <| sprintf "%s - cancel value       , unexpected %A" n v
            cexe  = fun ex -> error <| sprintf "%s - cancel exception   , unexpected %A" n ex
            ccanc = userCancel n cancelObject

        }

    let expectedValue (n : string) (exp : 'T) : TestContinuations<'T> = 
        { 
            (failure n) with 
                ncomp = fun v  -> if v <> exp then error <| sprintf "%s - normal completion, expected %A, got %A" n exp v
        }
        

    let startTestRun 
        (a  : Async2<'T>            ) 
        (tcs: TestContinuations<'T> )
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
        info <| sprintf "Running test case: %s" tcs.name
        Async2.Start n tcs.ncomp tcs.nexe tcs.ncanc
        Async2.Start e tcs.ecomp tcs.eexe tcs.ecanc
        Async2.Start c tcs.ccomp tcs.cexe tcs.ccanc
        
    let startTestRun_ExpectedValue (n : string) (a : Async2<'T>) (v : 'T) =
        startTestRun 
            a
            (expectedValue n v)

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
        startTestRun_ExpectedValue "bind" c 3

//    let testCombine () = 

    let testDelay () = 
        let a = async2 {
            return 1
        }
        let i = ref 0
        let b = Async2.Delay <| fun () -> 
                i := !i + 1
                a
        startTestRun_ExpectedValue "delay" b 1
        let expected = 3
        if !i <> expected then
            error <| sprintf "Expected to invoke delay %d times but was invoked %d times" expected !i

    let testFor () = 

        let a = async2 {
            return 1
        }
        let b : Async2<int> = async2 {
            let xx = ref 0

            for i in 0..9 do
                let! aa = a
                xx := !xx + aa

            return !xx                
        }
        startTestRun_ExpectedValue "for" b 10

        let disposed    = ref 0

        let forCancel   = "for - cancel"
        let c : Async2<int> = async2 {
            let xx = ref 0

            let range = Enumerable.Range(0,10).DisposeDetector(fun () -> disposed := !disposed + 1)
            for i in range do
                let! aa = a
                xx := !xx + aa
                if i > 5 then 
                    do! Async2.Cancel forCancel

            return !xx                
        }

        let uc = userCancel forCancel forCancel
        startTestRun 
            c 
            (
                {
                    failure forCancel
                        with    ncanc = uc
                                ccanc = uc
                                ecanc = uc
                }
            )

        let forException   = "for - exception"
        let d : Async2<int> = async2 {
            let xx = ref 0

            let range = Enumerable.Range(0,10).DisposeDetector(fun () -> disposed := !disposed + 1)
            for i in range do
                let! aa = a
                xx := !xx + aa
                if i > 5 then 
                    raise LoopException ()

            return !xx                
        }

        let ue = userException<LoopException> forException
        startTestRun 
            d 
            (
                {
                    failure forException
                        with    nexe = ue
                                cexe = ue
                                eexe = ue
                }
            )

        let expected = 6
        if !disposed <> expected then
            error <| sprintf "Expected to invoke Dispose %d times but was invoked %d times" expected !disposed

    let testWhile () = 

        let a = async2 {
            return 1
        }
        let b : Async2<int> = async2 {
            let xx = ref 0
            let yy = ref 0

            while !yy < 10 do
                yy := !yy + 1
                let! aa = a
                xx := !xx + aa

            return !xx                
        }
        startTestRun_ExpectedValue "while" b 10

        let whileCancel = "while - cancel"
        let c : Async2<int> = async2 {
            let xx = ref 0
            let yy = ref 0

            while !yy < 10 do
                yy := !yy + 1
                let! aa = a
                xx := !xx + aa
                if !yy > 5 then 
                    do! Async2.Cancel whileCancel

            return !xx                
        }

        let uc = userCancel whileCancel whileCancel
        startTestRun 
            c 
            (
                {
                    failure whileCancel
                        with    ncanc = uc
                                ccanc = uc
                                ecanc = uc
                }
            )

        let whileException = "while - exception"
        let d : Async2<int> = async2 {
            let xx = ref 0
            let yy = ref 0

            while !yy < 10 do
                yy := !yy + 1
                let! aa = a
                xx := !xx + aa
                if !yy > 5 then 
                    raise LoopException ()

            return !xx                
        }

        let ue = userException<LoopException> whileException
        startTestRun 
            d 
            (
                {
                    failure whileException
                        with    nexe = ue
                                cexe = ue
                                eexe = ue
                }
            )

    let testReturn () = 
        let a = async2 {
            return 1
        }
        startTestRun_ExpectedValue "return" a 1

    let testReturnFrom () = 
        let a = async2 {
            return 1
        }
        let b = async2 {
            return! a
        }
        startTestRun_ExpectedValue "return from" b 1

    let testZero () = 
        let x = 0
        let a = async2 {
            if x = 1 then
                return ()
        }
        startTestRun_ExpectedValue "zero" a ()

    let runTestCases () = 
        testBind ()
        testDelay ()
        testFor ()
        testReturn ()
        testReturnFrom ()
        testWhile ()
        testZero ()
        if errors > 0 then
            error "Error(s) detected"

        errors = 0

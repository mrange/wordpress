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

    type Disposable(a : unit->unit) =

        member x.InvokeDisposeAction () = a ()

        interface IDisposable with
            member x.Dispose () = x.InvokeDisposeAction ()

    type DisposeDetectorEnumerator<'T>(e : IEnumerator<'T>, a : unit->unit) =
        inherit Disposable(a)

        interface IEnumerator<'T> with
            member x.Current        = e.Current
            member x.Dispose ()     = base.InvokeDisposeAction (); e.Dispose ()
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
    let cancelObject        = "cancelObject"
    let testCaseInvocations = 3

    let disposable (a : unit->unit) = new Disposable(a)

    let errors = ref 0

    let sum (i : int ref) (a : int) =
        i := !i + a

    let inc (i : int ref) =
        sum i 1

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
        inc errors
        print ConsoleColor.Red      "ERROR" msg


    type TestContext<'T> =
        {
            Name        : string
            Normal_Comp : 'T          -> unit
            Normal_Exe  : exn         -> unit
            Normal_Canc : CancelReason-> unit
            Exe_Comp    : 'T          -> unit
            Exe_Exe     : exn         -> unit
            Exe_Canc    : CancelReason-> unit
            Cancel_Comp : 'T          -> unit
            Cancel_Exe  : exn         -> unit
            Cancel_Canc : CancelReason-> unit
        }

    let userException<'T when 'T :> exn> n (ex : exn) =
        match ex with
        | :? 'T -> ()
        | _ -> error <| sprintf "%s - exception exception, unexpected %A" n ex

    let userCancel n (co : obj) cr =
        match cr with
        | CancelReason.UserCancelled o when obj.ReferenceEquals (o, co) -> ()
        | _ -> error <| sprintf "%s - cancel cancel      , unexpected %A" n cr

    let testContext (n : string) (exp : 'T) : TestContext<'T> =
        {
            Name        = n
            Normal_Comp = fun v  -> if v <> exp then error <| sprintf "%s - normal completion, expected %A, got %A" n exp v
            Normal_Exe  = fun ex -> error <| sprintf "%s - normal exception   , unexpected %A" n ex
            Normal_Canc = fun cr -> error <| sprintf "%s - normal cancel      , unexpected %A" n cr
            Exe_Comp    = fun v  -> error <| sprintf "%s - normal value       , unexpected %A" n v
            Exe_Exe     = userException<TestException> n
            Exe_Canc    = fun cr -> error <| sprintf "%s - normal cancel      , unexpected %A" n cr
            Cancel_Comp = fun v  -> error <| sprintf "%s - cancel value       , unexpected %A" n v
            Cancel_Exe  = fun ex -> error <| sprintf "%s - cancel exception   , unexpected %A" n ex
            Cancel_Canc = userCancel n cancelObject
        }

    type TestBehavior   = unit->Async2<unit>
    type TestAsync2<'T> = TestBehavior->Async2<'T>

    let startTestRun
        (a      : TestAsync2<'T>    )
        (ctx    : TestContext<'T>   )
        =

        info <| sprintf "Running test case: %s" ctx.Name

        let normal  = a <| fun () -> Async2.Zero ()
        let exe     = a <| fun () -> raise TestException ()
        let cancel  = a <| fun () -> Async2.Cancel cancelObject

        Async2.Start normal ctx.Normal_Comp ctx.Normal_Exe  ctx.Normal_Canc
        Async2.Start exe    ctx.Exe_Comp    ctx.Exe_Exe     ctx.Exe_Canc
        Async2.Start cancel ctx.Cancel_Comp ctx.Cancel_Exe  ctx.Cancel_Canc

    let startTestRun_ExpectedValue
        (a      : TestAsync2<'T>    )
        (n      : string            )
        (v      : 'T                )
        =
        startTestRun
            a
            (testContext n v)

    let ret v =
        async2 {
            return v
        }

    let testBind () =
        let test behavior =
            async2 {
                let! aa = ret 1
                do! behavior ()
                let! bb = ret 2
                return aa + bb
            }

        startTestRun_ExpectedValue test "bind" 3

    let testCombine () =
        let actual = ref 0

        let test behavior =
            async2 {
                for i in 0..9 do    // the for loop is "combined" with the following lines
                    inc actual      // Visible side-effect of for loop
                do! behavior ()
                let! aa = ret 1
                return aa
            }

        startTestRun_ExpectedValue test "combine" 1

        let expected = 10 * testCaseInvocations
        if !actual <> expected then
            error <| sprintf "Expected to actual sum to be %d but is %d" expected !actual

    let testDelay () =
        let actual  = ref 0
        let test behavior =
            Async2.Delay <| fun () ->
                inc actual
                async2 {
                    do! behavior ()
                    return 1
                }

        startTestRun_ExpectedValue test "delay" 1

        let expected = testCaseInvocations
        if !actual <> expected then
            error <| sprintf "Expected to invoke delay %d times but was invoked %d times" expected !actual

    let testFor () =
        let actual = ref 0

        let test behavior =
            async2 {
                let acc = ref 0

                let range = Enumerable.Range(0,10).DisposeDetector(fun () -> inc actual)
                for i in range do
                    let! aa = ret 1
                    sum acc aa
                    do! behavior ()

                return !acc
            }

        startTestRun_ExpectedValue test "for" 10

        let expected = testCaseInvocations
        if !actual <> expected then
            error <| sprintf "Expected to invoke Dispose %d times but was invoked %d times" expected !actual

    let testWhile () =
        let test behavior =
            async2 {
                let acc = ref 0
                let i   = ref 0

                while !i < 10 do
                    let! aa = ret 1
                    sum acc aa
                    do! behavior ()
                    inc i

                return !acc
            }

        startTestRun_ExpectedValue test "while" 10

    let testReturn () =
        let test behavior =
            async2 {
                do! behavior ()
                return 1
            }

        startTestRun_ExpectedValue test "return" 1

    let testReturnFrom () =
        let test behavior =
            async2 {
                do! behavior ()
                return! ret 1
            }

        startTestRun_ExpectedValue test "return from" 1

    let testTryFinally () =
        let actual = ref 0
        let test behavior =
            async2 {
                try
                    do! behavior ()
                finally
                    inc actual
                return ()
            }

        startTestRun_ExpectedValue test "try finally" ()

        let expected = testCaseInvocations
        if !actual <> expected then
            error <| sprintf "Expected to run finally %d times but was run %d times" expected !actual

    let testTryWith () =
        let actual  = ref 0
        let zero    = ref 0

        let test behavior =
            async2 {
                try
                    do! behavior ()
                with
                | :? TestException -> inc actual
                                      raise TestException ()
                | _ ->  inc zero
                        failwith "Unexpected catch"

                return ()
            }

        let test2 behavior =
            async2 {
                try
                    do! behavior ()
                with
                | _ -> inc actual
                       raise TestException ()

                return ()
            }

        startTestRun_ExpectedValue test "try with" ()

        startTestRun_ExpectedValue test2 "try with" ()

        let expected = 2
        if !actual <> expected then
            error <| sprintf "Expected to run with %d times but was run %d times" expected !actual

    let testUsing () =
        let actual = ref 0

        let detector () = disposable <| fun () -> inc actual

        let adetector =
            async2 {
                return detector ()
            }

        let test behavior =
            async2 {
                use d = detector ()
                do! behavior ()
                return ()
            }

        startTestRun_ExpectedValue test "using" ()

        let test2 behavior =
            async2 {
                use! d = adetector
                do! behavior ()
                return ()
            }

        startTestRun_ExpectedValue test2 "using" ()

        let expected = testCaseInvocations * 2
        if !actual <> expected then
            error <| sprintf "Expected to invoke dispose %d times but was invoked %d times" expected !actual

    let testZero () =
        let test behavior =
            async2 {
                if false then
                    return ()
                do! behavior ()
            }
        startTestRun_ExpectedValue test "zero" ()

    let runTestCases () =
        testBind ()
        testCombine ()
        testDelay ()
        testFor ()
        testReturn ()
        testReturnFrom ()
        testTryFinally ()
        testTryWith ()
        testUsing ()
        testWhile ()
        testZero ()
        if !errors > 0 then
            error "Error(s) detected"

        !errors = 0

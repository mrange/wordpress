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

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices

module internal Win32 =

    [<DllImport("ole32.dll")>]
    extern int CoWaitForMultipleHandles(    UInt32          dwFlags     ,
                                            UInt32          dwTimeout   ,
                                            UInt32          cHandles    ,
                                            nativeint []    pHandles    ,
                                            [<Out>] UInt32& lpdwindex   )

    let COWAIT_WAITALL                      = 0x01u
    let COWAIT_ALERTABLE                    = 0x02u
    let COWAIT_INPUTAVAILABLE               = 0x04u
    let COWAIT_DISPATCH_CALLS               = 0x08u
    let COWAIT_DISPATCH_WINDOW_MESSAGES     = 0X10u


    let waitForMultipleHandles (whs : WaitHandle[]) =
        // TODO: Use AddRef/Release in order to make sure handles are valid
        let pHandles            = whs |> Array.map (fun wh -> wh.SafeWaitHandle.DangerousGetHandle ())
        let flags               = COWAIT_ALERTABLE ||| COWAIT_DISPATCH_CALLS ||| COWAIT_DISPATCH_WINDOW_MESSAGES
        let mutable dwindex     = 0u
        let hr = CoWaitForMultipleHandles(
                                            flags                   ,
                                            UInt32.MaxValue         ,
                                            uint32 pHandles.Length  ,
                                            pHandles                ,
                                            &dwindex                )
        if hr = 0 then int dwindex else -1


[<AutoOpen>]
module internal InternalUtils =

    let TraceException (ex : #exn) = Trace.WriteLine <| sprintf "Async2 caught exception: %A" ex

    let Dispose (d : #IDisposable) : unit =
        try
            d.Dispose ()
        with
        | ex -> TraceException ex

    let DefaultSynchronizationContext = SynchronizationContext ()
    let DefaultTo<'T when 'T : null> (v : 'T) (d : 'T) = if obj.ReferenceEquals (v, null) then d else v

    let Nop v = Unchecked.defaultof<_>

[<AbstractClass>]
type BaseDisposable() =

    let mutable isDisposed = 0

    interface IDisposable with
        member x.Dispose () =
            if Interlocked.Exchange(&isDisposed, 1) = 0 then
                try
                    x.OnDispose ()
                with
                | ex -> TraceException ex

    member x.IsDisposed = isDisposed <> 0

    abstract OnDispose : unit -> unit

type CancelReason =
    | ApplicationShutDown
    | ThreadShutDown
    | UnrecoverableErrorDetected of exn
    | TokenCancelled of CancellationToken
    | UserCancelled of obj

exception CancelException of CancelReason

[<AutoOpen>]
module PublicUtils =

    let Nop v = Unchecked.defaultof<_>

    let DefaultException e  = raise e
    let DefaultCancel cr    = raise <| CancelException cr

type Async2Context(threadId : int, threadName : string) as this =
    inherit BaseDisposable()

    let continuations   = Dictionary<int, WaitHandle*(int*CancelReason option->unit)>()
    let mutable last    = 0

    // Creates a new Async2Context
    static member New () =
        let thread = Thread.CurrentThread
        new Async2Context(thread.ManagedThreadId, thread.Name |> DefaultTo "<NULL>")

    override x.OnDispose () =
        continuations.Clear ()

    // Returns true if there are continuations being awaited
    member x.IsAwaiting =
        if x.IsDisposed then false
        else continuations.Count > 0

    // Checks if the calling thread is the same as the thread owning the Async2Context
    //  Used to detect unintended thread-switches
    member x.CheckCallingThread () =
        let thread = Thread.CurrentThread
        if threadId <> thread.ManagedThreadId then
            failwith
                "Async2Context may only be called by it's owner thread (%d,%s) but was called by another thread (%d,%s)"
                threadId
                threadName
                thread.ManagedThreadId
                thread.Name

    // Returns the awaited continuations
    member x.Continuations =
        if x.IsDisposed then Seq.empty
        else
            continuations
            |> Seq.map (fun kv -> let wh,a = kv.Value in kv.Key,wh,a)

    // Registers a continuation
    //  Call signal when WaitHandle is signalled or cancelled
    member x.RegisterContinuation (waitHandle : WaitHandle) (continuation : int*CancelReason option->unit) : unit =
        x.CheckCallingThread ()
        if x.IsDisposed then ()
        last <- last + 1
        continuations.Add (last, (waitHandle, continuation))

    // Unregisters a continuation
    //  Doesn't raise cancel action
    member x.UnregisterWaitHandle (i : int) : unit =
        x.CheckCallingThread ()
        if x.IsDisposed then ()
        ignore <| continuations.Remove i

    // Cancels all WaitHandles
    //  Raises cancel actions
    member x.CancelAllContinuations (cr : CancelReason) : unit =
        x.CheckCallingThread ()
        if x.IsDisposed then ()
        let kvs = continuations |> Seq.toArray
        continuations.Clear ()
        let cro = Some cr
        for kv in kvs do
            try
                let _, a = kv.Value
                a (kv.Key, cro)
            with
            | ex -> TraceException ex

    // Awaits until no more continuations remains
    member x.AwaitAllContinuations () : unit =
        x.CheckCallingThread ()
        if x.IsDisposed then ()
        try
            while continuations.Count > 0 do
                let cs =
                    continuations
                    |> Seq.map (fun kv -> kv)
                    |> Seq.toArray

                let waitHandles =
                    cs
                    |> Array.map (fun kv -> let (wh, _) = kv.Value in wh)
                let result  = WaitHandle.WaitAny waitHandles
                let kv      = cs.[result]
                let wh, a   = kv.Value
                a (kv.Key, None)
        with
        | ex -> TraceException ex
                reraise ()

type Async2<'T> = Async2Context*('T->unit)*(exn->unit)*(CancelReason->unit)->unit

module Async2 =

    // Used to create Async2 workflow from continuation functions
    let inline FromContinuations f : Async2<'T> = fun (ctx, comp, exe, canc) ->
        ctx.CheckCallingThread ()
        f ctx comp exe canc

    // Awaits a WaitHandle, if await is successful computes a value
    let inline internal AwaitWaitHandleAndDo (disposeHandle : bool) (waitHandle : WaitHandle) (a : unit->'T) : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            let continuation (id, cro) =
                ctx.UnregisterWaitHandle id
                if disposeHandle then
                    Dispose waitHandle
                match cro with
                | Some cr   -> canc cr
                | _         -> comp <| a ()
            ctx.RegisterContinuation waitHandle continuation

    // Awaits a WaitHandle
    let AwaitWaitHandle (disposeHandle : bool) (waitHandle : WaitHandle) : Async2<unit> =
        AwaitWaitHandleAndDo disposeHandle waitHandle Nop

    // Awaits a task
    let AwaitUnitTask (task : Task) : Async2<unit> =
        let ar : IAsyncResult = upcast task
        AwaitWaitHandleAndDo false ar.AsyncWaitHandle <| fun () -> Debug.Assert task.IsCompleted; task.Wait ()

    // Awaits a task
    let AwaitTask (task : Task<'T>) : Async2<'T> =
        let ar : IAsyncResult = upcast task
        AwaitWaitHandleAndDo false ar.AsyncWaitHandle <| fun () -> Debug.Assert task.IsCompleted; task.Result

    let AwaitOnTask (task : Task<'T>) : Async2<'T> = 
        let tryToRun (success, exc, cancel) =             
            if task.IsCompleted then
                success task.Result
                true
            elif task.IsCanceled then
                cancel (UserCancelled task)
                true
            elif task.IsFaulted then
                exc task.Exception
                true
            else
                false

        fun (ctx, success, exc, cancel) ->
            if tryToRun (success, exc, cancel) then ()
            else
                let ar : IAsyncResult = upcast task
                let wh = ar.AsyncWaitHandle
                ctx.RegisterContinuation wh (fun (i, cro) -> 
                    ctx.UnregisterWaitHandle i
                    match cro with
                    | Some cr   -> cancel cr
                    | _         -> 
                        if tryToRun (success, exc, cancel) then ()
                        else 
                            cancel (UnrecoverableErrorDetected <| Exception "tryToRun should succeed in this case")
                    ())

    // Awaits async workflow
    //  Checks that the continuation executes on the correct thread
    let AwaitAsync (a : Async<'T>) : Async2<'T> = fun (ctx, comp, exe, canc) ->
        let acomp v     = ctx.CheckCallingThread (); comp v
        let aexe ex     = ctx.CheckCallingThread (); exe ex
        let acanc ex    = ctx.CheckCallingThread (); canc <| UserCancelled ex
        Async.StartWithContinuations (a, acomp, aexe, acanc)

    // Performs a switch to captured SynchronizationContext and awaits the result
    let AwaitSwitchToContext (sc : SynchronizationContext) (a : unit->'T) : Async2<'T> =
        let evt         = new ManualResetEvent false
        let result      = ref Unchecked.defaultof<'T>
        let sc          = sc |> DefaultTo DefaultSynchronizationContext
        let callback    = SendOrPostCallback (fun _ -> result := a (); ignore <| evt.Set ())
        sc.Post (callback, null)
        AwaitWaitHandleAndDo true evt <| fun () -> !result

    // Cancels a workflow
    let Cancel (state : obj) : Async2<unit> =
        FromContinuations <| fun ctx comp exe canc ->
            canc <| UserCancelled state

    // Starts an Async2 workflow on the current thread
    let StartWithContinuations (f : Async2<'T>) comp exe canc : unit =
        use ctx = Async2Context.New ()
        try
            f (ctx, comp, exe, canc)
            ctx.AwaitAllContinuations ()
        with
        | ex -> ctx.CancelAllContinuations <| UnrecoverableErrorDetected ex
                exe ex

    // Starts an Async2 workflow on the current thread
    let StartImmediate (f : Async2<'T>) : 'T =
        use ctx = Async2Context.New ()

        let result = ref Unchecked.defaultof<'T>

        let comp v = result := v

        try
            f (ctx, comp, DefaultException, DefaultCancel)
            ctx.AwaitAllContinuations ()
        with
        | ex -> ctx.CancelAllContinuations <| UnrecoverableErrorDetected ex
                reraise ()

        !result

    // Starts an Async2 workflow on new thread
    let StartOnThread (apartmentState : ApartmentState) (f : Async2<'T>) comp exe canc : Thread =
        let start  = ThreadStart (fun () -> StartWithContinuations f comp exe canc)
        let thread = Thread (start)
        thread.IsBackground <- true
        thread.SetApartmentState apartmentState
        thread.Start ()
        thread

    // Starts an Async2 workflow on threadpool
    let StartOnThreadPool (f : Async2<'T>) comp exe canc : bool =
        let callback    = WaitCallback (fun _ -> StartWithContinuations f comp exe canc)
        ThreadPool.QueueUserWorkItem callback

    // Starts an Async2 workflow inside an Async2 workflow, note that the child workflow will share the same thread
    let StartChild (f : Async2<'T>) : Async2<Async2<'T>> =
        FromContinuations <| fun ctx comp exe canc ->
            let continuation= ref None
            let value       = ref None

            let child : Async2<'T> = fun (cctx, ccomp, cexe, ccanc) ->
                continuation := Some ccomp
                match !value with
                | Some v    -> ccomp v
                | _         -> ()

            let fcomp v =
                value := Some v
                match !continuation with
                | Some ccomp    -> ccomp v
                | None          -> ()

            f (ctx, fcomp, exe, canc)

            comp child

    // Computation expression support

    // Bind is invoked by let!, do!
    let Bind (f : Async2<'T>) (s : 'T->Async2<'U>) : Async2<'U> =
        FromContinuations <| fun ctx comp exe canc ->
            let fcomp (v : 'T) : unit =
                let ss = s v
                ss (ctx, comp, exe, canc)
            f (ctx, fcomp, exe, canc)

    // Combine is used when sequencing in computation expressions
    let Combine (f : Async2<unit>) (s : Async2<'T>) : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            let fcomp () : unit =
                s (ctx, comp, exe, canc)
            f (ctx, fcomp, exe, canc)

    // Wraps a computation expression as a function
    let Delay func : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            func () (ctx, comp, exe, canc)

    // For is invoked by for .. do
    let For (vs : seq<'T>) (b : 'T->Async2<unit>) : Async2<unit> =
        FromContinuations <| fun ctx comp exe canc ->
            let e = vs.GetEnumerator ()
            let bexe (ex : exn)  : unit =
                Dispose e
                exe ex
            let bcanc (cr : CancelReason) : unit =
                Dispose e
                canc cr
            let rec bcomp ()  : unit =
                if e.MoveNext () then
                    let bb = b e.Current
                    bb (ctx, bcomp, bexe, bcanc)

                else
                    Dispose e
                    comp ()
            try
                bcomp ()
            with
            | ex -> bexe ex

    // Return is invoked by return
    let Return (v : 'T) : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            comp v

    // ReturnFrom is invoked by return from
    let ReturnFrom v : Async2<'T> = v

    // Runs a workflow
    let Run (f : Async2<'T>) : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            f (ctx, comp, exe, canc)

    // Invoked by try..finally
    let TryFinally (b : Async2<'T>) (f : unit->unit) : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            let bcomp (v : 'T)  : unit =
                f ()
                comp v
            let bexe (ex : exn) : unit =
                f ()
                exe ex
            let bcanc (cr : CancelReason): unit =
                f ()
                canc cr
            try
                b (ctx, bcomp, bexe, bcanc)
            with
            | ex -> bexe ex

    // Invoked by try..with
    let TryWith (f : Async2<'T>) (e : exn->Async2<'T>) : Async2<'T> =
        FromContinuations <| fun ctx comp exe canc ->
            let fexe (ex : exn)  : unit =
                let ee = e ex
                ee (ctx, comp, exe, canc)
            try
                f (ctx, comp, fexe, canc)
            with
            | ex -> fexe ex


    // Invoked by use
    let Using<'T, 'U when 'T :> IDisposable> (v : 'T) (f : 'T->Async2<'U>) : Async2<'U> =
        FromContinuations <| fun ctx comp exe canc ->
            let fcomp (u : 'U)  : unit =
                Dispose v
                comp u
            let fexe (ex : exn) : unit =
                Dispose v
                exe ex
            let fcanc (cr : CancelReason): unit =
                Dispose v
                canc cr
            try
                let ff = f v
                ff (ctx, fcomp, fexe, fcanc)
            with
            | ex -> fexe ex

    // Invoked by while..do
    let While (t : unit->bool) (b : Async2<unit>) : Async2<unit> =
        FromContinuations <| fun ctx comp exe canc ->
            let rec bcomp ()  : unit =
                if t () then
                    b (ctx, bcomp, exe, canc)
                else
                    comp ()
            bcomp ()

    // Invoked by empty else branches
    let Zero () : Async2<unit> =
        FromContinuations <| fun ctx comp exe canc ->
            comp ()

// The computation expression builder
type Async2Builder() =
        member x.Bind(f, s)         = Async2.Bind f s
        member x.Combine(f, s)      = Async2.Combine f s
        member x.Delay(func)        = Async2.Delay func
        member x.For(vs, b)         = Async2.For vs b
        member x.Return(v)          = Async2.Return v
        member x.ReturnFrom(v)      = Async2.ReturnFrom v
        member x.Run(f)             = Async2.Run f
        member x.TryFinally(b, f)   = Async2.TryFinally b f
        member x.TryWith(f, e)      = Async2.TryWith f e
        member x.Using(v, f)        = Async2.Using v f
        member x.While(t, b)        = Async2.While t b
//        member x.Yield(v)           = Async2.Yield v
//        member x.YieldFrom(v)       = Async2.YieldFrom v
        member x.Zero()             = Async2.Zero ()


[<AutoOpen>]
module Async2AutoOpen =
    let async2 = Async2Builder()

module Async2Windows =
    open System.Windows
    open System.Windows.Threading

    [<AutoOpen>]
    module internal WindowsUtils =

        let DispatchOnIdle (d : Dispatcher) (a : unit->unit) =
            let d = d |> DefaultTo Dispatcher.CurrentDispatcher

            let del = Action
                        (
                            fun () ->
                                try
                                    a ()
                                with
                                | ex -> TraceException ex
                        )

            ignore <| d.BeginInvoke (DispatcherPriority.ApplicationIdle, del)



    [<AbstractClass>]
    type internal Async2DispatcherJob(context : Async2Context) =
        inherit BaseDisposable()

        member x.Context    = context

        override x.OnDispose () =
            Dispose context

    type internal Async2DispatcherJob<'T>
        (
            a       : Async2<'T>        ,
            comp    : 'T->unit          ,
            exe     : exn->unit         ,
            canc    : CancelReason->unit
        ) as this =
        inherit Async2DispatcherJob(Async2Context.New ())

        let mcomp v =
            try
                try
                    comp v
                with
                | ex -> TraceException ex
            finally
                Dispose this

        let mexe ex =
            try
                try
                    exe ex
                with
                | ex -> TraceException ex
            finally
                Dispose this

        let mcanc cr =
            try
                try
                    canc cr
                with
                | ex -> TraceException ex
            finally
                Dispose this

        do
            try
                a (this.Context, mcomp, mexe, mcanc)
            with
            | ex -> exe ex

    type internal Async2DispatcherContext(threadId : int, threadName : string) =

        let jobs                    = System.Collections.Generic.List<Async2DispatcherJob> ()
        let mutable isProcessing    = false

        // Async2DispatcherContext is thread-affine which means it's associated with a particular thread
        //  [<ThreadStatic>] utilizes thread-local storage to give us a new unique context per thread
        [<ThreadStatic>] [<DefaultValue>] static val mutable private current : Async2DispatcherContext

        static member Current =
            if Async2DispatcherContext.current = Unchecked.defaultof<_> then
                let thread = Thread.CurrentThread
                Async2DispatcherContext.current <- Async2DispatcherContext (thread.ManagedThreadId, thread.Name |> DefaultTo "<NULL>")
            Async2DispatcherContext.current

        // Checks if the calling thread is the same as the thread owning the Async2Context
        //  Used to detect unintended thread-switches
        member x.CheckCallingThread () =
            let thread = Thread.CurrentThread
            if threadId <> thread.ManagedThreadId then
                failwith
                    "Async2DispatcherContext may only be called by it's owner thread (%d,%s) but was called by another thread (%d,%s)"
                    threadId
                    threadName
                    thread.ManagedThreadId
                    thread.Name

        member x.AddJob (job : Async2DispatcherJob) =
            x.CheckCallingThread ()
            job.Context.CheckCallingThread ()

            jobs.Add job

            x.ProcessJobs ()

        member x.ProcessJobs () =
            x.CheckCallingThread ()

            if not isProcessing then
                isProcessing <- true
                try
                    try
                        let continuations =
                            jobs
                            |> Seq.filter (fun job -> not job.IsDisposed)
                            |> Seq.collect (fun job -> job.Context.Continuations)
                            |> Seq.toArray

                        if continuations.Length > 0 then
                            let waitHandles =
                                continuations
                                |> Array.map (fun (_, wh,_) -> wh)
                            let result = Win32.waitForMultipleHandles waitHandles
                            if result < 0 then
                                failwith "Win32.waitForMultipleHandles failed"
                            let id, wh, a = continuations.[result]
                            a (id, None)

                            ignore <| jobs.RemoveAll (Predicate (fun job ->
                                if job.Context.IsAwaiting then false
                                else
                                    Dispose job
                                    true
                                ))

                            if jobs.Count > 0 then
                                DispatchOnIdle Dispatcher.CurrentDispatcher x.ProcessJobs


                    with
                    | ex ->  TraceException ex
                finally
                    isProcessing <- false

    // Starts an Async2 workflow
    let StartWithContinuations (d : Dispatcher) (f : Async2<'T>) comp exe canc : unit =
        DispatchOnIdle d <| fun () ->
            let job = new Async2DispatcherJob<'T>(f, comp, exe,canc)
            Async2DispatcherContext.Current.AddJob job

    // Starts an Async2 workflow
    let Start (d : Dispatcher) (f : Async2<'T>) comp : unit =
        StartWithContinuations d f comp DefaultException DefaultCancel

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

[<AutoOpen>]
module Utils =
    let TraceException (ex : #exn) = Trace.WriteLine <| sprintf "Async2 caught exception: %A" ex

    let Dispose (d : #IDisposable) : unit = 
        try
            d.Dispose ()
        with
            | e -> TraceException e

type CancelReason =
    | ApplicationShutDown
    | ThreadShutDown
    | TokenCancelled
    | UserCancelled of obj

type Async2Context(threadId : int, threadName : string) =

    // Async2Context is thread-affine which means it's associated with a particular thread
    //  [<ThreadStatic>] utilizes thread-local storage to give us a new unique context per thread
    [<ThreadStatic>] [<DefaultValue>] static val mutable private context : Async2Context

    let waitHandles             = Dictionary<int, WaitHandle*(int*CancelReason option->unit)>()
    let mutable last            = 0

    // Get's the context for the current thread, creates on if necessary
    static member Context = 
        if Async2Context.context = Unchecked.defaultof<Async2Context> then
            let thread = Thread.CurrentThread
            Async2Context.context <- Async2Context (thread.ManagedThreadId, thread.Name)
        Async2Context.context

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

    // Registers a WaitHandle async2 will wait upon whenever the owning thread goes idle
    //  Call signal when WaitHandle is signalled or cancelled
    member x.RegisterWaitHandle (waitHandle : WaitHandle) (signal : int*CancelReason option->unit) : unit =   
        if waitHandle = null then failwith "waitHandle must not be null"
        x.CheckCallingThread ()
        last <- last + 1
        waitHandles.Add (last, (waitHandle, signal))

    // Unregisters a WaitHandle
    //  Doesn't raise cancel action
    member x.UnregisterWaitHandle (i : int) : unit = 
        x.CheckCallingThread ()
        ignore <| waitHandles.Remove i

    // Cancels all WaitHandles
    //  Raises cancel actions
    member x.CancelAllWaitHandles (cr : CancelReason) =
        x.CheckCallingThread ()
        let kvs = waitHandles |> Seq.toArray
        waitHandles.Clear ()
        let cro = Some cr
        for kv in kvs do
            try 
                let _, a = kv.Value
                a (kv.Key, cro)
            with 
                | e -> TraceException e

    // Waits until one WaitHandle is signalled
    //  Raises the signal on said handle
    member x.WaitOnHandles () =
        x.CheckCallingThread ()
        try
            while waitHandles.Count > 0 do
                let kvs = waitHandles |> Seq.toArray
                let whs = 
                    kvs 
                    |> Seq.map (fun kv -> let wh, _ = kv.Value in wh)
                    |> Seq.toArray
                let result = WaitHandle.WaitAny whs
                let kv = kvs.[result]
                let _, a = kv.Value
                a (kv.Key, None)
        with 
            | e -> TraceException e
                   reraise () 

type Async2<'T> = Async2Context*('T->unit)*(exn->unit)*(CancelReason->unit)->unit

module Async2 =

    // Extended

    // Used to create Async2 workflow from continuation functions
    let inline FromContinuations f : Async2<'T> = fun (ctx, comp, exe, canc) -> 
//        ctx.CheckCallingThread ()
        f ctx comp exe canc

    // Awaits a WaitHandle, if await is successful computes a value 
    let inline AwaitWaitHandleAndDo (waitHandle : WaitHandle) (a : unit->'T) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let signal (id, cro) = 
                ctx.UnregisterWaitHandle id
                match cro with
                | Some cr   -> canc cr 
                | _         -> comp <| a ()
            ctx.RegisterWaitHandle waitHandle signal

    // Awaits a WaitHandle 
    let AwaitWaitHandle (waitHandle : WaitHandle) : Async2<unit> = 
        AwaitWaitHandleAndDo waitHandle <| fun () -> ()

    // Awaits a task
    let AwaitTask (task : Task<'T>) : Async2<'T> = 
        let ar : IAsyncResult = upcast task
        AwaitWaitHandleAndDo ar.AsyncWaitHandle <| fun () -> task.Result

    // Cancels a workflow
    let Cancel (state : obj) : Async2<unit> = 
        FromContinuations <| fun ctx comp exe canc ->
            canc <| UserCancelled state

    // Starts an Async2 workflow
    let Start (f : Async2<'T>) comp exe canc : unit =  
        let ctx = Async2Context.Context
        try
            f (ctx, comp, exe, canc)
            ctx.WaitOnHandles ()
        with
        | e -> exe e
    
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
    let Combine (f : Async2<'T>) (s : Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let fcomp (v : 'T) : unit = 
                s (ctx, comp, exe, canc)
            f (ctx, fcomp, exe, canc)

    // Wraps a computation expression as a function
    let Delay func : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            func () (ctx, comp, exe, canc)

    // For is invoked by for .. do
    let For (vs : seq<'T>) (b : 'T->Async2<'U>) : Async2<'U> = 
        FromContinuations <| fun ctx comp exe canc ->
            let c = ref true
            let e = vs.GetEnumerator ()
            let bexe (ex : exn)  : unit = 
                c := false
                Dispose e
                exe ex
            let bcanc (cr : CancelReason) : unit = 
                c := false
                Dispose e
                canc cr
            let rec bcomp (v : 'U)  : unit =
                if !c && e.MoveNext () then
                    let bb = b e.Current
                    bb (ctx, bcomp, bexe, bcanc)
                else if !c then
                    Dispose e
                    comp v
            try 
                bcomp Unchecked.defaultof<'U>
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
            | e -> bexe e

    // Invoked by try..with
    let TryWith (f : Async2<'T>) (e : exn->Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let fexe (ex : exn)  : unit = 
                let ee = e ex
                ee (ctx, comp, exe, canc)
            try
                f (ctx, comp, fexe, canc)
            with
            | e -> fexe e


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
            | e -> fexe e

    // Invoked by while..do
    let While (t : unit->bool) (b : Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let c = ref true
            let bexe (ex : exn)  : unit = 
                c := false
                exe ex
            let bcanc (cr : CancelReason) : unit = 
                c := false
                canc cr
            let rec bcomp (v : 'T)  : unit = 
                if !c && t () then
                    b (ctx, bcomp, bexe, bcanc)
                else if !c then
                    comp v
            bcomp Unchecked.defaultof<'T>        

    // Invoked by yield
    let Yield (v : 'T) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            comp v

    // Invoked by yield! 
    let YieldFrom v : Async2<'T> = v

    // Invoked by empty else branches
    let Zero () : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            comp Unchecked.defaultof<'T>

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
        member x.TryWith(f, e)      = Async2.TryFinally f e
        member x.Using(v, f)        = Async2.Using v f
        member x.While(t, b)        = Async2.While t b
        member x.Yield(v)           = Async2.Yield v
        member x.YieldFrom(v)       = Async2.Yield v
        member x.Zero()             = Async2.Zero ()


[<AutoOpen>]                    
module Async2AutoOpen =
    let async2 = Async2Builder()


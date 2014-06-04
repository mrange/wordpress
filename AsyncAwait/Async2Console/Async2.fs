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
open System.IO
open System.Threading
open System.Threading.Tasks

[<AutoOpen>]
module Utils =
    let TraceException (ex : #exn) = ()

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

module Async2Module = 

    type Stream with
    
        member x.ReadAsync2 (buffer : byte[], offset : int, count : int) : Async2<int> = fun (ctx, comp, exe, canc) ->
            let mutable overlapped = NativeOverlapped ()
            overlapped.OffsetHigh   <- 0
            overlapped.OffsetLow    <- offset

            ()

module Async2 =

    // Extended

    let inline FromContinuations f : Async2<'T> = fun (ctx, comp, exe, canc) -> 
//        ctx.CheckCallingThread ()
        f ctx comp exe canc

    let inline AwaitWaitHandleAndDo (waitHandle : WaitHandle) (a : unit->'T) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let signal (id, cro) = 
                ctx.UnregisterWaitHandle id
                match cro with
                | Some cr   -> canc cr 
                | _         -> comp <| a ()
            ctx.RegisterWaitHandle waitHandle signal

    let AwaitWaitHandle (waitHandle : WaitHandle) : Async2<unit> = 
        AwaitWaitHandleAndDo waitHandle <| fun () -> ()

    let AwaitTask (task : Task<'T>) : Async2<'T> = 
        let ar : IAsyncResult = upcast task
        AwaitWaitHandleAndDo ar.AsyncWaitHandle <| fun () -> task.Result

    let Cancel (state : obj) : Async2<unit> = 
        FromContinuations <| fun ctx comp exe canc ->
            canc <| UserCancelled state

    let Start (f : Async2<'T>) comp exe canc : unit =  
        let ctx = Async2Context.Context
        try
            f (ctx, comp, exe, canc)
            ctx.WaitOnHandles ()
        with
        | e -> exe e
    
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

    let Bind (f : Async2<'T>) (s : 'T->Async2<'U>) : Async2<'U> = 
        FromContinuations <| fun ctx comp exe canc ->
            let fcomp (v : 'T) : unit = 
                let ss = s v
                ss (ctx, comp, exe, canc)
            f (ctx, fcomp, exe, canc)

    let Combine (f : Async2<'T>) (s : Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let fcomp (v : 'T) : unit = 
                s (ctx, comp, exe, canc)
            f (ctx, fcomp, exe, canc)

    let Delay func : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            func () (ctx, comp, exe, canc)

    let For (vs : seq<'T>) (b : 'T->Async2<'U>) : Async2<'U> = 
        FromContinuations <| fun ctx comp exe canc ->
            let e = vs.GetEnumerator ()
            let bexe (ex : exn)  : unit = 
                Dispose e
                exe ex
            let bcanc (cr : CancelReason) : unit = 
                Dispose e
                canc cr
            let rec bcomp (v : 'U)  : unit = 
                Dispose e
                if e.MoveNext () then
                    let bb = b e.Current
                    bb (ctx, bcomp, bexe, bcanc)
                else
                    Dispose e
                    comp v
            if e.MoveNext () then
                let bb = b e.Current
                bb (ctx, bcomp, bexe, bcanc)
            else
                Dispose e
                comp Unchecked.defaultof<'U>

    let Return (v : 'T) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            comp v

    let ReturnFrom v : Async2<'T> = v

    let Run (f : Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            f (ctx, comp, exe, canc)

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

    let TryWith (f : Async2<'T>) (e : exn->Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let fexe (ex : exn)  : unit = 
                let ee = e ex
                ee (ctx, comp, exe, canc)
            try
                f (ctx, comp, fexe, canc)
            with
            | e -> fexe e


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

    let While (t : unit->bool) (b : Async2<'T>) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            let rec fcomp (v : 'T)  : unit = 
                if t () then
                    b (ctx, fcomp, exe, canc)
                else
                    comp v        
            if t () then
                b (ctx, fcomp, exe, canc)
            else
                comp Unchecked.defaultof<'T>

    let Yield (v : 'T) : Async2<'T> = 
        FromContinuations <| fun ctx comp exe canc ->
            comp v

    let YieldFrom v : Async2<'T> = v

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


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
open System.Collections.Generic
open System.IO
open System.Threading

[<AutoOpen>]
module Utils =
    let Dispose (d : #IDisposable) : unit = 
        try
            d.Dispose ()
        with
            | e -> ()   // TODO: Trace


type CancelReason =
    | ApplicationShutDown
    | ThreadShutDown

type Async2Context() =
    [<ThreadStatic>] [<DefaultValue>] static val mutable private context : Async2Context

    static member Context = 
        if Async2Context.context = Unchecked.defaultof<Async2Context> then
            Async2Context.context <- Async2Context ()
        Async2Context.context

    let waitHandles = Dictionary<int, WaitHandle*(int*CancelReason option->unit)>()
    let mutable last= 0

    member x.WaitOnHandles () =
        try
            while waitHandles.Count > 0 do
                let kvs = waitHandles |> Seq.toArray
                let whs = 
                    kvs 
                    |> Seq.map (fun kv -> let wh,_ = kv.Value in wh)
                    |> Seq.toArray
                let result = WaitHandle.WaitAny whs
                let kv = kvs.[result]
                let _,a = kv.Value
                a (kv.Key, None)
        with 
            | e -> () // TODO: Cancel all waithandles

    member x.RegisterWaitHandle (waitHandle : WaitHandle) (signal : int*CancelReason option->unit) : unit =   
        last <- last + 1
        waitHandles.Add (last, (waitHandle,signal))

    member x.UnregisterWaitHandle (i : int) : unit = 
        ignore <| waitHandles.Remove i

type Async2<'T> = Async2Context*('T->unit)*(exn->unit)*(CancelReason->unit)->unit

module Async2 =

    let AwaitWaitHandle (waitHandle : WaitHandle) : Async2<unit> = fun (ctx,comp,exe,canc) ->
        let signal (id,cro) = 
            ctx.UnregisterWaitHandle id
            match cro with
            | Some cr   -> canc cr 
            | _         -> comp ()
        ctx.RegisterWaitHandle waitHandle signal

    let Bind (f : Async2<'T>) (s : 'T->Async2<'U>) : Async2<'U> = fun (ctx,comp,exe,canc) ->
        let fcomp (v : 'T) : unit = 
            let ss = s v
            ss (ctx,comp,exe,canc)
        f (ctx,fcomp,exe,canc)

    let Combine (f : Async2<'T>) (s : Async2<'T>) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        let fcomp (v : 'T) : unit = 
            s (ctx,comp,exe,canc)
        f (ctx,fcomp,exe,canc)

    let Delay func : Async2<'T> = fun (ctx,comp,exe,canc) ->
        func () (ctx,comp,exe,canc)

    let For (vs : seq<'T>) (b : 'T->Async2<'U>) : Async2<'U> = fun (ctx,comp,exe,canc) ->
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
                bb (ctx,bcomp,bexe,bcanc)
            else
                Dispose e
                comp v
        if e.MoveNext () then
            let bb = b e.Current
            bb (ctx,bcomp,bexe,bcanc)
        else
            Dispose e
            comp Unchecked.defaultof<'U>


    let Return (v : 'T) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        comp v

    let ReturnFrom v : Async2<'T> = v

    let Run (f : Async2<'T>) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        f (ctx,comp,exe,canc)

    let TryFinally (b : Async2<'T>) (f : unit->unit) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        let bcomp (v : 'T)  : unit = 
            f ()
            comp v
        let bexe (ex : exn) : unit = 
            f ()
            exe ex
        let bcanc (cr : CancelReason): unit = 
            f ()
            canc cr
        b (ctx, bcomp, bexe, bcanc)

    let TryWith (f : Async2<'T>) (e : exn->Async2<'T>) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        let fexe (ex : exn)  : unit = 
            let ee = e ex
            ee (ctx,comp,exe,canc)
        f (ctx, comp, fexe, canc)


    let Using<'T, 'U when 'T :> IDisposable> (v : 'T) (f : 'T->Async2<'U>) : Async2<'U> = fun (ctx,comp,exe,canc) ->
        let fcomp (u : 'U)  : unit = 
            Dispose v
            comp u
        let fexe (ex : exn) : unit = 
            Dispose v
            exe ex
        let fcanc (cr : CancelReason): unit = 
            Dispose v
            canc cr
        let ff = f v
        ff (ctx, fcomp, exe, canc)

    let While (t : unit->bool) (b : Async2<'T>) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        let rec fcomp (v : 'T)  : unit = 
            if t () then
                b (ctx,fcomp,exe,canc)
            else
                comp v        
        if t () then
            b (ctx,fcomp,exe,canc)
        else
            comp Unchecked.defaultof<'T>

    let Yield (v : 'T) : Async2<'T> = fun (ctx,comp,exe,canc) ->
        comp v

    let YieldFrom v : Async2<'T> = v

    let Zero () : Async2<'T> = fun (ctx,comp,exe,canc) ->
        comp Unchecked.defaultof<'T>

    let Start (f : Async2<'T>) comp exe canc : unit =  
        let ctx = Async2Context.Context
        f (ctx,comp,exe,canc)
        ctx.WaitOnHandles ()



type Async2Builder() = 
        member x.Bind(f,s)      = Async2.Bind f s
        member x.Combine(f,s)   = Async2.Combine f s
        member x.Delay(func)    = Async2.Delay func
        member x.For(vs,b)      = Async2.For vs b
        member x.Return(v)      = Async2.Return v
        member x.ReturnFrom(v)  = Async2.ReturnFrom v
        member x.Run(f)         = Async2.Run f
        member x.TryFinally(b,f)= Async2.TryFinally b f
        member x.TryWith(f,e)   = Async2.TryFinally f e
        member x.Using(v,f)     = Async2.Using v f
        member x.While(t,b)     = Async2.While t b
        member x.Yield(v)       = Async2.Yield v
        member x.YieldFrom(v)   = Async2.Yield v
        member x.Zero()         = Async2.Zero ()

let async2 = Async2Builder()

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

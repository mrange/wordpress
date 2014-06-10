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
open System.Diagnostics
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Windows.Forms
open System.Windows.Threading

let info (msg : string) =
    printfn "INFO  : %s" msg

let error (msg : string) =
    printfn "ERROR : %s" msg

let dispose o =
    try
        match o :> obj with
        | :? IDisposable as d -> d.Dispose()
        | _ -> ()
    with
        e -> info <| sprintf "Caught exception during dispose: %s" e.Message


let mutable readingFiles = 0;

let AwaitTask2 (task : Task<'T>) : Async<'T> =
    let awaitable   = task.ConfigureAwait(false) // Ignore the SynchronizationContext
    let awaiter     = awaitable.GetAwaiter()
    if awaiter.IsCompleted then
        async.Return <| awaiter.GetResult()
    else
        Async.FromContinuations <| fun (continuation, _, _) ->
            awaiter.OnCompleted <| Action(fun () ->
                let result = awaiter.GetResult()
                continuation result
                )

let readingFilesAsync (description : string) (fileName : string) =
    async {
        let before  = Thread.CurrentThread.ManagedThreadId

        readingFiles <- readingFiles + 1

        use sr = new StreamReader(fileName)

        let length  = int sr.BaseStream.Length
        let bytes   = Array.create length <| byte 0

        let! text = Async.AwaitTask <| sr.ReadToEndAsync()
//        let! text = AwaitTask2 <| sr.ReadToEndAsync()
//        let! result = Async.AwaitIAsyncResult <| sr.BaseStream.BeginRead(bytes, 0, length, null, null)

        readingFiles <- readingFiles - 1

        do! Async.SwitchToContext SynchronizationContext.Current

        let after   = Thread.CurrentThread.ManagedThreadId

        if before <> after then
            info <| sprintf "%s - Race condition detected" description
    }

let testCase
    (description    : string                        )
    (contextCreator : unit -> SynchronizationContext)
    (runner         : Async<unit> -> unit           ) =

    let previous    = SynchronizationContext.Current
    let context     = contextCreator ()

    SynchronizationContext.SetSynchronizationContext context

    let myTask =
        async {
            try
                let! test = readingFilesAsync description "SomeText.txt"
                return ()
            finally
                SynchronizationContext.SetSynchronizationContext null
                dispose context

            }
    runner myTask
    ()

let runTestCase
    (description    : string                        )
    (contextCreator : unit -> SynchronizationContext)
    (runner         : Async<unit> -> unit           ) =
    let threadStart     = ThreadStart(fun () -> testCase description contextCreator runner)
    let thread          = Thread (threadStart)
    thread.Name         <-sprintf "Test case - %s" <| description
    thread.IsBackground <- false
    thread.SetApartmentState ApartmentState.STA

    thread.Start ()

    let completed = thread.Join(TimeSpan.FromSeconds 1.)
    if not completed then
        error <| sprintf "%s - Detected dead-lock" description
        thread.Interrupt ()


[<EntryPoint>]
[<STAThread>]
let main argv =
    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    let synchronizationContexts : (string*(unit->SynchronizationContext)) list =
        [
            "Default"       , fun () -> null
            "WindowsForms"  , fun () -> upcast new WindowsFormsSynchronizationContext ()
            "Dispatcher"    , fun () -> upcast new DispatcherSynchronizationContext ()
        ]

    let asyncRunners : (string*(Async<unit>->unit)) list =
        [
            "SameThread"    , fun a -> Async.StartImmediate a
            "ThreadPool"    , fun a -> Async.Start a
            "Task"          , fun a -> ignore <| Async.StartAsTask a
        ]

    let runs = 5

    info <| "Starting test run..."

    for c, contextCreator in synchronizationContexts do
        for r, runner in asyncRunners do
            let description = sprintf "%s,%s" c r
            info <| sprintf "Test case %s - doing %d runs" description runs
            for i in 1..runs do
                runTestCase description contextCreator runner

    info <| "Test run done"

    0

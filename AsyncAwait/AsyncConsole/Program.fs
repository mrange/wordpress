
open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Windows.Forms
open System.Windows.Threading

let info (msg : string) =
    printfn "INFO  : %s" msg

let error (msg : string) =
    printfn "ERROR : %s" msg

let mutable readingFiles = 0;

let readingFilesAsync (fileName : string) =
    async {
        
        let before  = Thread.CurrentThread.ManagedThreadId

        readingFiles <- readingFiles + 1

        use sr = new StreamReader(fileName)
        let! text = Async.AwaitTask <| sr.ReadToEndAsync ()

        readingFiles <- readingFiles - 1

        let after   = Thread.CurrentThread.ManagedThreadId
        
        if before <> after then
            error "Race condition detected"
    }

let testCase (contextCreator : unit -> SynchronizationContext) (continueOnCapturedContext : bool) =

    let previous    = SynchronizationContext.Current
    let context     = contextCreator ()

    SynchronizationContext.SetSynchronizationContext context

    let description = if context <> null then context.GetType().Name else "Null"

    info <| 
        sprintf 
            "ContinueOnCapturedContext=%s and context=%s" 
            (continueOnCapturedContext.ToString())
            description 

    try
        let task        = Async.StartAsTask <| readingFilesAsync "SomeText.txt"
        let test        = task.Result
        ()
    finally
        try
            match context :> obj with
            | :? IDisposable as d -> d.Dispose()
            | _ -> ()
        with
            e -> info <| sprintf "Caught exception during dispose: %s" e.Message

        SynchronizationContext.SetSynchronizationContext null

let runTestCase (contextCreator : unit -> SynchronizationContext) (continueOnCapturedContext : bool) =
    let threadStart     = ThreadStart(fun () -> testCase contextCreator continueOnCapturedContext)
    let thread          = Thread (threadStart)
    thread.Name         <-"Test case"
    thread.IsBackground <- true
    thread.SetApartmentState ApartmentState.STA

    thread.Start ()

    let completed = thread.Join(TimeSpan.FromSeconds 1.)
    if not completed then
        error "Detected dead-lock"
        thread.Interrupt ()


[<EntryPoint>]
[<STAThread>]
let main argv = 
    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    let testCases : (bool*(unit->SynchronizationContext)) list = 
        [
            true    , fun () -> null
            false   , fun () -> null
            true    , fun () -> new WindowsFormsSynchronizationContext () :> SynchronizationContext
            false   , fun () -> new WindowsFormsSynchronizationContext () :> SynchronizationContext
            true    , fun () -> new DispatcherSynchronizationContext () :> SynchronizationContext
            false   , fun () -> new DispatcherSynchronizationContext () :> SynchronizationContext
        ]

    info <| "Starting test run..."

    for continueOnCapturedContext,contextCreator in testCases do
        runTestCase contextCreator continueOnCapturedContext

    info <| "Test run done"

    0

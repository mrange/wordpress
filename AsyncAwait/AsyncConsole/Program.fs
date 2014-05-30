
open System
open System.Diagnostics
open System.IO
open System.Threading
open System.Windows.Forms
open System.Windows.Threading

let traceThreadId (caller : string) =
    printfn "%s, thread id: %d" caller Thread.CurrentThread.ManagedThreadId

let readSomeTextAsync (fileName : string) =
    async {
        let trace () = traceThreadId "readSomeTextAsync"
        trace ()
        
        let ctx = SynchronizationContext.Current
        let desc= if ctx <> null then ctx.GetType().FullName else "Null"
        printfn "ReadSomeTextAsync, SynchronizationContext: %s" desc 

        try
            use sr = new StreamReader(fileName)

            let! text = Async.AwaitTask <| sr.ReadToEndAsync ()

            return text
        finally
            trace ()
    }

let readSomeText () =
    let trace () = traceThreadId "readSomeTextAsync"
    trace ()

    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    let readTask    = Async.StartAsTask <| readSomeTextAsync "SomeText.txt"
    let text        = readTask.Result

    printfn "Read %d characters" text.Length

    trace ()


[<EntryPoint>]
[<STAThread>]
let main argv = 

    printfn "Use default synchronization context"
    SynchronizationContext.SetSynchronizationContext null
    readSomeText ()

    printfn "Use windows form synchronization context"
    use context = new WindowsFormsSynchronizationContext ()
    SynchronizationContext.SetSynchronizationContext <| context 
    readSomeText ()

    printfn "Use dispatcher synchronization context"
    let context     = DispatcherSynchronizationContext ()
    SynchronizationContext.SetSynchronizationContext <| context 
    readSomeText ()

    SynchronizationContext.SetSynchronizationContext null

    ignore <| Console.ReadKey ()                    

    0

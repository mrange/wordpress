
open System
open System.Diagnostics
open System.IO
open System.Threading

let traceThreadId (caller : string) =
    Trace.WriteLine <| sprintf "%s, thread id: %d" caller Thread.CurrentThread.ManagedThreadId

let readSomeTextAsync (fileName : string) =
    async {
        let trace () = traceThreadId "readSomeTextAsync"
        trace ()

        try
            use sr = new StreamReader(fileName)

            let! text = Async.AwaitTask <| sr.ReadToEndAsync ()

            return text
        finally
            trace ()
    }

[<EntryPoint>]
let main argv = 
    let trace () = traceThreadId "readSomeTextAsync"
    trace ()

    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    let readTask    = Async.StartAsTask <| readSomeTextAsync "SomeText.txt"
    let text        = readTask.Result

    printfn "Read %d characters" text.Length

    trace ()

    ignore <| Console.ReadKey ()                    

    0

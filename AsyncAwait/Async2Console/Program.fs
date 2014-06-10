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
open System.IO
open System.Threading
open System.Threading.Tasks
open System.Windows
open System.Windows.Controls

open mrange

let readText (fileName : string) =
    async2 {
        use stream = new StreamReader (fileName)
        let! text = Async2.AwaitTask <| stream.ReadToEndAsync ()
        return text
    }

let composite =
    async2 {
        
        // Inserts a delay to visualize UI doesn't get blocked
        do! Async2.AwaitUnitTask <| Task.Delay 2000

        let! x = Async2.StartChild <| readText "SomeText.txt"
        let! y = Async2.StartChild <| readText "SomeOtherText.txt"

        let! xx = x
        let! yy = y

        let zz = xx + " " + yy

        return zz
    }

[<EntryPoint>]
[<STAThread>]
let main argv =
    Environment.CurrentDirectory <- AppDomain.CurrentDomain.BaseDirectory

    ignore <| Async2Test.runTestCases ()

    let button              = Button ()
    button.Content          <- "Go!"
    button.Padding          <- Thickness(8.)
    button.Margin           <- Thickness(4.)
    button.Width            <- 128.
    button.Height           <- 32.
    button.Click.Add (fun e -> ())

    let text                = TextBlock ()

    let onClick e           =
        let comp (v : string)   : unit =
            text.Text <- v
        let exe (ex : exn)  : unit =
            printfn "Exception was raised: %A" ex.Message
        let canc (cr : CancelReason) : unit =
            printfn "Operation cancelled: %A" cr

        Async2.Start composite comp exe canc

    button.Click.Add onClick

    let stackPanel          = StackPanel ()
    ignore <| stackPanel.Children.Add button
    ignore <| stackPanel.Children.Add text

    let window = Window ()
    window.MinWidth <- 640.
    window.MinHeight <- 400.
    window.Content <- stackPanel
    ignore <| window.ShowDialog ()

    0

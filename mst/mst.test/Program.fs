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

open mst
open mst.test

[<EntryPoint>]
let main argv = 
    //let run = Scenario.RunScenario Map.empty <| MSPaintScenarios.DrawShape
    let run = Scenario.RunScenario Map.empty <| NotepadScenarios.SimpleScenario

    printfn "Printing results (no results implies success):"
    for result in run.State.Results do
        printfn "%A" result

    0

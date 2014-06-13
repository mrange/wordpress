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

namespace mst.test

open mst
open mst.lowlevel

open System.Windows

module Windows = 

    let WaitForUI = Scenario.Retry 5 100

    let DoNotSaveChanges : Scenario<unit> = 
        scenario {
            do! WaitForUI (UIScenario.FocusElement <| ByName "Notepad")
            do! UIScenario.SendChar 'n' Modifier.LeftAlt
        }


    let ConfirmSaveAs : Scenario<unit> = 
        scenario {
            do! WaitForUI (UIScenario.FocusElement <| ByName "Confirm Save As")
            do! UIScenario.SendChar 'y' Modifier.LeftAlt
        }

    let NewFile : Scenario<unit> = 
        scenario {
            do! UIScenario.SendChar 'f' Modifier.LeftAlt
            do! UIScenario.SendChar 'n' Modifier.None

            let! _ = Scenario.Optional DoNotSaveChanges

            return ()
        }

    let OpenFile fileName : Scenario<unit> = 
        let OpenQuery = ByName "Open"
        scenario {
            do! UIScenario.SendChar 'f' Modifier.LeftAlt
            do! UIScenario.SendChar 'o' Modifier.None

            let! _ = Scenario.Optional DoNotSaveChanges

            do! WaitForUI (UIScenario.SetCurrentElement OpenQuery)

            do! UIScenario.SendChar 'n' Modifier.LeftAlt
            do! UIScenario.SendText fileName

            do! UIScenario.SendChar 'o' Modifier.LeftAlt

            do! WaitForUI (UIScenario.FailIfFound OpenQuery)

            return ()
        }

    let SaveFile fileName : Scenario<unit> = 
        let SaveAsQuery = ByName "Save As"
        scenario {
            do! UIScenario.SendChar 'f' Modifier.LeftAlt
            do! UIScenario.SendChar 'a' Modifier.None

            do! WaitForUI (UIScenario.SetCurrentElement SaveAsQuery)

            do! UIScenario.SendChar 'n' Modifier.LeftAlt
            do! UIScenario.SendText fileName

            do! UIScenario.SendChar 's' Modifier.LeftAlt

            let! _ = Scenario.Optional ConfirmSaveAs

            do! WaitForUI (UIScenario.FailIfFound SaveAsQuery)

            return ()
        }


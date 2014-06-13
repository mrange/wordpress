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

open Scenario
open UIScenario

module Windows = 

    let WaitForUI = Retry 5 100

    let DoNotSaveChanges : Scenario<unit> = 
        scenario {
            do! WaitForUI (FocusElement <| ByName "Notepad")
            do! SendChar 'n' Modifier.LeftAlt
        }


    let ConfirmSaveAs : Scenario<unit> = 
        scenario {
            do! WaitForUI (FocusElement <| ByName "Confirm Save As")
            do! SendChar 'y' Modifier.LeftAlt
        }

    let NewFile : Scenario<unit> = 
        scenario {
            do! SendChar 'f' Modifier.LeftAlt
            do! SendChar 'n' Modifier.None

            let! _ = Optional DoNotSaveChanges

            return ()
        }

    let OpenFile fileName : Scenario<unit> = 
        let OpenQuery = ByName "Open"
        scenario {
            do! SendChar 'f' Modifier.LeftAlt
            do! SendChar 'o' Modifier.None

            let! _ = Optional DoNotSaveChanges

            do! WaitForUI (SetCurrentElement OpenQuery)

            do! SendChar 'n' Modifier.LeftAlt
            do! SendText fileName

            do! SendChar 'o' Modifier.LeftAlt

            do! WaitForUI (FailIfFound OpenQuery)

            return ()
        }

    let SaveFile fileName : Scenario<unit> = 
        let SaveAsQuery = ByName "Save As"
        scenario {
            do! SendChar 'f' Modifier.LeftAlt
            do! SendChar 'a' Modifier.None

            do! WaitForUI (SetCurrentElement SaveAsQuery)

            do! SendChar 'n' Modifier.LeftAlt
            do! SendText fileName

            do! SendChar 's' Modifier.LeftAlt

            let! _ = Optional ConfirmSaveAs

            do! WaitForUI (FailIfFound SaveAsQuery)

            return ()
        }


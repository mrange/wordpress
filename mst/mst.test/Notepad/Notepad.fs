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

module Notepad = 

    let StartNotepad = UIScenario.StartWindowedProcess "notepad.exe"

    let TextAreaQuery   = ById "15"

    let ReadText: Scenario<string> =
        scenario {
            let! text = UIScenario.GetText TextAreaQuery
            return text
        }

    let TypeText text : Scenario<unit> =
        scenario {
            do! UIScenario.FocusElement TextAreaQuery
            do! UIScenario.SendText text
        }


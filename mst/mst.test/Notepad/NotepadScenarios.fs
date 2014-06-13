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

open Scenario
open Notepad
open Windows

module NotepadScenarios = 

    let SimpleScenario : Scenario<unit> =
        let testdata = "Hello there!"
        scenario {
            do! StartNotepad 
            
            // Write some text to notepad and save the file
            do! TypeText testdata
            do! SaveFile "Hello.txt"

            // Clear notepad and verify the content is empty
            do! NewFile 
            let! text = ReadText 
            let! _ = ExpectEqual "" text

            // Read the file again and validate the content
            do! OpenFile "Hello.txt"
            let! text = ReadText 
            let! _ = ExpectEqual testdata text

            return ()
        }

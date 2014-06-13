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

module LSystem =
    type Alphabet =
        | DoNothing     // X
        | Forward       // F
        | TurnLeft      // -
        | TurnRight     // +
        | Store         // [
        | Restore       // ]

    let Angle = 25.

    let Initial = [DoNothing]

    let Rule (a : Alphabet) = 
        match a with
                     // F       -        [     [     X         ]       +         X         ]       +         F       [     +         F       X         ]       -        X 
        | DoNothing -> [Forward;TurnLeft;Store;Store;DoNothing;Restore;TurnRight;DoNothing;Restore;TurnRight;Forward;Store;TurnRight;Forward;DoNothing;Restore;TurnLeft;DoNothing]
                     // F       F
        | Forward   -> [Forward;Forward]
        | _         -> [a]

    let rec Generate =  function 
                        | 0 ->  Initial         
                        | n ->  let previous = Generate (n - 1)
                                previous |> List.collect Rule




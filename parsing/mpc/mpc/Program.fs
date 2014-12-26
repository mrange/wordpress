
// ----------------------------------------------------------------------------

type ParseFailure =
    | Expected    of string
    | EndOfStream

type ParseResult<'T> =
    | Success of 'T*string*int
    | Failure of (ParseFailure list)*string*int

type Parser<'T> = string*int -> ParseResult<'T>

let Delay f : Parser<'T> = f ()

let Return v : Parser<'T> = 
    fun (str,pos) ->
        Success (v,str,pos)

let ReturnFrom p : Parser<'T> = p

let FailWith parseFailure : Parser<_> = 
    fun (str,pos) ->
        Failure (parseFailure,str,pos)

let Bind (t : Parser<'T>) (fu : 'T -> Parser<'U>) =
    fun (str,pos) ->
        let tr = t (str,pos)
        match tr with 
        | Success (tv,tstr,tpos)   -> 
            let u = fu tv
            u (tstr, tpos)
        | Failure (tfs,tstr,tpos) -> Failure (tfs,tstr,tpos)

let inline (>>=) t fu = Bind t fu

type ParseBuilder() =
    member x.Delay (f)      = Delay f
    member x.Bind (t,fu)    = Bind t fu
    member x.Return (v)     = Return v
    member x.ReturnFrom (p) = ReturnFrom p

let parse = ParseBuilder()

let Atom : Parser<char> =
    fun (str,pos) -> 
        if pos < str.Length then Success (str.[pos],str,pos+1)
        else Failure ([EndOfStream],str,pos)

(*
let TwoAtom : Parser<char*char> = 
    Atom >>= fun first -> 
        Atom >>= fun second -> 
            Return (first,second)

let TwoAtom : Parser<char*char> = 
    parse {
        let! first  = Atom
        let! second = Atom
        return first,second
    }

let Opt (p : Parser<'T>) : Parser<'T option> = 
    fun (str,pos) ->
        let pr = p (str,pos)
        match pr with
        | Success (pv,str,pos)  -> Success (Some pv,str,pos)
        | Failure (fs,str,pos)  -> Success (None,str,pos)

let Many (p : Parser<'T>) : Parser<'T list> = 
    let rec m op = 
        parse {
            let! r = op
            match r with
            | Some v -> 
                let! vs = m op
                return v::vs
            | _ -> return []
        }
    m (Opt p)
*)

let Satisfy (expected : string) (test : char->bool) : Parser<char> = 
    let failure = FailWith ([Expected expected])
    parse {
        let! ch = Atom
        if test ch then 
            return ch
        else 
            return! failure
    }

let AnyOf (anyOf : string) : Parser<char> = 
    let cs = anyOf.ToCharArray()
    let failure = FailWith (cs |> Array.map (fun ch -> Expected (ch.ToString())) |> List.ofArray)
    let set = cs |> Set.ofArray
    parse {
        let! ch = Atom
        if set.Contains ch then 
            return ch
        else 
            return! failure
    }

let Map (p : Parser<'T>) (map : 'T -> 'U) : Parser<'U> = 
    parse {
        let! pr = p
        let result = map pr
        return result
    }

let inline (>>?) l r = Map l r

let rec Many (p : Parser<'T>) : Parser<'T list> = 
    fun (str,pos) -> 
        let pr = p (str,pos)
        match pr with 
        | Success (pv,pstr, ppos) -> 
            let restr = Many p (pstr, ppos)
            match restr with
            | Success (restv,reststr,restpos) -> Success (pv::restv,reststr,restpos)
            | _ -> failwith "Many should always succeed"
        | _ -> Success ([],str,pos)

let Many1 (p : Parser<'T>) : Parser<'T list> = 
    parse {
        let! first  = p
        let! rest   = Many p
        return first::rest

    }

let OrElse (left : Parser<'T>) (right : Parser<'T>) : Parser<'T> =
    fun (str,pos) ->
        let leftr = left (str,pos)
        match leftr with
        | Success _ -> leftr
        | Failure (leftfs,_,_) -> 
            let rightr = right (str,pos)
            match rightr with
            | Success _ -> rightr
            | Failure (rightfs,_,_) -> Failure (leftfs@rightfs,str,pos)

let inline (<|>) l r = OrElse l r

let SepBy (term : Parser<'T>) (separator : Parser<'S>) (combine : 'T -> 'S -> 'T -> 'T): Parser<'T> =
    let rec sb acc (str,pos) =
        let sepr = separator (str, pos)
        match sepr with
        | Success (sepv,sepstr,seppos) -> 
            let nextr = term (sepstr,seppos)
            match nextr with
            | Success (nextv,nextstr,nextpos) ->
                let newacc = combine acc sepv nextv
                sb newacc (nextstr,nextpos)
            | _ -> nextr
        | _ -> Success (acc,str,pos)
    parse {
        let! first = term
        return! sb first
    }

// ----------------------------------------------------------------------------

type BinaryOperation =
    | Add
    | Subtract
    | Multiply
    | Divide

type AbstractSyntaxTree = 
    | Integer           of int
    | Identifier        of string
    | BinaryOperation   of BinaryOperation*AbstractSyntaxTree*AbstractSyntaxTree

let Digit : Parser<char> = Satisfy "digit" System.Char.IsDigit

let Integer : Parser<AbstractSyntaxTree> = 
    let pdigits = Many1 Digit
    parse {
        let! digits = pdigits
        let result = 
            digits
            |> List.map (fun ch -> int ch - int '0')
            |> List.fold (fun s v -> 10*s + v) 0
        return Integer result
    }

let Identifier : Parser<AbstractSyntaxTree> = 
    let pfirst  = Satisfy "identifier" System.Char.IsLetter
    let prest   = Many (Satisfy "identifier" System.Char.IsLetterOrDigit)
    parse {
        let! first  = pfirst
        let! rest   = prest
        let chars   = first::rest |> List.toArray
        let result  = System.String(chars)
        return Identifier result
    }

let CharToBinaryOperator ch = 
    match ch with
    | '+'   -> Add   
    | '-'   -> Subtract
    | '*'   -> Multiply
    | '/'   -> Divide
    | _     -> failwith "Unexpected operator: %A" ch

let AddOrSubtract : Parser<BinaryOperation> = 
    AnyOf "+-" >>? CharToBinaryOperator

let MultiplyOrDivide : Parser<BinaryOperation> = 
    AnyOf "*/" >>? CharToBinaryOperator

let CombineTerms l op r = BinaryOperation (op,l,r)

let Term : Parser<AbstractSyntaxTree> = Identifier <|> Integer

let MultiplyOrDivideExpr : Parser<AbstractSyntaxTree> = SepBy Term MultiplyOrDivide CombineTerms

let AddOrSubtractExpr : Parser<AbstractSyntaxTree> = SepBy MultiplyOrDivideExpr AddOrSubtract CombineTerms

let Expr = AddOrSubtractExpr

let Run (parser : Parser<'T>) (str : string) : ParseResult<'T> = 
    parser (str,0) 

// ----------------------------------------------------------------------------

[<EntryPoint>]
let main argv = 
    let tests = 
        [|
            "abc"
            "123"
            "123+456"
            "x?y+3"
            "x+y*3"
        |]
    for test in tests do 
        printfn "Parsing: '%s'\nResult: %A" test (Run Expr test)

    0
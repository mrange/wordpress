
// ----------------------------------------------------------------------------

type ParseFailureTree =
    | Empty   
    | Expected      of string
    | NotExpected   of string
    | Group         of ParseFailureTree list
    | Fork          of ParseFailureTree*ParseFailureTree

let inline Join (left : ParseFailureTree) (right : ParseFailureTree) = 
    match left, right with
    | _, Empty      -> left
    | Empty, _      -> right
    | _             -> Fork (left,right)

type ParseResult<'T> = ('T option)*ParseFailureTree*string*int

let inline Result v f str pos   : ParseResult<'T>   = (v,f,str,pos)
let inline Success v f str pos  : ParseResult<'T>   = Result (Some v) f str pos
let inline Failure f str pos    : ParseResult<_>    = Result None f str pos


type Parser<'T> = string*int -> ParseResult<'T>

let Delay f : Parser<'T> = f ()

let Return v : Parser<'T> = 
    fun (str,pos) -> 
        Success v Empty str pos

let ReturnFrom p : Parser<'T> = p

let FailWith f : Parser<_> = 
    fun (str,pos) -> 
        Failure f str pos

let Bind (t : Parser<'T>) (fu : 'T -> Parser<'U>) : Parser<'U> =
    fun (str,pos) ->
        let otv,tf,tstr,tpos = t (str,pos)
        match otv with 
        | Some tv   -> 
            let u = fu tv
            let ouv,uf,ustr,upos = u (tstr, tpos)
            Result ouv (Join tf uf) ustr upos
        | _ -> Failure tf tstr tpos

let inline (>>=) t fu = Bind t fu

type ParseBuilder() =
    member x.Delay (f)      = Delay f
    member x.Bind (t,fu)    = Bind t fu
    member x.Return (v)     = Return v
    member x.ReturnFrom (p) = ReturnFrom p

let parse = ParseBuilder()

let Atom : Parser<char> =
    fun (str,pos) -> 
        if pos < str.Length then Success str.[pos] Empty str (pos+1)
        else Failure (NotExpected "EOS") str pos

let EOS : Parser<unit> = 
    fun (str,pos) -> 
        if pos >= str.Length then Success () Empty str pos
        else Failure (Expected "EOS") str pos

let Satisfy expected (test : char->bool) : Parser<char> = 
    let failure = FailWith expected
    parse {
        let! ch = Atom
        if test ch then 
            return ch
        else 
            return! failure
    }

let IsChar (test : char) : Parser<unit> = 
    let failure = FailWith (Expected (test.ToString()))
    parse {
        let! ch = Atom
        if test = ch then 
            return ()
        else 
            return! failure
    }

let IsAnyOf (anyOf : string) : Parser<char> = 
    let cs = anyOf.ToCharArray()
    let failure = FailWith (cs |> Array.map (fun ch -> Expected (ch.ToString())) |> List.ofArray |> Group)
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
        let opv,pf,pstr,ppos = p (str,pos)
        match opv with 
        | Some pv -> 
            let orv,rf,rstr,rpos = Many p (pstr, ppos)
            match orv with
            | Some rv  -> Success (pv::rv) (Join pf rf) rstr rpos
            | _ -> failwith "Many should always succeed"
        | _ -> Success [] pf str pos

let Many1 (p : Parser<'T>) : Parser<'T list> = 
    parse {
        let! first  = p
        let! rest   = Many p
        return first::rest
    }

let OrElse (left : Parser<'T>) (right : Parser<'T>) : Parser<'T> =
    fun (str,pos) ->
        let olr,lf,lstr,lpos = left (str,pos)
        match olr with
        | Some lr -> Success lr lf lstr lpos
        | _ -> 
            let orr,rf,rstr,rpos = right (str,pos)
            match orr with
            | Some rr -> Success rr (Join lf rf) rstr rpos
            | _ -> Failure (Join lf rf) str pos

let inline (<|>) l r = OrElse l r

let SepBy (term : Parser<'T>) (separator : Parser<'S>) (combine : 'T -> 'S -> 'T -> 'T): Parser<'T> =
    let rec sb acc (str,pos) =
        let osr,sf,sstr,spos = separator (str, pos)
        match osr with
        | Some sr ->
            let onr,nf,nstr,npos = term (sstr,spos)
            match onr with
            | Some nr ->
                let newacc = combine acc sr nr
                sb newacc (nstr,npos)
            | _ -> Failure (Join sf nf) nstr npos
        | _ -> Success acc sf str pos
    parse {
        let! first = term
        return! sb first
    }

type ParseFailure =
    | IsExpecting       of string
    | IsNotExpecting    of string

let Run (parser : Parser<'T>) (str : string) = 
    let rec collapse acc t = 
        match t with 
        | Empty         -> acc
        | Expected e    -> (IsExpecting e)::acc
        | NotExpected e -> (IsNotExpecting e)::acc
        | Group g       -> g |> List.fold collapse acc
        | Fork (l,r)    ->
            let lacc = collapse acc l
            let racc = collapse lacc r
            racc

    let prettify (fs : ParseFailure list) str pos =
        let sb              = System.Text.StringBuilder(sprintf "Failed at position %d," pos)
        let isExpecting     = "was expecting"       , " or "
        let isNotExpecting  = "was not expecting"   , " nor "

        let groupFunction (f : ParseFailure) = 
            match f with
            | IsExpecting _     -> isExpecting
            | IsNotExpecting _  -> isNotExpecting

        let groups = fs |> Seq.groupBy groupFunction |> Array.ofSeq
        for i in 0..(groups.Length-1) do
            let (description,last),gfs = groups.[i]
            let prepend =
                match i with
                | 0                         -> " "
                | _                         -> ", "
            ignore <| sb.Append prepend
            ignore <| sb.Append description
            let gfs = gfs |> Array.ofSeq
            for i in 0..(gfs.Length-1) do
                let gf = gfs.[i]
                let str = 
                    match gf with
                    | IsExpecting v     -> v
                    | IsNotExpecting v  -> v
                let prepend =
                    match i with
                    | 0                         -> " "
                    | _ when i = gfs.Length-1   -> last
                    | _                         -> ", "
                ignore <| sb.Append prepend
                ignore <| sb.Append str

        ignore <| sb.Append '.'
        sb.ToString ()

    let orv,rf,rstr,rpos = parser (str,0) 
    match orv with
    | Some rv -> Some rv,"Parse successful",[],rstr,rpos
    | _ -> 
        let cfs = collapse [] rf
        let dfs = cfs |> Seq.distinct |> List.ofSeq
        let msg = prettify dfs rstr rpos
        None,msg,dfs,rstr,rpos

// ----------------------------------------------------------------------------

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
*)

type BinaryOperation =
    | Add
    | Subtract
    | Multiply
    | Divide

type AbstractSyntaxTree = 
    | Integer           of int
    | Identifier        of string
    | BinaryOperation   of BinaryOperation*AbstractSyntaxTree*AbstractSyntaxTree

let CharToBinaryOperator ch = 
    match ch with
    | '+'   -> Add   
    | '-'   -> Subtract
    | '*'   -> Multiply
    | '/'   -> Divide
    | _     -> failwith "Unexpected operator: %A" ch

let AddOrSubtract : Parser<BinaryOperation> = 
    IsAnyOf "+-" >>? CharToBinaryOperator

let MultiplyOrDivide : Parser<BinaryOperation> = 
    IsAnyOf "*/" >>? CharToBinaryOperator

let Digit : Parser<char> = Satisfy (Expected "digit") System.Char.IsDigit

let SubExpr : Parser<AbstractSyntaxTree> ref = ref (Return (Integer 0))

let MatchedParenthesis : Parser<AbstractSyntaxTree> = 
    let start   = IsChar '('
    let stop    = IsChar ')'
    parse {
        do! start
        let! result = !SubExpr
        do! stop

        return result
    }

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
    let pfirst  = Satisfy (Expected "letter") System.Char.IsLetter
    let prest   = Many (Satisfy (Group [Expected "letter";Expected "digit"]) System.Char.IsLetterOrDigit)
    parse {
        let! first  = pfirst
        let! rest   = prest
        let chars   = first::rest |> List.toArray
        let result  = System.String(chars)
        return Identifier result
    }

let CombineTerms l op r = BinaryOperation (op,l,r)

let Term : Parser<AbstractSyntaxTree> = Identifier <|> Integer <|> MatchedParenthesis

let MultiplyOrDivideExpr : Parser<AbstractSyntaxTree> = SepBy Term MultiplyOrDivide CombineTerms

let AddOrSubtractExpr : Parser<AbstractSyntaxTree> = SepBy MultiplyOrDivideExpr AddOrSubtract CombineTerms

do
    SubExpr := AddOrSubtractExpr

let FullExpr : Parser<AbstractSyntaxTree> = 
    parse {
        let! expr = AddOrSubtractExpr
        do! EOS
        return expr
    }

let rec Eval (lookup : string -> int) (ast : AbstractSyntaxTree) : int =
    match ast with
    | Integer               v   -> v
    | Identifier            id  -> lookup id
    | BinaryOperation (bop,l,r) ->
        let lv = Eval lookup l
        let rv = Eval lookup r
        match bop with
        | Add       -> lv + rv
        | Subtract  -> lv - rv
        | Multiply  -> lv * rv
        | Divide    -> lv / rv


// ----------------------------------------------------------------------------

let ColorPrint (cc : System.ConsoleColor) (prelude : string) (str : string) =
    let saved = System.Console.ForegroundColor
    System.Console.ForegroundColor <- cc
    try
        System.Console.Write prelude
        System.Console.WriteLine str
    finally
        System.Console.ForegroundColor <- saved

let PrintFailure = ColorPrint System.ConsoleColor.Red     "FAILURE: "
let PrintSuccess = ColorPrint System.ConsoleColor.Green   "SUCCESS: "

[<EntryPoint>]
let main argv = 
    let x   = 3
    let y   = 5
    let z   = 7
    let abc = 11

    let lookup str = 
        match str with
        | "x"   -> x
        | "y"   -> y
        | "z"   -> z
        | "abc" -> abc
        | _     -> System.Int32.MaxValue

    let tests = 
        [|
            "123+"          , None
            "abc"           , Some <| abc
            "123"           , Some <| 123
            "123+456"       , Some <| 123+456
            "x?y+3"         , None
            "x+y*3"         , Some <| x+y*3
            "x*y+3*(z+x)"   , Some <| x*y+3*(z+x)
        |]
    for test, expected in tests do 
        let result, message, failures, _, _ = Run FullExpr test
        match result, expected with
        | None, None -> 
            PrintSuccess <| sprintf "Parsing failed as expected for '%s'\nMessage:%s" test message
        | Some ast, Some i -> 
            let actual = Eval lookup ast
            if actual = i then
                PrintSuccess <| sprintf "Parsing and evaluation successful for '%s'\nExpected:%i\nActual:%i\nAST:%A" test i actual ast
            else
                PrintFailure <| sprintf "Parsing successful but evaluation failed for '%s'\nExpected:%i\nActual:%i\nAST:%A" test i actual ast
        | None, Some i ->
            PrintSuccess <| sprintf "Parsing failed for '%s'\nExpected:%i\nMessage:%s" test i message
        | Some ast, None ->
            PrintFailure <| sprintf "Parsing successful but expected to fail for '%s'\nAST:%A" test ast
    0
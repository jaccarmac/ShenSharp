﻿namespace Kl

open System
open System.IO
open System.Collections.Generic
open System.Text

type ConsoleReader() =
    let mutable line: byte[] = [||]
    let mutable index = 0
    member this.ReadByte() =
        if index >= line.Length then
            line <- Encoding.ASCII.GetBytes(Console.In.ReadLine() + Environment.NewLine)
            index <- 0
        if line.Length = 0 then
            -1
        else
            index <- index + 1
            int (line.[index - 1])

type ConsoleWriter() =
    let stream = Console.OpenStandardOutput()
    member this.WriteByte = stream.WriteByte

module Extensions =
    type Dictionary<'a, 'b> with
        member this.GetMaybe(key: 'a) =
            match this.TryGetValue(key) with
            | true, x -> Some x
            | false, _ -> None

    let (|Greater|Equal|Lesser|) (x, y) =
        if x > y
            then Greater
        elif x < y
            then Lesser
        else Equal

module Values =
    let truev = Bool true
    let falsev = Bool false

    let err s = raise(SimpleError s)

    let thunkw f = Pending(new Thunk(f))

    /// <summary>
    /// Runs Work repeated until a final Value is returned.
    /// </summary>
    let rec go work =
        match work with
        | Pending thunk -> go(thunk.Run())
        | Done result -> result

    let newGlobals() = {Symbols = new Defines<Value>(); Functions = new Defines<Function>()}
    let newEnv globals locals = {Globals = globals; Locals = locals}
    let emptyEnv() = newEnv (newGlobals()) Map.empty

    let rec toCons list =
        match list with
        | [] -> Empty
        | x :: xs -> Cons(x, toCons xs)

    let rec butLast xs =
        match xs with
        | [] -> failwith "butLast: empty list"
        | [_] -> []
        | h :: t -> h :: butLast t

module ExpressionPatterns =
    let private sequenceOption xs =
        let combine x lst =
            match x with
            | None -> None
            | Some v ->
                match lst with
                | None -> None
                | Some vs -> Some(v :: vs)
        List.foldBack combine xs (Some [])

    let rec private toListOption cons =
        match cons with
        | Empty -> Some []
        | Cons(x, y) -> Option.map (fun xs -> List.Cons(x, xs)) (toListOption y)
        | _ -> None

    let private (|Expr|_|) = toListOption

    let (|Last|_|) = List.tryLast

    let (|AndExpr|_|) = function
        | Expr [Sym "and"; left; right] -> Some(left, right)
        | _ -> None

    let (|OrExpr|_|) = function
        | Expr [Sym "or"; left; right] -> Some(left, right)
        | _ -> None

    let (|IfExpr|_|) = function
        | Expr [Sym "if"; condition; consequent; alternative] -> Some(condition, consequent, alternative)
        | _ -> None

    let private condClause = function
        | Expr [x; y] -> Some(x, y)
        | _ -> None

    let private (|CondClauses|_|) = List.map condClause >> sequenceOption

    let (|CondExpr|_|) = function
        | Expr(Sym "cond" :: CondClauses clauses) -> Some clauses
        | _ -> None

    let (|LetExpr|_|) = function
        | Expr [Sym "let"; Sym symbol; binding; body] -> Some(symbol, binding, body)
        | _ -> None

    let (|LambdaExpr|_|) = function
        | Expr [Sym "lambda"; Sym symbol; body] -> Some(symbol, body)
        | _ -> None

    let (|FreezeExpr|_|) = function
        | Expr [Sym "freeze"; body] -> Some body
        | _ -> None

    let (|TrapExpr|_|) = function
        | Expr [Sym "trap-error"; body; handler] -> Some(body, handler)
        | _ -> None

    let private param = function
        | Sym s -> Some s
        | _ -> None

    let private (|ParamList|_|) = toListOption >> Option.bind (List.map param >> sequenceOption)

    let (|DefunExpr|_|) = function
        | Expr [Sym "defun"; Sym name; ParamList paramz; body] -> Some(name, paramz, body)
        | _ -> None

    let (|DoExpr|_|) = function
        | Expr(Sym "do" :: exprs) -> Some exprs
        | _ -> None

    let (|AppExpr|_|) = function
        | Expr(f :: args) -> Some(f, args)
        | _ -> None

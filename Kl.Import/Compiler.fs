﻿namespace Kl.Import

open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range
open Kl
open Kl.Values
open Kl.Expressions
open Kl.Import.Syntax

module Compiler =

    let private parens fn = function
        | SynExpr.Paren _ as e -> e
        | e -> parenExpr fn e

    type private ExprType =
        | Bottom
        | KlValue
        | FsBoolean
        | FsUnit

    // needs application context to know for which function there
    // is an argument type error
    let private convert targetType (fsExpr, currentType) =
        let fn = "file" // TODO: pass filename in
        match currentType, targetType with
        | x, y when x = y -> fsExpr
        | Bottom, _ -> fsExpr
        | FsBoolean, KlValue -> parens fn (appExpr fn (idExpr fn "Bool") fsExpr)
        | FsUnit, KlValue -> idExpr fn "Empty"
        | KlValue, FsBoolean -> parens fn (appExpr fn (idExpr fn "isTrue") fsExpr)
        | _, FsUnit -> infixExpr fn (idExpr fn "|>") fsExpr (idExpr fn "ignore")
        | _, _ -> failwithf "can't convert %O to %O" currentType targetType

    let private (|>>) fsExprWithType targetType = convert targetType fsExprWithType

    let private rename s = "kl_" + s

    let rec private flattenDo = function
        | DoExpr(first, second) -> flattenDo first @ flattenDo second
        | klExpr -> [klExpr]

    // TODO: make sure error messages and conditions are
    //       consistent with Evaluator
    //       needs application context for this
    let rec private build ((fn, globals, locals) as context) = function
        | Empty -> idExpr fn "Empty", KlValue
        | Num x -> appExpr fn (idExpr fn "Num") (decimalExpr fn x), KlValue
        | Str s -> appExpr fn (idExpr fn "Str") (stringExpr fn s), KlValue
        | Sym "true" -> boolExpr fn true, FsBoolean
        | Sym "false" -> boolExpr fn false, FsBoolean
        | Sym s ->
            if Set.contains s locals
                then idExpr fn (rename s), KlValue
                else appExpr fn (idExpr fn "Sym") (stringExpr fn s), KlValue
        | AndExpr(left, right) ->
            infixExpr fn
                (idExpr fn "op_BooleanAnd")
                (parens fn (build context left  |>> FsBoolean))
                (parens fn (build context right |>> FsBoolean)), FsBoolean
        | OrExpr(left, right) ->
            infixExpr fn
                 (idExpr fn "op_BooleanOr")
                 (parens fn (build context left  |>> FsBoolean))
                 (parens fn (build context right |>> FsBoolean)), FsBoolean
        | IfExpr(condition, consequent, alternative) ->
            ifExpr fn
                (build context condition   |>> FsBoolean)
                (build context consequent  |>> KlValue)
                (build context alternative |>> KlValue), KlValue
        | CondExpr clauses ->
            let rec compileClauses = function
                | [] -> idExpr fn "Empty"
                | (Sym "true", consequent) :: _ ->
                    build context consequent |>> KlValue
                | (condition, consequent) :: rest ->
                    ifExpr fn
                        (build context condition  |>> FsBoolean)
                        (build context consequent |>> KlValue)
                        (compileClauses rest)
            compileClauses clauses, KlValue
        | LetExpr(param, binding, body) ->
            // TODO: might be able to put let on its own line instead of part of expr, depends on context
            // TODO: need to optimize types
            letExpr fn
                (namePat fn param)
                (build context binding |>> KlValue)
                (build (fn, globals, Set.add param locals) body |>> KlValue), KlValue
        | DoExpr _ as doExpr -> failwith "can't compile"
            //dos (List.map (build context) (flattenDo doExpr))
        | LambdaExpr(param, body) ->
            let param = rename param
            parens fn
                (appExpr fn
                    (idExpr fn "Func")
                    (appExpr fn
                        (idExpr fn "Lambda")
                        (appExpr fn
                            (idExpr fn "CompiledLambda")
                            (lambdaExpr fn
                                ["globals", shortType fn "Globals"; param, shortType fn "Value"]
                                (build (fn, globals, Set.union (Set.ofList ["globals"; param]) locals) body |>> KlValue))))), KlValue
        | FreezeExpr body ->
            parens fn
                (appExpr fn
                    (idExpr fn "Func")
                    (appExpr fn
                        (idExpr fn "Lambda")
                        (appExpr fn
                            (idExpr fn "CompiledLambda")
                            (lambdaExpr fn
                                ["globals", shortType fn "Globals"]
                                (build (fn, globals, Set.add "globals" locals) body |>> KlValue))))), KlValue
        | TrapExpr(body, LambdaExpr(param, handler)) ->
            tryWithExpr fn
                (build context body |>> KlValue)
                param
                (build (fn, globals, Set.add param locals) handler |>> KlValue), KlValue
        | TrapExpr(body, Sym handler) ->
            tryWithExpr fn
                (build context body |>> KlValue)
                "e"
                (appExpr fn
                    (appExpr fn
                        (idExpr fn (rename handler))
                        (idExpr fn "globals"))
                    (listExpr fn [idExpr fn "e"])), KlValue // TODO: need to get Message from exception
        | TrapExpr(body, handler) -> failwith "can't compile" // try...with, need to join branch types
            //tryWith (build body)  (build (globals, Set.add "e" locals) handler)
        | DefunExpr _ -> failwith "can't compile defun in expr position" // or can we?

        | AppExpr(Sym s, args) ->
            appExpr fn
                (appExpr fn
                    (idExpr fn (rename s)) (idExpr fn "globals"))
                    (listExpr fn (List.map (build context >> convert KlValue) args)), KlValue

        // TODO: if it's some other expression, we need an apply function
        | AppExpr(f, args) -> failwith "can't compile"

        | _ -> failwith "Unable to compile"

    let buildExpr context klExpr = build context klExpr |> fst

    let compileDefun fn globals name paramz body =
        letDecl fn
            name
            paramz
            (build (fn, globals, Set.empty) body |> fst)

    let compile fn globals =
        parsedFile fn [
            openDecl fn ["Kl"]
            openDecl fn ["Kl"; "Values"]
            openDecl fn ["Kl"; "Builtins"]
            letDecl fn
               "hi"
               ["kl_X"]
               (build (fn, globals, Set.singleton "X")
                   (OrExpr(
                       AndExpr(Sym "X", Sym "X"),
                       AndExpr(Sym "X", Sym "X"))) |>> KlValue)]

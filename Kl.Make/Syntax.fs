﻿namespace Kl.Make

open System
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range

/// <summary>
/// A collection of helper functions to simplify
/// syntax for building F# ASTs.
/// </summary>
/// <remarks>
/// Trying not to add anything specific to ShenSharp here so
/// there is a clear separation between AST helpers and
/// making decisions about transpiling KL to F#.
/// </remarks>
module internal Syntax =

    // Picked large values for line, col because there will be an unpredictable
    // ArrayIndexOutOfBoundsException if the numbers are too small
    let private loc fn = mkRange fn (mkPos 512 512) (mkPos 1024 1024)
    let attr fn name value : SynAttribute =  {
        TypeName = name
        ArgExpr = value
        Target = None
        AppliesToGetterAndSetter = false
        Range = loc fn
    }
    let ident fn s = new Ident(s, loc fn)
    let longIdent fn parts = List.map (ident fn) parts
    let longIdentWithDots fn parts =
        LongIdentWithDots.LongIdentWithDots(
            List.map (ident fn) parts,
            List.replicate (List.length parts - 1) (loc fn))
    let argInfo fn s = SynArgInfo.SynArgInfo([], false, Some(ident fn s))
    let nullArgInfo() = SynArgInfo.SynArgInfo([], false, None)
    let anonType fn = SynType.Anon(loc fn)
    let longType fn parts = SynType.LongIdent(longIdentWithDots fn parts)
    let shortType fn s = longType fn [s]
    let listType fn t = SynType.App(shortType fn "list", None, [t], [], None, true, loc fn)
    let wildPat fn = SynPat.Wild(loc fn)
    let namePat fn s = SynPat.Named(wildPat fn, ident fn s, false, None, loc fn)
    let unparenTypedPat fn pat synType = SynPat.Typed(pat, synType, loc fn)
    let typedPat fn pat synType = SynPat.Paren(unparenTypedPat fn pat synType, loc fn)
    let listPat fn pats = SynPat.ArrayOrList(false, pats, loc fn)
    let tuplePat fn pats = SynPat.Paren(SynPat.Tuple(pats, loc fn), loc fn)
    let appPat fn parts args =
        SynPat.LongIdent(
            longIdentWithDots fn parts,
            None,
            None,
            SynConstructorArgs.Pats(args),
            None,
            loc fn)
    let unitPat fn = SynPat.Paren(SynPat.Const(SynConst.Unit, loc fn), loc fn)
    let matchClause fn pat body =
        SynMatchClause.Clause(
            pat,
            None,
            body,
            loc fn,
            SequencePointInfoForTarget.SequencePointAtTarget)
    let nameTypeSimplePat fn s synType =
        SynSimplePat.Typed(
            SynSimplePat.Id(ident fn s, None, true, false, false, loc fn),
            synType,
            loc fn)
    let simpleBinding fn pat value =
        SynBinding.Binding(
            None,
            SynBindingKind.NormalBinding,
            false,
            false,
            [],
            PreXmlDoc.Empty,
            SynValData.SynValData(
                None,
                SynValInfo.SynValInfo([], nullArgInfo()),
                None),
            pat,
            None,
            value,
            loc fn,
            SequencePointInfoForBinding.NoSequencePointAtLetBinding)
    let letAttrsMultiParamBinding fn attrs name paramz body =
        SynBinding.Binding(
            None,
            SynBindingKind.NormalBinding,
            false,
            false,
            attrs,
            PreXmlDoc.Empty,
            SynValData.SynValData(
                None,
                SynValInfo.SynValInfo([List.map (fst >> argInfo fn) paramz], nullArgInfo()),
                None),
            SynPat.LongIdent(
                longIdentWithDots fn [name],
                None,
                None,
                SynConstructorArgs.Pats(
                    [tuplePat fn
                        (List.map
                            (fun (s, synType) -> unparenTypedPat fn (namePat fn s) synType)
                            paramz)]),
                None,
                loc fn),
            None,
            body,
            loc fn,
            SequencePointInfoForBinding.SequencePointAtBinding(loc fn))
    let letBindingAccessWithAttrs fn attrs access name paramz body =
        SynBinding.Binding(
            access,
            SynBindingKind.NormalBinding,
            false,
            false,
            attrs,
            PreXmlDoc.Empty,
            SynValData.SynValData(
                None,
                SynValInfo.SynValInfo(List.map (fun (s, _) -> [argInfo fn s]) paramz, nullArgInfo()),
                None),
            SynPat.LongIdent(
                longIdentWithDots fn [name],
                None,
                None,
                SynConstructorArgs.Pats(
                    List.map (fun (s, synType) -> typedPat fn (namePat fn s) synType) paramz),
                None,
                loc fn),
            None,
            body,
            loc fn,
            SequencePointInfoForBinding.SequencePointAtBinding(loc fn))
    let letAttrsBinding fn attrs = letBindingAccessWithAttrs fn attrs None
    let letBinding fn = letAttrsBinding fn []
    let letBindingPrivate fn = letBindingAccessWithAttrs fn [] (Some SynAccess.Private)
    let letUnitBinding fn attrs name body =
        SynBinding.Binding(
            None,
            SynBindingKind.NormalBinding,
            false,
            false,
            attrs,
            PreXmlDoc.Empty,
            SynValData.SynValData(
                None,
                SynValInfo.SynValInfo([[]], nullArgInfo()),
                None),
            SynPat.LongIdent(
                longIdentWithDots fn [name],
                None,
                None,
                SynConstructorArgs.Pats([unitPat fn]),
                None,
                loc fn),
            None,
            body,
            loc fn,
            SequencePointInfoForBinding.SequencePointAtBinding(loc fn))
    let parenExpr fn expr = SynExpr.Paren(expr, loc fn, None, loc fn)
    let parens fn = function
        | SynExpr.Paren _ as e -> e
        | e -> parenExpr fn e
    let unitExpr fn = SynExpr.Const(SynConst.Unit, loc fn)
    let boolExpr fn b = SynExpr.Const(SynConst.Bool b, loc fn)
    let intExpr fn n = SynExpr.Const(SynConst.Int32 n, loc fn)
    let decimalExpr fn n = SynExpr.Const(SynConst.Decimal n, loc fn)
    let stringExpr fn s = SynExpr.Const(SynConst.String(s, loc fn), loc fn)
    let idExpr fn s = SynExpr.Ident(ident fn s)
    let longIdExpr fn parts = SynExpr.LongIdent(false, longIdentWithDots fn parts, None, loc fn)
    let indexSetExpr fn obj index value =
        SynExpr.DotIndexedSet(
            obj,
            [SynIndexerArg.One index],
            value,
            loc fn,
            loc fn,
            loc fn)
    let appExpr fn f arg = SynExpr.App(ExprAtomicFlag.NonAtomic, false, f, arg, loc fn)
    let rec appExprN fn f = function
        | [] -> appExpr fn f (unitExpr fn)
        | [arg] -> appExpr fn f arg
        | arg :: args -> appExprN fn (appExpr fn f arg) args
    let infixExpr fn op left right =
        SynExpr.App(
            ExprAtomicFlag.NonAtomic,
            false,
            SynExpr.App(ExprAtomicFlag.NonAtomic, true, op, left, loc fn),
            right,
            loc fn)
    let appIdExpr fn s arg = parens fn (appExpr fn (idExpr fn s) arg)
    let appIdExprN fn s args = appExprN fn (idExpr fn s) args
    let infixIdExpr fn s left right = parens fn (infixExpr fn (idExpr fn s) left right)
    let rec nestedAppIdExpr fn ss args =
        match ss with
        | [] -> failwith "nested call can't be empty"
        | [s] -> parens fn (appIdExpr fn s args)
        | s :: ss -> parens fn (appIdExpr fn s (nestedAppIdExpr fn ss args))
    let ifExpr fn condition consequent alternative =
        parens fn
            (SynExpr.IfThenElse(
                condition,
                consequent,
                Some alternative,
                SequencePointInfoForBinding.NoSequencePointAtInvisibleBinding,
                false,
                loc fn,
                loc fn))
    let letExpr fn symbol value body =
        SynExpr.LetOrUse(
            false,
            false,
            [simpleBinding fn (namePat fn symbol) value],
            body,
            loc fn)
    let tryWithExpr fn body e handler =
        SynExpr.TryWith(
            body,
            loc fn,
            [SynMatchClause.Clause(
                namePat fn e,
                None,
                handler,
                loc fn,
                SequencePointInfoForTarget.SequencePointAtTarget)],
            loc fn,
            loc fn,
            SequencePointInfoForTry.SequencePointAtTry(loc fn),
            SequencePointInfoForWith.SequencePointAtWith(loc fn))
    let rec sequentialExpr fn = function
        | [] -> failwith "sequential cannot be empty"
        | [expr] -> expr
        | expr :: rest ->
            SynExpr.Sequential(
                SequencePointInfoForSeq.SequencePointsAtSeq,
                true,
                expr,
                sequentialExpr fn rest,
                loc fn)
    let doExpr fn expr = SynExpr.Do(expr, loc fn)
    let tupleExpr fn vals =
        parens fn
            (SynExpr.Tuple(
                vals,
                List.replicate (List.length vals - 1) (loc fn),
                loc fn))
    let listExpr fn vals = SynExpr.ArrayOrList(false, vals, loc fn)
    let arrayExpr fn vals = SynExpr.ArrayOrList(true, vals, loc fn)
    let rec lambdaExpr fn paramz body =
        parens fn
            (match paramz with
             | [] ->
                SynExpr.Lambda(
                    false,
                    false,
                    SynSimplePats.SimplePats([], loc fn),
                    body,
                    loc fn)
             | [s, synType] ->
                SynExpr.Lambda(
                    false,
                    false,
                    SynSimplePats.SimplePats([nameTypeSimplePat fn s synType], loc fn),
                    body,
                    loc fn)
             | (s, synType) :: paramz ->
                SynExpr.Lambda(
                    false,
                    false,
                    SynSimplePats.SimplePats([nameTypeSimplePat fn s synType], loc fn),
                    lambdaExpr fn paramz body,
                    loc fn))
    let matchLambdaExpr fn clauses =
        SynExpr.MatchLambda(
            false,
            loc fn,
            clauses,
            SequencePointInfoForBinding.SequencePointAtBinding(loc fn),
            loc fn)
    let matchExpr fn key clauses =
        SynExpr.Match(
            SequencePointInfoForBinding.SequencePointAtBinding(loc fn),
            key,
            clauses,
            false,
            loc fn)
    let openDecl fn parts = SynModuleDecl.Open(longIdentWithDots fn parts, loc fn)
    let letAttrsDecl fn attrs name paramz body =
        SynModuleDecl.Let(
            false,
            [letAttrsBinding fn attrs name paramz body],
            loc fn)
    let letAttrsMultiParamDecl fn attrs name paramz body =
        SynModuleDecl.Let(
            false,
            [letAttrsMultiParamBinding fn attrs name paramz body],
            loc fn)
    let letDecl fn name paramz body =
        SynModuleDecl.Let(
            false,
            [letBinding fn name paramz body],
            loc fn)
    let letUnitAttrsDecl fn attrs name body =
        SynModuleDecl.Let(
            false,
            [letUnitBinding fn attrs name body],
            loc fn)
    let letMultiDecl fn bindings = SynModuleDecl.Let(true, bindings, loc fn)
    let extnAttr fn =
        attr fn
            (longIdentWithDots fn [
                "System"
                "Runtime"
                "CompilerServices"
                "Extension"])
            (unitExpr fn)
    let sjoin sep (parts: string list) = String.Join(sep, parts)
    let moduleFile fn nameParts attrs decls =
        ParsedInput.ImplFile(
            ParsedImplFileInput.ParsedImplFileInput(
                fn + ".fs",
                false,
                QualifiedNameOfFile.QualifiedNameOfFile(ident fn (sjoin "." nameParts)),
                [],
                [],
                [SynModuleOrNamespace.SynModuleOrNamespace(
                    (List.map (ident fn) nameParts),
                    false,
                    true,
                    decls,
                    PreXmlDoc.Empty,
                    attrs,
                    None,
                    loc fn)],
                (false, false)))
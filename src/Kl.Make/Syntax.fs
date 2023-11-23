﻿/// <summary>
/// A collection of helper functions to simplify
/// syntax for building F# ASTs.
/// </summary>
/// <remarks>
/// Trying not to add anything specific to ShenSharp here so
/// there is a clear separation between AST helpers and
/// making decisions about transpiling KL to F#.
/// </remarks>
module internal Kl.Make.Syntax

open System
open FSharp.Compiler.SyntaxTree
open FSharp.Compiler.Range
open FSharp.Compiler.XmlDoc

let private fileName = "file.fs"
// Picked large values for line, col because there will be an unpredictable
// ArrayIndexOutOfBoundsException if the numbers are too small
let private loc = mkRange fileName (mkPos 512 512) (mkPos 1024 1024)
let attr target name value : SynAttribute = {
    TypeName = name
    ArgExpr = value
    Target = target
    AppliesToGetterAndSetter = false
    Range = loc
}
let attrs xs : SynAttributeList list = [{
    Attributes = xs
    Range = loc
}]
let ident s = new Ident(s, loc)
let longIdent parts = List.map ident parts
let longIdentWithDots parts =
    LongIdentWithDots.LongIdentWithDots(
        List.map ident parts,
        List.replicate (List.length parts - 1) loc)
let argInfo s = SynArgInfo.SynArgInfo([], false, Some(ident s))
let nullArgInfo = SynArgInfo.SynArgInfo([], false, None)
let anonType = SynType.Anon loc
let longType parts = SynType.LongIdent(longIdentWithDots parts)
let shortType s = longType [s]
let listType t = SynType.App(shortType "list", None, [t], [], None, true, loc)
let wildPat = SynPat.Wild loc
let namePat s = SynPat.Named(wildPat, ident s, false, None, loc)
let unparenTypedPat pat synType = SynPat.Typed(pat, synType, loc)
let typedPat pat synType = SynPat.Paren(unparenTypedPat pat synType, loc)
let listPat pats = SynPat.ArrayOrList(false, pats, loc)
let tuplePat pats = SynPat.Paren(SynPat.Tuple(false, pats, loc), loc)
let unitPat = SynPat.Paren(SynPat.Const(SynConst.Unit, loc), loc)
let matchClause pat body =
    SynMatchClause.Clause(
        pat,
        None,
        body,
        loc,
        DebugPointForTarget.Yes)
let nameTypeSimplePat s synType =
    SynSimplePat.Typed(
        SynSimplePat.Id(ident s, None, true, false, false, loc),
        synType,
        loc)
let simpleBinding pat value =
    SynBinding.Binding(
        None,
        SynBindingKind.NormalBinding,
        false,
        false,
        [],
        PreXmlDoc.Empty,
        SynValData.SynValData(
            None,
            SynValInfo.SynValInfo([], nullArgInfo),
            None),
        pat,
        None,
        value,
        loc,
        DebugPointForBinding
.NoDebugPointAtLetBinding)
let letAttrsMultiParamBinding attrs name paramz body =
    SynBinding.Binding(
        None,
        SynBindingKind.NormalBinding,
        false,
        false,
        attrs,
        PreXmlDoc.Empty,
        SynValData.SynValData(
            None,
            SynValInfo.SynValInfo([List.map (fst >> argInfo) paramz], nullArgInfo),
            None),
        SynPat.LongIdent(
            longIdentWithDots [name],
            None,
            None,
            SynArgPats.Pats(
                [tuplePat
                    (List.map
                        (fun (s, synType) -> unparenTypedPat (namePat s) synType)
                        paramz)]),
            None,
            loc),
        None,
        body,
        loc,
        DebugPointForBinding
.DebugPointAtBinding loc)
let letBindingAccessWithAttrs attrs access name paramz body =
    SynBinding.Binding(
        access,
        SynBindingKind.NormalBinding,
        false,
        false,
        attrs,
        PreXmlDoc.Empty,
        SynValData.SynValData(
            None,
            SynValInfo.SynValInfo(List.map (fun (s, _) -> [argInfo s]) paramz, nullArgInfo),
            None),
        SynPat.LongIdent(
            longIdentWithDots [name],
            None,
            None,
            SynArgPats.Pats(
                List.map (fun (s, synType) -> typedPat (namePat s) synType) paramz),
            None,
            loc),
        None,
        body,
        loc,
        DebugPointForBinding
.DebugPointAtBinding loc)
let letAttrsBinding attrs = letBindingAccessWithAttrs attrs None
let letBinding = letAttrsBinding []
let letUnitBinding attrs name body =
    SynBinding.Binding(
        None,
        SynBindingKind.NormalBinding,
        false,
        false,
        attrs,
        PreXmlDoc.Empty,
        SynValData.SynValData(
            None,
            SynValInfo.SynValInfo([[]], nullArgInfo),
            None),
        SynPat.LongIdent(
            longIdentWithDots [name],
            None,
            None,
            SynArgPats.Pats [unitPat],
            None,
            loc),
        None,
        body,
        loc,
        DebugPointForBinding
.DebugPointAtBinding loc)
let parenExpr expr = SynExpr.Paren(expr, loc, None, loc)
let parens = function
    | SynExpr.Paren _ as e -> e
    | e -> parenExpr e
let unitExpr = SynExpr.Const(SynConst.Unit, loc)
let boolExpr b = SynExpr.Const(SynConst.Bool b, loc)
let intExpr n = SynExpr.Const(SynConst.Int32 n, loc)
let decimalExpr n = SynExpr.Const(SynConst.Decimal n, loc)
let stringExpr s = SynExpr.Const(SynConst.String(s, loc), loc)
let idExpr s = SynExpr.Ident(ident s)
let longIdExpr parts = SynExpr.LongIdent(false, longIdentWithDots parts, None, loc)
let indexSetExpr obj index value =
    SynExpr.DotIndexedSet(
        obj,
        [SynIndexerArg.One index],
        value,
        loc,
        loc,
        loc)
let appExpr f arg = SynExpr.App(ExprAtomicFlag.NonAtomic, false, f, arg, loc)
let rec appExprN f = function
    | [] -> appExpr f unitExpr
    | [arg] -> appExpr f arg
    | arg :: args -> appExprN (appExpr f arg) args
let infixExpr op left right =
    SynExpr.App(
        ExprAtomicFlag.NonAtomic,
        false,
        SynExpr.App(ExprAtomicFlag.NonAtomic, true, op, left, loc),
        right,
        loc)
let appIdExpr s arg = parens (appExpr (idExpr s) arg)
let appIdExprN s args = appExprN (idExpr s) args
let infixIdExpr s left right = parens (infixExpr (idExpr s) left right)
let ifExpr condition consequent alternative =
    let expr =
        SynExpr.IfThenElse(
            condition,
            consequent,
            Some alternative,
            DebugPointForBinding
.NoDebugPointAtInvisibleBinding,
            false,
            loc,
            loc)
    parens expr
let letExpr symbol value body =
    SynExpr.LetOrUse(
        false,
        false,
        [simpleBinding (namePat symbol) value],
        body,
        loc)
let tryWithExpr body e handler =
    SynExpr.TryWith(
        body,
        loc,
        [SynMatchClause.Clause(
            namePat e,
            None,
            handler,
            loc,
            DebugPointForTarget.Yes)],
        loc,
        loc,
        DebugPointAtTry.Yes loc,
        DebugPointAtWith.Yes loc)
let rec sequentialExpr = function
    | [] -> failwith "sequential cannot be empty"
    | [expr] -> expr
    | expr :: rest ->
        SynExpr.Sequential(
            DebugPointAtSequential.Both,
            true,
            expr,
            sequentialExpr rest,
            loc)
let tupleExpr vals =
    parens
        (SynExpr.Tuple(
            false,
            vals,
            List.replicate (List.length vals - 1) loc,
            loc))
let listExpr vals = SynExpr.ArrayOrList(false, vals, loc)
let rec lambdaExpr paramz body =
    let expr =
        match paramz with
        | [] ->
            SynExpr.Lambda(
                false,
                false,
                SynSimplePats.SimplePats([], loc),
                body,
                None,
                loc)
        | [s, synType] ->
            SynExpr.Lambda(
                false,
                false,
                SynSimplePats.SimplePats([nameTypeSimplePat s synType], loc),
                body,
                None,
                loc)
        | (s, synType) :: paramz ->
            SynExpr.Lambda(
                false,
                false,
                SynSimplePats.SimplePats([nameTypeSimplePat s synType], loc),
                lambdaExpr paramz body,
                None,
                loc)
    parens expr
let matchLambdaExpr clauses =
    SynExpr.MatchLambda(
        false,
        loc,
        clauses,
        DebugPointForBinding
.DebugPointAtBinding loc,
        loc)
let openDecl parts = SynModuleDecl.Open(longIdentWithDots parts, loc)
let letAttrsDecl attrs name paramz body =
    SynModuleDecl.Let(false, [letAttrsBinding attrs name paramz body], loc)
let letAttrsUncurriedDecl attrs name paramz body =
    SynModuleDecl.Let(false, [letAttrsMultiParamBinding attrs name paramz body], loc)
let letDecl name paramz body =
    SynModuleDecl.Let(false, [letBinding name paramz body], loc)
let letUnitAttrsDecl attrs name body =
    SynModuleDecl.Let(false, [letUnitBinding attrs name body], loc)
let letMultiDecl bindings = SynModuleDecl.Let(true, bindings, loc)
let assemblyAttrDecl name value = SynModuleDecl.Attributes(attrs [attr (Some(ident "assembly")) name value], loc)
let moduleFile nameParts decls =
    ParsedInput.ImplFile(
        ParsedImplFileInput.ParsedImplFileInput(
            fileName,
            false,
            QualifiedNameOfFile.QualifiedNameOfFile(ident(String.Join(".", (nameParts: string list)))),
            [],
            [],
            [SynModuleOrNamespace.SynModuleOrNamespace(
                List.map ident nameParts,
                false,
                SynModuleOrNamespaceKind.NamedModule,
                decls,
                PreXmlDoc.Empty,
                [],
                None,
                loc)],
            (false, false)))

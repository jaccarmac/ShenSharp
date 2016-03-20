﻿namespace KlCompiler

type FsQuot = Quotations.Expr

open Kl

open Fantomas

open Microsoft.FSharp.Compiler.SimpleSourceCodeServices
open Microsoft.FSharp.Compiler.SourceCodeServices
open Microsoft.FSharp.Compiler.Ast
open Microsoft.FSharp.Compiler.Range

module FsAst =
    let defaultRange = mkFileIndexRange 50 (mkPos 100 100) (mkPos 200 200)

type FsFile =

    static member Of(ns: string, modules: SynModuleOrNamespace list) =
        ParsedInput.ImplFile(
            ParsedImplFileInput(
                "filename",
                false,
                QualifiedNameOfFile(new Ident(ns, FsAst.defaultRange)),
                [],
                [],
                modules,
                false))

type FsConst =

    static member Int32(x: int) =
        SynExpr.Const(SynConst.Int32(x), FsAst.defaultRange)

type FsModule =

    static member Of(name: string, members: SynModuleDecls) =
        SynModuleOrNamespace.SynModuleOrNamespace(
            [new Ident(name, FsAst.defaultRange)],
            true,
            members,
            PreXmlDocEmpty,
            [],
            None,
            FsAst.defaultRange)

    static member Let(bindings: SynBinding list) =
        SynModuleDecl.Let(false, bindings, FsAst.defaultRange)

    static member SingleLet(name: string, args: (string * string) list, body: SynExpr) =
        let eachArg (typ, nm) = [SynArgInfo.SynArgInfo([], false, Some(new Ident(nm, FsAst.defaultRange)))]
        let argInfos = List.map eachArg args
        let valData =
            SynValData.SynValData(
                None,
                SynValInfo.SynValInfo(
                    argInfos,
                    SynArgInfo.SynArgInfo([], false, None)),
                None)
        let eachCtorArg (typ, nm) =
            SynPat.Paren(
                SynPat.Typed(
                    SynPat.Named(
                        SynPat.Wild(FsAst.defaultRange),
                        new Ident(nm, FsAst.defaultRange),
                        false,
                        None,
                        FsAst.defaultRange),
                    SynType.LongIdent(
                        LongIdentWithDots.LongIdentWithDots(
                            [new Ident(typ, FsAst.defaultRange)],
                            [])),
                    FsAst.defaultRange),
                FsAst.defaultRange)
        let ctorArgs = List.map eachCtorArg args
        let namePat =
            SynPat.LongIdent(
                LongIdentWithDots.LongIdentWithDots([new Ident(name, FsAst.defaultRange)], []),
                None,
                None,
                SynConstructorArgs.Pats(ctorArgs),
                None,
                FsAst.defaultRange)
        let returnType = SynType.LongIdent(LongIdentWithDots.LongIdentWithDots([new Ident("KlValue", FsAst.defaultRange)], []))
        let returnInfo = SynBindingReturnInfo.SynBindingReturnInfo(returnType, FsAst.defaultRange, [])
        let binding =
            SynBinding.Binding(
                None,
                SynBindingKind.NormalBinding,
                false,
                false,
                [],
                PreXmlDoc.Empty,
                valData,
                namePat,
                Some returnInfo,
                body,
                FsAst.defaultRange,
                SequencePointInfoForBinding.NoSequencePointAtLetBinding)
        SynModuleDecl.Let(false, [binding], FsAst.defaultRange)

    static member LetRec(bindings: SynBinding list) =
        SynModuleDecl.Let(true, bindings, FsAst.defaultRange)

open Microsoft.FSharp.Quotations
open Microsoft.FSharp.Quotations.DerivedPatterns
open Microsoft.FSharp.Quotations.Patterns

type FsExpr =

    static member Paren(expr: SynExpr) =
        SynExpr.Paren(expr, FsAst.defaultRange, None, FsAst.defaultRange)

    static member Id(id: string) =
        SynExpr.Ident(new Ident(id, FsAst.defaultRange))

    static member Let(bindings: SynBinding list, body: SynExpr) =
        SynExpr.LetOrUse(false, false, bindings, body, FsAst.defaultRange)
        
    static member LetRec(bindings: SynBinding list, body: SynExpr) =
        SynExpr.LetOrUse(true, false, bindings, body, FsAst.defaultRange)

    static member Const(constant: SynConst) =
        SynExpr.Const(constant, FsAst.defaultRange)

    static member List(exprs: SynExpr list) =
        SynExpr.ArrayOrList(false, exprs, FsAst.defaultRange)

    static member App(f0: SynExpr, args0: SynExpr list) =
        let rec buildApply f args =
            match args with
            | single :: [] ->
                SynExpr.App(
                    ExprAtomicFlag.NonAtomic,
                    false,
                    f,
                    single,
                    FsAst.defaultRange)
            | first :: rest ->
                buildApply (SynExpr.App(ExprAtomicFlag.NonAtomic, false, f, first, FsAst.defaultRange)) rest
            | _ -> failwith "must have at least one argument"
        buildApply f0 args0

    static member Infix(lhs: SynExpr, op: SynExpr, rhs: SynExpr) =
        FsExpr.App(op, [lhs; rhs])

type FsIds =
    
    static member Plus = FsExpr.Id("op_Addition")
    static member PipePipe = FsExpr.Id("op_OrElse")
    static member AmpAmp = FsExpr.Id("op_AndAlso")

type FsBinding =

    static member Of(symbol: string, body: SynExpr) =
        SynBinding.Binding(
            Some(SynAccess.Public),
            SynBindingKind.NormalBinding,
            false,
            false,
            [],
            PreXmlDoc.Empty,
            SynValData.SynValData(
                None,
                SynValInfo.SynValInfo([], SynArgInfo.SynArgInfo([], false, None)),
                None),
            SynPat.Named(
                SynPat.Wild FsAst.defaultRange,
                new Ident(symbol, FsAst.defaultRange),
                false,
                None,
                FsAst.defaultRange),
            None,
            body,
            FsAst.defaultRange,
            SequencePointInfoForBinding.NoSequencePointAtLetBinding)

module KlCompiler =
    let sscs = new SimpleSourceCodeServices()
    let checker = FSharpChecker.Create()
    let getUntypedTree (file, input) = 
        // Get compiler options for the 'project' implied by a single script file
        let projOptions = 
            checker.GetProjectOptionsFromScript(file, input)
            |> Async.RunSynchronously

        // Run the first phase (untyped parsing) of the compiler
        let parseFileResults = 
            checker.ParseFileInProject(file, input, projOptions) 
            |> Async.RunSynchronously

        match parseFileResults.ParseTree with
        | Some tree -> tree
        | None -> failwith "Something went wrong during parsing!"
    let idExpr id = SynExpr.Ident(new Ident(id, range.Zero))
    let longIdExpr ids =
        let ident id = new Ident(id, FsAst.defaultRange)
        SynExpr.LongIdent(
            false,
            LongIdentWithDots.LongIdentWithDots(
                List.map ident ids,
                List.replicate ((ids.Length) - 1) FsAst.defaultRange),
            None,
            FsAst.defaultRange)
    let rec build expr =
        let klToFsId (klId:string) =
            klId.Replace("?", "_P_")
                .Replace("<", "_LT_")
                .Replace(">", "_GT_")
                .Replace("-", "_")
                .Replace(".", "_DOT_")
                .Replace("+", "_PLUS_")
                .Replace("*", "_STAR_")
                .TrimEnd('_')
        match expr with
        | EmptyExpr -> idExpr "EmptyValue"
        | BoolExpr b -> SynExpr.Const(SynConst.Bool b, range.Zero)
        | IntExpr i -> SynExpr.Const(SynConst.Int32 i, range.Zero)
        | DecimalExpr d -> SynExpr.Const(SynConst.Decimal d, range.Zero)
        | StringExpr s -> SynExpr.Const(SynConst.String(s, range.Zero), range.Zero) // TODO escape special chars
        | SymbolExpr s -> idExpr (klToFsId s)
        | AndExpr(left, right) -> FsExpr.App(FsIds.AmpAmp, [build left; build right])
        | OrExpr(left, right) -> FsExpr.App(FsIds.PipePipe, [build left; build right])
        | IfExpr(condition, ifTrue, ifFalse) ->
            SynExpr.IfThenElse(
                build condition,
                build ifTrue,
                build ifFalse |> Some,
                SequencePointInfoForBinding.NoSequencePointAtInvisibleBinding,
                false,
                FsAst.defaultRange,
                FsAst.defaultRange)
        | CondExpr(clauses) ->
            let rec buildClauses = function
                | (condition, ifTrue) :: rest ->
                    SynExpr.IfThenElse(
                        build condition,
                        build ifTrue,
                        buildClauses rest |> Some,
                        SequencePointInfoForBinding.NoSequencePointAtInvisibleBinding,
                        false,
                        range.Zero,
                        range.Zero)
                | [] ->
                    SynExpr.App(
                        ExprAtomicFlag.NonAtomic,
                        false,
                        idExpr "failwith",
                        SynExpr.Const(SynConst.String("No condition was true", range.Zero), range.Zero),
                        range.Zero)
            buildClauses clauses
        | LetExpr(symbol, binding, body) ->
            SynExpr.LetOrUse(
                false,
                false,
                [SynBinding.Binding(
                    None,
                    SynBindingKind.NormalBinding,
                    false,
                    false,
                    [],
                    PreXmlDoc.Empty,
                    SynValData.SynValData(
                        None,
                        SynValInfo.SynValInfo([], SynArgInfo.SynArgInfo([], false, None)),
                        None),
                    SynPat.Named(
                        SynPat.Wild range.Zero,
                        new Ident(symbol, range.Zero),
                        false,
                        None,
                        range.Zero),
                    None,
                    build binding,
                    range.Zero,
                    SequencePointInfoForBinding.NoSequencePointAtLetBinding)],
                build body,
                range.Zero)
        | LambdaExpr(symbol, body) -> failwith "lambda not impl"
            (*SynExpr.Lambda(false, false, [], body, FsAst.defaultRange)*)
        | DefunExpr(symbol, paramz, body) -> failwith "defun compilation not implemented"
        | FreezeExpr(expr) -> failwith "freeze compilation not implemented"
        | TrapExpr(pos, t, c) -> failwith "trap compilation not implemented"
        | AppExpr(pos, f, args) ->
            let builtin id = longIdExpr ["KlBuiltins"; id]
            let primitiveOp op =
                match op with
                | "intern"          -> Some(builtin "klIntern")
                | "pos"             -> Some(builtin "klStringPos")
                | "tlstr"           -> Some(builtin "klStringTail")
                | "cn"              -> Some(builtin "klStringConcat")
                | "str"             -> Some(builtin "klToString")
                | "string?"         -> Some(builtin "klIsString")
                | "n->string"       -> Some(builtin "klIntToString")
                | "string->n"       -> Some(builtin "klStringToInt")
                | "set"             -> Some(builtin "klSet") // needs env
                | "value"           -> Some(builtin "klValue") // needs env
                | "simple-error"    -> Some(builtin "klSimpleError")
                | "error-to-string" -> Some(builtin "klErrorToString")
                | "cons"            -> Some(builtin "klNewCons")
                | "hd"              -> Some(builtin "klHead")
                | "tl"              -> Some(builtin "klTail")
                | "cons?"           -> Some(builtin "klIsCons")
                | "="               -> Some(builtin "klEquals")
                | "type"            -> Some(builtin "klType")
                | "eval-kl"         -> Some(builtin "klEval") // needs env
                | "absvector"       -> Some(builtin "klNewVector")
                | "<-address"       -> Some(builtin "klReadVector")
                | "address->"       -> Some(builtin "klWriteVector")
                | "absvector?"      -> Some(builtin "klIsVector")
                | "write-byte"      -> Some(builtin "klWriteByte")
                | "read-byte"       -> Some(builtin "klReadByte")
                | "open"            -> Some(builtin "klOpen")
                | "close"           -> Some(builtin "klClose")
                | "get-time"        -> Some(builtin "klGetTime")
                | "+"               -> Some(builtin "klAdd")
                | "-"               -> Some(builtin "klSubtract")
                | "*"               -> Some(builtin "klMultiply")
                | "/"               -> Some(builtin "klDivide")
                | ">"               -> Some(builtin "klGreaterThan")
                | "<"               -> Some(builtin "klLessThan")
                | ">="              -> Some(builtin "klGreaterThanEqual")
                | "<="              -> Some(builtin "klLessThanEqual")
                | "number?"         -> Some(builtin "klIsNumber")
                | _                 -> None
            match f with
            | SymbolExpr op ->
                match primitiveOp op with
                | Some(op) -> FsExpr.Paren(FsExpr.App(op, [FsExpr.List(List.map build args)]))
                | _ -> failwith "unrecognized op or number of args"
            | _ -> failwith "can't support function"
module Kl.Make.Loader

open System
open System.IO
open Microsoft.FSharp.Compiler
open Microsoft.FSharp.Compiler.SourceCodeServices
open Kl.Values
open Kl.Evaluator
open Kl.Startup
open Reader
open Compiler
open ShenSharp.Shared
open System.Runtime.InteropServices

let private dllName = sprintf "%s.dll" generatedModule
let private pdbName = sprintf "%s.pdb" generatedModule
let private searchPattern = sprintf "%s.*" generatedModule
let private deps = [
    UriBuilder(typedefof<Kl.Expr>.Assembly.CodeBase).Uri.LocalPath;
    UriBuilder(typedefof<FSharp.Core.unit>.Assembly.CodeBase).Uri.LocalPath;
    UriBuilder(typedefof<bool>.Assembly.CodeBase).Uri.LocalPath;
    combine [RuntimeEnvironment.GetRuntimeDirectory(); "mscorlib.dll"];
]
let private sharedMetadataPath = combine ["ShenSharp.Shared"; "Shared.fs"]

let private import sourcePath sourceFiles =
    let globals = baseGlobals()
    for file in sourceFiles do
        printf "Loading %s " file
        stdout.Flush()
        let text = File.ReadAllText(combine [sourcePath; file])
        for ast in readAll text do
            printf "."
            eval globals ast |> ignore
        printfn ""
    printfn ""
    printfn "Applying post-import declarations..."
    postImport globals

let private raiseErrors messages =
    let errors = Seq.filter (fun (m: FSharpErrorInfo) -> m.Severity = FSharpErrorSeverity.Error) messages
    raise(Exception(String.Join("\r\n\r\n", Seq.map string errors)))

let private parseFile file =
    let input = File.ReadAllText file
    let checker = FSharpChecker.Create()
    let result = (async {
        let! projOptions =
            checker.GetProjectOptionsFromScript(file, input)
        let parseOptions =
            checker.GetParsingOptionsFromProjectOptions(fst projOptions)
        return! checker.ParseFile(file, input, fst parseOptions)
    } |> Async.RunSynchronously)
    match result.ParseTree with
    | Some tree -> tree
    | None -> raiseErrors result.Errors

let private emit asts =
    let service = FSharpChecker.Create()
    let (errors, returnCode) =
        service.Compile(
            asts,
            generatedModule,
            dllName,
            deps,
            pdbName,
            false,
            true) |> Async.RunSynchronously
    if returnCode <> 0 then
        raiseErrors errors

let private move source destination =
    if File.Exists destination then
        File.Delete destination
    Directory.CreateDirectory(Path.GetDirectoryName destination) |> ignore
    File.WriteAllBytes(destination, File.ReadAllBytes source)
    File.Delete source

let make sourcePath sourceFiles outputPath =
    let globals = import sourcePath sourceFiles
    printfn "Translating kernel..."
    let ast = buildInstallationFile generatedModule globals
    let sharedAst = parseFile sharedMetadataPath
    let metadataAst = buildMetadataFile generatedModule
    printfn "Compiling kernel..."
    printfn "%A" deps
    emit [ast; sharedAst; metadataAst]
    printfn "Copying artifacts to output path..."
    for file in Directory.GetFiles(".", searchPattern) do
        move file (combine [outputPath; file])
    printfn "Done."

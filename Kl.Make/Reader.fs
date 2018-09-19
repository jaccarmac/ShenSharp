﻿/// <summary>
/// The Reader parses KL source code into a value.
/// </summary>
/// <remarks>
/// Reader is strict about some details.
/// It will not handle extra spaces inside of parens.
/// </remarks>
module Kl.Make.Reader

open FParsec
open Kl
open Kl.Values

let private pValue, pValueRef = createParserForwardedToRef<Value, unit>()
let private pNum = regex "[-+]?[0-9]*\\.?[0-9]+([eE][-+]?[0-9]+)?" |>> decimal |>> Num
let private pStr = between (pchar '"') (pchar '"') (manySatisfy ((<>) '"')) |>> Str
let private pSym = regex "[^\\s\\x28\\x29]+" |>> Sym
let private pList = between (pchar '(') (pchar ')') (sepBy pValue spaces1) |>> toCons
let private pValues = spaces >>. (many (pValue .>> spaces))
do pValueRef := choice [pNum; pStr; pSym; pList]

let private runParser p s =
    match run p s with
    | Success(result, _, _) -> result
    | Failure(error, _, _) -> failwith error

/// <summary>Read first complete KL source expression into value.</summary>
/// <exception>Throws when syntax is invalid.</exception>
let read = runParser pValue

/// <summary>Read all KL source expressions into a list of values.</summary>
/// <exception>Throws when syntax is invalid.</exception>
let readAll = runParser pValues

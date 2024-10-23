[![Shen Version](https://img.shields.io/badge/shen-39.0-blue.svg)](https://github.com/Shen-Language)
[![Latest Nuget](https://img.shields.io/nuget/v/ShenSharp.svg)](https://www.nuget.org/packages/ShenSharp)

# Shen for the Common Language Runtime

<img src="https://raw.githubusercontent.com/rkoeninger/ShenSharp/master/assets/ShenSharp.png" align="right">

ShenSharp is a port of the [Shen language](http://shenlanguage.org/) to the CLR.

Shen is a portable, functional programming language by [Mark Tarver](http://marktarver.com/). It is a descendant of the LISP family of languages which also includes features commonly found in ML family languages. Shen offers pattern matching, a unique macro system, optional lazy evaluation, configurable static type checking, logic programming and a built-in compiler-compiler.

ShenSharp is built by loading the Shen kernel as Kλ and translating the resulting environment to F# ASTs and building that into a CLR assembly. Shen code loaded by this compiled kernel is translated into optimized expressions and evaluated on demand. Some optimizations used include inlining direct references to global symbols, expression simplification and tail call optimization using trampolines.

This port passes all tests in the Shen standard test suite, making this a certifiable implementation.

Refer to the CI build script ([`build.yml`](https://github.com/rkoeninger/ShenSharp/blob/master/.github/workflows/build.yml)) for the build process. In order to build, the pre-built Kλ needs to be fetched and the two solutions built in order. The build script runs through all of these steps.

### Shen Resources
  * [Open Source Implementations](http://www.shenlanguage.org/download_form.html)
  * [Tutorial](http://www.shenlanguage.org/learn-shen/index.html)
  * [Sources](https://github.com/Shen-Language/shen-sources)
  * [Wiki](https://github.com/Shen-Language/wiki/wiki)
  * [Mailing List](https://groups.google.com/forum/#!forum/qilang)

### CLR Platform Support Status
  * .NET 8

### Guided Tour of the Code
  * [Types](https://github.com/rkoeninger/ShenSharp/blob/master/src/Kl/Types.fs) -
    Defines types for Kλ Values, Functions, Environment and IO
  * [Evaluator](https://github.com/rkoeninger/ShenSharp/blob/master/src/Kl/Evaluator.fs) -
    Interprets Kλ syntax trees, rendering Kλ values and performing side effects
  * [Builtins](https://github.com/rkoeninger/ShenSharp/blob/master/src/Kl/Builtins.fs) -
    Primitive functions required by Kλ
  * [Startup](https://github.com/rkoeninger/ShenSharp/blob/master/src/Kl/Startup.fs) -
    Builds a primitive Kλ environment
  * [Reader](https://github.com/rkoeninger/ShenSharp/blob/master/src/Kl.Make/Reader.fs) -
    Parses Kλ code
  * [Compiler](https://github.com/rkoeninger/ShenSharp/blob/master/src/Kl.Make/Compiler.fs) -
    Translates an environment of Kλ expressions into a F# module AST
  * [Runtime](https://github.com/rkoeninger/ShenSharp/blob/master/src/Shen.Api/Runtime.fs) -
    Helpful functions for embedding Shen in another application
  * [ExtensionMethods](https://github.com/rkoeninger/ShenSharp/blob/master/src/Shen.Api/ExtensionMethods.fs) -
    Extension method versions of functions in Runtime to give API a C# feel
  * [RunRepl](https://github.com/rkoeninger/ShenSharp/blob/master/src/Shen.Repl/RunRepl.fs) -
    Interpretation of command line arguments passed to `Shen.exe`

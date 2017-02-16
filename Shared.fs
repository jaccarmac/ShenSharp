module ShenSharp.Shared

open System.Reflection
open System.Runtime.InteropServices

[<Literal>]
let Version = "0.5.0.0"

[<assembly: AssemblyProduct "ShenSharp">]
[<assembly: AssemblyDescription "Shen for the Common Language Runtime">]
[<assembly: AssemblyCompany "Robert Koeninger">]
[<assembly: AssemblyCopyright "Copyright © 2015-2017 Robert Koeninger">]

#if DEBUG
[<assembly: AssemblyConfiguration "Debug">]
#else
[<assembly: AssemblyConfiguration "Release">]
#endif

[<assembly: ComVisible false>]

[<assembly: AssemblyVersion(Version)>]
[<assembly: AssemblyFileVersion(Version)>]
[<assembly: AssemblyInformationalVersion(Version)>]

do ()

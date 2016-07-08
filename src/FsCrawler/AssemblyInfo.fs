namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FsCrawler")>]
[<assembly: AssemblyProductAttribute("FsCrawler")>]
[<assembly: AssemblyDescriptionAttribute("F# Web Crawler")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
    let [<Literal>] InformationalVersion = "1.0"

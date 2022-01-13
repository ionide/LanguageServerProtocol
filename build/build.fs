// open Fake
open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.Core.TargetOperators
open System

let configuration = Environment.environVarOrDefault "configuration" "Release"
let project = "LanguageServerProtocol"
let buildDir = "src" </> project </> "bin" </> "Debug"
let buildReleaseDir = "src" </> project </>  "bin" </> "Release"
let releaseDir = "release"

let clean = fun _ ->
  Shell.cleanDirs [ buildDir; buildReleaseDir; ]

let restore = fun _ ->
    DotNet.restore id ""

let build = fun _ ->
  DotNet.build (fun p ->
     { p with
         Configuration = DotNet.BuildConfiguration.fromString configuration
         MSBuildParams = MSBuild.CliArguments.Create() }) "LanguageServerProtocol.sln"


let replaceFsLibLog = fun _ ->
  let replacements =
    [ "FsLibLog\\n", "LanguageServerProtocol.Logging\n"
      "FsLibLog\\.", "LanguageServerProtocol.Logging" ]
  replacements
  |> List.iter (fun (``match``, replace) ->
    (!! "paket-files/TheAngryByrd/FsLibLog/**/FsLibLog*.fs")
    |> Shell.regexReplaceInFilesWithEncoding ``match`` replace System.Text.Encoding.UTF8
  )

let release = fun _ ->
    Directory.ensure releaseDir
    Shell.cleanDirs [ releaseDir ]

    DotNet.pack (fun p ->
       { p with
           OutputPath = Some (__SOURCE_DIRECTORY__ </> ".." </> releaseDir)
           Configuration = DotNet.BuildConfiguration.fromString configuration
           MSBuildParams = MSBuild.CliArguments.Create () }) "src/Ionide.LanguageServerProtocol.fsproj"

let push = fun _ ->
    let key =
        match Environment.getBuildParam "nuget-key" with
        | s when not (String.isNullOrWhiteSpace s) -> s
        | _ -> UserInput.getUserPassword "NuGet Key: "
    Paket.push (fun p -> { p with WorkingDir = (__SOURCE_DIRECTORY__ </> ".." </> releaseDir); ApiKey = key; ToolType = ToolType.CreateLocalTool() })


let initTargets() =
    let (==>!) x y = x ==> y |> ignore

    Target.create "Clean" clean
    Target.create "Restore" restore
    Target.create "Build" build
    Target.create "ReplaceFsLibLogNamespaces" replaceFsLibLog
    Target.create "Release" release
    Target.create "Push" push

    "Clean"
    ==> "Restore"
    ==> "ReplaceFsLibLogNamespaces"
    ==> "Build"
    ==> "Release"
    ==>! "Push"

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> Context.FakeExecutionContext.Create false "build.fsx"
    |> Context.RuntimeContext.Fake
    |> Context.setExecutionContext
    initTargets ()
    Target.runOrDefaultWithArguments "Release"

    0 // return an integer exit code
// open Fake
open Fake.Core
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.DotNet
open Fake.Core.TargetOperators
open System

let changelogFilename = __SOURCE_DIRECTORY__ </> ".." </> "CHANGELOG.md"
let changelog = Changelog.load changelogFilename
let mutable latestEntry =
    if Seq.isEmpty changelog.Entries
    then Changelog.ChangelogEntry.New("0.0.1", "0.0.1-alpha.1", Some DateTime.Today, None, [], false)
    else changelog.LatestEntry

let configuration = Environment.environVarOrDefault "configuration" "Release"
let project = "LanguageServerProtocol"
let buildDir = "src" </> project </> "bin" </> "Debug"
let buildReleaseDir = "src" </> project </>  "bin" </> "Release"
let releaseDir = "release"

let summary =
    "Building Language Server Protocol server and clients in F#"

let authors = "chethusk; Krzysztof-Cieslak;"
let tags = "LSP; editor tooling"

let gitOwner = "ionide"
let gitName = "LanguageServerProtocol"
let gitHome = "https://github.com/" + gitOwner
let gitUrl = gitHome + "/" + gitName

let packageReleaseNotes =
    sprintf "%s/blob/v%s/CHANGELOG.md" gitUrl latestEntry.NuGetVersion

// Helper function to remove blank lines
let isEmptyChange =
    function
    | Changelog.Change.Added s
    | Changelog.Change.Changed s
    | Changelog.Change.Deprecated s
    | Changelog.Change.Fixed s
    | Changelog.Change.Removed s
    | Changelog.Change.Security s
    | Changelog.Change.Custom (_, s) -> String.isNullOrWhiteSpace s.CleanedText

let releaseNotes =
    latestEntry.Changes
    |> List.filter (isEmptyChange >> not)
    |> List.map (fun c -> " * " + c.ToString())
    |> String.concat "\n"

let properties =
    [   ("Version", latestEntry.AssemblyVersion)
        ("Authors", authors)
        ("PackageProjectUrl", gitUrl)
        ("PackageTags", tags)
        ("RepositoryType", "git")
        ("RepositoryUrl", gitUrl)
        ("PackageLicenseExpression", "MIT")
        ("PackageReleaseNotes", packageReleaseNotes)
        ("PackageDescription", summary)
        ("EnableSourceLink", "true") ]


let clean = fun _ ->
  Shell.cleanDirs [ buildDir; buildReleaseDir; ]

let restore = fun _ ->
    DotNet.restore id ""

let build = fun _ ->
  DotNet.build (fun p ->
     { p with
         Configuration = DotNet.BuildConfiguration.fromString configuration
         MSBuildParams = { MSBuild.CliArguments.Create () with Properties = properties } }) "LanguageServerProtocol.sln"


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
           MSBuildParams = { MSBuild.CliArguments.Create () with Properties = properties } }) "src/Ionide.LanguageServerProtocol.fsproj"

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

module Ionide.LanguageServerProtocol.Tests.Benchmarks

open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.JsonUtils
open Ionide.LanguageServerProtocol.Server
open Newtonsoft.Json.Linq
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running
open BenchmarkDotNet.Configs
open System
open System.Collections.Concurrent

let inline private memorise (f: 'a -> 'b) : 'a -> 'b =
  let d = ConcurrentDictionary<'a, 'b>()
  fun key ->
    d.GetOrAdd(key, f)
let inline private memoriseByHash (f: 'a -> 'b) : 'a -> 'b =
  let d = ConcurrentDictionary<int, 'b>()

  fun key ->
    let hash = key.GetHashCode()

    match d.TryGetValue(hash) with
    | (true, value) -> value
    | _ ->
      let value = f key
      d.TryAdd(hash, value) |> ignore
      value

[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<CategoriesColumn>]
type TypeCheckBenchmarks() =
  static let values: obj array =
    [|
      1
      3.14
      true
      "foo"
      4uy
      Some "foo"
      123456
      "bar"
      false
      Some true
      987.654321
      0
      Some 0
      5u
      Some "string"
      Some "bar"
      Some "baz"
      Some "lorem ipsum dolor sit"
      321654
      2.71828
      "lorem ipsum dolor sit"
      Some 42
      Some true
      Some 3.14
    |]
  static let types =
    values
    |> Array.map (fun v -> v.GetType())

  static let isOptionType (ty: Type) =
    ty.IsGenericType
    &&
    ty.GetGenericTypeDefinition() = typedefof<_ option>
  static let memorisedIsOptionType =
    memorise isOptionType
  static let memorisedHashIsOptionType =
    memoriseByHash isOptionType

  [<BenchmarkCategory("IsNumeric"); Benchmark(Baseline = true)>]
  member _.IsNumeric_typeof() =
    let mutable count = 0
    for ty in types do
      if Type.numerics |> Array.exists ((=) ty) then
        count <- count + 1
    count
  [<BenchmarkCategory("IsNumeric"); Benchmark>]
  member _.IsNumeric_hash() =
    let mutable count = 0
    for ty in types do
      let hash = ty.GetHashCode()
      if Type.numericHashes |> Array.contains hash then
        count <- count + 1
    count

  [<BenchmarkCategory("IsBool"); Benchmark(Baseline = true)>]
  member _.IsBool_typeof() =
    let mutable count = 0
    for ty in types do
      if ty = typeof<bool> then
        count <- count + 1
    count
  [<BenchmarkCategory("IsBool"); Benchmark>]
  member _.IsBool_hash() =
    let mutable count = 0
    for ty in types do
      if ty.GetHashCode() = Type.boolHash then
        count <- count + 1
    count

  [<BenchmarkCategory("IsString"); Benchmark(Baseline = true)>]
  member _.IsString_typeof() =
    let mutable count = 0
    for ty in types do
      if ty = typeof<string> then
        count <- count + 1
    count
  [<BenchmarkCategory("IsString"); Benchmark>]
  member _.IsString_hash() =
    let mutable count = 0
    for ty in types do
      if ty.GetHashCode() = Type.stringHash then
        count <- count + 1
    count
  
  [<BenchmarkCategory("IsOption"); Benchmark(Baseline = true)>]
  member _.IsOption_check() =
    let mutable count = 0
    for ty in types do
      if isOptionType ty then
        count <- count + 1
    count
  [<BenchmarkCategory("IsOption"); Benchmark>]
  member _.IsOption_memoriseType() =
    let mutable count = 0
    for ty in types do
      if memorisedIsOptionType ty then
        count <- count + 1
    count
  [<BenchmarkCategory("IsOption"); Benchmark>]
  member _.IsOption_memoriseHash() =
    let mutable count = 0
    for ty in types do
      if memorisedHashIsOptionType ty then
        count <- count + 1
    count


[<MemoryDiagnoser>]
type MultipleTypesBenchmarks() =
  let initializeParams: InitializeParams =
    { ProcessId = Some 42
      ClientInfo = Some { Name = "foo"; Version = None }
      RootPath = Some "/"
      RootUri = Some "file://..."
      InitializationOptions = None
      Capabilities =
        Some
          { Workspace =
              Some
                { ApplyEdit = Some true
                  WorkspaceEdit =
                    Some
                      { DocumentChanges = Some true
                        ResourceOperations =
                          Some [| ResourceOperationKind.Create
                                  ResourceOperationKind.Rename
                                  ResourceOperationKind.Delete |]
                        FailureHandling = Some FailureHandlingKind.Abort
                        NormalizesLineEndings = None
                        ChangeAnnotationSupport = Some { GroupsOnLabel = Some false } }
                  DidChangeConfiguration = None
                  DidChangeWatchedFiles = None
                  Symbol =
                    Some
                      { DynamicRegistration = Some false
                        SymbolKind = Some { ValueSet = Some SymbolKindCapabilities.DefaultValueSet } }
                  SemanticTokens = Some { RefreshSupport = Some true }
                  InlayHint = Some { RefreshSupport = Some false } }
            TextDocument =
              Some
                { Synchronization =
                    Some
                      { DynamicRegistration = Some true
                        WillSave = Some true
                        WillSaveWaitUntil = Some false
                        DidSave = Some true }
                  PublishDiagnostics = { RelatedInformation = None; TagSupport = None }
                  Completion = None
                  Hover =
                    Some
                      { DynamicRegistration = Some true
                        ContentFormat = Some [| "markup"; "plaintext" |] }
                  SignatureHelp =
                    Some
                      { DynamicRegistration = Some true
                        SignatureInformation = Some { DocumentationFormat = None } }
                  References = Some { DynamicRegistration = Some false }
                  DocumentHighlight = Some { DynamicRegistration = None }
                  DocumentSymbol = None
                  Formatting = Some { DynamicRegistration = Some true }
                  RangeFormatting = Some { DynamicRegistration = Some true }
                  OnTypeFormatting = None
                  Definition = Some { DynamicRegistration = Some false }
                  CodeAction =
                    Some
                      { DynamicRegistration = Some true
                        CodeActionLiteralSupport =
                          Some
                            { CodeActionKind =
                                { ValueSet = [| "foo"; "bar"; "baz"; "alpha"; "beta"; "gamma"; "delta"; "x"; "y"; "z" |] } }
                        IsPreferredSupport = Some true
                        DisabledSupport = Some false
                        DataSupport = None
                        ResolveSupport = Some { Properties = [| "foo"; "bar"; "baz" |] }
                        HonorsChangeAnnotations = Some false }
                  CodeLens = Some { DynamicRegistration = Some true }
                  DocumentLink = Some { DynamicRegistration = Some true }
                  Rename = None
                  FoldingRange =
                    Some
                      { DynamicRegistration = Some false
                        LineFoldingOnly = Some true
                        RangeLimit = None }
                  SelectionRange = Some { DynamicRegistration = None }
                  SemanticTokens =
                    Some
                      { DynamicRegistration = Some false
                        Requests = { Range = Some true; Full = Some(U2.Second { Delta = Some true }) }
                        TokenTypes =
                          "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Proin tortor purus platea sit eu id nisi litora libero. Neque vulputate consequat ac amet augue blandit maximus aliquet congue. Pharetra vestibulum posuere ornare faucibus fusce dictumst orci aenean eu facilisis ut volutpat commodo senectus purus himenaeos fames primis convallis nisi."
                          |> fun s -> s.Split(' ')
                        TokenModifiers =
                          "Phasellus fermentum malesuada phasellus netus dictum aenean placerat egestas amet. Ornare taciti semper dolor tristique morbi. Sem leo tincidunt aliquet semper eu lectus scelerisque quis. Sagittis vivamus mollis nisi mollis enim fermentum laoreet."
                          |> fun s -> s.Split(' ')
                        Formats = [| TokenFormat.Relative |]
                        OverlappingTokenSupport = Some false
                        MultilineTokenSupport = Some true }
                  InlayHint =
                    Some
                      { DynamicRegistration = Some true
                        ResolveSupport = Some { Properties = [| "Tooltip"; "Position"; "TextEdits" |] } } }
            Experimental = None }
      trace = None
      WorkspaceFolders =
        Some [| { Uri = "..."; Name = "foo" }
                { Uri = "/"; Name = "bar" }
                { Uri = "something long stuff and even longer and longer and longer"
                  Name = "bar" } |] }

  let inlayHint: InlayHint =
    { Label =
        InlayHintLabel.Parts [| { InlayHintLabelPart.Value = "1st label"
                                  Tooltip = Some(InlayHintTooltip.String "1st label tooltip")
                                  Location = Some { Uri = "1st"; Range = mkRange' (1, 2) (3, 4) }
                                  Command = None }
                                { Value = "2nd label"
                                  Tooltip = Some(InlayHintTooltip.String "1st label tooltip")
                                  Location = Some { Uri = "2nd"; Range = mkRange' (5, 8) (10, 9) }
                                  Command = Some { Title = "2nd command"; Command = "foo"; Arguments = None } }
                                { InlayHintLabelPart.Value = "3rd label"
                                  Tooltip =
                                    Some(
                                      InlayHintTooltip.Markup
                                        { Kind = MarkupKind.Markdown
                                          Value =
                                            """
                                            # Header
                                            Description
                                            * List 1
                                            * List 2
                                            """ }
                                    )
                                  Location = Some { Uri = "3rd"; Range = mkRange' (1, 2) (3, 4) }
                                  Command = None } |]
      Position = { Line = 5; Character = 10 }
      Kind = Some InlayHintKind.Type
      TextEdits =
        Some [| { Range = mkRange' (5, 10) (6, 5); NewText = "foo bar" }
                { Range = mkRange' (5, 0) (5, 2); NewText = "baz" } |]
      Tooltip = Some(InlayHintTooltip.Markup { Kind = MarkupKind.PlainText; Value = "some tooltip" })
      PaddingLeft = Some true
      PaddingRight = Some false
      Data = Some(JToken.FromObject "some data") }

  let all: obj [] = [| initializeParams; inlayHint |]

  [<Benchmark>]
  member _.All_Roundtrip() =
    for o in all do
      let json = inlayHint |> serialize
      let res = json.ToObject(o.GetType(), jsonRpcFormatter.JsonSerializer)
      ()

  [<Benchmark>]
  member b.All_MultipleRoundtrips() =
    for _ in 0..250 do
      b.All_Roundtrip()

let run (args: string []) =
  let switcher = BenchmarkSwitcher.FromTypes([| typeof<MultipleTypesBenchmarks>; typeof<TypeCheckBenchmarks> |])
  switcher.Run(args) |> ignore
  0
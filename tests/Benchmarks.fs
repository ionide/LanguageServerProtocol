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
open System.Collections.Generic
open BenchmarkDotNet.Order

let inline private memorise (f: 'a -> 'b) : 'a -> 'b =
  let d = ConcurrentDictionary<'a, 'b>()
  fun key -> d.GetOrAdd(key, f)

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
    [| 1
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
       Some 3.14 |]

  static let types = values |> Array.map (fun v -> v.GetType())

  static let isOptionType (ty: Type) =
    ty.IsGenericType
    && ty.GetGenericTypeDefinition() = typedefof<_ option>

  static let memorisedIsOptionType = memorise isOptionType
  static let memorisedHashIsOptionType = memoriseByHash isOptionType

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
      if ty = typeof<bool> then count <- count + 1

    count

  [<BenchmarkCategory("IsBool"); Benchmark>]
  member _.IsBool_hash() =
    let mutable count = 0

    for ty in types do
      if ty.GetHashCode() = Type.boolHash then count <- count + 1

    count

  [<BenchmarkCategory("IsString"); Benchmark(Baseline = true)>]
  member _.IsString_typeof() =
    let mutable count = 0

    for ty in types do
      if ty = typeof<string> then count <- count + 1

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
      if isOptionType ty then count <- count + 1

    count

  [<BenchmarkCategory("IsOption"); Benchmark>]
  member _.IsOption_memoriseType() =
    let mutable count = 0

    for ty in types do
      if memorisedIsOptionType ty then count <- count + 1

    count

  [<BenchmarkCategory("IsOption"); Benchmark>]
  member _.IsOption_memoriseHash() =
    let mutable count = 0

    for ty in types do
      if memorisedHashIsOptionType ty then count <- count + 1

    count

module Example =
  open System.Text.Json.Serialization

  type private Random with
    member rand.NextBool() =
      // 2nd is exclusive!
      rand.Next(0, 2) = 0

    member rand.NextOption(value) = if rand.NextBool() then Some value else None
    member rand.NextCount depth = rand.Next(2, max 4 depth)

    member rand.NextDepth depth =
      if depth <= 1 then
        0
      elif depth = 2 then
        1
      else
        let lower = max 2 (depth - 1)
        let upper = max lower (depth - 1)
        rand.Next(lower, upper + 1)

  [<RequireQualifiedAccess>]
  type SingleCaseUnion =
    | Lorem
    | Ipsum

  type SingleCaseUnionHolder = { SingleCaseUnion: SingleCaseUnion }

  module SingleCaseUnionHolder =
    let gen (rand: Random) (depth: int) =
      { SingleCaseUnion =
          if rand.NextBool() then
            SingleCaseUnion.Ipsum
          else
            SingleCaseUnion.Ipsum }

  type WithExtensionData =
    { NoExtensionData: string
      [<JsonExtensionData>]
      mutable AdditionalData: IDictionary<string, JToken> }

  module WithExtensionData =
    let gen (rand: Random) (depth: int) =
      { NoExtensionData = $"WithExtensionData {depth}"
        AdditionalData =
          List.init (rand.NextCount depth) (fun i ->
            let key = $"Data{depth}Ele{i}"
            let value = JToken.FromObject(i * depth)
            (key, value))
          |> Map.ofList }

  type RecordWithOption =
    { RequiredValue: string
      OptionalValue: string option
      AnotherOptionalValue: int option
      FinalOptionalValue: int option }

  module RecordWithOption =
    let gen (rand: Random) (depth: int) =
      { RequiredValue = $"RecordWithOption {depth}"
        OptionalValue = rand.NextOption $"Hello {depth}"
        AnotherOptionalValue = rand.NextOption(42000 + depth)
        FinalOptionalValue = rand.NextOption(13000 + depth) }

  [<RequireQualifiedAccess>]
  [<ErasedUnion>]
  type ErasedUnionData =
    | Alpha of string
    | Beta of int
    | Gamma of bool
    | Delta of float
    | Epsilon of RecordWithOption

  module ErasedUnionData =
    let gen (rand: Random) (depth: int) =
      match rand.Next(0, 5) with
      | 0 -> ErasedUnionData.Alpha $"Erased {depth}"
      | 1 -> ErasedUnionData.Beta(42000 + depth)
      | 2 -> ErasedUnionData.Gamma false
      | 3 -> ErasedUnionData.Delta(42000.123 + (float depth))
      | 4 -> ErasedUnionData.Epsilon(RecordWithOption.gen rand (depth - 1))
      | _ -> failwith "unreachable"

  type ErasedUnionDataHolder = { ErasedUnion: ErasedUnionData }

  module ErasedUnionDataHolder =
    let gen (rand: Random) (depth: int) = { ErasedUnion = ErasedUnionData.gen rand depth }

  type U2Holder =
    { BoolString: U2<bool, string>
      StringInt: U2<string, int>
      BoolErasedUnionData: U2<bool, ErasedUnionDataHolder> }

  module U2Holder =
    let gen (rand: Random) (depth: int) =
      { BoolString =
          if rand.NextBool() then
            U2.First true
          else
            U2.Second $"U2 {depth}"
        StringInt =
          if rand.NextBool() then
            U2.First $"U2 {depth}"
          else
            U2.Second(42000 + depth)
        BoolErasedUnionData =
          if rand.NextBool() then
            U2.First true
          else
            U2.Second(ErasedUnionDataHolder.gen rand (depth - 1)) }

  [<RequireQualifiedAccess>]
  type MyEnum =
    | X = 1
    | Y = 2
    | Z = 3

  type MyEnumHolder = { EnumValue: MyEnum; EnumArray: MyEnum [] }

  module MyEnumHolder =
    let gen (rand: Random) (depth: int) =
      let n = Enum.GetNames(typeof<MyEnum>).Length

      { EnumValue = rand.Next(0, n) |> enum<_>
        EnumArray = Array.init (rand.NextCount depth) (fun i -> rand.Next(0, n) |> enum<_>) }

  type MapHolder = { MyMap: Map<string, string> }

  module MapHolder =
    let gen (rand: Random) (depth: int) =
      { MyMap =
          Array.init (rand.NextCount depth) (fun i ->
            let key = $"Key{i}"
            let value = $"Data{i}@{depth}"
            (key, value))
          |> Map.ofArray }

  type BasicData =
    { IntData: int
      FloatData: float
      BoolData: bool
      StringData: string
      CharData: char
      StringOptionData: string option
      IntArrayOptionData: int [] option }

  module BasicData =
    let gen (rand: Random) (depth: int) =
      { IntData = rand.Next(0, 500)
        FloatData = rand.NextDouble()
        BoolData = rand.NextBool()
        StringData = $"Data {depth}"
        CharData = '_'
        StringOptionData = rand.NextOption $"Option {depth}"
        IntArrayOptionData = Array.init (rand.NextCount depth) id |> rand.NextOption }


  [<RequireQualifiedAccess>]
  [<ErasedUnion>]
  type Data =
    | SingleCaseUnion of SingleCaseUnionHolder
    | WithExtensionData of WithExtensionData
    | RecordWithOption of RecordWithOption
    | ErasedUnion of ErasedUnionDataHolder
    | U2 of U2Holder
    | Enum of MyEnumHolder
    | Map of MapHolder
    | BasicData of BasicData
    | More of Data []

  module Data =
    let rec gen (rand: Random) (depth: int) =
      match rand.Next(0, 11) with
      | _ when depth <= 0 -> Data.More [||]
      | 0 -> Data.SingleCaseUnion(SingleCaseUnionHolder.gen rand depth)
      | 1 -> Data.WithExtensionData(WithExtensionData.gen rand depth)
      | 2 -> Data.RecordWithOption(RecordWithOption.gen rand depth)
      | 3 -> Data.ErasedUnion(ErasedUnionDataHolder.gen rand depth)
      | 4 -> Data.U2(U2Holder.gen rand depth)
      | 5 -> Data.Enum(MyEnumHolder.gen rand depth)
      | 6 -> Data.Map(MapHolder.gen rand depth)
      | 7 -> Data.BasicData(BasicData.gen rand depth)
      | 8
      | 9
      | 10 ->
        Data.More(
          Array.init (rand.NextCount depth) (fun _ ->
            let depth = rand.NextDepth depth
            gen rand depth)
        )
      | _ -> failwith "unreachable"

  let createData (seed: int, additionalWidth: int, maxDepth: int) =
    // Note: deterministic (-> seed)
    let rand = Random(seed)

    let always =
      [| Data.SingleCaseUnion(SingleCaseUnionHolder.gen rand maxDepth)
         Data.WithExtensionData(WithExtensionData.gen rand maxDepth)
         Data.RecordWithOption(RecordWithOption.gen rand maxDepth)
         Data.ErasedUnion(ErasedUnionDataHolder.gen rand maxDepth)
         Data.U2(U2Holder.gen rand maxDepth)
         Data.Enum(MyEnumHolder.gen rand maxDepth)
         Data.Map(MapHolder.gen rand maxDepth)
         Data.BasicData(BasicData.gen rand maxDepth) |]

    let additional =
      Array.init additionalWidth (fun _ ->
        let depth = rand.NextDepth maxDepth
        Data.gen rand depth)

    let data = Array.append always additional
    Data.More data

[<MemoryDiagnoser>]
[<GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)>]
[<CategoriesColumn>]
[<Orderer(SummaryOrderPolicy.Declared, MethodOrderPolicy.Declared)>]
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
                  InlayHint = Some { RefreshSupport = Some false }
                  InlineValue = Some { RefreshSupport = Some false }
                  CodeLens = Some { RefreshSupport = Some true } }
            TextDocument =
              Some
                { Synchronization =
                    Some
                      { DynamicRegistration = Some true
                        WillSave = Some true
                        WillSaveWaitUntil = Some false
                        DidSave = Some true }
                  PublishDiagnostics = Some { RelatedInformation = None; TagSupport = None }
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
                  CallHierarchy = Some { DynamicRegistration = None }
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
                  TypeHierarchy = Some { DynamicRegistration = None }
                  InlayHint =
                    Some
                      { DynamicRegistration = Some true
                        ResolveSupport = Some { Properties = [| "Tooltip"; "Position"; "TextEdits" |] } } }
            Experimental = None
            Window = None }
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

  let allLsp: obj [] = [| initializeParams; inlayHint |]

  /// Some complex data which covers all converters
  let example = Example.createData (1234, 9, 5)
  let option = {| Some = Some 123; None = (None: int option) |}

  let withCounts (counts) data =
    data
    |> Array.collect (fun data -> counts |> Array.map (fun count -> [| box count; box data |]))

  member _.AllLsp_Roundtrip() =
    for o in allLsp do
      let json = inlayHint |> serialize
      let res = json.ToObject(o.GetType(), jsonRpcFormatter.JsonSerializer)
      ()

  [<BenchmarkCategory("LSP"); Benchmark>]
  [<Arguments(1)>]
  [<Arguments(250)>]
  member b.AllLsp_Roundtrips(count: int) =
    for _ in 1..count do
      b.AllLsp_Roundtrip()

  member _.Example_Roundtrip() =
    let json = example |> serialize
    let res = json.ToObject(example.GetType(), jsonRpcFormatter.JsonSerializer)
    ()

  [<BenchmarkCategory("Example"); Benchmark>]
  [<Arguments(1)>]
  [<Arguments(50)>]
  member b.Example_Roundtrips(count: int) =
    for _ in 1..count do
      b.Example_Roundtrip()

  [<BenchmarkCategory("Basic"); Benchmark>]
  [<Arguments(1)>]
  [<Arguments(50)>]
  member _.Option_Roundtrips(count: int) =
    for _ in 1..count do
      let json = option |> serialize
      let _ = json.ToObject(option.GetType(), jsonRpcFormatter.JsonSerializer)
      ()

  member _.SingleCaseUnion_ArgumentsSource() =
    [| Example.SingleCaseUnion.Ipsum; Example.SingleCaseUnion.Lorem |]
    |> withCounts [| 1; 50 |]

  [<BenchmarkCategory("Basic"); Benchmark>]
  [<ArgumentsSource("SingleCaseUnion_ArgumentsSource")>]
  member _.SingleCaseUnion_Roundtrips(count: int, data: Example.SingleCaseUnion) =
    for _ in 1..count do
      let json = data |> serialize
      let _ = json.ToObject(typeof<Example.SingleCaseUnion>, jsonRpcFormatter.JsonSerializer)
      ()

  member _.ErasedUnion_ArgumentsSource() =
    [| Example.ErasedUnionData.Alpha "foo"
       Example.ErasedUnionData.Beta 42
       Example.ErasedUnionData.Gamma true
       Example.ErasedUnionData.Delta 3.14
       Example.ErasedUnionData.Epsilon
         { RequiredValue = "foo"
           OptionalValue = None
           AnotherOptionalValue = None
           FinalOptionalValue = None } |]
    |> withCounts [| 1; 50 |]

  [<BenchmarkCategory("Basic"); Benchmark>]
  [<ArgumentsSource("ErasedUnion_ArgumentsSource")>]
  member _.ErasedUnion_Roundtrips(count: int, data: Example.ErasedUnionData) =
    for _ in 1..count do
      let json = data |> serialize
      let _ = json.ToObject(typeof<Example.ErasedUnionData>, jsonRpcFormatter.JsonSerializer)
      ()


let run (args: string []) =
  let switcher = BenchmarkSwitcher.FromTypes([| typeof<MultipleTypesBenchmarks>; typeof<TypeCheckBenchmarks> |])
  switcher.Run(args) |> ignore
  0
module Ionide.LanguageServerProtocol.Tests.Benchmarks

open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.Server
open Newtonsoft.Json.Linq
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

type Record1 = { Name: string; Value: int }

[<MemoryDiagnoser>]
type U2Benchmarks() =
  let simpleU2First: U2<int, string> = U2.First 42
  let simpleU2FirstJson = serialize simpleU2First
  let simpleU2Second: U2<int, string> = U2.Second "foo"
  let simpleU2SecondJson = serialize simpleU2Second
  let complexU2: U2<string, Record1> = U2.Second { Name = "foo"; Value = 42 }
  let complexU2Json = serialize complexU2

  [<Benchmark>]
  member _.SimpleU2First_Serialize() = simpleU2First |> serialize

  [<Benchmark>]
  member _.SimpleU2First_Deserialize() = simpleU2FirstJson |> deserialize<U2<int, string>>

  [<Benchmark>]
  member _.SimpleU2First_Roundtrip() = simpleU2First |> serialize |> deserialize<U2<int, string>>

  [<Benchmark>]
  member _.SimpleU2Second_Serialize() = simpleU2Second |> serialize

  [<Benchmark>]
  member _.SimpleU2Second_Deserialize() = simpleU2SecondJson |> deserialize<U2<int, string>>

  [<Benchmark>]
  member _.SimpleU2Second_Roundtrip() = simpleU2First |> serialize |> deserialize<U2<int, string>>

  [<Benchmark>]
  member _.complexU2_Serialize() = complexU2 |> serialize

  [<Benchmark>]
  member _.complexU2_Deserialize() = complexU2Json |> deserialize<U2<string, Record1>>

  [<Benchmark>]
  member _.complexU2_Roundtrip() =
    simpleU2First
    |> serialize
    |> deserialize<U2<string, Record1>>

[<MemoryDiagnoser>]
type InlayHintBenchmarks() =
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

  let jtoken = serialize inlayHint
  let json = jtoken.ToString()


  [<Benchmark>]
  member _.ComplexInlayHint_Serialize() = inlayHint |> serialize

  [<Benchmark>]
  member _.ComplexInlayHint_Deserialize() = jtoken |> deserialize<InlayHint>

  [<Benchmark>]
  member _.ComplexInlayHint_Roundtrip() = inlayHint |> serialize |> deserialize<InlayHint>


let run (args) =
  let switcher = BenchmarkSwitcher.FromTypes([| typeof<U2Benchmarks>; typeof<InlayHintBenchmarks> |])
  switcher.Run(args) |> ignore
  0
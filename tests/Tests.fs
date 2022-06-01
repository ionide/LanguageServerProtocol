module Ionide.LanguageServerProtocol.Tests.Tests

open Expecto
open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Tests
open Newtonsoft.Json.Linq
open Newtonsoft.Json

type Record1 = { Name: string; Value: int }
type Record2 = { Name: string; Position: int }
type InlayHintData = { TextDocument: TextDocumentIdentifier; Range: Range }

/// Note: By default private fields don't get serialized
/// That can be changed by either a custom ContractResolver
/// or annotating all private fields with `JsonPropertyAttribute`.
///
/// The latter is used here
///
///
/// But this cannot be deserialized again because:
/// > Unable to find a constructor to use for type [...]
///
/// Solvable with custom Converter, but not worth for LSP
/// -> deserialization of private records is not supported
/// And this record is just kept as reminder of this limitation
///
/// Considering Serialization is used for communicating with LSP client (public API),
/// this is not really an issue.
type private PrivateRecord =
  { [<JsonProperty>]
    Data: string
    [<JsonProperty>]
    Value: int }

[<ErasedUnion>]
type EU2 = EU2 of string * int

[<RequireQualifiedAccess>]
type NoFields =
  | First
  | Second
  | Third

[<RequireQualifiedAccess>]
[<ErasedUnion>]
[<Struct>]
type StructEU =
  | First of Number: int
  | Second of Name: string


let private serializationTests =
  testList
    "(de)serialization"
    [ let thereAndBackAgain (input: 'a) : 'a = input |> serialize |> deserialize

      let testThereAndBackAgain input =
        let output = thereAndBackAgain input
        Expect.equal output input "Input -> serialize -> deserialize should be Input again"

      testList
        "U2"
        [ testCase "can (de)serialize U2<int,string>.First"
          <| fun _ ->
               let input: U2<int, string> = U2.First 42
               testThereAndBackAgain input
          testCase "can (de)serialize U2<int,string>.Second"
          <| fun _ ->
               let input: U2<int, string> = U2.Second "foo"
               testThereAndBackAgain input
          testCase "deserialize to first type match"
          <| fun _ ->
               // Cannot distinguish between same type -> pick first
               let input: U2<int, int> = U2.Second 42
               let output = thereAndBackAgain input
               Expect.notEqual output input "First matching type gets matched"
          testCase "deserialize Second int to first float"
          <| fun _ ->
               // Cannot distinguish between float and int
               let input: U2<float, int> = U2.Second 42
               let output = thereAndBackAgain input
               Expect.notEqual output input "First matching type gets matched"

          testCase "can (de)serialize Record1 in U2<Record1, int>"
          <| fun _ ->
               let input: U2<Record1, int> = U2.First { Record1.Name = "foo"; Value = 42 }
               testThereAndBackAgain input

          testCase "can (de)serialize Record1 in U2<int, Record1>"
          <| fun _ ->
               let input: U2<int, Record1> = U2.Second { Record1.Name = "foo"; Value = 42 }
               testThereAndBackAgain input

          testCase "can (de)serialize Record1 in U2<Record1, Record2>"
          <| fun _ ->
               let input: U2<Record1, Record2> = U2.First { Record1.Name = "foo"; Value = 42 }
               testThereAndBackAgain input

          testCase "deserialize to first complex match"
          <| fun _ ->
               // Because of `serializer.MissingMemberHandling = MissingMemberHandling.Ignore`:
               // Cannot distinguish Record1 from Record2 (-> missing fields get filled with default)
               // -> There can only be one complex type in `U2`, the other one must be a basic type!
               let input: U2<Record2, Record1> = U2.Second { Record1.Name = "foo"; Value = 42 }
               let output = thereAndBackAgain input
               Expect.notEqual output input "First complex type gets matched" ]

      testList
        "ErasedUnionConverter"
        [
          // most tests in `U2`
          testCase "cannot serialize case with more than one field"
          <| fun _ ->
               let input = EU2("foo", 42)

               Expect.throws
                 (fun _ ->
                   serialize input
                   |> fun t -> printfn "%A" (t.ToString())
                   |> ignore)
                 "ErasedUnion with multiple fields should not serializable"
          testCase "can (de)serialize struct union"
          <| fun _ ->
               let input = StructEU.Second "foo"
               testThereAndBackAgain input ]

      testList
        "SingleCaseUnionConverter"
        [ testCase "can (de)serialize union with all zero field cases"
          <| fun _ ->
               let input = NoFields.Second
               testThereAndBackAgain input ]

      testList
        (nameof InlayHint)
        [
          // Life of InlayHint:
          // * output of `textDocument/inlayHint` (`InlayHint[]`)
          // * input of `inlayHint/resolve`
          // * output of `inlayHint/resolve`
          // -> must be serializable as well as deserializable
          testCase "can (de)serialize minimal InlayHint"
          <| fun _ ->
               let theInlayHint: InlayHint =
                 { Label = InlayHintLabel.String "test"
                   Position = { Line = 0; Character = 0 }
                   Kind = None
                   TextEdits = None
                   Tooltip = None
                   PaddingLeft = None
                   PaddingRight = None
                   Data = None }

               testThereAndBackAgain theInlayHint
          testCase "can roundtrip InlayHint with all fields (simple)"
          <| fun _ ->
               let theInlayHint: InlayHint =
                 { Label = InlayHintLabel.String "test"
                   Position = { Line = 5; Character = 10 }
                   Kind = Some InlayHintKind.Parameter
                   TextEdits =
                     Some [| { Range = { Start = { Line = 5; Character = 10 }; End = { Line = 6; Character = 5 } }
                               NewText = "foo bar" }
                             { Range = { Start = { Line = 4; Character = 0 }; End = { Line = 5; Character = 2 } }
                               NewText = "baz" } |]
                   Tooltip = Some(InlayHintTooltip.String "tooltipping")
                   PaddingLeft = Some true
                   PaddingRight = Some false
                   Data = Some(JToken.FromObject "some data") }

               testThereAndBackAgain theInlayHint
          testCase "can keep Data with JToken"
          <| fun _ ->
               // JToken doesn't use structural equality
               // -> Expecto equal check fails even when same content in complex JToken
               let data =
                 { InlayHintData.TextDocument = { Uri = "..." }
                   Range = { Start = { Line = 5; Character = 7 }; End = { Line = 5; Character = 10 } } }

               let theInlayHint: InlayHint =
                 { Label = InlayHintLabel.String "test"
                   Position = { Line = 0; Character = 0 }
                   Kind = None
                   TextEdits = None
                   Tooltip = None
                   PaddingLeft = None
                   PaddingRight = None
                   Data = Some(JToken.FromObject data) }

               let output = thereAndBackAgain theInlayHint

               let outputData =
                 output.Data
                 |> Option.map (fun t -> t.ToObject<InlayHintData>())

               Expect.equal outputData (Some data) "Data should not change"
          testCase "can roundtrip InlayHint with all fields (complex)"
          <| fun _ ->
               let theInlayHint: InlayHint =
                 { Label =
                     InlayHintLabel.Parts [| { InlayHintLabelPart.Value = "1st label"
                                               Tooltip = Some(InlayHintTooltip.String "1st label tooltip")
                                               Location = Some { Uri = "1st"; Range = mkRange' (1, 2) (3, 4) }
                                               Command = None }
                                             { Value = "2nd label"
                                               Tooltip = Some(InlayHintTooltip.String "1st label tooltip")
                                               Location = Some { Uri = "2nd"; Range = mkRange' (5, 8) (10, 9) }
                                               Command =
                                                 Some { Title = "2nd command"; Command = "foo"; Arguments = None } }
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

               testThereAndBackAgain theInlayHint ] ]

let tests = testList "LSP" [ serializationTests ]
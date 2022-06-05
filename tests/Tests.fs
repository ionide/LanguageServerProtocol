module Ionide.LanguageServerProtocol.Tests.Tests

open System
open Expecto
open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Tests
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open Ionide.LanguageServerProtocol.JsonRpc

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

type AllRequired = { RequiredName: string; RequiredValue: int }
type OneOptional = { RequiredName: string; OptionalValue: int option }
type AllOptional = { OptionalName: string option; OptionalValue: int option }

let private serializationTests =
  testList
    "(de)serialization"
    [ let mkLower (str: string) = sprintf "%c%s" (Char.ToLowerInvariant str[0]) (str.Substring(1))

      /// Note: changes first letter into lower case
      let removeProperty (name: string) (json: JToken) =
        let prop = (json :?> JObject).Property(name |> mkLower)
        prop.Remove()
        json

      /// Note: changes first letter into lower case
      let addProperty (name: string) (value: 'a) (json: JToken) =
        let jObj = json :?> JObject
        jObj.Add(JProperty(name |> mkLower, value))
        json

      let tryGetProperty (name: string) (json: JToken) =
        let jObj = json :?> JObject
        jObj.Property(name |> mkLower) |> Option.ofObj

      let logJson (json: JToken) =
        printfn $"%s{json.ToString()}"
        json

      let thereAndBackAgain (input: 'a) : 'a = input |> serialize |> deserialize

      let testThereAndBackAgain input =
        let output = thereAndBackAgain input
        Expect.equal output input "Input -> serialize -> deserialize should be Input again"

      testList
        "Optional & Required Fields"
        [ testList
            "Two Required"
            [ testCase "fails when required field is not given"
              <| fun _ ->
                   let input = { AllRequired.RequiredName = "foo"; RequiredValue = 42 }

                   let json =
                     serialize input
                     |> removeProperty (nameof input.RequiredValue)

                   Expect.throws
                     (fun _ -> json |> deserialize<AllRequired> |> ignore)
                     "Should fail without all required fields"
              testCase "doesn't fail with additional fields"
              <| fun _ ->
                   let input = { AllRequired.RequiredName = "foo"; RequiredValue = 42 }
                   let json = serialize input |> addProperty "myProp" "hello world"

                   json |> deserialize<AllRequired> |> ignore ]

          testList
            "One Required, One Optional"
            [ testCase "doesn't fail when optional field not given"
              <| fun _ ->
                   let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> removeProperty (nameof input.OptionalValue)

                   json |> deserialize<OneOptional> |> ignore
              testCase "fails when required field is not given"
              <| fun _ ->
                   let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> removeProperty (nameof input.RequiredName)

                   Expect.throws
                     (fun _ -> json |> deserialize<AllRequired> |> ignore)
                     "Should fail without all required fields"

              testCase "doesn't fail with all fields"
              <| fun _ ->
                   let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }
                   let json = serialize input
                   json |> deserialize<OneOptional> |> ignore
              testCase "doesn't fail with additional properties"
              <| fun _ ->
                   let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> addProperty "foo" "bar"
                     |> addProperty "baz" 42

                   json |> deserialize<OneOptional> |> ignore ]

          testList
            "Two Optional"
            [ testCase "doesn't fail when one optional field not given"
              <| fun _ ->
                   let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> removeProperty (nameof input.OptionalValue)

                   json |> deserialize<AllOptional> |> ignore
              testCase "doesn't fail when all optional fields not given"
              <| fun _ ->
                   let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> removeProperty (nameof input.OptionalName)
                     |> removeProperty (nameof input.OptionalValue)

                   json |> deserialize<AllOptional> |> ignore
              testCase "doesn't emit optional missing fields"
              <| fun _ ->
                   let input = { AllOptional.OptionalName = None; OptionalValue = None }
                   let json = serialize input
                   Expect.isEmpty (json.Children()) "There should be no properties"

              testCase "doesn't fail when all fields given"
              <| fun _ ->
                   let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }
                   let json = serialize input
                   json |> deserialize<AllOptional> |> ignore
              testCase "doesn't fail when additional properties"
              <| fun _ ->
                   let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> addProperty "foo" "bar"
                     |> addProperty "baz" 42

                   json |> deserialize<AllOptional> |> ignore
              testCase "doesn't fail when no field but additional properties"
              <| fun _ ->
                   let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

                   let json =
                     serialize input
                     |> removeProperty (nameof input.OptionalName)
                     |> removeProperty (nameof input.OptionalValue)
                     |> addProperty "foo" "bar"
                     |> addProperty "baz" 42

                   json |> deserialize<AllOptional> |> ignore ] ]

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

          testCase "can deserialize to correct record"
          <| fun _ ->
               // Note: only possible because Records aren't compatible with each other.
               // If Record2.Position optional -> gets deserialized to `Record2` because first match
               let input: U2<Record2, Record1> = U2.Second { Record1.Name = "foo"; Value = 42 }
               testThereAndBackAgain input
          testList
            "optional"
            [ testCase "doesn't emit optional missing member"
              <| fun _ ->
                   let input: U2<string, OneOptional> =
                     U2.Second { OneOptional.RequiredName = "foo"; OptionalValue = None }

                   let json = serialize input :?> JObject
                   Expect.hasLength (json.Properties()) 1 "There should be just one property"
                   let prop = json.Property("requiredName")
                   Expect.equal (prop.Value.ToString()) "foo" "Required Property should have correct value"

              testCase "can deserialize with optional missing member"
              <| fun _ ->
                   let input: U2<string, OneOptional> =
                     U2.Second { OneOptional.RequiredName = "foo"; OptionalValue = None }

                   testThereAndBackAgain input
              testCase "can deserialize with optional existing member"
              <| fun _ ->
                   let input: U2<string, OneOptional> =
                     U2.Second { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

                   testThereAndBackAgain input
              testCase "fails with missing required value"
              <| fun _ ->
                   let json = JToken.Parse """{"optionalValue": 42}"""

                   Expect.throws
                     (fun _ -> json |> deserialize<OneOptional> |> ignore)
                     "Should fail without required member"

              ] 

          testList "string vs int" [
            testCase "can deserialize int to U2<int,string>" <| fun _ ->
              let input: U2<int, string> = U2.First 42
              testThereAndBackAgain input
            testCase "can deserialize string to U2<int,string>" <| fun _ ->
              let input: U2<int, string> = U2.Second "foo"
              testThereAndBackAgain input
            testCase "can deserialize 42 string to U2<int,string>" <| fun _ ->
              let input: U2<int, string> = U2.Second "42"
              testThereAndBackAgain input

            testCase "can deserialize int to U2<string, int>" <| fun _ ->
              let input: U2<string, int> = U2.Second 42
              testThereAndBackAgain input
            testCase "can deserialize string to U2<string, string>" <| fun _ ->
              let input: U2<string, int> = U2.First "foo"
              testThereAndBackAgain input
            testCase "can deserialize 42 string to U2<string,int>" <| fun _ ->
              let input: U2<string, int> = U2.First "42"
              testThereAndBackAgain input
          ]
          testList "string vs bool" [
            testCase "can deserialize bool to U2<bool,string>" <| fun _ ->
              let input: U2<bool, string> = U2.First true
              testThereAndBackAgain input
            testCase "can deserialize string to U2<bool,string>" <| fun _ ->
              let input: U2<bool, string> = U2.Second "foo"
              testThereAndBackAgain input
            testCase "can deserialize true string to U2<bool,string>" <| fun _ ->
              let input: U2<bool, string> = U2.Second "true"
              testThereAndBackAgain input

            testCase "can deserialize bool true to U2<string, bool>" <| fun _ ->
              let input: U2<string, bool> = U2.Second true
              testThereAndBackAgain input
            testCase "can deserialize bool false to U2<string, bool>" <| fun _ ->
              let input: U2<string, bool> = U2.Second false
              testThereAndBackAgain input
            testCase "can deserialize string to U2<string, string>" <| fun _ ->
              let input: U2<string, bool> = U2.First "foo"
              testThereAndBackAgain input
            testCase "can deserialize true string to U2<string,bool>" <| fun _ ->
              let input: U2<string, bool> = U2.First "true"
              testThereAndBackAgain input
          ]
        ]

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
        "JsonProperty"
        [ testCase "keep null when serializing VersionedTextDocumentIdentifier"
          <| fun _ ->
               let textDoc = { VersionedTextDocumentIdentifier.Uri = "..."; Version = None }
               let json = textDoc |> serialize :?> JObject
               let prop = json.Property("version")
               let value = prop.Value
               Expect.equal (value.Type) (JTokenType.Null) "Version should be null"

               let prop =
                 json
                 |> tryGetProperty (nameof textDoc.Version)
                 |> Flip.Expect.wantSome "Property Version should exist"

               Expect.equal prop.Value.Type (JTokenType.Null) "Version should be null"
          testCase "can deserialize null Version in VersionedTextDocumentIdentifier"
          <| fun _ ->
               let textDoc = { VersionedTextDocumentIdentifier.Uri = "..."; Version = None }
               testThereAndBackAgain textDoc

          testCase "serialize to name specified in JsonProperty in Response"
          <| fun _ ->
               let response: Response = { Version = "123"; Id = None; Error = None; Result = None }
               let json = response |> serialize
               // Version -> jsonrpc
               Expect.isNone
                 (json |> tryGetProperty (nameof response.Version))
                 "Version should exist, but instead as jsonrpc"

               Expect.isSome (json |> tryGetProperty "jsonrpc") "jsonrcp should exist because of Version"
               // Id & Error optional -> not in json
               Expect.isNone (json |> tryGetProperty (nameof response.Id)) "None Id shouldn't be in json"
               Expect.isNone (json |> tryGetProperty (nameof response.Error)) "None Error shouldn't be in json"
               // Result even when null/None
               let prop =
                 json
                 |> tryGetProperty (nameof response.Result)
                 |> Flip.Expect.wantSome "Result should exist even when null/None"

               Expect.equal prop.Value.Type (JTokenType.Null) "Result should be null"
          testCase "can (de)serialize empty response"
          <| fun _ ->
               let response: Response = { Version = "123"; Id = None; Error = None; Result = None }
               testThereAndBackAgain response
          testCase "can (de)serialize Response.Result"
          <| fun _ ->
               let response: Response =
                 { Version = "123"
                   Id = None
                   Error = None
                   Result = Some(JToken.Parse "\"some result\"") }

               testThereAndBackAgain response
          testCase "can (de)serialize Result when Error is None"
          <| fun _ ->
               // Note: It's either `Error` or `Result`, but not both together
               let response: Response =
                 { Version = "123"
                   Id = Some 42
                   Error = None
                   Result = Some(JToken.Parse "\"some result\"") }

               testThereAndBackAgain response
          testCase "can (de)serialize Error when error is Some"
          <| fun _ ->
               let response: Response =
                 { Version = "123"
                   Id = Some 42
                   Error = Some { Code = 13; Message = "oh no"; Data = Some(JToken.Parse "\"some data\"") }
                   Result = None }

               testThereAndBackAgain response
          testCase "doesn't serialize Result when Error is Some"
          <| fun _ ->
               let response: Response =
                 { Version = "123"
                   Id = Some 42
                   Error = Some { Code = 13; Message = "oh no"; Data = Some(JToken.Parse "\"some data\"") }
                   Result = Some(JToken.Parse "\"some result\"") }

               let output = thereAndBackAgain response
               Expect.isSome output.Error "Error should be serialized"
               Expect.isNone output.Result "Result should not be serialized when Error is Some" ]

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
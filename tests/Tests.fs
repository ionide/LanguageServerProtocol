module Ionide.LanguageServerProtocol.Tests.Tests

open System
open Expecto
open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Tests
open System.Text.Json
open System.Text.Json.Nodes
open System.Text.Json.Serialization
open Ionide.LanguageServerProtocol.JsonRpc
open System.Collections.Generic

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
type private PrivateRecord = {
  [<JsonPropertyName("data")>]
  Data: string
  [<JsonPropertyName("value")>]
  Value: int
}

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

type MutableField = { Name: string; mutable Value: int }

type RequiredAttributeFields() =
  member val NoProperty: string = null with get, set
  member val NoPropertyOption: string option = None with get, set

  [<JsonRequired>]
  member val DisallowNull: string = null with get, set

  [<JsonRequired>]
  member val Always: string option = None with get, set

  member val AllowNull: string = null with get, set

type ExtensionDataField() =
  member val Name: string = null with get, set
  member val Value: string option = None with get, set

  [<JsonExtensionData>]
  member val AdditionalData: Dictionary<string, JsonElement> = Dictionary() with get, set

let private serializationTests =
  testList "(de)serialization" [

    /// Decapitalizes first letter
    let mkLower (str: string) = sprintf "%c%s" (Char.ToLowerInvariant str[0]) (str.Substring(1))

    /// Note: changes first letter into lower case
    let removeProperty (name: string) (json: JsonElement) =
      let node = System.Text.Json.Nodes.JsonNode.Parse(json.GetRawText()).AsObject()

      node.Remove(
        name
        |> mkLower
      )
      |> ignore

      JsonSerializer.Deserialize<JsonElement>(node.ToJsonString())

    /// Note: changes first letter into lower case
    let addProperty (name: string) (value: 'a) (json: JsonElement) =
      let node = System.Text.Json.Nodes.JsonNode.Parse(json.GetRawText()).AsObject()

      node.Add(
        name
        |> mkLower,
        System.Text.Json.Nodes.JsonValue.Create(value)
      )

      JsonSerializer.Deserialize<JsonElement>(node.ToJsonString())

    let tryGetProperty (name: string) (json: JsonElement) =
      match
        json.TryGetProperty(
          name
          |> mkLower
        )
      with
      | true, prop -> Some prop
      | _ -> None

    let logJson (json: JsonElement) =
      printfn $"%s{json.GetRawText()}"
      json

    let thereAndBackAgain (input: 'a) : 'a =
      input
      |> serialize
      |> deserialize

    let testThereAndBackAgain input =
      let output = thereAndBackAgain input
      // Compare via JSON round-trip to handle JsonElement which lacks structural equality
      let inputJson = (serialize input).GetRawText()
      let outputJson = (serialize output).GetRawText()
      Expect.equal outputJson inputJson "Input -> serialize -> deserialize should be Input again"

    testList "mutable field" [
      // Newtonsoft.Json serializes all public fields
      // F# emits a public field for mutable data:
      // `{ mutable Data: int }`
      // -> public property `Data` & public field `Data@`
      // -> Data gets serialized twice
      // Solution: exclude fields with trailing `@` (-> consider private)
      testCase "doesn't serialize backing field"
      <| fun _ ->
        let o: MutableField = { MutableField.Name = "foo"; Value = 42 }

        let json =
          o
          |> serialize

        let props =
          json.EnumerateObject()
          |> Seq.map (fun p -> p.Name)

        let expected = [
          "name"
          "value"
        ]

        Expect.sequenceEqual props expected "backing field should not get serialized"
    ]

    testList "ExtensionData" [
      let mkExtensionDataField name value additionalData =
        let o = ExtensionDataField()
        o.Name <- name
        o.Value <- value
        let d = Dictionary<string, JsonElement>()

        additionalData
        |> Map.iter (fun k v -> d.[k] <- v)

        o.AdditionalData <- d
        o

      let testThereAndBackAgain (input: ExtensionDataField) =
        let output = thereAndBackAgain input
        // Dictionary isn't structural comparable -> compare fields separately
        Expect.equal output.Name input.Name "Name should match"
        Expect.equal output.Value input.Value "Value should match"

        let toSorted (d: Dictionary<string, JsonElement>) =
          d
          |> Seq.map (fun kv -> kv.Key, kv.Value.GetRawText())
          |> Seq.sort
          |> Seq.toList

        Expect.equal (toSorted output.AdditionalData) (toSorted input.AdditionalData) "AdditionalData should match"

      testCase "can (de)serialize with all fields and additional data"
      <| fun _ ->
        let input =
          mkExtensionDataField
            "foo"
            (Some "bar")
            (Map.ofList [
              "alpha", JsonSerializer.SerializeToElement("lorem", lspSerializerOptions)
              "beta", JsonSerializer.SerializeToElement("ipsum", lspSerializerOptions)
              "gamma", JsonSerializer.SerializeToElement("dolor", lspSerializerOptions)
            ])

        testThereAndBackAgain input

      testCase "can (de)serialize with all fields and no additional data"
      <| fun _ ->
        let input = mkExtensionDataField "foo" (Some "bar") Map.empty
        testThereAndBackAgain input

      testCase "can (de)serialize when just required fields"
      <| fun _ ->
        let input = mkExtensionDataField "foo" None Map.empty
        testThereAndBackAgain input

      testCase "can (de)serialize with required fields and additional data"
      <| fun _ ->
        let input =
          mkExtensionDataField
            "foo"
            None
            (Map.ofList [
              "alpha", JsonSerializer.SerializeToElement("lorem", lspSerializerOptions)
              "beta", JsonSerializer.SerializeToElement("ipsum", lspSerializerOptions)
              "gamma", JsonSerializer.SerializeToElement("dolor", lspSerializerOptions)
            ])

        testThereAndBackAgain input

      testCase "uses default when required field is not given"
      <| fun _ ->
        // STJ does not throw on missing non-option fields; Name defaults to null
        let json = JsonSerializer.Deserialize<JsonElement>("""{"value":"bar","alpha":"lorem","beta":"ipsum"}""")

        let output =
          json
          |> deserialize<ExtensionDataField>

        Expect.equal output.Name null "Missing Name should default to null"

      testCase "serializes items in AdditionalData as properties"
      <| fun _ ->
        let input =
          mkExtensionDataField
            "foo"
            (Some "bar")
            (Map.ofList [
              "alpha", JsonSerializer.SerializeToElement("lorem", lspSerializerOptions)
              "beta", JsonSerializer.SerializeToElement("ipsum", lspSerializerOptions)
              "gamma", JsonSerializer.SerializeToElement("dolor", lspSerializerOptions)
            ])

        let json =
          input
          |> serialize

        let expected = """{"name":"foo","value":"bar","alpha":"lorem","beta":"ipsum","gamma":"dolor"}"""
        Expect.equal (json.GetRawText()) expected "Items in AdditionalData should be normal properties"

      testCase "AdditionalData is not null when no additional properties"
      <| fun _ ->
        let json = JsonSerializer.Deserialize<JsonElement>("""{"name":"foo"}""")

        let output =
          json
          |> deserialize<ExtensionDataField>

        Expect.isNotNull output.AdditionalData "Empty AdditionalData should not be null"
    ]

    testList "capitalization" [
      testCase "changes lower cases start in F# to lower case in JSON"
      <| fun _ ->
        let o = {| Name = "foo"; SomeValue = 42 |}
        let json = serialize o

        let props =
          json.EnumerateObject()
          |> Seq.map (fun p -> p.Name)
          |> Seq.toList

        Expect.contains props "name" "name should be lower case start"
        Expect.contains props "someValue" "someValue should be lowercase start, but keep upper case 2nd word"

      testCase "keeps capitalization of Map"
      <| fun _ ->
        let keys =
          [|
            "foo"
            "Bar"
            "BAZ"
            "SomeValue"
            "anotherValue"
            "l"
            "P"
          |]
          |> Array.sort

        let m =
          keys
          |> Seq.mapi (fun i k -> (k, i))
          |> Map.ofSeq

        let json = serialize m

        let propNames =
          json.EnumerateObject()
          |> Seq.map (fun p -> p.Name)
          |> Seq.toArray
          |> Array.sort

        Expect.equal propNames keys "Property names from Map should be unchanged"
      testCase "can deserialize Map back"
      <| fun _ ->
        let m =
          [|
            "foo"
            "Bar"
            "BAZ"
            "SomeValue"
            "anotherValue"
            "l"
            "P"
          |]
          |> Seq.mapi (fun i k -> (k, i))
          |> Map.ofSeq

        testThereAndBackAgain m
    ]

    testList "Optional & Required Fields" [
      testList "Two Required" [
        testCase "uses default when required field is not given"
        <| fun _ ->
          // STJ does not throw on missing non-option fields; it uses the type default
          let input = { AllRequired.RequiredName = "foo"; RequiredValue = 42 }

          let json =
            serialize input
            |> removeProperty (nameof input.RequiredValue)

          let output =
            json
            |> deserialize<AllRequired>

          Expect.equal output.RequiredValue 0 "Missing int field should default to 0"
        testCase "doesn't fail with additional fields"
        <| fun _ ->
          let input = { AllRequired.RequiredName = "foo"; RequiredValue = 42 }

          let json =
            serialize input
            |> addProperty "myProp" "hello world"

          json
          |> deserialize<AllRequired>
          |> ignore
      ]

      testList "One Required, One Optional" [
        testCase "doesn't fail when optional field not given"
        <| fun _ ->
          let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> removeProperty (nameof input.OptionalValue)

          json
          |> deserialize<OneOptional>
          |> ignore
        testCase "uses default when required field is not given"
        <| fun _ ->
          // STJ does not throw on missing non-option fields; it uses the type default
          let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> removeProperty (nameof input.RequiredName)

          let output =
            json
            |> deserialize<OneOptional>

          Expect.equal output.RequiredName null "Missing string field should default to null"

        testCase "doesn't fail with all fields"
        <| fun _ ->
          let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }
          let json = serialize input

          json
          |> deserialize<OneOptional>
          |> ignore
        testCase "doesn't fail with additional properties"
        <| fun _ ->
          let input = { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> addProperty "foo" "bar"
            |> addProperty "baz" 42

          json
          |> deserialize<OneOptional>
          |> ignore
      ]

      testList "Two Optional" [
        testCase "doesn't fail when one optional field not given"
        <| fun _ ->
          let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> removeProperty (nameof input.OptionalValue)

          json
          |> deserialize<AllOptional>
          |> ignore
        testCase "doesn't fail when all optional fields not given"
        <| fun _ ->
          let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> removeProperty (nameof input.OptionalName)
            |> removeProperty (nameof input.OptionalValue)

          json
          |> deserialize<AllOptional>
          |> ignore
        testCase "doesn't emit optional missing fields"
        <| fun _ ->
          let input = { AllOptional.OptionalName = None; OptionalValue = None }
          let json = serialize input
          Expect.isEmpty (json.EnumerateObject()) "There should be no properties"

        testCase "doesn't fail when all fields given"
        <| fun _ ->
          let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }
          let json = serialize input

          json
          |> deserialize<AllOptional>
          |> ignore
        testCase "doesn't fail when additional properties"
        <| fun _ ->
          let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> addProperty "foo" "bar"
            |> addProperty "baz" 42

          json
          |> deserialize<AllOptional>
          |> ignore
        testCase "doesn't fail when no field but additional properties"
        <| fun _ ->
          let input = { AllOptional.OptionalName = Some "foo"; OptionalValue = Some 42 }

          let json =
            serialize input
            |> removeProperty (nameof input.OptionalName)
            |> removeProperty (nameof input.OptionalValue)
            |> addProperty "foo" "bar"
            |> addProperty "baz" 42

          json
          |> deserialize<AllOptional>
          |> ignore
      ]

      testList "Existing JsonProperty.Required" [
        testCase "all according to Required Attribute should not fail"
        <| fun _ ->
          let json =
            JsonSerializer.Deserialize<JsonElement>(
              """{"noProperty":"lorem","noPropertyOption":"ipsum","disallowNull":"dolor","always":"sit","allowNull":"amet"}"""
            )

          json
          |> deserialize<RequiredAttributeFields>
          |> ignore

        testCase "No property succeeds when not provided (STJ requires explicit JsonRequired)"
        <| fun _ ->
          // Unlike Newtonsoft's default "required" behavior, STJ only enforces presence for
          // [<JsonRequired>] fields. NoProperty has no [<JsonRequired>] -> a missing value
          // defaults to null instead of failing.
          let json =
            JsonSerializer.Deserialize<JsonElement>(
              """{"noPropertyOption":"ipsum","disallowNull":"dolor","always":"sit","allowNull":"amet"}"""
            )

          let output =
            json
            |> deserialize<RequiredAttributeFields>

          Expect.equal output.NoProperty null "Missing non-required field should default to null"

        testCase "No property on option succeeds when not provided"
        <| fun _ ->
          let json =
            JsonSerializer.Deserialize<JsonElement>(
              """{"noProperty":"lorem","disallowNull":"dolor","always":"sit","allowNull":"amet"}"""
            )

          json
          |> deserialize<RequiredAttributeFields>
          |> ignore

        testCase "DisallowNull with null value is accepted by STJ"
        <| fun _ ->
          // STJ [<JsonRequired>] only enforces presence, not non-null (unlike Newtonsoft Required.DisallowNull)
          let json =
            JsonSerializer.Deserialize<JsonElement>(
              """{"noProperty":"lorem","noPropertyOption":"ipsum","disallowNull":null,"always":"sit","allowNull":"amet"}"""
            )

          let output =
            json
            |> deserialize<RequiredAttributeFields>

          Expect.equal output.DisallowNull null "STJ JsonRequired allows null values"

        testCase "Option with Always fails when not present"
        <| fun _ ->
          let json =
            JsonSerializer.Deserialize<JsonElement>(
              """{"noProperty":"lorem","noPropertyOption":"ipsum","disallowNull":"dolor","allowNull":"amet"}"""
            )

          Expect.throws
            (fun _ ->
              json
              |> deserialize<RequiredAttributeFields>
              |> ignore
            )
            "Always is required despite Option"

        testCase "AllowNull doesn't fail when null"
        <| fun _ ->
          let json =
            JsonSerializer.Deserialize<JsonElement>(
              """{"noProperty":"lorem","noPropertyOption":"ipsum","disallowNull":"dolor","always":"sit","allowNull":null}"""
            )

          json
          |> deserialize<RequiredAttributeFields>
          |> ignore
      ]
    ]

    testList "U2" [
      testCase "can (de)serialize U2<int,string>.First"
      <| fun _ ->
        let input: U2<int, string> = U2.C1 42
        testThereAndBackAgain input
      testCase "can (de)serialize U2<int,string>.Second"
      <| fun _ ->
        let input: U2<int, string> = U2.C2 "foo"
        testThereAndBackAgain input
      testCase "deserialize to first type match"
      <| fun _ ->
        // Cannot distinguish between same type -> pick first
        let input: U2<int, int> = U2.C2 42
        let output = thereAndBackAgain input
        Expect.notEqual output input "First matching type gets matched"
      testCase "deserialize Second int to first float"
      <| fun _ ->
        // Cannot distinguish between float and int
        let input: U2<float, int> = U2.C2 42
        let output = thereAndBackAgain input
        Expect.notEqual output input "First matching type gets matched"

      testCase "can (de)serialize Record1 in U2<Record1, int>"
      <| fun _ ->
        let input: U2<Record1, int> = U2.C1 { Record1.Name = "foo"; Value = 42 }
        testThereAndBackAgain input

      testCase "can (de)serialize Record1 in U2<int, Record1>"
      <| fun _ ->
        let input: U2<int, Record1> = U2.C2 { Record1.Name = "foo"; Value = 42 }
        testThereAndBackAgain input

      testCase "can (de)serialize Record1 in U2<Record1, Record2>"
      <| fun _ ->
        let input: U2<Record1, Record2> = U2.C1 { Record1.Name = "foo"; Value = 42 }
        testThereAndBackAgain input

      testCase "can deserialize to correct record"
      <| fun _ ->
        // Note: only possible because Records aren't compatible with each other.
        // If Record2.Position optional -> gets deserialized to `Record2` because first match
        let input: U2<Record2, Record1> = U2.C2 { Record1.Name = "foo"; Value = 42 }
        testThereAndBackAgain input
      testList "optional" [
        testCase "doesn't emit optional missing member"
        <| fun _ ->
          let input: U2<string, OneOptional> = U2.C2 { OneOptional.RequiredName = "foo"; OptionalValue = None }

          let json = serialize input

          let props =
            json.EnumerateObject()
            |> Seq.toList

          Expect.hasLength props 1 "There should be just one property"
          let prop = json.GetProperty("requiredName")
          Expect.equal (prop.GetString()) "foo" "Required Property should have correct value"

        testCase "can deserialize with optional missing member"
        <| fun _ ->
          let input: U2<string, OneOptional> = U2.C2 { OneOptional.RequiredName = "foo"; OptionalValue = None }

          testThereAndBackAgain input
        testCase "can deserialize with optional existing member"
        <| fun _ ->
          let input: U2<string, OneOptional> = U2.C2 { OneOptional.RequiredName = "foo"; OptionalValue = Some 42 }

          testThereAndBackAgain input
        testCase "uses default when required value is missing"
        <| fun _ ->
          // STJ does not throw on missing non-option fields; RequiredName defaults to null
          let json = JsonSerializer.Deserialize<JsonElement>("""{"optionalValue": 42}""")

          let output =
            json
            |> deserialize<OneOptional>

          Expect.equal output.RequiredName null "Missing RequiredName should default to null"

      ]

      testList "string vs int" [
        testCase "can deserialize int to U2<int,string>"
        <| fun _ ->
          let input: U2<int, string> = U2.C1 42
          testThereAndBackAgain input
        testCase "can deserialize string to U2<int,string>"
        <| fun _ ->
          let input: U2<int, string> = U2.C2 "foo"
          testThereAndBackAgain input
        testCase "can deserialize 42 string to U2<int,string>"
        <| fun _ ->
          let input: U2<int, string> = U2.C2 "42"
          testThereAndBackAgain input

        testCase "can deserialize int to U2<string, int>"
        <| fun _ ->
          let input: U2<string, int> = U2.C2 42
          testThereAndBackAgain input
        testCase "can deserialize string to U2<string, string>"
        <| fun _ ->
          let input: U2<string, int> = U2.C1 "foo"
          testThereAndBackAgain input
        testCase "can deserialize 42 string to U2<string,int>"
        <| fun _ ->
          let input: U2<string, int> = U2.C1 "42"
          testThereAndBackAgain input
      ]
      testList "string vs bool" [
        testCase "can deserialize bool to U2<bool,string>"
        <| fun _ ->
          let input: U2<bool, string> = U2.C1 true
          testThereAndBackAgain input
        testCase "can deserialize string to U2<bool,string>"
        <| fun _ ->
          let input: U2<bool, string> = U2.C2 "foo"
          testThereAndBackAgain input
        testCase "can deserialize true string to U2<bool,string>"
        <| fun _ ->
          let input: U2<bool, string> = U2.C2 "true"
          testThereAndBackAgain input

        testCase "can deserialize bool true to U2<string, bool>"
        <| fun _ ->
          let input: U2<string, bool> = U2.C2 true
          testThereAndBackAgain input
        testCase "can deserialize bool false to U2<string, bool>"
        <| fun _ ->
          let input: U2<string, bool> = U2.C2 false
          testThereAndBackAgain input
        testCase "can deserialize string to U2<string, string>"
        <| fun _ ->
          let input: U2<string, bool> = U2.C1 "foo"
          testThereAndBackAgain input
        testCase "can deserialize true string to U2<string,bool>"
        <| fun _ ->
          let input: U2<string, bool> = U2.C1 "true"
          testThereAndBackAgain input
      ]
    ]

    testList "ErasedUnionConverter" [
      // most tests in `U2`
      testCase "cannot serialize case with more than one field"
      <| fun _ ->
        let input = EU2("foo", 42)

        Expect.throws
          (fun _ ->
            serialize input
            |> fun t -> printfn "%A" (t.ToString())
            |> ignore
          )
          "ErasedUnion with multiple fields should not serializable"
      testCase "can (de)serialize struct union"
      <| fun _ ->
        let input = StructEU.Second "foo"
        testThereAndBackAgain input
    ]

    testList "SingleCaseUnionConverter" [
      testCase "can (de)serialize union with all zero field cases"
      <| fun _ ->
        let input = NoFields.Second
        testThereAndBackAgain input
    ]

    testList "JsonProperty" [
      testCase "keep null when serializing OptionalVersionedTextDocumentIdentifier"
      <| fun _ ->
        let textDoc = { OptionalVersionedTextDocumentIdentifier.Uri = "..."; Version = None }

        let json =
          textDoc
          |> serialize

        // STJ omits None options (WhenWritingNull), so version is absent
        Expect.isNone (tryGetProperty (nameof textDoc.Version) json) "None Version should not be present in JSON"
      testCase "can deserialize null Version in OptioanlVersionedTextDocumentIdentifier"
      <| fun _ ->
        let textDoc = { OptionalVersionedTextDocumentIdentifier.Uri = "..."; Version = None }
        testThereAndBackAgain textDoc

      testCase "serialize to name specified in JsonProperty in Response"
      <| fun _ ->
        let response: Response = { Version = "123"; Id = None; Error = None; Result = None }

        let json =
          response
          |> serialize
        // Version -> jsonrpc
        Expect.isNone
          (json
           |> tryGetProperty (nameof response.Version))
          "Version should not exist as 'version', but as 'jsonrpc'"

        Expect.isSome
          (json
           |> tryGetProperty "jsonrpc")
          "jsonrpc should exist because of Version"
        // Id & Error optional -> not in json
        Expect.isNone
          (json
           |> tryGetProperty (nameof response.Id))
          "None Id shouldn't be in json"

        Expect.isNone
          (json
           |> tryGetProperty (nameof response.Error))
          "None Error shouldn't be in json"
        // Result always present (even null/None) because JsonIgnoreCondition.Never
        let prop =
          json
          |> tryGetProperty (nameof response.Result)
          |> Flip.Expect.wantSome "Result should exist even when null/None"

        Expect.equal prop.ValueKind JsonValueKind.Null "Result should be null"
      testCase "can (de)serialize empty response"
      <| fun _ ->
        let response: Response = { Version = "123"; Id = None; Error = None; Result = None }
        testThereAndBackAgain response
      testCase "can (de)serialize Response.Result"
      <| fun _ ->
        let response: Response = {
          Version = "123"
          Id = None
          Error = None
          Result = Some(JsonSerializer.Deserialize<JsonElement>("\"some result\""))
        }

        testThereAndBackAgain response
      testCase "can (de)serialize Result when Error is None"
      <| fun _ ->
        // Note: It's either `Error` or `Result`, but not both together
        let response: Response = {
          Version = "123"
          Id = Some 42
          Error = None
          Result = Some(JsonSerializer.Deserialize<JsonElement>("\"some result\""))
        }

        testThereAndBackAgain response
      testCase "can (de)serialize Error when error is Some"
      <| fun _ ->
        let response: Response = {
          Version = "123"
          Id = Some 42
          Error =
            Some {
              Code = 13
              Message = "oh no"
              Data = Some(JsonSerializer.Deserialize<JsonElement>("\"some data\""))
            }
          Result = None
        }

        testThereAndBackAgain response
      testCase "doesn't serialize Result when Error is Some"
      <| fun _ ->
        let response: Response = {
          Version = "123"
          Id = Some 42
          Error =
            Some {
              Code = 13
              Message = "oh no"
              Data = Some(JsonSerializer.Deserialize<JsonElement>("\"some data\""))
            }
          Result = Some(JsonSerializer.Deserialize<JsonElement>("\"some result\""))
        }

        let output = thereAndBackAgain response
        Expect.isSome output.Error "Error should be serialized"
        Expect.isNone output.Result "Result should not be serialized when Error is Some"
    ]

    testList (nameof InlayHint) [
      // Life of InlayHint:
      // * output of `textDocument/inlayHint` (`InlayHint[]`)
      // * input of `inlayHint/resolve`
      // * output of `inlayHint/resolve`
      // -> must be serializable as well as deserializable
      testCase "can (de)serialize minimal InlayHint"
      <| fun _ ->
        let theInlayHint: InlayHint = {
          Label = U2.C1 "test"
          Position = { Line = 0u; Character = 0u }
          Kind = None
          TextEdits = None
          Tooltip = None
          PaddingLeft = None
          PaddingRight = None
          Data = None
        }

        testThereAndBackAgain theInlayHint
      testCase "can roundtrip InlayHint with all fields (simple)"
      <| fun _ ->
        let theInlayHint: InlayHint = {
          Label = U2.C1 "test"
          Position = { Line = 5u; Character = 10u }
          Kind = Some InlayHintKind.Parameter
          TextEdits =
            Some [|
              {
                Range = { Start = { Line = 5u; Character = 10u }; End = { Line = 6u; Character = 5u } }
                NewText = "foo bar"
              }
              {
                Range = { Start = { Line = 4u; Character = 0u }; End = { Line = 5u; Character = 2u } }
                NewText = "baz"
              }
            |]
          Tooltip = Some(U2.C1 "tooltipping")
          PaddingLeft = Some true
          PaddingRight = Some false
          Data = Some(LSPAny(JsonSerializer.SerializeToElement("some data", lspSerializerOptions)))
        }

        testThereAndBackAgain theInlayHint
      testCase "can keep Data with JsonElement"
      <| fun _ ->
        // JToken doesn't use structural equality
        // -> Expecto equal check fails even when same content in complex JToken
        let data = {
          InlayHintData.TextDocument = { Uri = "..." }
          Range = { Start = { Line = 5u; Character = 7u }; End = { Line = 5u; Character = 10u } }
        }

        let theInlayHint: InlayHint = {
          Label = U2.C1 "test"
          Position = { Line = 0u; Character = 0u }
          Kind = None
          TextEdits = None
          Tooltip = None
          PaddingLeft = None
          PaddingRight = None
          Data = Some(LSPAny(JsonSerializer.SerializeToElement(data, lspSerializerOptions)))
        }

        let output = thereAndBackAgain theInlayHint

        let outputData =
          output.Data
          |> Option.map (fun t -> JsonSerializer.Deserialize<InlayHintData>(t.JsonElement, lspSerializerOptions))

        Expect.equal outputData (Some data) "Data should not change"
      testCase "can roundtrip InlayHint with all fields (complex)"
      <| fun _ ->
        let theInlayHint: InlayHint = {
          Label =
            U2.C2 [|
              {
                InlayHintLabelPart.Value = "1st label"
                Tooltip = Some(U2.C1 "1st label tooltip")
                Location = Some { Uri = "1st"; Range = mkRange' (1u, 2u) (3u, 4u) }
                Command = None
              }
              {
                Value = "2nd label"
                Tooltip = Some(U2.C1 "1st label tooltip")
                Location = Some { Uri = "2nd"; Range = mkRange' (5u, 8u) (10u, 9u) }
                Command = Some { Title = "2nd command"; Command = "foo"; Arguments = None }
              }
              {
                InlayHintLabelPart.Value = "3rd label"
                Tooltip =
                  Some(
                    U2.C2 {
                      Kind = MarkupKind.Markdown
                      Value =
                        """
                                                          # Header
                                                          Description
                                                          * List 1
                                                          * List 2
                                                          """
                    }
                  )
                Location = Some { Uri = "3rd"; Range = mkRange' (1u, 2u) (3u, 4u) }
                Command = None
              }
            |]
          Position = { Line = 5u; Character = 10u }
          Kind = Some InlayHintKind.Type
          TextEdits =
            Some [|
              { Range = mkRange' (5u, 10u) (6u, 5u); NewText = "foo bar" }
              { Range = mkRange' (5u, 0u) (5u, 2u); NewText = "baz" }
            |]
          Tooltip = Some(U2.C2 { Kind = MarkupKind.PlainText; Value = "some tooltip" })
          PaddingLeft = Some true
          PaddingRight = Some false
          Data = Some(LSPAny(JsonSerializer.SerializeToElement("some data", lspSerializerOptions)))
        }

        testThereAndBackAgain theInlayHint
    ]

    testList (nameof InlineValue) [
      // Life of InlineValue:
      // * output of `textDocument/inlineValue` (`InlineValue[]`)
      // -> must be serializable as well as deserializable
      testCase "can roundtrip InlineValue with all fields (simple)"
      <| fun _ ->
        let theInlineValue: InlineValue =
          {
            InlineValueText.Range = { Start = { Line = 5u; Character = 7u }; End = { Line = 5u; Character = 10u } }
            Text = "test"
          }
          |> U3.C1

        testThereAndBackAgain theInlineValue
    ]

    testList (nameof TypeHierarchyItem) [
      testCase "can roundtrip HierarchyItem with all fields (simple)"
      <| fun _ ->
        let item: TypeHierarchyItem = {
          Name = "test"
          Kind = SymbolKind.Function
          Tags = None
          Detail = None
          Uri = "..."
          Range = mkRange' (1u, 2u) (3u, 4u)
          SelectionRange = mkRange' (1u, 2u) (1u, 4u)
          Data = None
        }

        testThereAndBackAgain item
    ]

    Shotgun.tests
    StartWithSetup.tests
  ]

[<Tests>]
let tests =
  testList "LSP" [
    serializationTests
    Utils.tests
  ]
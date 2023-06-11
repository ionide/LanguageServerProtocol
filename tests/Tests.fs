module Ionide.LanguageServerProtocol.Tests.Tests

open System
open Expecto
open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Tests
open Newtonsoft.Json.Linq
open Newtonsoft.Json
open Ionide.LanguageServerProtocol.JsonRpc
open System.Collections.Generic
open System.Runtime.Serialization

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

type MutableField = { Name: string; mutable Value: int }

type RequiredAttributeFields =
  { NoProperty: string
    NoPropertyOption: string option
    [<JsonProperty(Required = Required.DisallowNull)>]
    DisallowNull: string
    [<JsonProperty(Required = Required.Always)>]
    Always: string option
    [<JsonProperty(Required = Required.AllowNull)>]
    AllowNull: string }

type ExtensionDataField =
  { Name: string
    Value: string option
    /// Note: Must be mutable to allow deserializing to something `null` (uninitialized)
    [<JsonExtensionData>]
    mutable AdditionalData: IDictionary<string, JToken> }
  /// Required because:
  /// If no AdditionalData, AdditionalData stays null
  /// -> Must be initialized manually
  /// But no ctor in Record
  /// -> initialize in After Deserialization if necessary
  ///
  /// Note: it's possible to set in `OnDeserializing` -- which is before Dictionary gets filled.
  /// But cannot use `Map` there: Map values cannot be mutated
  [<OnDeserialized>]
  member x.OnDeserialized(context: StreamingContext) =
    if isNull x.AdditionalData then
      x.AdditionalData <- Map.empty

let private serializationTests =
  testList
    "(de)serialization"
    [

      /// Decapitalizes first letter
      let mkLower (str: string) = sprintf "%c%s" (Char.ToLowerInvariant str[0]) (str.Substring(1))

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
        "mutable field"
        [
          // Newtonsoft.Json serializes all public fields
          // F# emits a public field for mutable data:
          // `{ mutable Data: int }`
          // -> public property `Data` & public field `Data@`
          // -> Data gets serialized twice
          // Solution: exclude fields with trailing `@` (-> consider private)
          testCase "doesn't serialize backing field"
          <| fun _ ->
               let o: MutableField = { MutableField.Name = "foo"; Value = 42 }
               let json = o |> serialize :?> JObject
               let props = json.Properties() |> Seq.map (fun p -> p.Name)
               let expected = [ "name"; "value" ]
               Expect.sequenceEqual props expected "backing field should not get serialized" ]

      testList
        "ExtensionData"
        [ let testThereAndBackAgain (input: ExtensionDataField) =
            let output = thereAndBackAgain input
            // Dictionaries aren't structural comparable
            // and additional: `Dictionary` when deserialized, whatever user provided for serializing (probably `Map`)
            // -> custom compare `AdditionalData`
            let extractAdditionalData o =
              let ad = o.AdditionalData
              let o = { o with AdditionalData = Map.empty }
              (o, ad)

            let (input, inputAdditionalData) = extractAdditionalData input
            let (output, outputAdditionalData) = extractAdditionalData output

            Expect.equal
              output
              input
              "Input -> serialize -> deserialize should be Input again (ignoring AdditionalData)"

            Expect.sequenceEqual outputAdditionalData inputAdditionalData "AdditionalData should match"

          testCase "can (de)serialize with all fields and additional data"
          <| fun _ ->
               let input =
                 { ExtensionDataField.Name = "foo"
                   Value = Some "bar"
                   AdditionalData =
                     [ "alpha", JToken.FromObject("lorem")
                       "beta", JToken.FromObject("ipsum")
                       "gamma", JToken.FromObject("dolor") ]
                     |> Map.ofList }

               testThereAndBackAgain input

          testCase "can (de)serialize with all fields and no additional data"
          <| fun _ ->
               let input =
                 { ExtensionDataField.Name = "foo"
                   Value = Some "bar"
                   AdditionalData = Map.empty }

               testThereAndBackAgain input

          testCase "can (de)serialize when just required fields"
          <| fun _ ->
               let input = { ExtensionDataField.Name = "foo"; Value = None; AdditionalData = Map.empty }
               testThereAndBackAgain input

          testCase "can (de)serialize with required fields and additional data"
          <| fun _ ->
               let input =
                 { ExtensionDataField.Name = "foo"
                   Value = None
                   AdditionalData =
                     [ "alpha", JToken.FromObject("lorem")
                       "beta", JToken.FromObject("ipsum")
                       "gamma", JToken.FromObject("dolor") ]
                     |> Map.ofList }

               testThereAndBackAgain input

          testCase "fails when not required field"
          <| fun _ ->
               let json = JObject(JProperty("value", "bar"), JProperty("alpha", "lorem"), JProperty("beta", "ipsum"))

               Expect.throws
                 (fun _ -> json |> deserialize<ExtensionDataField> |> ignore)
                 "Should throw when required property is missing"

          testCase "serializes items in AdditionalData as properties"
          <| fun _ ->
               let input =
                 { ExtensionDataField.Name = "foo"
                   Value = Some "bar"
                   AdditionalData =
                     [ "alpha", JToken.FromObject("lorem")
                       "beta", JToken.FromObject("ipsum")
                       "gamma", JToken.FromObject("dolor") ]
                     |> Map.ofList }

               let json = input |> serialize

               let expected =
                 JObject(
                   JProperty("name", "foo"),
                   JProperty("value", "bar"),
                   JProperty("alpha", "lorem"),
                   JProperty("beta", "ipsum"),
                   JProperty("gamma", "dolor")
                 )

               Expect.equal
                 (json.ToString())
                 (expected.ToString())
                 "Items in AdditionalData should be normal properties"

          testCase "AdditionalData is not null when no additional properties"
          <| fun _ ->
               let json = JObject(JProperty("name", "foo"))
               let output = json |> deserialize<ExtensionDataField>
               Expect.isNotNull output.AdditionalData "Empty AdditionalData should not be null" ]

      testList
        "capitalization"
        [ testCase "changes lower cases start in F# to lower case in JSON"
          <| fun _ ->
               let o = {| Name = "foo"; SomeValue = 42 |}
               let json = serialize o :?> JObject

               let name = json.Property("name")
               Expect.equal name.Name "name" "name should be lower case start"

               let someValue = json.Property("someValue")

               Expect.equal
                 someValue.Name
                 "someValue"
                 "someValue should be lowercase start, but keep upper case 2nd word"

          testCase "keeps capitalization of Map"
          <| fun _ ->
               let keys =
                 [| "foo"; "Bar"; "BAZ"; "SomeValue"; "anotherValue"; "l"; "P" |]
                 |> Array.sort

               let m = keys |> Seq.mapi (fun i k -> (k, i)) |> Map.ofSeq
               let json = serialize m :?> JObject

               let propNames =
                 json.Properties()
                 |> Seq.map (fun p -> p.Name)
                 |> Seq.toArray
                 |> Array.sort

               Expect.equal propNames keys "Property names from Map should be unchanged"
          testCase "can deserialize Map back"
          <| fun _ ->
               let m =
                 [| "foo"; "Bar"; "BAZ"; "SomeValue"; "anotherValue"; "l"; "P" |]
                 |> Seq.mapi (fun i k -> (k, i))
                 |> Map.ofSeq

               testThereAndBackAgain m ]

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

                   json |> deserialize<AllOptional> |> ignore ]

          testList
            "Existing JsonProperty.Required"
            [ let o: RequiredAttributeFields =
                { NoProperty = ""
                  NoPropertyOption = None
                  DisallowNull = ""
                  Always = None
                  AllowNull = "" }

              let l = mkLower

              testCase "all according to Required Attribute should not fail"
              <| fun _ ->
                   let json =
                     JObject(
                       JProperty(l (nameof o.NoProperty), "lorem"),
                       JProperty(l (nameof o.NoPropertyOption), "ipsum"),
                       JProperty(l (nameof o.DisallowNull), "dolor"),
                       JProperty(l (nameof o.Always), "sit"),
                       JProperty(l (nameof o.AllowNull), "amet")
                     )

                   json |> deserialize<RequiredAttributeFields> |> ignore

              testCase "No property fails when not provided"
              <| fun _ ->
                   let json =
                     JObject(
                       JProperty(l (nameof o.NoPropertyOption), "ipsum"),
                       JProperty(l (nameof o.DisallowNull), "dolor"),
                       JProperty(l (nameof o.Always), "sit"),
                       JProperty(l (nameof o.AllowNull), "amet")
                     )

                   Expect.throws
                     (fun _ -> json |> deserialize<RequiredAttributeFields> |> ignore)
                     "No Property means required and should fail when not present"

              testCase "No property on option succeeds when not provided"
              <| fun _ ->
                   let json =
                     JObject(
                       JProperty(l (nameof o.NoProperty), "lorem"),
                       JProperty(l (nameof o.DisallowNull), "dolor"),
                       JProperty(l (nameof o.Always), "sit"),
                       JProperty(l (nameof o.AllowNull), "amet")
                     )

                   json |> deserialize<RequiredAttributeFields> |> ignore

              testCase "DisallowNull fails when null"
              <| fun _ ->
                   let json =
                     JObject(
                       JProperty(l (nameof o.NoProperty), "lorem"),
                       JProperty(l (nameof o.NoPropertyOption), "ipsum"),
                       JProperty(l (nameof o.DisallowNull), null),
                       JProperty(l (nameof o.Always), "sit"),
                       JProperty(l (nameof o.AllowNull), "amet")
                     )

                   Expect.throws
                     (fun _ -> json |> deserialize<RequiredAttributeFields> |> ignore)
                     "DisallowNull cannot be null"

              testCase "Option with Always fails when not present"
              <| fun _ ->
                   let json =
                     JObject(
                       JProperty(l (nameof o.NoProperty), "lorem"),
                       JProperty(l (nameof o.NoPropertyOption), "ipsum"),
                       JProperty(l (nameof o.DisallowNull), "dolor"),
                       JProperty(l (nameof o.AllowNull), "amet")
                     )

                   Expect.throws
                     (fun _ -> json |> deserialize<RequiredAttributeFields> |> ignore)
                     "Always is required despite Option"

              testCase "AllowNull doesn't fail when null"
              <| fun _ ->
                   let json =
                     JObject(
                       JProperty(l (nameof o.NoProperty), "lorem"),
                       JProperty(l (nameof o.NoPropertyOption), "ipsum"),
                       JProperty(l (nameof o.DisallowNull), "dolor"),
                       JProperty(l (nameof o.Always), "sit"),
                       JProperty(l (nameof o.AllowNull), null)
                     )

                   json |> deserialize<RequiredAttributeFields> |> ignore ] ]

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

          testList
            "string vs int"
            [ testCase "can deserialize int to U2<int,string>"
              <| fun _ ->
                   let input: U2<int, string> = U2.First 42
                   testThereAndBackAgain input
              testCase "can deserialize string to U2<int,string>"
              <| fun _ ->
                   let input: U2<int, string> = U2.Second "foo"
                   testThereAndBackAgain input
              testCase "can deserialize 42 string to U2<int,string>"
              <| fun _ ->
                   let input: U2<int, string> = U2.Second "42"
                   testThereAndBackAgain input

              testCase "can deserialize int to U2<string, int>"
              <| fun _ ->
                   let input: U2<string, int> = U2.Second 42
                   testThereAndBackAgain input
              testCase "can deserialize string to U2<string, string>"
              <| fun _ ->
                   let input: U2<string, int> = U2.First "foo"
                   testThereAndBackAgain input
              testCase "can deserialize 42 string to U2<string,int>"
              <| fun _ ->
                   let input: U2<string, int> = U2.First "42"
                   testThereAndBackAgain input ]
          testList
            "string vs bool"
            [ testCase "can deserialize bool to U2<bool,string>"
              <| fun _ ->
                   let input: U2<bool, string> = U2.First true
                   testThereAndBackAgain input
              testCase "can deserialize string to U2<bool,string>"
              <| fun _ ->
                   let input: U2<bool, string> = U2.Second "foo"
                   testThereAndBackAgain input
              testCase "can deserialize true string to U2<bool,string>"
              <| fun _ ->
                   let input: U2<bool, string> = U2.Second "true"
                   testThereAndBackAgain input

              testCase "can deserialize bool true to U2<string, bool>"
              <| fun _ ->
                   let input: U2<string, bool> = U2.Second true
                   testThereAndBackAgain input
              testCase "can deserialize bool false to U2<string, bool>"
              <| fun _ ->
                   let input: U2<string, bool> = U2.Second false
                   testThereAndBackAgain input
              testCase "can deserialize string to U2<string, string>"
              <| fun _ ->
                   let input: U2<string, bool> = U2.First "foo"
                   testThereAndBackAgain input
              testCase "can deserialize true string to U2<string,bool>"
              <| fun _ ->
                   let input: U2<string, bool> = U2.First "true"
                   testThereAndBackAgain input ] ]

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

               testThereAndBackAgain theInlayHint ]

      testList
        (nameof InlineValue)
        [
          // Life of InlineValue:
          // * output of `textDocument/inlineValue` (`InlineValue[]`)
          // -> must be serializable as well as deserializable
          testCase "can roundtrip InlineValue with all fields (simple)"
          <| fun _ ->
               let theInlineValue: InlineValue =
                  { InlineValueText.Range = { Start = { Line = 5; Character = 7 }; End = { Line = 5; Character = 10 } } 
                    Text = "test" }
                  |> InlineValue.InlineValueText 

               testThereAndBackAgain theInlineValue ]

      testList
        (nameof HierarchyItem)
        [
          testCase "can roundtrip HierarchyItem with all fields (simple)"
          <| fun _ ->
               let item: HierarchyItem =
                  { Name = "test"
                    Kind = SymbolKind.Function 
                    Tags = None
                    Detail = None
                    Uri = "..."
                    Range = mkRange' (1, 2) (3, 4)
                    SelectionRange = mkRange' (1, 2) (1, 4)
                    Data = None }
               testThereAndBackAgain item
        ]
      Shotgun.tests
      StartWithSetup.tests ]

[<Tests>]
let tests = testList "LSP" [ serializationTests; Utils.tests ]
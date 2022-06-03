module Ionide.LanguageServerProtocol.JsonUtils

open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open System
open System.Collections.Concurrent
open Ionide.LanguageServerProtocol.Types
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization

/// Handles fields of type `Option`:
/// * Allows missing json properties when `Option` -> Optional
/// * Fails when missing json property when not `Option` -> Required
/// * Additional properties in json are always ignored
///
/// Example:
/// ```fsharp
/// type Data = { Name: string; Value: int option }
/// ```
/// ```json
/// { "name": "foo", "value": 42 }    // ok
/// { "name": "foo" }                 // ok
/// { "value": 42 }                   // error
/// {}                                // error
/// { "name": "foo", "data": "bar" }  // ok
/// ```
type OptionAndCamelCasePropertyNamesContractResolver() =
  inherit CamelCasePropertyNamesContractResolver()

  override _.CreateObjectContract(objectType: Type) =
    let contract = ``base``.CreateObjectContract(objectType)

    let isOptionType (ty: Type) =
      ty.IsGenericType
      //TODO: handle ValueOption
      && ty.GetGenericTypeDefinition() = typedefof<Option<_>>

    let props = contract.Properties

    for prop in props do
      if isOptionType prop.PropertyType then
        prop.Required <- Required.Default
      else
        prop.Required <- Required.Always

    contract


let inline memorise (f: 'a -> 'b) : ('a -> 'b) =
  let d = ConcurrentDictionary<'a, 'b>()
  fun key -> d.GetOrAdd(key, f)

//TODO: Cache stuff
type ErasedUnionConverter() =
  inherit JsonConverter()

  let canConvert =
    memorise (fun t ->
      FSharpType.IsUnion t
      && (
      // Union
      t.GetCustomAttributes(typedefof<ErasedUnionAttribute>, false).Length > 0
      ||
      // Case
      t.BaseType.GetCustomAttributes(typedefof<ErasedUnionAttribute>, false).Length > 0))

  override __.CanConvert(t) = canConvert t

  override __.WriteJson(writer, value, serializer) =
    let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
    // Must be exactly 1 field
    // Deliberately fail here to signal incorrect usage
    // (vs. CanConvert = false -> silent and serialization to `case` & `fields`)
    match fields with
    | [| unionField |] -> serializer.Serialize(writer, unionField)
    | _ -> failwith $"Expected exactly one field for case `{value.GetType().Name}`, but were {fields.Length}"

  override __.ReadJson(reader: JsonReader, t, _existingValue, serializer) =
    let tryReadValue (json: JToken) (targetType: Type) =
      //TODO: handle simple types without exception handling?
      try
        json.ToObject(targetType, serializer) |> Some
      with
      | _ -> None

    let tryMakeUnionCase (json: JToken) (case: UnionCaseInfo) =
      match case.GetFields() with
      | [| field |] ->
        let ty = field.PropertyType

        match tryReadValue json ty with
        | None -> None
        | Some value -> FSharpValue.MakeUnion(case, [| value |]) |> Some
      | fields ->
        failwith
          $"Expected union {case.DeclaringType.Name} to have exactly one field in each case, but case {case.Name} has {fields.Length} fields"


    let cases = FSharpType.GetUnionCases(t)
    let json = JToken.ReadFrom reader
    let c = cases |> Array.tryPick (tryMakeUnionCase json)

    match c with
    | None -> failwith $"Could not create an instance of the type '%s{t.Name}'"
    | Some c -> c

/// converter that can convert enum-style DUs
type SingleCaseUnionConverter() =
  inherit JsonConverter()


  let canConvert =
    let allCases (t: System.Type) = FSharpType.GetUnionCases t

    memorise (fun t ->
      FSharpType.IsUnion t
      && allCases t
         |> Array.forall (fun c -> c.GetFields().Length = 0))

  override _.CanConvert t = canConvert t

  override _.WriteJson(writer: Newtonsoft.Json.JsonWriter, value: obj, serializer: Newtonsoft.Json.JsonSerializer) =
    serializer.Serialize(writer, string value)

  override _.ReadJson(reader: Newtonsoft.Json.JsonReader, t, _existingValue, serializer) =
    let caseName = string reader.Value

    match
      FSharpType.GetUnionCases(t)
      |> Array.tryFind (fun c -> c.Name.Equals(caseName, StringComparison.OrdinalIgnoreCase))
      with
    | Some caseInfo -> FSharpValue.MakeUnion(caseInfo, [||])
    | None -> failwith $"Could not create an instance of the type '%s{t.Name}' with the name '%s{caseName}'"

type U2BoolObjectConverter() =
  inherit JsonConverter()

  let canConvert =
    memorise (fun (t: System.Type) ->
      t.IsGenericType
      && t.GetGenericTypeDefinition() = typedefof<U2<_, _>>
      && t.GetGenericArguments().Length = 2
      && t.GetGenericArguments().[0] = typeof<bool>
      && not (t.GetGenericArguments().[1].IsValueType))

  override _.CanConvert t = canConvert t

  override _.WriteJson(writer, value, serializer) =
    let case, fields = FSharpValue.GetUnionFields(value, value.GetType())

    match case.Name with
    | "First" -> writer.WriteValue(value :?> bool)
    | "Second" -> serializer.Serialize(writer, fields.[0])
    | _ -> failwith $"Unrecognized case '{case.Name}' for union type '{value.GetType().FullName}'."

  override _.ReadJson(reader, t, _existingValue, serializer) =
    let cases = FSharpType.GetUnionCases(t)

    match reader.TokenType with
    | JsonToken.Boolean ->
      // 'First' side
      FSharpValue.MakeUnion(cases.[0], [| box (reader.Value :?> bool) |])
    | JsonToken.StartObject ->
      // Second side
      let value = serializer.Deserialize(reader, (t.GetGenericArguments().[1]))
      FSharpValue.MakeUnion(cases.[1], [| value |])
    | _ ->
      failwithf $"Unrecognized json TokenType '%s{string reader.TokenType}' when reading value of type '{t.FullName}'"

type OptionConverter() =
  inherit JsonConverter()

  override __.CanConvert(t) =
    t.IsGenericType
    && t.GetGenericTypeDefinition() = typedefof<option<_>>

  override __.WriteJson(writer, value, serializer) =
    let value =
      if isNull value then
        null
      else
        let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
        fields.[0]

    serializer.Serialize(writer, value)

  override __.ReadJson(reader, t, _existingValue, serializer) =
    let cases = FSharpType.GetUnionCases(t)

    match reader.TokenType with
    | JsonToken.Null -> FSharpValue.MakeUnion(cases.[0], [||])
    | _ ->
      let innerType = t.GetGenericArguments().[0]

      let innerType =
        if innerType.IsValueType then
          (typedefof<Nullable<_>>).MakeGenericType([| innerType |])
        else
          innerType

      let value = serializer.Deserialize(reader, innerType)

      if isNull value then
        FSharpValue.MakeUnion(cases.[0], [||])
      else
        FSharpValue.MakeUnion(cases.[1], [| value |])
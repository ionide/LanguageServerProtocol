module Ionide.LanguageServerProtocol.JsonUtils

open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open System
open System.Collections.Concurrent
open Ionide.LanguageServerProtocol.Types
open Newtonsoft.Json.Linq
open Newtonsoft.Json.Serialization
open System.Reflection

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
      && ty.GetGenericTypeDefinition() = typedefof<Option<_>>

    let props = contract.Properties

    for prop in props do
      if isOptionType prop.PropertyType then
        prop.Required <- Required.Default
      else
        prop.Required <- Required.Always

    contract


let inline private memoriseByHash (f: 'a -> 'b): 'a -> 'b =
  let d = ConcurrentDictionary<int, 'b>()
  fun key ->
    let hash = key.GetHashCode()
    match d.TryGetValue(hash) with
    | (true, value) -> value
    | _ ->
      let value = f key
      d.TryAdd(hash, value) |> ignore
      value

type private CaseInfo =
  { Info: UnionCaseInfo
    Fields: PropertyInfo []
    GetFieldValues: obj -> obj []
    Create: obj [] -> obj }

type private UnionInfo =
  { Cases: CaseInfo []
    GetTag: obj -> int }
  member u.GetCaseOf(value: obj) =
    let tag = u.GetTag value
    u.Cases |> Array.find (fun case -> case.Info.Tag = tag)

module private UnionInfo =
  let create (ty: Type) =
    assert (ty |> FSharpType.IsUnion)

    let cases =
      FSharpType.GetUnionCases ty
      |> Array.map (fun case ->
        { Info = case
          Fields = case.GetFields()
          GetFieldValues = FSharpValue.PreComputeUnionReader case
          Create = FSharpValue.PreComputeUnionConstructor case })

    { Cases = cases; GetTag = FSharpValue.PreComputeUnionTagReader ty }

let private getUnionInfo: Type -> _ = memoriseByHash (fun t -> UnionInfo.create t)

/// Newtonsoft.Json parses parses a number inside quotations as number too:
/// `"42"` -> can be parsed to `42: int`
/// This converter prevents that. `"42"` cannot be parsed to `int` (or `float`) any more
type StrictNumberConverter() =
  inherit JsonConverter()

  static let defaultSerializer = JsonSerializer()
  static let numericHashes = [|
    typeof<int>.GetHashCode()
    typeof<float>.GetHashCode()
    typeof<byte>.GetHashCode()
    typeof<uint>.GetHashCode()
    //ENHANCEMENT: other number types
  |]
  static let isNumeric (t: Type) =
    let hash = t.GetHashCode()
    numericHashes |> Array.contains hash

  override _.CanConvert(t) = isNumeric t

  override __.ReadJson(reader, t, _, serializer) =
    match reader.TokenType with
    | JsonToken.Integer
    | JsonToken.Float ->
        // cannot use `serializer`: Endless recursion into StrictNumberConverter for same value
        defaultSerializer.Deserialize(reader, t)
    | _ ->
        failwith $"Expected a number, but was {reader.TokenType}"

  override _.CanWrite = false
  override _.WriteJson(_,_,_) = raise (NotImplementedException())

/// Like `StrictNumberConverter`, but prevents numbers to be parsed as string:
/// `42` -> no quotation marks -> not a string
type StrictStringConverter() =
  inherit JsonConverter()

  static let stringHash = typeof<string>.GetHashCode()
  override _.CanConvert(t) = t.GetHashCode() = stringHash

  override __.ReadJson(reader, t, _, serializer) =
    match reader.TokenType with
    | JsonToken.String ->
        reader.Value
    | _ ->
        failwith $"Expected a string, but was {reader.TokenType}"

  override _.CanWrite = false
  override _.WriteJson(_,_,_) = raise (NotImplementedException())

/// Like `StrictNumberConverter`, but prevents boolean to be parsed as string:
/// `true` -> no quotation marks -> not a string
type StrictBoolConverter() =
  inherit JsonConverter()

  static let boolHash = typeof<bool>.GetHashCode()
  override _.CanConvert(t) = t.GetHashCode() = boolHash

  override __.ReadJson(reader, t, _, serializer) =
    match reader.TokenType with
    | JsonToken.Boolean ->
        reader.Value
    | _ ->
        failwith $"Expected a bool, but was {reader.TokenType}"

  override _.CanWrite = false
  override _.WriteJson(_,_,_) = raise (NotImplementedException())

type ErasedUnionConverter() =
  inherit JsonConverter()

  let canConvert =
    memoriseByHash (fun t ->
      FSharpType.IsUnion t
      && (
      // Union
      t.GetCustomAttributes(typedefof<ErasedUnionAttribute>, false).Length > 0
      ||
      // Case
      t.BaseType.GetCustomAttributes(typedefof<ErasedUnionAttribute>, false).Length > 0))

  override __.CanConvert(t) = canConvert t

  override __.WriteJson(writer, value, serializer) =
    let union = getUnionInfo (value.GetType())
    let case = union.GetCaseOf value
    // Must be exactly 1 field
    // Deliberately fail here to signal incorrect usage
    // (vs. `CanConvert` = `false` -> silent and fallback to serialization with `case` & `fields`)
    match case.GetFieldValues value with
    | [| value |] -> serializer.Serialize(writer, value)
    | values -> failwith $"Expected exactly one field for case `{value.GetType().Name}`, but were {values.Length}"

  override __.ReadJson(reader: JsonReader, t, _existingValue, serializer) =
    let tryReadValue (json: JToken) (targetType: Type) =
        //TODO: custom handling simple types to prevent exceptions?
        try
          json.ToObject(targetType, serializer) |> Some
        with
        | _ -> None

    let union = getUnionInfo t
    let json = JToken.ReadFrom reader

    let tryMakeUnionCase (json: JToken) (case: CaseInfo) =
      match case.Fields with
      | [| field |] ->
        let ty = field.PropertyType

        match tryReadValue json ty with
        | None -> None
        | Some value -> case.Create [| value |] |> Some
      | fields ->
        failwith
          $"Expected union {case.Info.DeclaringType.Name} to have exactly one field in each case, but case {case.Info.Name} has {fields.Length} fields"

    let c = union.Cases |> Array.tryPick (tryMakeUnionCase json)

    match c with
    | None -> failwith $"Could not create an instance of the type '%s{t.Name}'"
    | Some c -> c

/// converter that can convert enum-style DUs
type SingleCaseUnionConverter() =
  inherit JsonConverter()

  let canConvert =
    let allCases (t: System.Type) = FSharpType.GetUnionCases t

    memoriseByHash (fun t ->
      FSharpType.IsUnion t
      && allCases t
         |> Array.forall (fun c -> c.GetFields().Length = 0))

  override _.CanConvert t = canConvert t

  override _.WriteJson(writer: Newtonsoft.Json.JsonWriter, value: obj, serializer: Newtonsoft.Json.JsonSerializer) =
    serializer.Serialize(writer, string value)

  override _.ReadJson(reader: Newtonsoft.Json.JsonReader, t, _existingValue, serializer) =
    let caseName = string reader.Value

    let union = getUnionInfo t

    let case =
      union.Cases
      |> Array.tryFind (fun c -> c.Info.Name.Equals(caseName, StringComparison.OrdinalIgnoreCase))

    match case with
    | Some case -> case.Create [||]
    | None -> failwith $"Could not create an instance of the type '%s{t.Name}' with the name '%s{caseName}'"

type OptionConverter() =
  inherit JsonConverter()

  let canConvert =
    memoriseByHash (fun (t: System.Type) ->
      t.IsGenericType
      && t.GetGenericTypeDefinition() = typedefof<option<_>>)

  override __.CanConvert(t) = canConvert t

  override __.WriteJson(writer, value, serializer) =
    let value =
      if isNull value then
        null
      else
        let union = getUnionInfo (value.GetType())
        let case = union.GetCaseOf value
        case.GetFieldValues value |> Array.head

    serializer.Serialize(writer, value)

  override __.ReadJson(reader, t, _existingValue, serializer) =
    match reader.TokenType with
    | JsonToken.Null -> null // = None
    | _ ->
      let innerType = t.GetGenericArguments()[0]

      let innerType =
        if innerType.IsValueType then
          (typedefof<Nullable<_>>).MakeGenericType([| innerType |])
        else
          innerType

      let value = serializer.Deserialize(reader, innerType)

      if isNull value then
        null
      else
        let union = getUnionInfo t
        union.Cases[ 1 ].Create [| value |]
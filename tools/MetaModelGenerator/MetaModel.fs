namespace MetaModelGenerator


type StructuredDocs = string list

module StructuredDocs =
  let parse (s: string) =
    s.Trim('\n').Split([| '\n' |])
    |> Array.toList

module Proposed =
  let skipProposed = true

  let inline checkProposed x =
    if skipProposed then
      (^a: (member Proposed: bool option) x)
      <> Some true
    else
      true

module Preconverts =
  open Newtonsoft.Json
  open Newtonsoft.Json.Linq

  type SingleOrArrayConverter<'T>() =
    inherit JsonConverter()

    override x.CanConvert(tobject: System.Type) = tobject = typeof<'T array>

    override _.WriteJson(writer: JsonWriter, value, serializer: JsonSerializer) : unit =
      failwith "Should never be writing this structure, it comes from Microsoft LSP Spec"

    override _.ReadJson(reader: JsonReader, objectType: System.Type, existingValue: obj, serializer: JsonSerializer) =
      let token = JToken.Load reader

      match token.Type with
      | JTokenType.Array -> serializer.Deserialize(reader, objectType)
      | JTokenType.Null -> null
      | _ -> Some [| token.ToObject<'T>(serializer) |]

module rec MetaModel =
  open System
  open Newtonsoft.Json.Linq
  open Newtonsoft.Json
  open Ionide.LanguageServerProtocol

  type MetaData = { Version: string }

  /// Indicates in which direction a message is sent in the protocol.
  [<JsonConverter(typeof<Newtonsoft.Json.Converters.StringEnumConverter>)>]
  type MessageDirection =
    | ClientToServer = 0
    | ServerToClient = 1
    | Both = 2

  /// Represents a LSP request
  type Request = {
    /// Whether the request is deprecated or not. If deprecated the property contains the deprecation message."
    Deprecated: string option
    /// An optional documentation;
    Documentation: string option
    /// An optional error data type.
    ErrorData: Type option
    /// The direction in which this request is sent in the protocol.
    MessageDirection: MessageDirection
    /// The request's method name.
    Method: string
    /// The parameter type(s) if any.
    [<JsonConverter(typeof<Preconverts.SingleOrArrayConverter<Type>>)>]
    Params: Type array option
    /// Optional partial result type if the request supports partial result reporting.
    PartialResult: Type option
    /// Whether this is a proposed feature. If omitted the feature is final.",
    Proposed: bool option
    /// Optional a dynamic registration method if it different from the request's method."
    RegistrationMethod: string option
    /// Optional registration options if the request supports dynamic registration."
    RegistrationOptions: Type option
    /// The result type.
    Result: Type
    /// Since when (release number) this request is available. Is undefined if not known.",
    Since: string option
  } with

    member x.ParamsSafe =
      x.Params
      |> Option.Array.toArray

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  /// Represents a LSP notification
  type Notification = {

    /// Whether the notification is deprecated or not. If deprecated the property contains the deprecation message."
    Deprecated: string option
    /// An optional documentation;
    Documentation: string option
    /// The direction in which this notification is sent in the protocol.
    MessageDirection: MessageDirection
    /// The request's method name.
    Method: string
    /// The parameter type(s) if any.
    [<JsonConverter(typeof<Preconverts.SingleOrArrayConverter<Type>>)>]
    Params: Type array option
    /// Whether this is a proposed feature. If omitted the notification is final.",
    Proposed: bool option
    /// Optional a dynamic registration method if it different from the request's method."
    RegistrationMethod: string option
    /// Optional registration options if the notification supports dynamic registration."
    RegistrationOptions: Type option
    /// Since when (release number) this notification is available. Is undefined if not known.",
    Since: string option
  } with

    member x.ParamsSafe =
      x.Params
      |> Option.Array.toArray

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  [<RequireQualifiedAccess>]
  type BaseTypes =
    | Uri
    | DocumentUri
    | Integer
    | Uinteger
    | Decimal
    | RegExp
    | String
    | Boolean
    | Null

    static member Parse(s: string) =
      match s with
      | "URI" -> Uri
      | "DocumentUri" -> DocumentUri
      | "integer" -> Integer
      | "uinteger" -> Uinteger
      | "decimal" -> Decimal
      | "RegExp" -> RegExp
      | "string" -> String
      | "boolean" -> Boolean
      | "null" -> Null
      | _ -> failwithf "Unknown base type: %s" s

    member x.ToDotNetType() =
      match x with
      | Uri -> "URI"
      | DocumentUri -> "DocumentUri"
      | Integer -> "int32"
      | Uinteger -> "uint32"
      | Decimal -> "decimal"
      | RegExp -> "RegExp"
      | String -> "string"
      | Boolean -> "bool"
      | Null -> "null"

  [<Literal>]
  let BaseTypeConst = "base"

  type BaseType = { Kind: string; Name: BaseTypes }

  [<Literal>]
  let ReferenceTypeConst = "reference"

  type ReferenceType = { Kind: string; Name: string }

  [<Literal>]
  let ArrayTypeConst = "array"

  type ArrayType = { Kind: string; Element: Type }

  [<Literal>]
  let MapTypeConst = "map"

  type MapType = { Kind: string; Key: MapKeyType; Value: Type }

  [<RequireQualifiedAccess>]
  type MapKeyNameEnum =
    | Uri
    | DocumentUri
    | String
    | Integer

    static member Parse(s: string) =
      match s with
      | "URI" -> Uri
      | "DocumentUri" -> DocumentUri
      | "string" -> String
      | "integer" -> Integer
      | _ -> failwithf "Unknown map key name: %s" s

    member x.ToDotNetType() =
      match x with
      | Uri -> "URI"
      | DocumentUri -> "DocumentUri"
      | String -> "string"
      | Integer -> "int32"

  [<RequireQualifiedAccess>]
  type MapKeyType =
    | ReferenceType of ReferenceType
    | Base of {| Kind: string; Name: MapKeyNameEnum |}

  [<Literal>]
  let AndTypeConst = "and"

  type AndType = { Kind: string; Items: Type array }

  [<Literal>]
  let OrTypeConst = "or"

  type OrType = { Kind: string; Items: Type array }

  [<Literal>]
  let TupleTypeConst = "tuple"

  type TupleType = { Kind: string; Items: Type array }

  type Property = {
    Deprecated: string option
    Documentation: string option
    Name: string
    Optional: bool option
    Proposed: bool option
    Required: bool option
    Since: string option
    Type: Type
  } with

    member x.IsOptional =
      x.Optional
      |> Option.defaultValue false

    member x.NameAsPascalCase = String.toPascalCase x.Name

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse


  [<Literal>]
  let StructureTypeLiteral = "literal"

  type StructureLiteral = {
    Deprecated: string option
    Documentation: string option
    Properties: Property array
    Proposed: bool option
    Since: string option
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

    member x.PropertiesSafe =
      x.Properties
      |> Array.filter Proposed.checkProposed

  type StructureLiteralType = { Kind: string; Value: StructureLiteral }

  [<Literal>]
  let StringLiteralTypeConst = "stringLiteral"

  type StringLiteralType = { Kind: string; Value: string }

  [<Literal>]
  let IntegerLiteralTypeConst = "integerLiteral"

  type IntegerLiteralType = { Kind: string; Value: decimal }

  [<Literal>]
  let BooleanLiteralTypeConst = "booleanLiteral"

  type BooleanLiteralType = { Kind: string; Value: bool }

  [<RequireQualifiedAccess>]
  type Type =
    | BaseType of BaseType
    | ReferenceType of ReferenceType
    | ArrayType of ArrayType
    | MapType of MapType
    | AndType of AndType
    | OrType of OrType
    | TupleType of TupleType
    | StructureLiteralType of StructureLiteralType
    | StringLiteralType of StringLiteralType
    | IntegerLiteralType of IntegerLiteralType
    | BooleanLiteralType of BooleanLiteralType

    member x.isStructureLiteralType =
      match x with
      | StructureLiteralType _ -> true
      | _ -> false


  type Structure = {
    Deprecated: string option
    Documentation: string option
    Extends: Type array option
    Mixins: Type array option
    Name: string
    Properties: Property array option
    Proposed: bool option
    Since: string option
  } with

    member x.ExtendsSafe = Option.Array.toArray x.Extends
    member x.MixinsSafe = Option.Array.toArray x.Mixins

    member x.PropertiesSafe =
      Option.Array.toArray x.Properties
      |> Seq.filter Proposed.checkProposed

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  type TypeAlias = {
    Deprecated: string option
    Documentation: string option
    Name: string
    Proposed: bool option
    Since: string option
    Type: Type
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  [<JsonConverter(typeof<Newtonsoft.Json.Converters.StringEnumConverter>)>]
  type EnumerationTypeNameValues =
    | String = 0
    | Integer = 1
    | Uinteger = 2

  type EnumerationType = { Kind: string; Name: EnumerationTypeNameValues }

  type EnumerationEntry = {
    Deprecated: string option
    Documentation: string option

    Name: string
    Proposed: bool option
    Since: string option
    Value: string
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  type Enumeration = {
    Deprecated: string option
    Documentation: string option
    Name: string
    Proposed: bool option
    Since: string option
    SupportsCustomValues: bool option
    Type: EnumerationType
    Values: EnumerationEntry array
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

    member x.ValuesSafe =
      x.Values
      |> Array.filter Proposed.checkProposed

  type MetaModel = {
    MetaData: MetaData
    Requests: Request array
    Notifications: Notification array
    Structures: Structure array
    TypeAliases: TypeAlias array
    Enumerations: Enumeration array
  } with

    member x.StructuresSafe =
      x.Structures
      |> Array.filter Proposed.checkProposed

    member x.TypeAliasesSafe =
      x.TypeAliases
      |> Array.filter Proposed.checkProposed

    member x.EnumerationsSafe =
      x.Enumerations
      |> Array.filter Proposed.checkProposed

  module Converters =

    type MapKeyTypeConverter() =
      inherit JsonConverter<MapKeyType>()

      override _.WriteJson(writer: JsonWriter, value: MapKeyType, serializer: JsonSerializer) : unit =
        failwith "Should never be writing this structure, it comes from Microsoft LSP Spec"

      override _.ReadJson
        (
          reader: JsonReader,
          objectType: System.Type,
          existingValue: MapKeyType,
          hasExistingValue,
          serializer: JsonSerializer
        ) =
        let jobj = JObject.Load(reader)
        let kind = jobj.["kind"].Value<string>()

        match kind with
        | ReferenceTypeConst ->
          let name = jobj.["name"].Value<string>()
          MapKeyType.ReferenceType { Kind = kind; Name = name }
        | "base" ->
          let name = jobj.["name"].Value<string>()
          MapKeyType.Base {| Kind = kind; Name = MapKeyNameEnum.Parse name |}
        | _ -> failwithf "Unknown map key type: %s" kind

    type TypeConverter() =
      inherit JsonConverter<Type>()

      override _.WriteJson(writer: JsonWriter, value: MetaModel.Type, serializer: JsonSerializer) : unit =
        failwith "Should never be writing this structure, it comes from Microsoft LSP Spec"

      override _.ReadJson
        (
          reader: JsonReader,
          objectType: System.Type,
          existingValue: Type,
          hasExistingValue,
          serializer: JsonSerializer
        ) =
        let jobj = JObject.Load(reader)
        let kind = jobj.["kind"].Value<string>()

        match kind with
        | BaseTypeConst ->
          let name = jobj.["name"].Value<string>()
          Type.BaseType { Kind = kind; Name = BaseTypes.Parse name }
        | ReferenceTypeConst ->
          let name = jobj.["name"].Value<string>()
          Type.ReferenceType { Kind = kind; Name = name }
        | ArrayTypeConst ->
          let element = jobj.["element"].ToObject<Type>(serializer)
          Type.ArrayType { Kind = kind; Element = element }
        | MapTypeConst ->
          let key = jobj.["key"].ToObject<MapKeyType>(serializer)
          let value = jobj.["value"].ToObject<Type>(serializer)
          Type.MapType { Kind = kind; Key = key; Value = value }
        | AndTypeConst ->
          let items = jobj.["items"].ToObject<Type[]>(serializer)
          Type.AndType { Kind = kind; Items = items }
        | OrTypeConst ->
          let items = jobj.["items"].ToObject<Type[]>(serializer)
          Type.OrType { Kind = kind; Items = items }
        | TupleTypeConst ->
          let items = jobj.["items"].ToObject<Type[]>(serializer)
          Type.TupleType { Kind = kind; Items = items }
        | StructureTypeLiteral ->
          let value = jobj.["value"].ToObject<StructureLiteral>(serializer)
          Type.StructureLiteralType { Kind = kind; Value = value }
        | StringLiteralTypeConst ->
          let value = jobj.["value"].Value<string>()
          Type.StringLiteralType { Kind = kind; Value = value }
        | IntegerLiteralTypeConst ->
          let value = jobj.["value"].Value<decimal>()
          Type.IntegerLiteralType { Kind = kind; Value = value }
        | BooleanLiteralTypeConst ->
          let value = jobj.["value"].Value<bool>()
          Type.BooleanLiteralType { Kind = kind; Value = value }
        | _ -> failwithf "Unknown type kind: %s" kind


  let metaModelSerializerSettings =
    let settings = JsonSerializerSettings()
    settings.Converters.Add(Converters.TypeConverter() :> JsonConverter)
    settings.Converters.Add(Converters.MapKeyTypeConverter() :> JsonConverter)
    settings.Converters.Add(JsonUtils.OptionConverter() :> JsonConverter)
    settings
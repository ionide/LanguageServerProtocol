namespace Ionide.LanguageServerProtocol.Types

open Ionide.LanguageServerProtocol
open System.Text.Json
open System.Text.Json.Serialization


/// Types in typescript can have hardcoded values for their fields, this attribute is used to mark
/// the default value for a field in a type and is used when deserializing the type to json
/// but these types might not actually be used as a discriminated union or only partially used
/// so we don't generate a dedicated union type because of that
///
/// see https://microsoft.github.io/language-server-protocol/specifications/lsp/3.18/specification/#resourceChanges for a dedicated example
type UnionKindAttribute(value: string) =
  inherit System.Attribute()
  member x.Value = value

/// Represents a Union type where the individual cases are erased when serialized or deserialized
/// For instance a union could be defined as: "string | int | bool" and when serialized it would be
/// serialized as a only a value based on the actual case
type ErasedUnionAttribute() =
  inherit System.Attribute()

/// Represents a Union type where the individual cases are erased when serialized or deserialized
/// For instance a union could be defined as: "string | int | bool" and when serialized it would be
/// serialized as a only a value based on the actual case
[<ErasedUnion>]
type U2<'T1, 'T2> =
  /// Represents a single case of a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C1 of 'T1
  /// Represents a single case of a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C2 of 'T2

  override x.ToString() =
    match x with
    | C1 c -> string c
    | C2 c -> string c

/// Represents a Union type where the individual cases are erased when serialized or deserialized
/// For instance a union could be defined as: "string | int | bool" and when serialized it would be
/// serialized as a only a value based on the actual case
[<ErasedUnion>]
type U3<'T1, 'T2, 'T3> =
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C1 of 'T1
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C2 of 'T2
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C3 of 'T3

  override x.ToString() =
    match x with
    | C1 c -> string c
    | C2 c -> string c
    | C3 c -> string c

/// Represents a Union type where the individual cases are erased when serialized or deserialized
/// For instance a union could be defined as: "string | int | bool" and when serialized it would be
/// serialized as a only a value based on the actual case
[<ErasedUnion>]
type U4<'T1, 'T2, 'T3, 'T4> =
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C1 of 'T1
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C2 of 'T2
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C3 of 'T3
  /// Represents a Union type where the individual cases are erased when serialized or deserialized
  /// For instance a union could be defined as: "string | int | bool" and when serialized it would be
  /// serialized as a only a value based on the actual case
  | C4 of 'T4

  override x.ToString() =
    match x with
    | C1 c -> string c
    | C2 c -> string c
    | C3 c -> string 3
    | C4 c -> string 3

// ---------------------------------------------------------------------------
// LSPAny — a JsonElement-derived wrapper with deep structural equality
// ---------------------------------------------------------------------------

/// The LSP any type.
/// Please note that strictly speaking a property with the value `undefined`
/// can't be converted into JSON preserving the property name. However for
/// convenience it is allowed and assumed that all these properties are
/// optional as well.
/// @since 3.17.0
///
/// A reference type wrapping `System.Text.Json.JsonElement` with deep structural
/// equality and comparison via `JsonElement.DeepEquals` (kind-then-content,
/// object properties order-insensitive).
[<JsonConverter(typeof<LSPAnyConverter>)>]
type LSPAny(value: JsonElement) =

  // ---- private helpers ----------------------------------------------------

  static member private Hash(je: JsonElement) : int =
    match je.ValueKind with
    | JsonValueKind.Null -> 0
    | JsonValueKind.True -> 1
    | JsonValueKind.False -> 2
    | JsonValueKind.Number ->
      // Raw text preserves distinctions like 1 vs 1.0
      je.GetRawText().GetHashCode()
    | JsonValueKind.String -> je.GetString().GetHashCode()
    | JsonValueKind.Array ->
      je.EnumerateArray()
      |> Seq.fold
        (fun acc el ->
          acc * 31
          + LSPAny.Hash(el)
        )
        17
    | JsonValueKind.Object ->
      je.EnumerateObject()
      |> Seq.fold
        (fun acc p ->
          acc
          ^^^ (p.Name.GetHashCode()
               * 31
               + LSPAny.Hash(p.Value))
        )
        17
    | _ -> 0

  // ---- public surface -----------------------------------------------------

  /// The underlying `JsonElement`.
  member _.JsonElement = value

  override _.ToString() = value.GetRawText()
  override _.GetHashCode() = LSPAny.Hash(value)

  override x.Equals(obj) =
    match obj with
    | :? LSPAny as other -> JsonElement.DeepEquals(value, other.JsonElement)
    | _ -> false

  interface System.IEquatable<LSPAny> with
    member x.Equals(other) = JsonElement.DeepEquals(value, other.JsonElement)

/// STJ JsonConverter for LSPAny. Reads and writes the underlying JsonElement
/// transparently so the wire format is indistinguishable from a bare JsonElement.
and LSPAnyConverter() =
  inherit JsonConverter<LSPAny>()

  override _.Read(reader, _t, _opts) =
    use doc = JsonDocument.ParseValue(&reader)
    LSPAny(doc.RootElement.Clone())

  override _.Write(writer, value, _opts) = value.JsonElement.WriteTo(writer)

/// Helpers for constructing and deconstructing `LSPAny` values.
[<RequireQualifiedAccess>]
module LSPAny =
  /// Wrap a `JsonElement` in an `LSPAny`.
  let ofJsonElement (je: JsonElement) : LSPAny = LSPAny je

  /// Unwrap the inner `JsonElement` from an `LSPAny`.
  let toJsonElement (x: LSPAny) : JsonElement = x.JsonElement

  /// Serialize any value to `LSPAny` using the default STJ rules.
  let inline ofValue (value: 'T) : LSPAny = LSPAny(JsonSerializer.SerializeToElement(value))

  /// Deserialize an `LSPAny` to the requested type using the default STJ rules.
  let inline toValue<'T> (x: LSPAny) : 'T = JsonSerializer.Deserialize<'T>(x.JsonElement)
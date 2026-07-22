namespace Ionide.LanguageServerProtocol.Types

open System
open System.Text.Json

/// Marks the fixed discriminator value of a protocol record used in an erased union.
type UnionKindAttribute(value: string) =
  inherit Attribute()
  member _.Value = value

/// Marks a protocol union whose case wrapper is erased on the JSON wire.
type ErasedUnionAttribute() =
  inherit Attribute()

[<ErasedUnion; System.Diagnostics.DebuggerDisplay("U2")>]
type U2<'T1, 'T2> =
  | C1 of 'T1
  | C2 of 'T2

  override _.ToString() = "U2"

[<ErasedUnion; System.Diagnostics.DebuggerDisplay("U3")>]
type U3<'T1, 'T2, 'T3> =
  | C1 of 'T1
  | C2 of 'T2
  | C3 of 'T3

  override _.ToString() = "U3"

[<ErasedUnion; System.Diagnostics.DebuggerDisplay("U4")>]
type U4<'T1, 'T2, 'T3, 'T4> =
  | C1 of 'T1
  | C2 of 'T2
  | C3 of 'T3
  | C4 of 'T4

  override _.ToString() = "U4"

/// A JSON value carried in an LSP `any` slot.
[<Sealed>]
type LSPAny private (element: JsonElement) =
  let element = element.Clone()

  /// The underlying System.Text.Json value.
  member _.JsonElement = element

  override _.ToString() = element.GetRawText()

  override _.GetHashCode() = StringComparer.Ordinal.GetHashCode(element.GetRawText())

  override _.Equals(obj: obj) =
    match obj with
    | :? LSPAny as value -> StringComparer.Ordinal.Equals(element.GetRawText(), value.JsonElement.GetRawText())
    | _ -> false

  interface IEquatable<LSPAny> with
    member _.Equals(other) =
      not (obj.ReferenceEquals(other, null))
      && StringComparer.Ordinal.Equals(element.GetRawText(), other.JsonElement.GetRawText())

  /// Wraps a System.Text.Json value without retaining its owning JsonDocument.
  static member fromJsonElement(element: JsonElement) = LSPAny(element)
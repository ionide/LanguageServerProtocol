namespace Ionide.LanguageServerProtocol.Types

open Ionide.LanguageServerProtocol


/// Types in typescript can have hardcoded values for their fields, this attribute is used to mark
/// the default value for a field in a type and is used when deserializing the type to json
/// but these types might not actually be used as a discriminated union or only partially used 
/// so we don't generate a dedicated union type because of that
/// 
/// see https://microsoft.github.io/language-server-protocol/specifications/lsp/3.18/specification/#resourceChanges for a dedicated example
type UnionKindAttribute( value: string ) =
    inherit System.Attribute()
    member x.Value = value

/// Represents a Union type where the individual cases are erased when serialized or deserialized
/// For instance a union could be defined as: "string | int | bool" and when serialized it would be
/// serialized as a only a value based on the actual case
type ErasedUnionAttribute() =
    inherit System.Attribute()

[<ErasedUnion>]
type U2<'T1, 'T2> =
    | C1 of 'T1
    | C2 of 'T2

[<ErasedUnion>]
type U3<'T1, 'T2, 'T3> =
    | C1 of 'T1
    | C2 of 'T2
    | C3 of 'T3

[<ErasedUnion>]
type U4<'T1, 'T2, 'T3, 'T4> =
    | C1 of 'T1
    | C2 of 'T2
    | C3 of 'T3
    | C4 of 'T4


type LspResult<'t> = Result<'t, JsonRpc.Error>
type AsyncLspResult<'t> = Async<LspResult<'t>>


module LspResult =

  let success x : LspResult<_> = Result.Ok x

  let invalidParams s : LspResult<_> = Result.Error(JsonRpc.Error.Create(JsonRpc.ErrorCodes.invalidParams, s))

  let internalError<'a> (s: string) : LspResult<'a> =
    Result.Error(JsonRpc.Error.Create(JsonRpc.ErrorCodes.internalError, s))

  let notImplemented<'a> : LspResult<'a> = Result.Error(JsonRpc.Error.MethodNotFound)

  let requestCancelled<'a> : LspResult<'a> = Result.Error(JsonRpc.Error.RequestCancelled)

module AsyncLspResult =

  let success x : AsyncLspResult<_> = async.Return(Result.Ok x)

  let invalidParams s : AsyncLspResult<_> =
    async.Return(Result.Error(JsonRpc.Error.Create(JsonRpc.ErrorCodes.invalidParams, s)))

  let internalError s : AsyncLspResult<_> =
    async.Return(Result.Error(JsonRpc.Error.Create(JsonRpc.ErrorCodes.internalError, s)))

  let notImplemented<'a> : AsyncLspResult<'a> = async.Return(Result.Error(JsonRpc.Error.MethodNotFound))

  let requestCancelled<'a> : AsyncLspResult<'a> = async.Return(Result.Error(JsonRpc.Error.RequestCancelled))

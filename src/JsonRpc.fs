module Ionide.LanguageServerProtocol.JsonRpc

open System.Text.Json
open System.Text.Json.Serialization

type MessageTypeTest = {
  [<JsonPropertyName("jsonrpc")>]
  Version: string
  Id: int option
  Method: string option
}

[<RequireQualifiedAccess>]
type MessageType =
  | Notification
  | Request
  | Response
  | Error

let getMessageType messageTest =
  match messageTest with
  | { Version = "2.0"; Id = Some _; Method = Some _ } -> MessageType.Request
  | { Version = "2.0"; Id = Some _; Method = None } -> MessageType.Response
  | { Version = "2.0"; Id = None; Method = Some _ } -> MessageType.Notification
  | _ -> MessageType.Error

type Request = {
  [<JsonPropertyName("jsonrpc")>]
  Version: string
  Id: int
  Method: string
  Params: JsonElement option
} with

  static member Create(id: int, method': string, rpcParams: JsonElement option) = {
    Version = "2.0"
    Id = id
    Method = method'
    Params = rpcParams
  }

type Notification = {
  [<JsonPropertyName("jsonrpc")>]
  Version: string
  Method: string
  Params: JsonElement option
} with

  static member Create(method': string, rpcParams: JsonElement option) = {
    Version = "2.0"
    Method = method'
    Params = rpcParams
  }

module ErrorCodes =
  ///<summary>This is the start range of JSON-RPC reserved error codes.
  ///It doesn't denote a real error code. No LSP error codes should
  ///be defined between the start and end range. For backwards
  ///compatibility the <see cref="ServerNotInitialized"/> and the <see cref="UnknownErrorCode" />
  ///are left in the range.</summary>
  let jsonrpcReservedErrorRangeStart = -32099
  ///This is the end range of JSON-RPC reserved error codes. It doesn't denote a real error code.
  let jsonrpcReservedErrorRangeEnd = -32000

  /// This is the start range of LSP reserved error codes. It doesn't denote a real error code.
  let lspReservedErrorRangeStart = -32899
  /// This is the end range of LSP reserved error codes. It doesn't denote a real error code.
  let lspReservedErrorRangeEnd = -32899

open Ionide.LanguageServerProtocol.Types

type Error = {
  Code: int
  Message: string
  Data: JsonElement option
} with

  static member Create(code: int, message: string) = { Code = code; Message = message; Data = None }

  static member ParseError(?message) =
    let message = defaultArg message "Parse error"
    Error.Create(int ErrorCodes.ParseError, message)

  static member InvalidRequest(?message) =
    let message = defaultArg message "Invalid Request"
    Error.Create(int ErrorCodes.InvalidRequest, message)

  static member MethodNotFound(?message) =
    let message = defaultArg message "Method not found"
    Error.Create(int ErrorCodes.MethodNotFound, message)

  static member InvalidParams(?message) =
    let message = defaultArg message "Invalid params"
    Error.Create(int ErrorCodes.InvalidParams, message)

  static member InternalError(?message: string) =
    let message = defaultArg message "Internal error"
    Error.Create(int ErrorCodes.InternalError, message)

  static member RequestCancelled(?message) =
    let message = defaultArg message "Request cancelled"
    Error.Create(int LSPErrorCodes.RequestCancelled, message)

type Response = {
  [<JsonPropertyName("jsonrpc")>]
  Version: string
  Id: int option
  Error: Error option
  Result: JsonElement option
} with

  static member Success(id: int, result: JsonElement option) = {
    Version = "2.0"
    Id = Some id
    Result = result
    Error = None
  }

  static member Failure(id: int, error: Error) = { Version = "2.0"; Id = Some id; Result = None; Error = Some error }

/// Custom STJ converter for Response:
/// - Always writes "result" (as null) when Error is None
/// - Omits "result" when Error is Some
type ResponseConverter() =
  inherit JsonConverter<Response>()

  override _.Read(reader, _t, opts) =
    // Parse manually to avoid infinite recursion
    let mutable version = ""
    let mutable id: int option = None
    let mutable error: Error option = None
    let mutable result: JsonElement option = None

    if reader.TokenType = JsonTokenType.StartObject then
      while reader.Read()
            && reader.TokenType
               <> JsonTokenType.EndObject do
        if reader.TokenType = JsonTokenType.PropertyName then
          let propName = reader.GetString()

          reader.Read()
          |> ignore

          match propName with
          | "jsonrpc" -> version <- reader.GetString()
          | "id" when
            reader.TokenType
            <> JsonTokenType.Null
            ->
            id <- Some(reader.GetInt32())
          | "error" when
            reader.TokenType
            <> JsonTokenType.Null
            ->
            error <- Some(JsonSerializer.Deserialize<Error>(&reader, opts))
          | "result" when
            reader.TokenType
            <> JsonTokenType.Null
            ->
            result <- Some(JsonSerializer.Deserialize<JsonElement>(&reader, opts))
          | _ -> reader.Skip()

    { Version = version; Id = id; Error = error; Result = result }

  override _.Write(writer, value, opts) =
    writer.WriteStartObject()
    writer.WriteString("jsonrpc", value.Version)

    match value.Id with
    | Some id -> writer.WriteNumber("id", id)
    | None -> ()

    match value.Error with
    | Some err ->
      writer.WritePropertyName("error")
      JsonSerializer.Serialize(writer, err, opts)
    | None ->
      writer.WritePropertyName("result")

      match value.Result with
      | Some je -> je.WriteTo(writer)
      | None -> writer.WriteNullValue()

    writer.WriteEndObject()


/// Result type composed of a success value or an error of type JsonRpc.Error
type LspResult<'t> = Result<'t, Error>
/// Async Result type composed of a success value or an error of type JsonRpc.Error
type AsyncLspResult<'t> = Async<LspResult<'t>>


module LspResult =

  let success x : LspResult<_> = Result.Ok x

  let invalidParams message : LspResult<_> = Result.Error(Error.InvalidParams message)

  let internalError<'a> (message: string) : LspResult<'a> =
    Result.Error(Error.Create(int ErrorCodes.InvalidParams, message))

  let notImplemented<'a> : LspResult<'a> = Result.Error(Error.MethodNotFound())

  let requestCancelled<'a> : LspResult<'a> = Result.Error(Error.RequestCancelled())

module AsyncLspResult =

  let success x : AsyncLspResult<_> = async.Return(Result.Ok x)

  let invalidParams message : AsyncLspResult<_> = async.Return(LspResult.invalidParams message)

  let internalError message : AsyncLspResult<_> = async.Return(LspResult.internalError message)

  let notImplemented<'a> : AsyncLspResult<'a> = async.Return LspResult.notImplemented

  let requestCancelled<'a> : AsyncLspResult<'a> = async.Return LspResult.requestCancelled


module Requests =
  open StreamJsonRpc
  open System
  open System.Threading
  open System.Threading.Tasks

  let requestHandling<'param, 'result> (run: 'param -> AsyncLspResult<'result>) : Delegate =
    let runAsTask param ct =
      // Execute non-async portion of `run` before forking the async portion into a task.
      // This is needed to avoid reordering of messages from a client.
      let asyncLspResult = run param

      let asyncContinuation =
        async {
          let! lspResult = asyncLspResult

          return
            match lspResult with
            | Ok result -> result
            | Error error ->
              let rpcException = LocalRpcException(error.Message)
              rpcException.ErrorCode <- error.Code

              rpcException.ErrorData <-
                error.Data
                |> Option.map (fun je -> je :> obj)
                |> Option.defaultValue null

              raise rpcException
        }

      Async.StartAsTask(asyncContinuation, cancellationToken = ct)

    Func<'param, CancellationToken, Task<'result>>(runAsTask) :> Delegate

  /// Notifications don't generate a response or error, but to unify things we consider them as always successful.
  /// They will still not send any response because their ID is null.
  let internal notificationSuccess (response: Async<unit>) =
    async {
      do! response
      return Result.Ok()
    }
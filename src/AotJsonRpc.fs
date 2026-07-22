module Ionide.LanguageServerProtocol.JsonRpc

open System
open System.Threading
open System.Threading.Tasks
open Ionide.LanguageServerProtocol.Types
open StreamJsonRpc

module ErrorCodes =
  let jsonrpcReservedErrorRangeStart = -32099
  let jsonrpcReservedErrorRangeEnd = -32000
  let lspReservedErrorRangeStart = -32899
  let lspReservedErrorRangeEnd = -32899

type Error = {
  Code: int
  Message: string
  Data: LSPAny option
} with

  override _.ToString() = "Language server protocol error"

  static member Create(code: int, message: string) = { Code = code; Message = message; Data = None }

  static member ParseError(?message) = Error.Create(int Types.ErrorCodes.ParseError, defaultArg message "Parse error")

  static member InvalidRequest(?message) =
    Error.Create(int Types.ErrorCodes.InvalidRequest, defaultArg message "Invalid Request")

  static member MethodNotFound(?message) =
    Error.Create(int Types.ErrorCodes.MethodNotFound, defaultArg message "Method not found")

  static member InvalidParams(?message) =
    Error.Create(int Types.ErrorCodes.InvalidParams, defaultArg message "Invalid params")

  static member InternalError(?message: string) =
    Error.Create(int Types.ErrorCodes.InternalError, defaultArg message "Internal error")

  static member RequestCancelled(?message) =
    Error.Create(int LSPErrorCodes.RequestCancelled, defaultArg message "Request cancelled")

type LspResult<'result> = Result<'result, Error>
type AsyncLspResult<'result> = Async<LspResult<'result>>

module LspResult =
  let success x : LspResult<_> = Ok x
  let invalidParams message : LspResult<_> = Error(Error.InvalidParams message)

  let internalError<'a> (message: string) : LspResult<'a> =
    Error(Error.Create(int Types.ErrorCodes.InvalidParams, message))

  let notImplemented<'a> : LspResult<'a> = Error(Error.MethodNotFound())
  let requestCancelled<'a> : LspResult<'a> = Error(Error.RequestCancelled())

module AsyncLspResult =
  let success x : AsyncLspResult<_> = async.Return(Ok x)
  let invalidParams message : AsyncLspResult<_> = async.Return(LspResult.invalidParams message)
  let internalError message : AsyncLspResult<_> = async.Return(LspResult.internalError message)
  let notImplemented<'a> : AsyncLspResult<'a> = async.Return LspResult.notImplemented
  let requestCancelled<'a> : AsyncLspResult<'a> = async.Return LspResult.requestCancelled

module Requests =
  let requestHandling<'param, 'result> (run: 'param -> AsyncLspResult<'result>) : Delegate =
    let runAsTask param ct =
      let pending = run param

      async {
        let! result = pending

        match result with
        | Ok value -> return value
        | Error error ->
          let rpcException = LocalRpcException(error.Message)
          rpcException.ErrorCode <- error.Code

          rpcException.ErrorData <-
            error.Data
            |> Option.map box
            |> Option.defaultValue null

          return raise rpcException
      }
      |> fun operation -> Async.StartAsTask(operation, cancellationToken = ct)

    Func<'param, CancellationToken, Task<'result>>(runAsTask) :> Delegate

  let internal notificationSuccess (response: Async<unit>) =
    async {
      do! response
      return Ok()
    }
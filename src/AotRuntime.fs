namespace Ionide.LanguageServerProtocol

module internal AotRuntime =
  open System
  open System.Text.Json
  open System.Threading
  open System.Threading.Tasks
  open Ionide.LanguageServerProtocol.JsonRpc
  open Ionide.LanguageServerProtocol.StaticMetadata
  open StreamJsonRpc

  let private findHandling server (handlings: Map<string, Mappings.ServerRequestHandling<'server>>) route =
    match Map.tryFind route handlings with
    | Some handling -> handling.Run server
    | None ->
      let rpcException = LocalRpcException(String.Concat("Method not found: ", route))
      rpcException.ErrorCode <- int Types.ErrorCodes.MethodNotFound
      raise rpcException

  let invokeWithParameter<'server, 'parameter, 'result when 'server :> ILspServer>
    (server: 'server)
    (handlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    route
    (request: JsonElement)
    (cancellationToken: CancellationToken)
    (_infer: 'parameter -> AsyncLspResult<'result>)
    =
    task {
      let handling = findHandling server handlings route :?> Func<'parameter, CancellationToken, Task<'result>>
      let parameter = ProtocolMetadata.Deserialize<'parameter>(request)
      let! result = handling.Invoke(parameter, cancellationToken)
      return ProtocolMetadata.Serialize(result)
    }

  let invokeWithoutParameter<'server, 'result when 'server :> ILspServer>
    (server: 'server)
    (handlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    route
    (cancellationToken: CancellationToken)
    (_infer: unit -> AsyncLspResult<'result>)
    =
    task {
      let handling = findHandling server handlings route :?> Func<unit, CancellationToken, Task<'result>>
      let! result = handling.Invoke((), cancellationToken)
      return ProtocolMetadata.Serialize(result)
    }

  let notifyWithParameter<'server, 'parameter when 'server :> ILspServer>
    (server: 'server)
    (handlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    route
    (request: JsonElement)
    (cancellationToken: CancellationToken)
    (_infer: 'parameter -> Async<unit>)
    : Task =
    invokeWithParameter
      server
      handlings
      route
      request
      cancellationToken
      (fun parameter ->
        async {
          do! _infer parameter
          return Ok()
        }
      )
    :> Task

  let notifyWithoutParameter<'server when 'server :> ILspServer>
    (server: 'server)
    (handlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    route
    (cancellationToken: CancellationToken)
    (_infer: unit -> Async<unit>)
    : Task =
    invokeWithoutParameter
      server
      handlings
      route
      cancellationToken
      (fun () ->
        async {
          do! _infer ()
          return Ok()
        }
      )
    :> Task
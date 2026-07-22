namespace Ionide.LanguageServerProtocol

module Server =
  open System
  open System.IO
  open System.Threading
  open System.Threading.Tasks
  open Ionide.LanguageServerProtocol.JsonRpc
  open Ionide.LanguageServerProtocol.Logging
  open Ionide.LanguageServerProtocol.StaticMetadata
  open StreamJsonRpc
  open StreamJsonRpc.Protocol

  type ClientNotificationSender = string -> obj -> AsyncLspResult<unit>

  type ClientRequestSender =
    abstract member Send<'a> : string -> obj -> AsyncLspResult<'a>

  let logger = LogProvider.getLoggerByName "LSP Server"

  type LspCloseReason =
    | RequestedByClient = 0
    | ErrorExitWithoutShutdown = 1
    | ErrorStreamClosed = 2

  let requestHandling<'param, 'result> (run: 'param -> AsyncLspResult<'result>) = Requests.requestHandling run

  let serverRequestHandling<'server, 'param, 'result when 'server :> ILspServer>
    (run: 'server -> 'param -> AsyncLspResult<'result>)
    : Mappings.ServerRequestHandling<'server> =
    { Run = fun server -> requestHandling (run server) }

  let defaultRequestHandlings () : Map<string, Mappings.ServerRequestHandling<'server>> =
    Mappings.routeMappings ()
    |> Map.ofList

  type private StaticProtocolRpc(handler: IJsonRpcMessageHandler) =
    inherit JsonRpc(handler)

    override _.IsFatalException(exception': Exception) =
      match exception' with
      | :? LocalRpcException
      | :? System.Text.Json.JsonException -> false
      | _ -> true

    override this.CreateErrorDetails(request, exception') =
      match exception' with
      | :? System.Text.Json.JsonException as jsonException ->
        JsonRpcError.ErrorDetail(Code = JsonRpcErrorCode.ParseError, Message = jsonException.Message)
      | _ -> base.CreateErrorDetails(request, exception')

  let private run<'client, 'server when 'client :> ILspClient and 'server :> ILspServer>
    (requestHandlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    (handler: IJsonRpcMessageHandler)
    (clientCreator: (ClientNotificationSender * ClientRequestSender) -> 'client)
    (serverCreator: 'client -> 'server)
    =
    use jsonRpc = new StaticProtocolRpc(handler)

    let sendNotification methodName (value: obj) =
      async {
        do!
          ProtocolMetadata.NotifyAsync(jsonRpc, methodName, ProtocolMetadata.Serialize(value))
          |> Async.AwaitTask

        return LspResult.success ()
      }

    let sendRequest methodName (value: obj) =
      async {
        let! response =
          ProtocolMetadata.InvokeAsync(jsonRpc, methodName, ProtocolMetadata.Serialize(value))
          |> Async.AwaitTask

        return
          ProtocolMetadata.Deserialize<'response>(response)
          |> LspResult.success
      }

    use client =
      clientCreator (
        sendNotification,
        { new ClientRequestSender with
            member _.Send methodName value = sendRequest methodName value
        }
      )

    use server = serverCreator client
    let mutable shutdownReceived = false
    let mutable exitReceived = false
    use exitSemaphore = new SemaphoreSlim(0, 1)

    let target =
      StaticProtocolTarget(
        server,
        requestHandlings,
        Action(fun () -> shutdownReceived <- true),
        Action(fun () ->
          exitReceived <- true

          exitSemaphore.Release()
          |> ignore
        )
      )

    jsonRpc.AddLocalRpcTarget(ProtocolMetadata.Target, target, null)
    jsonRpc.StartListening()
    let completed = Task.WaitAny(jsonRpc.Completion, exitSemaphore.WaitAsync())

    if
      completed = 0
      && not jsonRpc.Completion.IsCompletedSuccessfully
    then
      jsonRpc.Completion.GetAwaiter().GetResult()

    match shutdownReceived, exitReceived with
    | true, true -> LspCloseReason.RequestedByClient
    | false, true -> LspCloseReason.ErrorExitWithoutShutdown
    | _ -> LspCloseReason.ErrorStreamClosed

  let start<'client, 'server when 'client :> ILspClient and 'server :> ILspServer>
    (requestHandlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    (input: Stream)
    (output: Stream)
    (clientCreator: (ClientNotificationSender * ClientRequestSender) -> 'client)
    (serverCreator: 'client -> 'server)
    =
    use handler = new HeaderDelimitedMessageHandler(output, input, FormatterFactory.Create())
    run requestHandlings handler clientCreator serverCreator

  let startWs<'client, 'server when 'client :> ILspClient and 'server :> ILspServer>
    (requestHandlings: Map<string, Mappings.ServerRequestHandling<'server>>)
    (socket: Net.WebSockets.WebSocket)
    (clientCreator: (ClientNotificationSender * ClientRequestSender) -> 'client)
    (serverCreator: 'client -> 'server)
    =
    use handler = new WebSocketMessageHandler(socket, FormatterFactory.Create())
    run requestHandlings handler clientCreator serverCreator
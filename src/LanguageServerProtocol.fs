namespace Ionide.LanguageServerProtocol

module JsonRpc =
  open StreamJsonRpc

  let verboseLogging (rpc: JsonRpc) = rpc.TraceSource.Switch.Level <- System.Diagnostics.SourceLevels.Verbose
  let addTraceLogger listener (rpc: JsonRpc) = rpc.TraceSource.Listeners.Add listener

module Server =
  open System.IO
  open StreamJsonRpc
  open Newtonsoft.Json
  open Ionide.LanguageServerProtocol.JsonUtils

  let jsonRpcFormatter () =
    let f = new JsonMessageFormatter()
    f.JsonSerializer.NullValueHandling <- NullValueHandling.Ignore
    f.JsonSerializer.ConstructorHandling <- ConstructorHandling.AllowNonPublicDefaultConstructor
    f.JsonSerializer.MissingMemberHandling <- MissingMemberHandling.Ignore
    f.JsonSerializer.Converters.Add(StrictNumberConverter())
    f.JsonSerializer.Converters.Add(StrictStringConverter())
    f.JsonSerializer.Converters.Add(StrictBoolConverter())
    f.JsonSerializer.Converters.Add(SingleCaseUnionConverter())
    f.JsonSerializer.Converters.Add(OptionConverter())
    f.JsonSerializer.Converters.Add(ErasedUnionConverter())
    f.JsonSerializer.ContractResolver <- OptionAndCamelCasePropertyNamesContractResolver()
    f

  let configureServerAndClient<'server, 'client when 'server :> ILspServer and 'client :> ILspClient>
    (serverInput: Stream)
    (serverOutput: Stream)
    (serverInstance: 'server)
    (clientInstance: 'client)
    modifyJsonSerializer
    =

    let createJsonFormatter () =
      let f = jsonRpcFormatter ()
      modifyJsonSerializer f.JsonSerializer
      f

    let commonOptions =
      JsonRpcTargetOptions(
        ClientRequiresNamedArguments = true,
        UseSingleObjectParameterDeserialization = true,
        DisposeOnDisconnect = true
      )

    let serverJson = createJsonFormatter ()
    let serverRpcHandler = new HeaderDelimitedMessageHandler(serverOutput, serverInput, serverJson)
    let serverRpc = new JsonRpc(serverRpcHandler)
    serverRpc.AddLocalRpcTarget(serverInstance, commonOptions)

    // for the client side we invert the source/target streams
    let clientJson = createJsonFormatter ()
    let clientRpcHandler = new HeaderDelimitedMessageHandler(serverInput, serverOutput, clientJson)
    let clientRpc = new JsonRpc(clientRpcHandler)
    clientRpc.AddLocalRpcTarget(clientInstance, commonOptions)

    serverRpc, clientRpc

  let configureBidirectionalServer<'server when 'server :> ILspServer and 'server :> ILspClient>
    (serverInput: Stream)
    (serverOutput: Stream)
    (serverInstance: 'server)
    modifyJsonSerializer
    =
    configureServerAndClient serverInput serverOutput serverInstance serverInstance modifyJsonSerializer
    |> fst

  let start (serverRpc: JsonRpc) =
    serverRpc.StartListening()
    serverRpc.Completion
module Ionide.LanguageServerProtocol.Tests.AotProtocolTests

open System
open System.Globalization
open System.IO
open System.IO.Pipes
open System.Text
open System.Threading.Tasks
open Expecto
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.JsonRpc
open Ionide.LanguageServerProtocol.Types

let private waitTimeout = TimeSpan.FromSeconds 5.0

let private frame (json: string) =
  String.Concat(
    "Content-Length: ",
    Encoding.UTF8.GetByteCount(json).ToString(CultureInfo.InvariantCulture),
    "\r\n\r\n",
    json
  )

let private writeMessage (stream: Stream) json =
  let bytes =
    json
    |> frame
    |> Encoding.UTF8.GetBytes

  stream.Write(bytes, 0, bytes.Length)
  stream.Flush()

type private ProtocolClient(notificationSender: Server.ClientNotificationSender) =
  inherit LspClient()

  override _.WindowLogMessage(parameters) =
    async {
      let! result = notificationSender "window/logMessage" parameters

      match result with
      | Ok() -> return ()
      | Error error -> return failwith error.Message
    }

type private ProtocolServer
  (
    client: ILspClient,
    initialized: TaskCompletionSource<string option>,
    documentOpened: TaskCompletionSource<string>,
    shutdown: TaskCompletionSource<unit>
  ) =
  inherit LspServer()

  override _.Dispose() = ()

  override _.Initialize(parameters) =
    async {
      do! client.WindowLogMessage { Type = MessageType.Info; Message = "server ready" }

      parameters.ClientInfo
      |> Option.map _.Name
      |> initialized.SetResult

      return LspResult.success InitializeResult.Default
    }

  override _.TextDocumentDidOpen(parameters) =
    parameters.TextDocument.Uri
    |> documentOpened.SetResult

    async.Return()

  override _.Shutdown() =
    shutdown.SetResult()
    AsyncLspResult.success ()

[<Tests>]
let tests =
  testList "protocol" [
    testCaseAsync "handles a complete protocol session"
    <| async {
      let initialized = TaskCompletionSource<string option>(TaskCreationOptions.RunContinuationsAsynchronously)
      let documentOpened = TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously)
      let shutdown = TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

      use serverInput = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.None)
      use clientOutput = new AnonymousPipeClientStream(PipeDirection.Out, serverInput.GetClientHandleAsString())
      use serverOutput = new AnonymousPipeServerStream(PipeDirection.Out, HandleInheritability.None)
      use clientInput = new AnonymousPipeClientStream(PipeDirection.In, serverOutput.GetClientHandleAsString())
      use outputReader = new StreamReader(clientInput, Encoding.UTF8)

      let outputTask = outputReader.ReadToEndAsync()

      let serverTask =
        Task.Run(fun () ->
          Server.start
            (Server.defaultRequestHandlings ())
            serverInput
            serverOutput
            (fun (notificationSender, _) -> new ProtocolClient(notificationSender))
            (fun client -> new ProtocolServer(client, initialized, documentOpened, shutdown))
        )

      writeMessage
        clientOutput
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"clientInfo":{"name":"regression client"},"capabilities":{}}}"""

      let! clientName =
        initialized.Task.WaitAsync(waitTimeout)
        |> Async.AwaitTask

      Expect.equal clientName (Some "regression client") "initialize parameters should reach the server"

      writeMessage
        clientOutput
        """{"jsonrpc":"2.0","method":"textDocument/didOpen","params":{"textDocument":{"uri":"file:///protocol.fs","languageId":"fsharp","version":1,"text":"let value = 1"}}}"""

      let! openedUri =
        documentOpened.Task.WaitAsync(waitTimeout)
        |> Async.AwaitTask

      Expect.equal openedUri "file:///protocol.fs" "notifications should reach the server"

      writeMessage clientOutput """{"jsonrpc":"2.0","id":2,"method":"shutdown"}"""

      do!
        shutdown.Task.WaitAsync(waitTimeout)
        |> Async.AwaitTask

      writeMessage clientOutput """{"jsonrpc":"2.0","method":"exit"}"""

      let! closeReason =
        serverTask.WaitAsync(waitTimeout)
        |> Async.AwaitTask

      let! output =
        outputTask.WaitAsync(waitTimeout)
        |> Async.AwaitTask

      Expect.equal closeReason Server.LspCloseReason.RequestedByClient "shutdown followed by exit should close cleanly"
      Expect.stringContains output "\"id\":1" "initialize should receive a response"
      Expect.stringContains output "\"id\":2" "shutdown should receive a response"
      Expect.stringContains output "\"method\":\"window/logMessage\"" "the server should send client traffic"
      Expect.stringContains output "\"message\":\"server ready\"" "outbound parameters should be serialized"
    }
  ]

[<EntryPoint>]
let main args = runTestsWithCLIArgs [ Sequenced ] args tests

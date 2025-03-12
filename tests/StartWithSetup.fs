module Ionide.LanguageServerProtocol.Tests.StartWithSetup

open Expecto
open System.IO.Pipes
open System.IO
open Ionide.LanguageServerProtocol
open Ionide.LanguageServerProtocol.Server
open Nerdbank.Streams
open StreamJsonRpc
open System
open System.Reflection
open StreamJsonRpc.Protocol

type TestLspClient(sendServerNotification: ClientNotificationSender, sendServerRequest: ClientRequestSender) =
  inherit LspClient()

let setupEndpoints (_: LspClient) : Map<string, System.Delegate> =
  []
  |> Map.ofList

let requestWithContentLength (request: string) = $"Content-Length: {request.Length}\r\n\r\n{request}"

let shutdownRequest = @"{""jsonrpc"":""2.0"",""method"":""shutdown"",""id"":1}"

let exitRequest = @"{""jsonrpc"":""2.0"",""method"":""exit"",""id"":1}"


type Foo = { bar: string; baz: string }

let tests =
  testList "startWithSetup" [
    testAsync "can start up multiple times in same process" {
      use inputServerPipe1 = new AnonymousPipeServerStream()
      use inputClientPipe1 = new AnonymousPipeClientStream(inputServerPipe1.GetClientHandleAsString())
      use outputServerPipe1 = new AnonymousPipeServerStream()

      use inputWriter1 = new StreamWriter(inputServerPipe1)
      inputWriter1.AutoFlush <- true

      let server1 =
        async {
          let result =
            startWithSetup setupEndpoints inputClientPipe1 outputServerPipe1 (fun x -> new TestLspClient(x)) defaultRpc

          Expect.equal (int result) 0 "server startup failed"
        }

      let! server1Async = Async.StartChild(server1)

      use inputServerPipe2 = new AnonymousPipeServerStream()
      use inputClientPipe2 = new AnonymousPipeClientStream(inputServerPipe2.GetClientHandleAsString())
      use outputServerPipe2 = new AnonymousPipeServerStream()

      use inputWriter2 = new StreamWriter(inputServerPipe2)
      inputWriter2.AutoFlush <- true

      let server2 =
        async {
          let result =
            (startWithSetup setupEndpoints inputClientPipe2 outputServerPipe2 (fun x -> new TestLspClient(x)) defaultRpc)

          Expect.equal (int result) 0 "server startup failed"
        }

      let! server2Async = Async.StartChild(server2)

      inputWriter1.Write(requestWithContentLength (shutdownRequest))
      inputWriter1.Write(requestWithContentLength (exitRequest))

      inputWriter2.Write(requestWithContentLength (shutdownRequest))
      inputWriter2.Write(requestWithContentLength (exitRequest))

      do! server1Async
      do! server2Async
    }

    testCaseAsync "Handle JsonSerializationError gracefully"
    <| async {

      let struct (server, client) = FullDuplexStream.CreatePair()

      use jsonRpcHandler = new HeaderDelimitedMessageHandler(server, defaultJsonRpcFormatter ())
      use serverRpc = defaultRpc jsonRpcHandler

      let functions: Map<string, Delegate> = Map [ "lol", Func<Foo, Foo>(fun (foo: Foo) -> foo) :> Delegate ]

      functions
      |> Seq.iter (fun (KeyValue(name, rpcDelegate)) ->
        let rpcAttribute = JsonRpcMethodAttribute(name)
        rpcAttribute.UseSingleObjectParameterDeserialization <- true
        serverRpc.AddLocalRpcMethod(rpcDelegate.GetMethodInfo(), rpcDelegate.Target, rpcAttribute)
      )

      serverRpc.StartListening()
      let create (s: Stream) : JsonRpc = JsonRpc.Attach(s, target = null)
      let clientRpc: JsonRpc = create client

      try
        let! (_: Foo) =
          clientRpc.InvokeWithParameterObjectAsync<Foo>("lol", {| bar = "lol" |})
          |> Async.AwaitTask

        ()
      with Flatten(:? RemoteInvocationException as ex) ->
        Expect.equal
          ex.Message
          "Required property 'baz' not found in JSON. Path ''."
          "Get parse error message and not crash process"

        Expect.equal ex.ErrorCode ((int) JsonRpcErrorCode.ParseError) "Should get parse error code"
    }
  ]
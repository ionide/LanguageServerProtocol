﻿module Ionide.LanguageServerProtocol.Tests.Root

[<EntryPoint>]
let main args =
  let (|ShouldRunBenchmarks|_|) (args: string []) =
    let nArgs = args.Length
    let markers = [| "--benchmark"; "--benchmarks"; "-b" |]

    let args =
      args
      |> Array.filter (fun arg -> markers |> Array.contains arg |> not)

    if args.Length = nArgs then None else Some args

  match args with
  | ShouldRunBenchmarks args ->
    // `--filter *` to run all
    Benchmarks.run args
  | _ -> Expecto.Tests.runTestsWithCLIArgs [] args Tests.tests
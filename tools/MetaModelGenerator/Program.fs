namespace MetaModelGenerator

module Main =
  open Argu
  open System
  open System.Text.Json
  open System.IO

  type TypeArgs =
    | MetaModelPath of string
    | OutputFilePath of string

    interface IArgParserTemplate with
      member this.Usage: string =
        match this with
        | MetaModelPath _ ->
          "The path to metaModel.json. See https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#metaModel"
        | OutputFilePath _ -> "The path to the output file. Should end with .fs"

  type ClientServerArgs =
    | MetaModelPath of string
    | OutputFilePath of string

    interface IArgParserTemplate with
      member this.Usage: string =
        match this with
        | MetaModelPath _ ->
          "The path to metaModel.json. See https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#metaModel"
        | OutputFilePath _ -> "The path to the output file. Should end with .fs"

  type AotMetadataArgs =
    | MetaModelPath of string
    | ContractAssemblyPath of string
    | CSharpOutputFilePath of string
    | FSharpOutputFilePath of string

    interface IArgParserTemplate with
      member this.Usage =
        match this with
        | MetaModelPath _ -> "The path to metaModel.json."
        | ContractAssemblyPath _ -> "The path to the built netstandard2.0 protocol assembly."
        | CSharpOutputFilePath _ -> "The generated C# metadata source path."
        | FSharpOutputFilePath _ -> "The generated F# static target source path."

  type AotTypesArgs =
    | MetaModelPath of string
    | OutputFilePath of string

    interface IArgParserTemplate with
      member this.Usage =
        match this with
        | MetaModelPath _ -> "The path to metaModel.json."
        | OutputFilePath _ -> "The generated modern F# protocol types source path."

  type CommandArgs =
    | [<CliPrefix(CliPrefix.None)>] Types of ParseResults<TypeArgs>
    | [<CliPrefix(CliPrefix.None)>] ClientServer of ParseResults<ClientServerArgs>
    | [<CliPrefix(CliPrefix.None)>] AotTypes of ParseResults<AotTypesArgs>
    | [<CliPrefix(CliPrefix.None)>] AotMetadata of ParseResults<AotMetadataArgs>

    interface IArgParserTemplate with
      member this.Usage =
        match this with
        | Types _ -> "Generates Types from metaModel.json."
        | ClientServer _ -> "Generates Client/Server"
        | AotTypes _ -> "Generates System.Text.Json-compatible protocol types."
        | AotMetadata _ -> "Generates Native AOT serialization and RPC metadata."

  let readMetaModel metamodelPath =
    async {

      printfn "Reading in %s" metamodelPath

      let! metaModel =
        File.ReadAllTextAsync(metamodelPath)
        |> Async.AwaitTask

      printfn "Deserializing metaModel"

      let parsedMetaModel =
        JsonSerializer.Deserialize<MetaModel.MetaModel>(metaModel, MetaModel.metaModelSerializerOptions)

      return parsedMetaModel
    }


  [<EntryPoint>]
  let main argv =

    let errorHandler =
      ProcessExiter(
        colorizer =
          function
          | ErrorCode.HelpText -> None
          | _ -> Some ConsoleColor.Red
      )

    let parser = ArgumentParser.Create<CommandArgs>(programName = "MetaModelGenerator", errorHandler = errorHandler)

    let results = parser.ParseCommandLine argv

    match results.GetSubCommand() with
    | Types r ->
      let metaModelPath = r.GetResult <@ TypeArgs.MetaModelPath @>
      let OutputFilePath = r.GetResult <@ TypeArgs.OutputFilePath @>

      let metaModel =
        readMetaModel metaModelPath
        |> Async.RunSynchronously

      GenerateTypes.generateType metaModel OutputFilePath
      |> Async.RunSynchronously

    | ClientServer r ->

      let metaModelPath = r.GetResult <@ ClientServerArgs.MetaModelPath @>
      let OutputFilePath = r.GetResult <@ ClientServerArgs.OutputFilePath @>

      let metaModel =
        readMetaModel metaModelPath
        |> Async.RunSynchronously

      GenerateClientServer.generateClientServer metaModel OutputFilePath
      |> Async.RunSynchronously

    | AotTypes r ->
      let metaModelPath = r.GetResult <@ AotTypesArgs.MetaModelPath @>
      let outputFilePath = r.GetResult <@ AotTypesArgs.OutputFilePath @>

      let metaModel =
        readMetaModel metaModelPath
        |> Async.RunSynchronously

      GenerateTypes.generateAotType metaModel outputFilePath
      |> Async.RunSynchronously

    | AotMetadata r ->
      let metaModelPath = r.GetResult <@ AotMetadataArgs.MetaModelPath @>
      let contractAssemblyPath = r.GetResult <@ AotMetadataArgs.ContractAssemblyPath @>
      let csharpOutputPath = r.GetResult <@ AotMetadataArgs.CSharpOutputFilePath @>
      let fsharpOutputPath = r.GetResult <@ AotMetadataArgs.FSharpOutputFilePath @>

      let metaModel =
        readMetaModel metaModelPath
        |> Async.RunSynchronously

      GenerateAotMetadata.generate metaModel contractAssemblyPath csharpOutputPath fsharpOutputPath
      |> Async.RunSynchronously

    0
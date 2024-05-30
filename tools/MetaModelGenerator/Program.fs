namespace MetaModelGenerator

module Main =
  open Argu
  open System

  type TypeArgs =
  | MetaModelPath of string
  | OutputFilePath of string
    interface IArgParserTemplate with
        member this.Usage: string = 
            match this with
            | MetaModelPath _ -> "The path to metaModel.json. See https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#metaModel"
            | OutputFilePath _ -> "The path to the output file. Should end with .fs"
      
  type CommandArgs =
  | [<CliPrefix(CliPrefix.None)>] Types of ParseResults<TypeArgs>
    interface IArgParserTemplate with
      member this.Usage =
        match this with
        | Types _ -> "Generates Types from metaModel.json."

  [<EntryPoint>]
  let main argv =
    
    let errorHandler = ProcessExiter(colorizer = function ErrorCode.HelpText -> None | _ -> Some ConsoleColor.Red)
    let parser = ArgumentParser.Create<CommandArgs>(programName = "MetaModelGenerator", errorHandler = errorHandler)

    let results = parser.ParseCommandLine argv
    match results.GetSubCommand() with
    | Types r ->
      let metaModelPath = r.GetResult <@ TypeArgs.MetaModelPath @>
      let OutputFilePath = r.GetResult <@ TypeArgs.OutputFilePath @>

      GenerateTypes.generateType metaModelPath OutputFilePath |> Async.RunSynchronously
      

    0
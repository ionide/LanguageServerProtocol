module Ionide.LanguageServerProtocol.Tests.Benchmarks

open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.Server
open Newtonsoft.Json.Linq
open BenchmarkDotNet.Attributes
open BenchmarkDotNet.Running

[<MemoryDiagnoser>]
type MultipleTypesBenchmarks() =
  let initializeParams: InitializeParams =
    { ProcessId = Some 42
      ClientInfo = Some { Name = "foo"; Version = None }
      RootPath = Some "/"
      RootUri = Some "file://..."
      InitializationOptions = None
      Capabilities =
        Some
          { Workspace =
              Some
                { ApplyEdit = Some true
                  WorkspaceEdit =
                    Some
                      { DocumentChanges = Some true
                        ResourceOperations =
                          Some [| ResourceOperationKind.Create
                                  ResourceOperationKind.Rename
                                  ResourceOperationKind.Delete |]
                        FailureHandling = Some FailureHandlingKind.Abort
                        NormalizesLineEndings = None
                        ChangeAnnotationSupport = Some { GroupsOnLabel = Some false } }
                  DidChangeConfiguration = None
                  DidChangeWatchedFiles = None
                  Symbol =
                    Some
                      { DynamicRegistration = Some false
                        SymbolKind = Some { ValueSet = Some SymbolKindCapabilities.DefaultValueSet } }
                  SemanticTokens = Some { RefreshSupport = Some true }
                  InlayHint = Some { RefreshSupport = Some false } }
            TextDocument =
              Some
                { Synchronization =
                    Some
                      { DynamicRegistration = Some true
                        WillSave = Some true
                        WillSaveWaitUntil = Some false
                        DidSave = Some true }
                  PublishDiagnostics = { RelatedInformation = None; TagSupport = None }
                  Completion = None
                  Hover =
                    Some
                      { DynamicRegistration = Some true
                        ContentFormat = Some [| "markup"; "plaintext" |] }
                  SignatureHelp =
                    Some
                      { DynamicRegistration = Some true
                        SignatureInformation = Some { DocumentationFormat = None } }
                  References = Some { DynamicRegistration = Some false }
                  DocumentHighlight = Some { DynamicRegistration = None }
                  DocumentSymbol = None
                  Formatting = Some { DynamicRegistration = Some true }
                  RangeFormatting = Some { DynamicRegistration = Some true }
                  OnTypeFormatting = None
                  Definition = Some { DynamicRegistration = Some false }
                  CodeAction =
                    Some
                      { DynamicRegistration = Some true
                        CodeActionLiteralSupport =
                          Some
                            { CodeActionKind =
                                { ValueSet = [| "foo"; "bar"; "baz"; "alpha"; "beta"; "gamma"; "delta"; "x"; "y"; "z" |] } }
                        IsPreferredSupport = Some true
                        DisabledSupport = Some false
                        DataSupport = None
                        ResolveSupport = Some { Properties = [| "foo"; "bar"; "baz" |] }
                        HonorsChangeAnnotations = Some false }
                  CodeLens = Some { DynamicRegistration = Some true }
                  DocumentLink = Some { DynamicRegistration = Some true }
                  Rename = None
                  FoldingRange =
                    Some
                      { DynamicRegistration = Some false
                        LineFoldingOnly = Some true
                        RangeLimit = None }
                  SelectionRange = Some { DynamicRegistration = None }
                  SemanticTokens =
                    Some
                      { DynamicRegistration = Some false
                        Requests = { Range = Some(U2.First true); Full = Some(U2.Second { Delta = Some true }) }
                        TokenTypes =
                          "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Proin tortor purus platea sit eu id nisi litora libero. Neque vulputate consequat ac amet augue blandit maximus aliquet congue. Pharetra vestibulum posuere ornare faucibus fusce dictumst orci aenean eu facilisis ut volutpat commodo senectus purus himenaeos fames primis convallis nisi."
                          |> fun s -> s.Split(' ')
                        TokenModifiers =
                          "Phasellus fermentum malesuada phasellus netus dictum aenean placerat egestas amet. Ornare taciti semper dolor tristique morbi. Sem leo tincidunt aliquet semper eu lectus scelerisque quis. Sagittis vivamus mollis nisi mollis enim fermentum laoreet."
                          |> fun s -> s.Split(' ')
                        Formats = [| TokenFormat.Relative |]
                        OverlappingTokenSupport = Some false
                        MultilineTokenSupport = Some true }
                  InlayHint =
                    Some
                      { DynamicRegistration = Some true
                        ResolveSupport = Some { Properties = [| "Tooltip"; "Position"; "TextEdits" |] } } }
            Experimental = None }
      trace = None
      WorkspaceFolders =
        Some [| { Uri = "..."; Name = "foo" }
                { Uri = "/"; Name = "bar" }
                { Uri = "something long stuff and even longer and longer and longer"
                  Name = "bar" } |] }

  let inlayHint: InlayHint =
    { Label =
        InlayHintLabel.Parts [| { InlayHintLabelPart.Value = "1st label"
                                  Tooltip = Some(InlayHintTooltip.String "1st label tooltip")
                                  Location = Some { Uri = "1st"; Range = mkRange' (1, 2) (3, 4) }
                                  Command = None }
                                { Value = "2nd label"
                                  Tooltip = Some(InlayHintTooltip.String "1st label tooltip")
                                  Location = Some { Uri = "2nd"; Range = mkRange' (5, 8) (10, 9) }
                                  Command = Some { Title = "2nd command"; Command = "foo"; Arguments = None } }
                                { InlayHintLabelPart.Value = "3rd label"
                                  Tooltip =
                                    Some(
                                      InlayHintTooltip.Markup
                                        { Kind = MarkupKind.Markdown
                                          Value =
                                            """
                                            # Header
                                            Description
                                            * List 1
                                            * List 2
                                            """ }
                                    )
                                  Location = Some { Uri = "3rd"; Range = mkRange' (1, 2) (3, 4) }
                                  Command = None } |]
      Position = { Line = 5; Character = 10 }
      Kind = Some InlayHintKind.Type
      TextEdits =
        Some [| { Range = mkRange' (5, 10) (6, 5); NewText = "foo bar" }
                { Range = mkRange' (5, 0) (5, 2); NewText = "baz" } |]
      Tooltip = Some(InlayHintTooltip.Markup { Kind = MarkupKind.PlainText; Value = "some tooltip" })
      PaddingLeft = Some true
      PaddingRight = Some false
      Data = Some(JToken.FromObject "some data") }

  let all: obj [] = [| initializeParams; inlayHint |]

  [<Benchmark>]
  member _.All_Roundtrip() =
    for o in all do
      let json = inlayHint |> serialize
      let res = json.ToObject(o.GetType(), jsonRpcFormatter.JsonSerializer)
      ()
  [<Benchmark>]
  member b.All_MultipleRoundtrips() =
    for i in 0..250 do
      b.All_Roundtrip()


let run (args) =
  let switcher = BenchmarkSwitcher.FromTypes([| typeof<MultipleTypesBenchmarks> |])
  switcher.Run(args) |> ignore
  0
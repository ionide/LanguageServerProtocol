namespace Ionide.LanguageServerProtocol

open Ionide.LanguageServerProtocol.Types

open System
open System.Threading
open System.Threading.Tasks

[<Interface>]
type LspServer =
  inherit System.IDisposable

  abstract member Dispose: unit -> unit

  /// The initialize request is sent as the first request from the client to the server.
  /// The initialize request may only be sent once.
  abstract member initialize: InitializeParams * IProgress<'T> -> Task<InitializeResult>


  /// The initialized notification is sent from the client to the server after the client received the result
  /// of the initialize request but before the client is sending any other request or notification to the server.
  /// The server can use the initialized notification for example to dynamically register capabilities.
  /// The initialized notification may only be sent once.
  abstract member initialized: EventHandler<InitializedParams>

  /// The shutdown request is sent from the client to the server. It asks the server to shut down, but to not
  /// exit (otherwise the response might not be delivered correctly to the client). There is a separate exit
  /// notification that asks the server to exit.
  abstract member shutdown: unit -> Task<unit>

  /// A notification to ask the server to exit its process.
  abstract member exit: unit -> Task<unit>

  /// The hover request is sent from the client to the server to request hover information at a given text
  /// document position.
  abstract member ``textDocument/hover``:
    TextDocumentPositionParams * IProgress<Hover option> * CancellationToken -> Task<Hover option>

  /// The document open notification is sent from the client to the server to signal newly opened text
  /// documents.
  ///
  /// The document’s truth is now managed by the client and the server must not try to read the document’s
  /// truth using the document’s uri. Open in this sense means it is managed by the client. It doesn't
  /// necessarily mean that its content is presented in an editor. An open notification must not be sent
  /// more than once without a corresponding close notification send before. This means open and close
  /// notification must be balanced and the max open count for a particular textDocument is one.
  abstract member ``textDocument/didOpen``: EventHandler<DidOpenTextDocumentParams>

  /// The document change notification is sent from the client to the server to signal changes to a text document.
  abstract member ``textDocument/didChange``: EventHandler<DidChangeTextDocumentParams>

  /// The Completion request is sent from the client to the server to compute completion items at a given
  /// cursor position. Completion items are presented in the IntelliSense user interface.
  ///
  /// If computing full completion items is expensive, servers can additionally provide a handler for the
  /// completion item resolve request (`completionItem/resolve`). This request is sent when a completion
  /// item is selected in the user interface. A typical use case is for example: the `textDocument/completion`
  /// request doesn’t fill in the documentation property for returned completion items since it is expensive
  /// to compute. When the item is selected in the user interface then a `completionItem/resolve` request is
  /// sent with the selected completion item as a param. The returned completion item should have the
  /// documentation property filled in. The request can delay the computation of the detail and documentation
  /// properties. However, properties that are needed for the initial sorting and filtering, like sortText,
  /// filterText, insertText, and textEdit must be provided in the textDocument/completion request and must
  /// not be changed during resolve.
  abstract member ``textDocument/completion``:
    CompletionParams * IProgress<CompletionItem array> * CancellationToken -> Task<CompletionList option>

  /// The request is sent from the client to the server to resolve additional information for a given
  /// completion item.
  abstract member ``completionItem/resolve``: CompletionItem -> Task<CompletionItem>

  /// The rename request is sent from the client to the server to perform a workspace-wide rename of a symbol.
  abstract member ``textDocument/rename``:
    RenameParams * IProgress<WorkspaceEdit option> * CancellationToken -> Task<WorkspaceEdit option>

  /// The prepare rename request is sent from the client to the server to setup and test the validity of a rename operation at a given location.
  /// If None is returned then it is deemed that a ‘textDocument/rename’ request is not valid at the given position.
  abstract member ``textDocument/prepareRename``:
    PrepareRenameParams * IProgress<PrepareRenameResult option> * CancellationToken -> Task<PrepareRenameResult option>

  /// The goto definition request is sent from the client to the server to resolve the definition location of
  /// a symbol at a given text document position.
  abstract member ``textDocument/definition``:
    TextDocumentPositionParams * IProgress<Location array> * CancellationToken -> Task<GotoResult option>

  /// The references request is sent from the client to the server to resolve project-wide references for
  /// the symbol denoted by the given text document position.
  abstract member ``textDocument/references``:
    ReferenceParams * IProgress<Location array> * CancellationToken -> Task<Location array option>

  /// The document highlight request is sent from the client to the server to resolve a document highlights
  /// for a given text document position. For programming languages this usually highlights all references
  /// to the symbol scoped to this file.
  ///
  /// However we kept `textDocument/documentHighlight` and `textDocument/references` separate requests since
  /// the first one is allowed to be more fuzzy. Symbol matches usually have a DocumentHighlightKind of Read
  /// or Write whereas fuzzy or textual matches use Text as the kind.
  abstract member ``textDocument/documentHighlight``:
    TextDocumentPositionParams * IProgress<DocumentHighlight array> * CancellationToken ->
      Task<DocumentHighlight array option>

  /// The document links request is sent from the client to the server to request the location of links
  /// in a document.
  abstract member ``textDocument/documentLink``:
    DocumentLinkParams * IProgress<DocumentLink array> * CancellationToken -> Task<DocumentLink array option>

  /// The goto type definition request is sent from the client to the server to resolve the type definition
  /// location of a symbol at a given text document position.
  abstract member ``textDocument/typeDefinition``:
    TextDocumentPositionParams * IProgress<Location array> * CancellationToken -> Task<GotoResult option>

  /// The goto implementation request is sent from the client to the server to resolve the implementation
  /// location of a symbol at a given text document position.
  abstract member ``textDocument/implementation``:
    TextDocumentPositionParams * IProgress<Location array> * CancellationToken -> Task<GotoResult option>

  /// The code action request is sent from the client to the server to compute commands for a given text
  /// document and range. These commands are typically code fixes to either fix problems or to
  /// beautify/refactor code. The result of a textDocument/codeAction request is an array of Command literals
  /// which are typically presented in the user interface. When the command is selected the server should be
  /// contacted again (via the workspace/executeCommand) request to execute the command.
  abstract member ``textDocument/codeAction``:
    CodeActionParams * IProgress<TextDocumentCodeActionResult array> * CancellationToken ->
      Task<TextDocumentCodeActionResult option>

  /// The code action request is sent from the client to the server to compute commands for a given text
  /// document and range. These commands are typically code fixes to either fix problems or to
  /// beautify/refactor code. The result of a textDocument/codeAction request is an array of Command literals
  /// which are typically presented in the user interface. When the command is selected the server should be
  /// contacted again (via the workspace/executeCommand) request to execute the command.
  abstract member ``codeAction/resolve``: CodeAction * CancellationToken -> Task<CodeAction option>

  /// The code lens request is sent from the client to the server to compute code lenses for a given
  /// text document.
  abstract member ``textDocument/codeLens``:
    CodeLensParams * IProgress<CodeLens array> * CancellationToken -> Task<CodeLens array option>

  /// The code lens resolve request is sent from the client to the server to resolve the command for
  /// a given code lens item.
  abstract member ``codeLens/resolve``: CodeLens * CancellationToken -> Task<CodeLens>

  /// The signature help request is sent from the client to the server to request signature information at
  /// a given cursor position.
  abstract member ``textDocument/signatureHelp``: SignatureHelpParams * CancellationToken -> Task<SignatureHelp option>

  /// The document link resolve request is sent from the client to the server to resolve the target of
  /// a given document link.
  abstract member ``documentLink/resolve``: DocumentLink * CancellationToken -> Task<DocumentLink>

  /// The document color request is sent from the client to the server to list all color references
  /// found in a given text document. Along with the range, a color value in RGB is returned.
  abstract member ``textDocument/documentColor``:
    DocumentColorParams * IProgress<ColorInformation array> * CancellationToken -> Task<ColorInformation array>

  /// The color presentation request is sent from the client to the server to obtain a list of
  /// presentations for a color value at a given location. Clients can use the result to
  abstract member ``textDocument/colorPresentation``:
    ColorPresentationParams * IProgress<ColorPresentation array> * CancellationToken -> Task<ColorPresentation array>

  /// The document formatting request is sent from the client to the server to format a whole document.
  abstract member ``textDocument/formatting``:
    DocumentFormattingParams * IProgress<TextEdit array> * CancellationToken -> Task<TextEdit array option>

  /// The document range formatting request is sent from the client to the server to format a given
  /// range in a document.
  abstract member ``textDocument/rangeFormatting``:
    DocumentRangeFormattingParams * IProgress<TextEdit array> * CancellationToken -> Task<TextEdit array option>

  /// The document on type formatting request is sent from the client to the server to format parts
  /// of the document during typing.
  abstract member ``textDocument/onTypeFormatting``:
    DocumentOnTypeFormattingParams * CancellationToken -> Task<TextEdit array option>

  /// The document symbol request is sent from the client to the server to return a flat list of all symbols
  /// found in a given text document. Neither the symbol’s location range nor the symbol’s container name
  /// should be used to infer a hierarchy.
  abstract member ``textDocument/documentSymbol``:
    DocumentSymbolParams * IProgress<U2<SymbolInformation array, DocumentSymbol array>> * CancellationToken ->
      Task<U2<SymbolInformation array, DocumentSymbol array> option>

  /// The watched files notification is sent from the client to the server when the client detects changes
  /// to files watched by the language client. It is recommended that servers register for these file
  /// events using the registration mechanism. In former implementations clients pushed file events without
  /// the server actively asking for it.
  abstract member ``workspace/didChangeWatchedFiles``: EventHandler<DidChangeWatchedFilesParams>

  /// The `workspace/didChangeWorkspaceFolders` notification is sent from the client to the server to inform
  /// the server about workspace folder configuration changes. The notification is sent by default if both
  /// *ServerCapabilities/workspace/workspaceFolders* and *ClientCapabilities/workapce/workspaceFolders* are
  /// true; or if the server has registered to receive this notification it first.
  abstract member ``workspace/didChangeWorkspaceFolders``: EventHandler<DidChangeWorkspaceFoldersParams>

  /// A notification sent from the client to the server to signal the change of configuration settings.
  abstract member ``workspace/didChangeConfiguration``: EventHandler<DidChangeConfigurationParams>

  /// The will create files request is sent from the client to the server before files are actually created
  /// as long as the creation is triggered from within the client either by a user action or by applying a
  /// workspace edit
  abstract member ``workspace/willCreateFiles``: CreateFilesParams * CancellationToken -> Task<WorkspaceEdit option>

  /// The did create files notification is sent from the client to the server when files were created
  /// from within the client.
  abstract member ``workspace/didCreateFiles``: EventHandler<CreateFilesParams>

  /// The will rename files request is sent from the client to the server before files are actually renamed
  /// as long as the rename is triggered from within the client either by a user action or by applying a
  /// workspace edit.
  abstract member ``workspace/willRenameFiles``: RenameFilesParams * CancellationToken -> Task<WorkspaceEdit option>

  /// The did rename files notification is sent from the client to the server when files were renamed from
  /// within the client.
  abstract member ``workspace/didRenameFiles``: EventHandler<RenameFilesParams>

  /// The will delete files request is sent from the client to the server before files are actually deleted
  /// as long as the deletion is triggered from within the client either by a user action or by applying a
  /// workspace edit.
  abstract member ``workspace/willDeleteFiles``: DeleteFilesParams * CancellationToken -> Task<WorkspaceEdit option>

  /// The did delete files notification is sent from the client to the server when files were deleted from
  /// within the client.
  abstract member ``workspace/didDeleteFiles``: EventHandler<DeleteFilesParams>

  /// The workspace symbol request is sent from the client to the server to list project-wide symbols matching
  /// the query string.
  abstract member ``workspace/symbol``:
    WorkspaceSymbolParams * IProgress<SymbolInformation array> * CancellationToken ->
      Task<SymbolInformation array option>

  /// The `workspace/executeCommand` request is sent from the client to the server to trigger command execution
  /// on the server. In most cases the server creates a `WorkspaceEdit` structure and applies the changes to the
  /// workspace using the request `workspace/applyEdit` which is sent from the server to the client.
  abstract member ``workspace/executeCommand``:
    ExecuteCommandParams * CancellationToken -> Task<Newtonsoft.Json.Linq.JToken>

  /// The document will save notification is sent from the client to the server before the document is
  /// actually saved.
  abstract member ``textDocument/willSave``: WillSaveTextDocumentParams * CancellationToken -> Task<unit>

  /// The document will save request is sent from the client to the server before the document is actually saved.
  /// The request can return an array of TextEdits which will be applied to the text document before it is saved.
  /// Please note that clients might drop results if computing the text edits took too long or if a server
  /// constantly fails on this request. This is done to keep the save fast and reliable.
  abstract member ``textDocument/willSaveWaitUntil``:
    WillSaveTextDocumentParams * CancellationToken -> Task<TextEdit array option>

  /// The document save notification is sent from the client to the server when the document was saved
  /// in the client.
  abstract member ``textDocument/didSave``: EventHandler<DidSaveTextDocumentParams>

  /// The document close notification is sent from the client to the server when the document got closed in the
  /// client. The document’s truth now exists where the document’s uri points to (e.g. if the document’s uri is
  /// a file uri the truth now exists on disk). As with the open notification the close notification is about
  /// managing the document’s content. Receiving a close notification doesn't mean that the document was open in
  /// an editor before. A close notification requires a previous open notification to be sent.
  abstract member ``textDocument/didClose``: EventHandler<DidCloseTextDocumentParams>

  /// The folding range request is sent from the client to the server to return all folding ranges found in a given text document.
  abstract member ``textDocument/foldingRange``:
    FoldingRangeParams * IProgress<FoldingRange array> * CancellationToken -> Task<FoldingRange array option>

  /// The selection range request is sent from the client to the server to return suggested selection ranges at an array of given positions.
  /// A selection range is a range around the cursor position which the user might be interested in selecting.
  abstract member ``textDocument/selectionRange``:
    SelectionRangeParams * IProgress<SelectionRange array> * CancellationToken -> Task<SelectionRange array option>

  abstract member ``textDocument/semanticTokensFull``:
    SemanticTokensParams * IProgress<SemanticTokensPartialResult> * CancellationToken -> Task<SemanticTokens option>

  abstract member ``textDocument/semanticTokensFullDelta``:
    SemanticTokensDeltaParams * CancellationToken -> Task<U2<SemanticTokens, SemanticTokensDelta> option>

  abstract member ``textDocument/semanticTokensRange``:
    SemanticTokensRangeParams * IProgress<SemanticTokensDeltaPartialResult> * CancellationToken ->
      Task<SemanticTokens option>

  /// The inlay hints request is sent from the client to the server to compute inlay hints for a given [text document, range] tuple
  ///  that may be rendered in the editor in place with other text.
  abstract member ``textDocument/inlayHint``: InlayHintParams * CancellationToken -> Task<InlayHint array option>

  /// The request is sent from the client to the server to resolve additional information for a given inlay hint.
  /// This is usually used to compute the `tooltip`, `location` or `command` properties of a inlay hint’s label part
  /// to avoid its unnecessary computation during the `textDocument/inlayHint` request.
  ///
  /// Consider the clients announces the `label.location` property as a property that can be resolved lazy using the client capability
  /// ```typescript
  /// textDocument.inlayHint.resolveSupport = { properties: ['label.location'] };
  /// ```
  /// then an inlay hint with a label part without a location needs to be resolved using the `inlayHint/resolve` request before it can be used.
  abstract member ``inlayHint/resolve``: InlayHint * CancellationToken -> Task<InlayHint>
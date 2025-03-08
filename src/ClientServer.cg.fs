namespace Ionide.LanguageServerProtocol

open Ionide.LanguageServerProtocol.Types
open Ionide.LanguageServerProtocol.JsonRpc

type ILspServer =
    inherit System.IDisposable
    // Notifications
    /// The `workspace/didChangeWorkspaceFolders` notification is sent from the client to the server when the workspace
    /// folder configuration changes.
    abstract member WorkspaceDidChangeWorkspaceFolders: DidChangeWorkspaceFoldersParams -> Async<unit>
    /// The `window/workDoneProgress/cancel` notification is sent from  the client to the server to cancel a progress
    /// initiated on the server side.
    abstract member WindowWorkDoneProgressCancel: WorkDoneProgressCancelParams -> Async<unit>
    /// The did create files notification is sent from the client to the server when
    /// files were created from within the client.
    ///
    /// @since 3.16.0
    abstract member WorkspaceDidCreateFiles: CreateFilesParams -> Async<unit>
    /// The did rename files notification is sent from the client to the server when
    /// files were renamed from within the client.
    ///
    /// @since 3.16.0
    abstract member WorkspaceDidRenameFiles: RenameFilesParams -> Async<unit>
    /// The will delete files request is sent from the client to the server before files are actually
    /// deleted as long as the deletion is triggered from within the client.
    ///
    /// @since 3.16.0
    abstract member WorkspaceDidDeleteFiles: DeleteFilesParams -> Async<unit>
    /// A notification sent when a notebook opens.
    ///
    /// @since 3.17.0
    abstract member NotebookDocumentDidOpen: DidOpenNotebookDocumentParams -> Async<unit>
    abstract member NotebookDocumentDidChange: DidChangeNotebookDocumentParams -> Async<unit>
    /// A notification sent when a notebook document is saved.
    ///
    /// @since 3.17.0
    abstract member NotebookDocumentDidSave: DidSaveNotebookDocumentParams -> Async<unit>
    /// A notification sent when a notebook closes.
    ///
    /// @since 3.17.0
    abstract member NotebookDocumentDidClose: DidCloseNotebookDocumentParams -> Async<unit>
    /// The initialized notification is sent from the client to the
    /// server after the client is fully initialized and the server
    /// is allowed to send requests from the server to the client.
    abstract member Initialized: InitializedParams -> Async<unit>
    /// The exit event is sent from the client to the server to
    /// ask the server to exit its process.
    abstract member Exit: unit -> Async<unit>
    /// The configuration change notification is sent from the client to the server
    /// when the client's configuration has changed. The notification contains
    /// the changed configuration as defined by the language client.
    abstract member WorkspaceDidChangeConfiguration: DidChangeConfigurationParams -> Async<unit>
    /// The document open notification is sent from the client to the server to signal
    /// newly opened text documents. The document's truth is now managed by the client
    /// and the server must not try to read the document's truth using the document's
    /// uri. Open in this sense means it is managed by the client. It doesn't necessarily
    /// mean that its content is presented in an editor. An open notification must not
    /// be sent more than once without a corresponding close notification send before.
    /// This means open and close notification must be balanced and the max open count
    /// is one.
    abstract member TextDocumentDidOpen: DidOpenTextDocumentParams -> Async<unit>
    /// The document change notification is sent from the client to the server to signal
    /// changes to a text document.
    abstract member TextDocumentDidChange: DidChangeTextDocumentParams -> Async<unit>
    /// The document close notification is sent from the client to the server when
    /// the document got closed in the client. The document's truth now exists where
    /// the document's uri points to (e.g. if the document's uri is a file uri the
    /// truth now exists on disk). As with the open notification the close notification
    /// is about managing the document's content. Receiving a close notification
    /// doesn't mean that the document was open in an editor before. A close
    /// notification requires a previous open notification to be sent.
    abstract member TextDocumentDidClose: DidCloseTextDocumentParams -> Async<unit>
    /// The document save notification is sent from the client to the server when
    /// the document got saved in the client.
    abstract member TextDocumentDidSave: DidSaveTextDocumentParams -> Async<unit>
    /// A document will save notification is sent from the client to the server before
    /// the document is actually saved.
    abstract member TextDocumentWillSave: WillSaveTextDocumentParams -> Async<unit>
    /// The watched files notification is sent from the client to the server when
    /// the client detects changes to file watched by the language client.
    abstract member WorkspaceDidChangeWatchedFiles: DidChangeWatchedFilesParams -> Async<unit>
    abstract member SetTrace: SetTraceParams -> Async<unit>
    abstract member CancelRequest: CancelParams -> Async<unit>
    abstract member Progress: ProgressParams -> Async<unit>
    // Requests
    /// A request to resolve the implementation locations of a symbol at a given text
    /// document position. The request's parameter is of type {@link TextDocumentPositionParams}
    /// the response is of type {@link Definition} or a Thenable that resolves to such.
    abstract member TextDocumentImplementation:
        ImplementationParams -> AsyncLspResult<U2<Definition,DefinitionLink array> option>

    /// A request to resolve the type definition locations of a symbol at a given text
    /// document position. The request's parameter is of type {@link TextDocumentPositionParams}
    /// the response is of type {@link Definition} or a Thenable that resolves to such.
    abstract member TextDocumentTypeDefinition:
        TypeDefinitionParams -> AsyncLspResult<U2<Definition,DefinitionLink array> option>

    /// A request to list all color symbols found in a given text document. The request's
    /// parameter is of type {@link DocumentColorParams} the
    /// response is of type {@link ColorInformation ColorInformation[]} or a Thenable
    /// that resolves to such.
    abstract member TextDocumentDocumentColor: DocumentColorParams -> AsyncLspResult<ColorInformation array>
    /// A request to list all presentation for a color. The request's
    /// parameter is of type {@link ColorPresentationParams} the
    /// response is of type {@link ColorInformation ColorInformation[]} or a Thenable
    /// that resolves to such.
    abstract member TextDocumentColorPresentation: ColorPresentationParams -> AsyncLspResult<ColorPresentation array>
    /// A request to provide folding ranges in a document. The request's
    /// parameter is of type {@link FoldingRangeParams}, the
    /// response is of type {@link FoldingRangeList} or a Thenable
    /// that resolves to such.
    abstract member TextDocumentFoldingRange: FoldingRangeParams -> AsyncLspResult<FoldingRange array option>

    /// A request to resolve the type definition locations of a symbol at a given text
    /// document position. The request's parameter is of type {@link TextDocumentPositionParams}
    /// the response is of type {@link Declaration} or a typed array of {@link DeclarationLink}
    /// or a Thenable that resolves to such.
    abstract member TextDocumentDeclaration:
        DeclarationParams -> AsyncLspResult<U2<Declaration,DeclarationLink array> option>

    /// A request to provide selection ranges in a document. The request's
    /// parameter is of type {@link SelectionRangeParams}, the
    /// response is of type {@link SelectionRange SelectionRange[]} or a Thenable
    /// that resolves to such.
    abstract member TextDocumentSelectionRange: SelectionRangeParams -> AsyncLspResult<SelectionRange array option>

    /// A request to result a `CallHierarchyItem` in a document at a given position.
    /// Can be used as an input to an incoming or outgoing call hierarchy.
    ///
    /// @since 3.16.0
    abstract member TextDocumentPrepareCallHierarchy:
        CallHierarchyPrepareParams -> AsyncLspResult<CallHierarchyItem array option>

    /// A request to resolve the incoming calls for a given `CallHierarchyItem`.
    ///
    /// @since 3.16.0
    abstract member CallHierarchyIncomingCalls:
        CallHierarchyIncomingCallsParams -> AsyncLspResult<CallHierarchyIncomingCall array option>

    /// A request to resolve the outgoing calls for a given `CallHierarchyItem`.
    ///
    /// @since 3.16.0
    abstract member CallHierarchyOutgoingCalls:
        CallHierarchyOutgoingCallsParams -> AsyncLspResult<CallHierarchyOutgoingCall array option>

    /// @since 3.16.0
    abstract member TextDocumentSemanticTokensFull: SemanticTokensParams -> AsyncLspResult<SemanticTokens option>

    /// @since 3.16.0
    abstract member TextDocumentSemanticTokensFullDelta:
        SemanticTokensDeltaParams -> AsyncLspResult<U2<SemanticTokens,SemanticTokensDelta> option>

    /// @since 3.16.0
    abstract member TextDocumentSemanticTokensRange: SemanticTokensRangeParams -> AsyncLspResult<SemanticTokens option>

    /// A request to provide ranges that can be edited together.
    ///
    /// @since 3.16.0
    abstract member TextDocumentLinkedEditingRange:
        LinkedEditingRangeParams -> AsyncLspResult<LinkedEditingRanges option>

    /// The will create files request is sent from the client to the server before files are actually
    /// created as long as the creation is triggered from within the client.
    ///
    /// The request can return a `WorkspaceEdit` which will be applied to workspace before the
    /// files are created. Hence the `WorkspaceEdit` can not manipulate the content of the file
    /// to be created.
    ///
    /// @since 3.16.0
    abstract member WorkspaceWillCreateFiles: CreateFilesParams -> AsyncLspResult<WorkspaceEdit option>
    /// The will rename files request is sent from the client to the server before files are actually
    /// renamed as long as the rename is triggered from within the client.
    ///
    /// @since 3.16.0
    abstract member WorkspaceWillRenameFiles: RenameFilesParams -> AsyncLspResult<WorkspaceEdit option>
    /// The did delete files notification is sent from the client to the server when
    /// files were deleted from within the client.
    ///
    /// @since 3.16.0
    abstract member WorkspaceWillDeleteFiles: DeleteFilesParams -> AsyncLspResult<WorkspaceEdit option>
    /// A request to get the moniker of a symbol at a given text document position.
    /// The request parameter is of type {@link TextDocumentPositionParams}.
    /// The response is of type {@link Moniker Moniker[]} or `null`.
    abstract member TextDocumentMoniker: MonikerParams -> AsyncLspResult<Moniker array option>

    /// A request to result a `TypeHierarchyItem` in a document at a given position.
    /// Can be used as an input to a subtypes or supertypes type hierarchy.
    ///
    /// @since 3.17.0
    abstract member TextDocumentPrepareTypeHierarchy:
        TypeHierarchyPrepareParams -> AsyncLspResult<TypeHierarchyItem array option>

    /// A request to resolve the supertypes for a given `TypeHierarchyItem`.
    ///
    /// @since 3.17.0
    abstract member TypeHierarchySupertypes:
        TypeHierarchySupertypesParams -> AsyncLspResult<TypeHierarchyItem array option>

    /// A request to resolve the subtypes for a given `TypeHierarchyItem`.
    ///
    /// @since 3.17.0
    abstract member TypeHierarchySubtypes: TypeHierarchySubtypesParams -> AsyncLspResult<TypeHierarchyItem array option>
    /// A request to provide inline values in a document. The request's parameter is of
    /// type {@link InlineValueParams}, the response is of type
    /// {@link InlineValue InlineValue[]} or a Thenable that resolves to such.
    ///
    /// @since 3.17.0
    abstract member TextDocumentInlineValue: InlineValueParams -> AsyncLspResult<InlineValue array option>
    /// A request to provide inlay hints in a document. The request's parameter is of
    /// type {@link InlayHintsParams}, the response is of type
    /// {@link InlayHint InlayHint[]} or a Thenable that resolves to such.
    ///
    /// @since 3.17.0
    abstract member TextDocumentInlayHint: InlayHintParams -> AsyncLspResult<InlayHint array option>
    /// A request to resolve additional properties for an inlay hint.
    /// The request's parameter is of type {@link InlayHint}, the response is
    /// of type {@link InlayHint} or a Thenable that resolves to such.
    ///
    /// @since 3.17.0
    abstract member InlayHintResolve: InlayHint -> AsyncLspResult<InlayHint>
    /// The document diagnostic request definition.
    ///
    /// @since 3.17.0
    abstract member TextDocumentDiagnostic: DocumentDiagnosticParams -> AsyncLspResult<DocumentDiagnosticReport>
    /// The workspace diagnostic request definition.
    ///
    /// @since 3.17.0
    abstract member WorkspaceDiagnostic: WorkspaceDiagnosticParams -> AsyncLspResult<WorkspaceDiagnosticReport>
    /// The initialize request is sent from the client to the server.
    /// It is sent once as the request after starting up the server.
    /// The requests parameter is of type {@link InitializeParams}
    /// the response if of type {@link InitializeResult} of a Thenable that
    /// resolves to such.
    abstract member Initialize: InitializeParams -> AsyncLspResult<InitializeResult>
    /// A shutdown request is sent from the client to the server.
    /// It is sent once when the client decides to shutdown the
    /// server. The only notification that is sent after a shutdown request
    /// is the exit event.
    abstract member Shutdown: unit -> AsyncLspResult<unit>
    /// A document will save request is sent from the client to the server before
    /// the document is actually saved. The request can return an array of TextEdits
    /// which will be applied to the text document before it is saved. Please note that
    /// clients might drop results if computing the text edits took too long or if a
    /// server constantly fails on this request. This is done to keep the save fast and
    /// reliable.
    abstract member TextDocumentWillSaveWaitUntil: WillSaveTextDocumentParams -> AsyncLspResult<TextEdit array option>

    /// Request to request completion at a given text document position. The request's
    /// parameter is of type {@link TextDocumentPosition} the response
    /// is of type {@link CompletionItem CompletionItem[]} or {@link CompletionList}
    /// or a Thenable that resolves to such.
    ///
    /// The request can delay the computation of the {@link CompletionItem.detail `detail`}
    /// and {@link CompletionItem.documentation `documentation`} properties to the `completionItem/resolve`
    /// request. However, properties that are needed for the initial sorting and filtering, like `sortText`,
    /// `filterText`, `insertText`, and `textEdit`, must not be changed during resolve.
    abstract member TextDocumentCompletion:
        CompletionParams -> AsyncLspResult<U2<CompletionItem array,CompletionList> option>

    /// Request to resolve additional information for a given completion item.The request's
    /// parameter is of type {@link CompletionItem} the response
    /// is of type {@link CompletionItem} or a Thenable that resolves to such.
    abstract member CompletionItemResolve: CompletionItem -> AsyncLspResult<CompletionItem>
    /// Request to request hover information at a given text document position. The request's
    /// parameter is of type {@link TextDocumentPosition} the response is of
    /// type {@link Hover} or a Thenable that resolves to such.
    abstract member TextDocumentHover: HoverParams -> AsyncLspResult<Hover option>
    abstract member TextDocumentSignatureHelp: SignatureHelpParams -> AsyncLspResult<SignatureHelp option>

    /// A request to resolve the definition location of a symbol at a given text
    /// document position. The request's parameter is of type {@link TextDocumentPosition}
    /// the response is of either type {@link Definition} or a typed array of
    /// {@link DefinitionLink} or a Thenable that resolves to such.
    abstract member TextDocumentDefinition:
        DefinitionParams -> AsyncLspResult<U2<Definition,DefinitionLink array> option>

    /// A request to resolve project-wide references for the symbol denoted
    /// by the given text document position. The request's parameter is of
    /// type {@link ReferenceParams} the response is of type
    /// {@link Location Location[]} or a Thenable that resolves to such.
    abstract member TextDocumentReferences: ReferenceParams -> AsyncLspResult<Location array option>

    /// Request to resolve a {@link DocumentHighlight} for a given
    /// text document position. The request's parameter is of type {@link TextDocumentPosition}
    /// the request response is an array of type {@link DocumentHighlight}
    /// or a Thenable that resolves to such.
    abstract member TextDocumentDocumentHighlight:
        DocumentHighlightParams -> AsyncLspResult<DocumentHighlight array option>

    /// A request to list all symbols found in a given text document. The request's
    /// parameter is of type {@link TextDocumentIdentifier} the
    /// response is of type {@link SymbolInformation SymbolInformation[]} or a Thenable
    /// that resolves to such.
    abstract member TextDocumentDocumentSymbol:
        DocumentSymbolParams -> AsyncLspResult<U2<SymbolInformation array,DocumentSymbol array> option>

    /// A request to provide commands for the given text document and range.
    abstract member TextDocumentCodeAction: CodeActionParams -> AsyncLspResult<U2<Command,CodeAction> array option>
    /// Request to resolve additional information for a given code action.The request's
    /// parameter is of type {@link CodeAction} the response
    /// is of type {@link CodeAction} or a Thenable that resolves to such.
    abstract member CodeActionResolve: CodeAction -> AsyncLspResult<CodeAction>

    /// A request to list project-wide symbols matching the query string given
    /// by the {@link WorkspaceSymbolParams}. The response is
    /// of type {@link SymbolInformation SymbolInformation[]} or a Thenable that
    /// resolves to such.
    ///
    /// @since 3.17.0 - support for WorkspaceSymbol in the returned data. Clients
    ///  need to advertise support for WorkspaceSymbols via the client capability
    ///  `workspace.symbol.resolveSupport`.
    abstract member WorkspaceSymbol:
        WorkspaceSymbolParams -> AsyncLspResult<U2<SymbolInformation array,WorkspaceSymbol array> option>

    /// A request to resolve the range inside the workspace
    /// symbol's location.
    ///
    /// @since 3.17.0
    abstract member WorkspaceSymbolResolve: WorkspaceSymbol -> AsyncLspResult<WorkspaceSymbol>
    /// A request to provide code lens for the given text document.
    abstract member TextDocumentCodeLens: CodeLensParams -> AsyncLspResult<CodeLens array option>
    /// A request to resolve a command for a given code lens.
    abstract member CodeLensResolve: CodeLens -> AsyncLspResult<CodeLens>
    /// A request to provide document links
    abstract member TextDocumentDocumentLink: DocumentLinkParams -> AsyncLspResult<DocumentLink array option>
    /// Request to resolve additional information for a given document link. The request's
    /// parameter is of type {@link DocumentLink} the response
    /// is of type {@link DocumentLink} or a Thenable that resolves to such.
    abstract member DocumentLinkResolve: DocumentLink -> AsyncLspResult<DocumentLink>
    /// A request to format a whole document.
    abstract member TextDocumentFormatting: DocumentFormattingParams -> AsyncLspResult<TextEdit array option>
    /// A request to format a range in a document.
    abstract member TextDocumentRangeFormatting: DocumentRangeFormattingParams -> AsyncLspResult<TextEdit array option>

    /// A request to format a document on type.
    abstract member TextDocumentOnTypeFormatting:
        DocumentOnTypeFormattingParams -> AsyncLspResult<TextEdit array option>

    /// A request to rename a symbol.
    abstract member TextDocumentRename: RenameParams -> AsyncLspResult<WorkspaceEdit option>
    /// A request to test and perform the setup necessary for a rename.
    ///
    /// @since 3.16 - support for default behavior
    abstract member TextDocumentPrepareRename: PrepareRenameParams -> AsyncLspResult<PrepareRenameResult option>
    /// A request send from the client to the server to execute a command. The request might return
    /// a workspace edit which the client will apply to the workspace.
    abstract member WorkspaceExecuteCommand: ExecuteCommandParams -> AsyncLspResult<LSPAny option>

type ILspClient =
    inherit System.IDisposable
    // Notifications
    /// The show message notification is sent from a server to a client to ask
    /// the client to display a particular message in the user interface.
    abstract member WindowShowMessage: ShowMessageParams -> Async<unit>
    /// The log message notification is sent from the server to the client to ask
    /// the client to log a particular message.
    abstract member WindowLogMessage: LogMessageParams -> Async<unit>
    /// The telemetry event notification is sent from the server to the client to ask
    /// the client to log telemetry data.
    abstract member TelemetryEvent: LSPAny -> Async<unit>
    /// Diagnostics notification are sent from the server to the client to signal
    /// results of validation runs.
    abstract member TextDocumentPublishDiagnostics: PublishDiagnosticsParams -> Async<unit>
    abstract member LogTrace: LogTraceParams -> Async<unit>
    abstract member CancelRequest: CancelParams -> Async<unit>
    abstract member Progress: ProgressParams -> Async<unit>
    // Requests
    /// The `workspace/workspaceFolders` is sent from the server to the client to fetch the open workspace folders.
    abstract member WorkspaceWorkspaceFolders: unit -> AsyncLspResult<WorkspaceFolder array option>
    /// The 'workspace/configuration' request is sent from the server to the client to fetch a certain
    /// configuration setting.
    ///
    /// This pull model replaces the old push model where the client signaled configuration change via an
    /// event. If the server still needs to react to configuration changes (since the server caches the
    /// result of `workspace/configuration` requests) the server should register for an empty configuration
    /// change event and empty the cache if such an event is received.
    abstract member WorkspaceConfiguration: ConfigurationParams -> AsyncLspResult<LSPAny array>
    /// The `window/workDoneProgress/create` request is sent from the server to the client to initiate progress
    /// reporting from the server.
    abstract member WindowWorkDoneProgressCreate: WorkDoneProgressCreateParams -> AsyncLspResult<unit>
    /// @since 3.16.0
    abstract member WorkspaceSemanticTokensRefresh: unit -> AsyncLspResult<unit>
    /// A request to show a document. This request might open an
    /// external program depending on the value of the URI to open.
    /// For example a request to open `https://code.visualstudio.com/`
    /// will very likely open the URI in a WEB browser.
    ///
    /// @since 3.16.0
    abstract member WindowShowDocument: ShowDocumentParams -> AsyncLspResult<ShowDocumentResult>
    /// @since 3.17.0
    abstract member WorkspaceInlineValueRefresh: unit -> AsyncLspResult<unit>
    /// @since 3.17.0
    abstract member WorkspaceInlayHintRefresh: unit -> AsyncLspResult<unit>
    /// The diagnostic refresh request definition.
    ///
    /// @since 3.17.0
    abstract member WorkspaceDiagnosticRefresh: unit -> AsyncLspResult<unit>
    /// The `client/registerCapability` request is sent from the server to the client to register a new capability
    /// handler on the client side.
    abstract member ClientRegisterCapability: RegistrationParams -> AsyncLspResult<unit>
    /// The `client/unregisterCapability` request is sent from the server to the client to unregister a previously registered capability
    /// handler on the client side.
    abstract member ClientUnregisterCapability: UnregistrationParams -> AsyncLspResult<unit>
    /// The show message request is sent from the server to the client to show a message
    /// and a set of options actions to the user.
    abstract member WindowShowMessageRequest: ShowMessageRequestParams -> AsyncLspResult<MessageActionItem option>
    /// A request to refresh all code actions
    ///
    /// @since 3.16.0
    abstract member WorkspaceCodeLensRefresh: unit -> AsyncLspResult<unit>
    /// A request sent from the server to the client to modified certain resources.
    abstract member WorkspaceApplyEdit: ApplyWorkspaceEditParams -> AsyncLspResult<ApplyWorkspaceEditResult>

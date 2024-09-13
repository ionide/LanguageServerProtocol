namespace Ionide.LanguageServerProtocol

open Ionide.LanguageServerProtocol.Types

type ILSPServer =
    // Notifications
    abstract member WorkspaceDidChangeWorkspaceFolders: DidChangeWorkspaceFoldersParams -> Async<unit>
    abstract member WindowWorkDoneProgressCancel: WorkDoneProgressCancelParams -> Async<unit>
    abstract member WorkspaceDidCreateFiles: CreateFilesParams -> Async<unit>
    abstract member WorkspaceDidRenameFiles: RenameFilesParams -> Async<unit>
    abstract member WorkspaceDidDeleteFiles: DeleteFilesParams -> Async<unit>
    abstract member NotebookDocumentDidOpen: DidOpenNotebookDocumentParams -> Async<unit>
    abstract member NotebookDocumentDidChange: DidChangeNotebookDocumentParams -> Async<unit>
    abstract member NotebookDocumentDidSave: DidSaveNotebookDocumentParams -> Async<unit>
    abstract member NotebookDocumentDidClose: DidCloseNotebookDocumentParams -> Async<unit>
    abstract member Initialized: InitializedParams -> Async<unit>
    abstract member Exit: unit -> Async<unit>
    abstract member WorkspaceDidChangeConfiguration: DidChangeConfigurationParams -> Async<unit>
    abstract member TextDocumentDidOpen: DidOpenTextDocumentParams -> Async<unit>
    abstract member TextDocumentDidChange: DidChangeTextDocumentParams -> Async<unit>
    abstract member TextDocumentDidClose: DidCloseTextDocumentParams -> Async<unit>
    abstract member TextDocumentDidSave: DidSaveTextDocumentParams -> Async<unit>
    abstract member TextDocumentWillSave: WillSaveTextDocumentParams -> Async<unit>
    abstract member WorkspaceDidChangeWatchedFiles: DidChangeWatchedFilesParams -> Async<unit>
    abstract member SetTrace: SetTraceParams -> Async<unit>
    abstract member CancelRequest: CancelParams -> Async<unit>
    abstract member Progress: ProgressParams -> Async<unit>
    // Requests
    abstract member TextDocumentImplementation: ImplementationParams -> Option<U2<Definition,DefinitionLink array>>
    abstract member TextDocumentTypeDefinition: TypeDefinitionParams -> Option<U2<Definition,DefinitionLink array>>
    abstract member TextDocumentDocumentColor: DocumentColorParams -> ColorInformation array
    abstract member TextDocumentColorPresentation: ColorPresentationParams -> ColorPresentation array
    abstract member TextDocumentFoldingRange: FoldingRangeParams -> Option<FoldingRange array>
    abstract member TextDocumentDeclaration: DeclarationParams -> Option<U2<Declaration,DeclarationLink array>>
    abstract member TextDocumentSelectionRange: SelectionRangeParams -> Option<SelectionRange array>
    abstract member TextDocumentPrepareCallHierarchy: CallHierarchyPrepareParams -> Option<CallHierarchyItem array>

    abstract member CallHierarchyIncomingCalls:
        CallHierarchyIncomingCallsParams -> Option<CallHierarchyIncomingCall array>

    abstract member CallHierarchyOutgoingCalls:
        CallHierarchyOutgoingCallsParams -> Option<CallHierarchyOutgoingCall array>

    abstract member TextDocumentSemanticTokensFull: SemanticTokensParams -> Option<SemanticTokens>

    abstract member TextDocumentSemanticTokensFullDelta:
        SemanticTokensDeltaParams -> Option<U2<SemanticTokens,SemanticTokensDelta>>

    abstract member TextDocumentSemanticTokensRange: SemanticTokensRangeParams -> Option<SemanticTokens>
    abstract member TextDocumentLinkedEditingRange: LinkedEditingRangeParams -> Option<LinkedEditingRanges>
    abstract member WorkspaceWillCreateFiles: CreateFilesParams -> Option<WorkspaceEdit>
    abstract member WorkspaceWillRenameFiles: RenameFilesParams -> Option<WorkspaceEdit>
    abstract member WorkspaceWillDeleteFiles: DeleteFilesParams -> Option<WorkspaceEdit>
    abstract member TextDocumentMoniker: MonikerParams -> Option<Moniker array>
    abstract member TextDocumentPrepareTypeHierarchy: TypeHierarchyPrepareParams -> Option<TypeHierarchyItem array>
    abstract member TypeHierarchySupertypes: TypeHierarchySupertypesParams -> Option<TypeHierarchyItem array>
    abstract member TypeHierarchySubtypes: TypeHierarchySubtypesParams -> Option<TypeHierarchyItem array>
    abstract member TextDocumentInlineValue: InlineValueParams -> Option<InlineValue array>
    abstract member TextDocumentInlayHint: InlayHintParams -> Option<InlayHint array>
    abstract member InlayHintResolve: InlayHint -> InlayHint
    abstract member TextDocumentDiagnostic: DocumentDiagnosticParams -> DocumentDiagnosticReport
    abstract member WorkspaceDiagnostic: WorkspaceDiagnosticParams -> WorkspaceDiagnosticReport
    abstract member Initialize: InitializeParams -> InitializeResult
    abstract member Shutdown: unit -> unit
    abstract member TextDocumentWillSaveWaitUntil: WillSaveTextDocumentParams -> Option<TextEdit array>
    abstract member TextDocumentCompletion: CompletionParams -> Option<U2<CompletionItem array,CompletionList>>
    abstract member CompletionItemResolve: CompletionItem -> CompletionItem
    abstract member TextDocumentHover: HoverParams -> Option<Hover>
    abstract member TextDocumentSignatureHelp: SignatureHelpParams -> Option<SignatureHelp>
    abstract member TextDocumentDefinition: DefinitionParams -> Option<U2<Definition,DefinitionLink array>>
    abstract member TextDocumentReferences: ReferenceParams -> Option<Location array>
    abstract member TextDocumentDocumentHighlight: DocumentHighlightParams -> Option<DocumentHighlight array>

    abstract member TextDocumentDocumentSymbol:
        DocumentSymbolParams -> Option<U2<SymbolInformation array,DocumentSymbol array>>

    abstract member TextDocumentCodeAction: CodeActionParams -> Option<U2<Command,CodeAction> array>
    abstract member CodeActionResolve: CodeAction -> CodeAction
    abstract member WorkspaceSymbol: WorkspaceSymbolParams -> Option<U2<SymbolInformation array,WorkspaceSymbol array>>
    abstract member WorkspaceSymbolResolve: WorkspaceSymbol -> WorkspaceSymbol
    abstract member TextDocumentCodeLens: CodeLensParams -> Option<CodeLens array>
    abstract member CodeLensResolve: CodeLens -> CodeLens
    abstract member TextDocumentDocumentLink: DocumentLinkParams -> Option<DocumentLink array>
    abstract member DocumentLinkResolve: DocumentLink -> DocumentLink
    abstract member TextDocumentFormatting: DocumentFormattingParams -> Option<TextEdit array>
    abstract member TextDocumentRangeFormatting: DocumentRangeFormattingParams -> Option<TextEdit array>
    abstract member TextDocumentOnTypeFormatting: DocumentOnTypeFormattingParams -> Option<TextEdit array>
    abstract member TextDocumentRename: RenameParams -> Option<WorkspaceEdit>
    abstract member TextDocumentPrepareRename: PrepareRenameParams -> Option<PrepareRenameResult>
    abstract member WorkspaceExecuteCommand: ExecuteCommandParams -> Option<LSPAny>

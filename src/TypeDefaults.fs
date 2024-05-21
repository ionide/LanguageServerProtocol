namespace Ionide.LanguageServerProtocol.Types

type SymbolKindCapabilities =

  static member DefaultValueSet =
    [| SymbolKind.File
       SymbolKind.Module
       SymbolKind.Namespace
       SymbolKind.Package
       SymbolKind.Class
       SymbolKind.Method
       SymbolKind.Property
       SymbolKind.Field
       SymbolKind.Constructor
       SymbolKind.Enum
       SymbolKind.Interface
       SymbolKind.Function
       SymbolKind.Variable
       SymbolKind.Constant
       SymbolKind.String
       SymbolKind.Number
       SymbolKind.Boolean
       SymbolKind.Array |]

type GotoResult = U2<Location, Location[]>

type TextDocumentCodeActionResult = U2<Command, CodeAction>[]
# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).


## [0.4.14] - 04.04.2023

### Fixed

* [PublishDiagnosticsCapabilities should be optional](https://github.com/ionide/LanguageServerProtocol/pull/48) (thanks @sharpSteff!)

## [0.4.13] - 05.03.2023

### Added

* [Added types and bindings for the following endpoints](https://github.com/ionide/LanguageServerProtocol/pull/46) (thanks @tcs4c70!)
  * textDocument/prepareCallHierarchy
  * callHierarchy/incomingCalls
  * callHierarchy/outgoingCalls
  * textDocument/prepareTypeHierarchy
  * typeHierarchy/supertypes
  * typeHierarchy/subtypes

## [0.4.12] - 21.12.2022

### Added

* [Add textDocument/inlineValue types and functionality](https://github.com/ionide/LanguageServerProtocol/pull/44) (thanks @kaashyapan!)

## [0.4.11] - 12.12.2022

### Added

* [Add types for Progress and WorkDone](https://github.com/ionide/LanguageServerProtocol/pull/43) (thanks @TheAngryByrd!)

## [0.4.10] - 30.09.2022

### Added

* Fix type constraints for diagnostics sources to prevent serialization errors

## [0.4.9] - 21.09.2022

### Added

* [Allow consumers to customize the JsonRpc used](https://github.com/ionide/LanguageServerProtocol/pull/40)

## [0.4.8] - 12.09.2022

### Added

* [Introduce interfaces for LSP server and client to allow for easier testing](https://github.com/ionide/LanguageServerProtocol/pull/37) (thanks @TheAngryByrd!)

## [0.4.7] - 24.08.2022

### Added

* [Client workspace capabilities for Code Lenses](https://github.com/ionide/LanguageServerProtocol/pull/34)

## [0.4.6] - 23.08.2022

### Added

* [Error.RequestCancelled support](https://github.com/ionide/LanguageServerProtocol/pull/31) (thanks @razzmatazz!)

### Fixed

* [Make server-reported LSP errors not crash the transport](https://github.com/ionide/LanguageServerProtocol/pull/33) (thanks @razzmatazz!)

## [0.4.5] - 07.08.2022

### Added

* [textDocument/prepareRename types and functionality](https://github.com/ionide/LanguageServerProtocol/pull/30) and [client/server capabilities](https://github.com/ionide/LanguageServerProtocol/pull/31) (thanks @artempyanykh!)

### Changed

* [JsonRpc no longer swallows exceptions](https://github.com/ionide/LanguageServerProtocol/pull/29) (thanks @artempyanykh!)

## [0.4.4] - 27.06.2022

### Added

* [Deserialization support for erased unions](https://github.com/ionide/LanguageServerProtocol/pull/27) (Thanks @Booksbaum!)

## [0.4.3] - 08.06.2022

### Fixed

* [Fix a typo in the workspace/executeCommand registration](https://github.com/ionide/LanguageServerProtocol/pull/28) (Thanks @keynmol!)

## [0.4.2] - 26.05.2022

### Fixed

* [Make the inlayHint client capability optional](https://github.com/ionide/LanguageServerProtocol/pull/23) (thanks @artempyanykh!) 
* [Handle exceptions from serialization](https://github.com/ionide/LanguageServerProtocol/pull/25) (thanks @artempyanykh!)
* [Make the InlayHintWorkspaceClientCapabilities part of WorkspaceClientCapabilities](https://github.com/ionide/LanguageServerProtocol/pull/26) (thanks @Booksbaum!)

## [0.4.1] - 14.05.2022

### Changed

* [`textDocument/symbol` now returns `DocumentSymbol[]` instead of `SymbolInformation[]`](https://github.com/ionide/LanguageServerProtocol/pull/18) (thanks @artempyanykh!)

### Fixed

* [Workaround a VSCode language client bug preventing server shutdown](https://github.com/ionide/LanguageServerProtocol/pull/21) (thanks @artempyanykh!)

### Added

* [Types and methods for InlayHint support](https://github.com/ionide/LanguageServerProtocol/pull/22) (thanks @Booksbaum!)

## [0.4.0] - 28.04.2022

### Added

* [Add types for workspace folders](https://github.com/ionide/LanguageServerProtocol/pull/15) (thanks @artempyanykh!)
* [Add types for workspace file notifications](https://github.com/ionide/LanguageServerProtocol/pull/17) (thanks @artempyanykh!)

### Changed

* [Use the StreamJsonRpc library as the transport layer instead of our own](https://github.com/ionide/LanguageServerProtocol/pull/10) (thanks @razzmatazz!)


## [0.3.1] - 8.1.2022

### Added

* Add XmlDocs to the generated package

## [0.3.0] - 23.11.2021

### Added

* Expose client `CodeAction` caps as CodeActionClientCapabilities. (by @razzmatazz)
* Map CodeAction.IsPreferred & CodeAction.Disabled props. (by @razzmatazz)

## [0.2.0] - 17.11.2021

### Added

* Add support for `codeAction/resolve` (by @razzmatazz)

## [0.1.1] - 15.11.2021

### Added

* Initial implementation

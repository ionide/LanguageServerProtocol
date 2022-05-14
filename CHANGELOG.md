# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

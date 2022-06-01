[<AutoOpen>]
module Ionide.LanguageServerProtocol.Tests.Utils

open Ionide.LanguageServerProtocol.Types

let inline mkPos l c = { Line = l; Character = c }
let inline mkRange s e = { Start = s; End = e }
let inline mkRange' (sl, sc) (el, ec) = mkRange (mkPos sl sc) (mkPos el ec)
let inline mkPosRange l c = mkRange (mkPos l c) (mkPos l c)
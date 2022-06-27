/// Checks all LSP types with FsCheck: `(value |> serialize |> deserialize) = value`
///
/// Note: success doesn't necessary mean everything got correctly (de)serialized,
///       just everything can do a roundtrip (`(value |> serialize |> deserialize) = value`).
///       Serialized JSON might still be incorrect!
///       -> Just checking for exceptions while (de)serializing!
module Ionide.LanguageServerProtocol.Tests.Shotgun

open System
open System.Reflection
open Ionide.LanguageServerProtocol.Types
open FsCheck
open Newtonsoft.Json.Linq
open Expecto
open Ionide.LanguageServerProtocol.Server
open Ionide.LanguageServerProtocol.Types

// must be public
type Gens =
  static member NoNulls<'T>() : Arbitrary<'T> =
    if typeof<'T>.IsValueType then
      Arb.Default.Derive<'T>()
    else
      Arb.Default.Derive<'T>()
      |> Arb.filter (fun v -> (box v) <> null)

  static member JToken() : Arbitrary<JToken> =
    // actual value doesn't matter -> handled by user
    // and complexer JTokens cannot be directly compared with `=`
    JToken.FromObject(123) |> Gen.constant |> Arb.fromGen

  static member String() : Arbitrary<string> = Arb.Default.String() |> Arb.filter (fun s -> not (isNull s))
  static member Float() : Arbitrary<float> = Arb.Default.NormalFloat() |> Arb.convert float NormalFloat

  static member Uri() : Arbitrary<Uri> =
    // actual value doesn't matter -> always use example uri
    System.Uri("foo://example.com:8042/over/there?name=ferret#nose")
    |> Gen.constant
    |> Arb.fromGen

  static member DocumentSymbol() : Arbitrary<DocumentSymbol> =
    // DocumentSymbol is recursive -> Stack overflow when default generation
    // https://fscheck.github.io/FsCheck/TestData.html#Generating-recursive-data-types
    let maxDepth = 5

    let create name detail kind range selectionRange children =
      { Name = name
        Detail = detail
        Kind = kind
        Range = range
        SelectionRange = selectionRange
        Children = children }

    let genDocSymbol = Gen.map6 create Arb.generate Arb.generate Arb.generate Arb.generate Arb.generate
    // Children is still open
    let rec gen size =
      let size = min size maxDepth

      if size <= 0 then
        genDocSymbol (
          Gen.oneof [ Gen.constant (None)
                      Gen.constant (Some [||]) ]
        )
      else
        let children = gen (size - 1) |> Gen.arrayOf |> Gen.optionOf
        genDocSymbol children

    Gen.sized gen |> Arb.fromGen

  static member SelectionRange() : Arbitrary<SelectionRange> =
    let maxDepth = 5
    let create range parent = { Range = range; Parent = parent }
    let genSelectionRange = Gen.map2 create Arb.generate

    let rec gen size =
      let size = min size maxDepth

      if size <= 0 then
        genSelectionRange (Gen.constant None)
      else
        let parent = gen (size - 1) |> Gen.optionOf
        genSelectionRange parent

    Gen.sized gen |> Arb.fromGen

let private fsCheckConfig = { FsCheckConfig.defaultConfig with arbitrary = [ typeof<Gens> ] }

type private Roundtripper =
  static member ThereAndBackAgain(input: 'a) = input |> serialize |> deserialize<'a>

  static member TestThereAndBackAgain(input: 'a) =
    let output = Roundtripper.ThereAndBackAgain input
    // Fails: Dictionary doesn't support structural identity (-> different instances with same content aren't equal)
    // Expect.equal output input "Input -> serialize -> deserialize should be Input again"
    Utils.convertExtensionDataDictionariesToMap output
    Utils.convertExtensionDataDictionariesToMap input
    Expect.equal output input "Input -> serialize -> deserialize should be Input again"

  static member TestProperty<'a when 'a: equality> name =
    testPropertyWithConfig fsCheckConfig name (Roundtripper.TestThereAndBackAgain<'a>)

let tests =
  testList
    "shotgun"
    [
      // Type Abbreviations get erased
      // -> not available as type and don't get pick up below
      // -> specify manual
      let abbrevTys =
        [| nameof DocumentUri, typeof<DocumentUri>
           nameof DocumentSelector, typeof<DocumentSelector>
           nameof TextDocumentCodeActionResult, typeof<TextDocumentCodeActionResult> |]

      let tys =
        let shouldTestType (t: Type) = Utils.isLspType [ not << Lsp.Is.Generic; not << Lsp.Is.Nested ] t

        let example = typeof<Ionide.LanguageServerProtocol.Types.TextDocumentIdentifier>
        let ass = example.Assembly

        ass.GetTypes()
        |> Array.filter shouldTestType
        |> Array.map (fun t -> t.Name, t)
        |> Array.append abbrevTys
        |> Array.sortBy fst

      let propTester =
        typeof<Roundtripper>.GetMethod
          (nameof (Roundtripper.TestProperty), BindingFlags.Static ||| BindingFlags.NonPublic)

      for (name, ty) in tys do
        let m = propTester.MakeGenericMethod([| ty |])
        m.Invoke(null, [| name |]) |> unbox<Test> ]
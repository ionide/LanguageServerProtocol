[<AutoOpen>]
module Ionide.LanguageServerProtocol.Tests.Utils

open Ionide.LanguageServerProtocol.Types
open System.Reflection
open Newtonsoft.Json
open System.Collections.Generic
open Newtonsoft.Json.Linq
open System
open Expecto
open System.Collections
open System.Runtime.CompilerServices

let inline mkPos l c = { Line = l; Character = c }
let inline mkRange s e = { Start = s; End = e }
let inline mkRange' (sl, sc) (el, ec) = mkRange (mkPos sl sc) (mkPos el ec)
let inline mkPosRange l c = mkRange (mkPos l c) (mkPos l c)

module Lsp =
  let internal example = typeof<Ionide.LanguageServerProtocol.Types.TextDocumentIdentifier>

  let internal path =
    let name = example.FullName
    let i = name.IndexOf '+'
    name.Substring(0, i + 1)

  module Is =
    /// Not directly in `Ionide.LanguageServerProtocol.Types`, but nested deeper
    ///
    /// For example cases of unions are nested inside their corresponding union
    ///
    /// Note: don't confuse with `ty.IsNested`:
    /// Modules are static classes and everything inside them is nested inside module
    /// -> `IsNested` always true for types inside module
    let Nested (ty: Type) = ty.DeclaringType <> example.DeclaringType

    /// Generics like `U2<_,_>` or `U2<int, string>`
    ///
    /// Note: doesn't differentiate between generic type def and generic type (`U2<_,_>` vs `U2<int, string>`)
    let Generic (ty: Type) = ty.IsGenericType

    /// Generic type defs like `U2<_,_>`
    ///
    /// Note: unlike `generic`, only excludes actual generic type definitions (`U2<_,_>` but not `U2<int, string>`)
    let GenericTypeDef (ty: Type) = ty.IsGenericTypeDefinition

    /// Note: Union type are abstract: `U2<_,_>` -> `U2` is abstract, while it's cases are concrete types
    let Abstract (ty: Type) = ty.IsAbstract

    /// Abstract & Sealed
    ///
    /// Note: Always excludes -> this rule does nothing
    let Static (ty: Type) = ty.IsAbstract && ty.IsSealed

/// Lsp Type: inside `Ionide.LanguageServerProtocol.Types`
///
/// AdditionalRules: use rules inside `Lsp` module
let isLspType (additionalRules: (Type -> bool) list) (ty: Type) =
  ty.FullName.StartsWith Lsp.path
  && not (
    // private or internal
    (ty.IsNestedPrivate || ty.IsNestedAssembly)
    || ty.IsInterface
    ||
    // static -> modules
    (ty.IsAbstract && ty.IsSealed)
    || ty.BaseType = typeof<Attribute>
  )
  && (additionalRules |> List.forall (fun rule -> rule ty))

/// Replaces contents of properties with `JsonExtensionData`Attribute of type `IDictionary<string, JToken>`
/// with a `Map` containing same elements.
/// `null` field is kept.
///
/// Note: Mutates `o`!
///
/// Note: Only converts `JsonExtensionData`.
/// For all other cases: `Map` works just fine. And not using `Map` is probably incorrect.
///
/// **Use Case**:
/// Dictionaries don't use structural identity
/// -> `Expecto.equal` fails with two dictionaries -- or objects that contain dictionary
///
/// In Newtonsoft.Json: `JsonExtensionData` required a Dictionary (IDictionary).
/// Unfortunately F# `Map` cannot be used because no default ctor and not mutable.
/// -> Must use something else -- like `Dictionary` (see LSP Type `FormattingOptions`)
/// But now LSP data cannot be compared with `=` or `Expecto.equal`.
/// And the type with `Dictionary` might be nested deep down.
///
/// -> Replace all dictionaries with `Map` -> comparison works again
let rec convertExtensionDataDictionariesToMap (o: obj) =
  let isGenericTypeOf expected (actual: Type) =
    actual.IsGenericType
    && actual.GetGenericTypeDefinition() = expected

  let (|IsGenericType|_|) expected actual = if actual |> isGenericTypeOf expected then Some() else None

  match o with
  | null -> ()
  | :? string
  | :? bool
  | :? int
  | :? float
  | :? byte
  | :? uint -> ()
  | :? JToken -> ()
  | :? IDictionary as dict ->
    for kv in dict |> Seq.cast<DictionaryEntry> do
      convertExtensionDataDictionariesToMap kv.Value
  | :? IEnumerable as ls ->
    for v in ls do
      convertExtensionDataDictionariesToMap v
  | :? ITuple as t ->
    for i in 0 .. (t.Length - 1) do
      convertExtensionDataDictionariesToMap (t[i])
  | _ when
    let ty = o.GetType()

    isLspType [] ty
    || ty |> isGenericTypeOf typedefof<_ option>
    || ty.FullName.StartsWith "Ionide.LanguageServerProtocol.Tests.Utils+TestData+"
    ->
    let ty = o.GetType()
    let props = ty.GetProperties(BindingFlags.Instance ||| BindingFlags.Public)

    let propsWithValues =
      props
      |> Seq.choose (fun prop ->
        try
          let v = prop.GetValue o
          (prop, v) |> Some
        with
        | ex ->
          failwithf "Couldn't get value of %s in %A: %s" prop.Name ty ex.Message
          None)

    for (prop, value) in propsWithValues do
      match value with
      | null -> ()
      | :? Map<string, JToken> -> ()
      | :? IDictionary<string, JToken> as dict ->
        match prop.GetCustomAttribute<JsonExtensionDataAttribute>() with
        | null -> ()
        | _ when not prop.CanWrite ->
          // assumption: Dictionary is mutable
          // otherwise: can be handled by getting and setting backing field
          // but not done here (for serializing in record: must be mutable set after in `OnDeserialized` to prevent null)
          ()
        | _ ->
          let v = dict |> Seq.map (fun kv -> kv.Key, kv.Value) |> Map.ofSeq
          prop.SetValue(o, v)
      | _ -> convertExtensionDataDictionariesToMap value
  | _ -> ()


module TestData =
  type WithExtensionData =
    { Value: string
      [<JsonExtensionData>]
      mutable AdditionalData: IDictionary<string, JToken> }

  [<RequireQualifiedAccess>]
  type MyUnion =
    | Case1 of string
    | Case2 of WithExtensionData
    | Case3 of string * WithExtensionData * int

  [<RequireQualifiedAccess>]
  type MyContainer =
    | Fin
    | Datum of WithExtensionData * MyContainer
    | Data of WithExtensionData [] * MyContainer
    | Big of MyContainer []
    | Text of string * MyContainer

let tests =
  testList
    "test utils"
    [ testList
        (nameof isLspType)
        [ testCase "string isn't lsp type"
          <| fun _ ->
               let isLsp = typeof<string> |> isLspType []
               Expect.isFalse isLsp "string isn't lsp"
          testCase "DocumentLink is lsp type"
          <| fun _ ->
               let isLsp =
                 typeof<Ionide.LanguageServerProtocol.Types.DocumentLink>
                 |> isLspType []

               Expect.isTrue isLsp "DocumentLink is lsp"
          testCase "DocumentLink is direct, non-generic lsp type"
          <| fun _ ->
               let isLsp =
                 typeof<Ionide.LanguageServerProtocol.Types.DocumentLink>
                 |> isLspType [ not << Lsp.Is.Nested
                                not << Lsp.Is.Generic ]

               Expect.isTrue isLsp "DocumentLink is lsp"

          testCase "U2 is lsp type"
          <| fun _ ->
               let isLsp = typeof<U2<int, string>> |> isLspType []
               Expect.isTrue isLsp "U2 is lsp"
          testCase "U2<int, string> is not non-generic lsp type"
          <| fun _ ->
               let isLsp =
                 typeof<U2<int, string>>
                 |> isLspType [ not << Lsp.Is.Generic ]

               Expect.isFalse isLsp "U2 is generic lsp"
          testCase "U2<int, string> is non-generic-type-def lsp type"
          <| fun _ ->
               let isLsp =
                 typeof<U2<int, string>>
                 |> isLspType [ not << Lsp.Is.GenericTypeDef ]

               Expect.isTrue isLsp "U2 is not generic type def lsp"
          testCase "U2<_,_> is not non-generic lsp type"
          <| fun _ ->
               let isLsp = typedefof<U2<_, _>> |> isLspType [ not << Lsp.Is.Generic ]
               Expect.isFalse isLsp "U2 is generic lsp"
          testCase "U2<_,_> is not non-generic-type-def lsp type"
          <| fun _ ->
               let isLsp =
                 typedefof<U2<_, _>>
                 |> isLspType [ not << Lsp.Is.GenericTypeDef ]

               Expect.isFalse isLsp "U2 is generic type def lsp"
          testCase "U2<_,_> is not non-abstract lsp type"
          <| fun _ ->
               let isLsp = typedefof<U2<_, _>> |> isLspType [ not << Lsp.Is.Abstract ]
               Expect.isFalse isLsp "U2 is abstract lsp"

          testCase "MarkedString.String is lsp"
          <| fun _ ->
               let o = MarkedString.String "foo"
               let isLsp = o.GetType() |> isLspType []
               Expect.isTrue isLsp "MarkedString.String is lsp"
          testCase "MarkedString.String isn't direct lsp"
          <| fun _ ->
               let o = MarkedString.String "foo"
               let isLsp = o.GetType() |> isLspType [ not << Lsp.Is.Nested ]
               Expect.isFalse isLsp "MarkedString.String is not direct lsp"

          testCase "Client isn't lsp"
          <| fun _ ->
               let isLsp =
                 typeof<Ionide.LanguageServerProtocol.Client.Client>
                 |> isLspType []

               Expect.isFalse isLsp "Client isn't lsp" ]

      testList
        (nameof convertExtensionDataDictionariesToMap)
        [ let testConvert preActual expectedAfterwards =
            Expect.notEqual preActual expectedAfterwards "Dictionary and Map shouldn't be equal"

            convertExtensionDataDictionariesToMap preActual
            Expect.equal preActual expectedAfterwards "Converter Map should be comparable"

          let dict =
            [| "alpha", JToken.FromObject "lorem"
               "beta", JToken.FromObject "ipsum"
               "gamma", JToken.FromObject "dolor" |]
            |> Map.ofArray

          let createWithExtensionData () : TestData.WithExtensionData =
            { Value = "foo"; AdditionalData = dict |> Dictionary }

          testCase "can convert direct dictionary field"
          <| fun _ ->
               let actual = createWithExtensionData ()
               let expected = { actual with AdditionalData = dict }
               testConvert actual expected

          testCase "can convert inside union in case with single value"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual = TestData.MyUnion.Case2 extData
               let expected = TestData.MyUnion.Case2 { extData with AdditionalData = dict }
               testConvert actual expected

          testCase "can convert inside union in case with multiple values"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual = TestData.MyUnion.Case3("foo", extData, 42)
               let expected = TestData.MyUnion.Case3("foo", { extData with AdditionalData = dict }, 42)
               testConvert actual expected

          testCase "can convert in U2"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual: U2<int, _> = U2.Second extData
               let expected: U2<int, _> = U2.Second { extData with AdditionalData = dict }
               testConvert actual expected

          testCase "can convert in tuple"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual = ("foo", extData, 42)
               let expected = ("foo", { extData with AdditionalData = dict }, 42)
               testConvert actual expected

          testCase "can convert in array"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual: obj [] = [| "foo"; extData; 42 |]
               let expected: obj [] = [| "foo"; { extData with AdditionalData = dict }; 42 |]
               testConvert actual expected

          testCase "can convert in list"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual: obj list = [ "foo"; extData; 42 ]
               let expected: obj list = [ "foo"; { extData with AdditionalData = dict }; 42 ]
               testConvert actual expected

          testCase "can convert option"
          <| fun _ ->
               let extData = createWithExtensionData ()
               let actual = Some extData
               let expected = Some { extData with AdditionalData = dict }
               testConvert actual expected

          testCase "replaces all dictionaries"
          <| fun _ ->
               let extDataMap =
                 Array.init 5 (fun i ->
                   let m =
                     Array.init (i + 3) (fun j -> ($"Dict{i}Element{j}", JToken.FromObject(i + j)))
                     |> Map.ofArray

                   { TestData.WithExtensionData.Value = $"Hello {i}"
                     TestData.WithExtensionData.AdditionalData = m })

               let extDataDict =
                 extDataMap
                 |> Array.map (fun extData -> { extData with AdditionalData = Dictionary extData.AdditionalData })

               let actual = TestData.MyContainer.Data(extDataDict, TestData.MyContainer.Fin)
               let expected = TestData.MyContainer.Data(extDataMap, TestData.MyContainer.Fin)
               testConvert actual expected

          testCase "can replace deeply nested"
          <| fun _ ->
               let createExtensionData mkDict seed : TestData.WithExtensionData =
                 { Value = $"Seed {seed}"
                   AdditionalData =
                     let count = seed % 4 + 3

                     List.init (seed % 4 + 3) (fun i -> ($"Seed{seed}Element{i}Of{count}", JToken.FromObject(count + i)))
                     |> Map.ofList
                     |> mkDict }

               /// builds always same object for same depth
               let rec buildObject mkDict (depth: int) =
                 match depth % 4 with
                 | _ when depth <= 0 -> TestData.MyContainer.Fin
                 | 0 ->
                   [| 1 .. (max 3 (depth / 2)) |]
                   |> Array.map (fun i -> buildObject mkDict (depth - 1 - (i % 2)))
                   |> TestData.MyContainer.Big
                 | 1 ->
                   let o = buildObject mkDict (depth - 1)
                   let d = createExtensionData mkDict depth
                   TestData.MyContainer.Datum(d, o)
                 | 2 ->
                   let o = buildObject mkDict (depth - 1)

                   let ds =
                     [| 1 .. max 3 (depth / 2) |]
                     |> Array.map (fun i -> createExtensionData mkDict (depth * i))

                   TestData.MyContainer.Data(ds, o)
                 | 3 ->
                   let o = buildObject mkDict (depth - 1)
                   let d = $"Depth={depth}"
                   TestData.MyContainer.Text(d, o)

                 | _ -> failwith "unreachable"

               let depth = 7
               let expected = buildObject id depth
               let actual = buildObject Dictionary depth
               testConvert actual expected ] ]
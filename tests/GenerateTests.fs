namespace Ionide.LanguageServerProtocol

module Option =
  module Array =
    /// Returns true if the given array is empty or None
    let isEmpty (x: 'a array option) =
      match x with
      | None -> true
      | Some x -> Array.isEmpty x

    /// Returns empty array if None, otherwise the array
    let toArray (x: 'a array option) = Option.defaultValue [||] x

type StructuredDocs = string list


module Proposed =
  let skipProposed = true

  let inline checkProposed x =
    if skipProposed then
      (^a: (member Proposed: bool option) x)
      <> Some true
    else
      true

module StructuredDocs =
  let parse (s: string) =
    s.Trim('\n').Split([| '\n' |])
    |> Array.toList

module String =
  open System

  let toPascalCase (s: string) =
    s.[0]
    |> Char.ToUpper
    |> fun c ->
        c.ToString()
        + s.Substring(1)

module Array =
  /// <summary>Places separator between each element of items</summary>
  let intersperse (separator: 'a) (items: 'a array) : 'a array = [|
    let mutable notFirst = false

    for element in items do
      if notFirst then
        yield separator

      yield element
      notFirst <- true
  |]

module rec MetaModel =
  open System
  open Newtonsoft.Json.Linq
  open Newtonsoft.Json

  let metaModelVersion = "3.17.0"

  let metaModel = IO.Path.Join(__SOURCE_DIRECTORY__, "..", "data", metaModelVersion, "metaModel.json")
  let metaModelSchema = IO.Path.Join(__SOURCE_DIRECTORY__, "..", "data", metaModelVersion, "metaModel.schema.json")
  type MetaData = { Version: string }
  type Requests = { Method: string }


  [<RequireQualifiedAccess>]
  type BaseTypes =
    | Uri
    | DocumentUri
    | Integer
    | Uinteger
    | Decimal
    | RegExp
    | String
    | Boolean
    | Null

    static member Parse(s: string) =
      match s with
      | "URI" -> Uri
      | "DocumentUri" -> DocumentUri
      | "integer" -> Integer
      | "uinteger" -> Uinteger
      | "decimal" -> Decimal
      | "RegExp" -> RegExp
      | "string" -> String
      | "boolean" -> Boolean
      | "null" -> Null
      | _ -> failwithf "Unknown base type: %s" s

    member x.ToDotNetType() =
      match x with
      | Uri -> "URI"
      | DocumentUri -> "DocumentUri"
      | Integer -> "int32"
      | Uinteger -> "uint32"
      | Decimal -> "decimal"
      | RegExp -> "RegExp"
      | String -> "string"
      | Boolean -> "bool"
      | Null -> "null"

  [<Literal>]
  let BaseTypeConst = "base"

  type BaseType = { Kind: string; Name: BaseTypes }

  [<Literal>]
  let ReferenceTypeConst = "reference"

  type ReferenceType = { Kind: string; Name: string }

  [<Literal>]
  let ArrayTypeConst = "array"

  type ArrayType = { Kind: string; Element: Type }

  [<Literal>]
  let MapTypeConst = "map"

  type MapType = { Kind: string; Key: MapKeyType; Value: Type }

  [<RequireQualifiedAccess>]
  type MapKeyNameEnum =
    | Uri
    | DocumentUri
    | String
    | Integer

    static member Parse(s: string) =
      match s with
      | "URI" -> Uri
      | "DocumentUri" -> DocumentUri
      | "string" -> String
      | "integer" -> Integer
      | _ -> failwithf "Unknown map key name: %s" s

    member x.ToDotNetType() =
      match x with
      | Uri -> "URI"
      | DocumentUri -> "DocumentUri"
      | String -> "string"
      | Integer -> "int32"

  [<RequireQualifiedAccess>]
  type MapKeyType =
    | ReferenceType of ReferenceType
    | Base of {| Kind: string; Name: MapKeyNameEnum |}

  [<Literal>]
  let AndTypeConst = "and"

  type AndType = { Kind: string; Items: Type array }

  [<Literal>]
  let OrTypeConst = "or"

  type OrType = { Kind: string; Items: Type array }

  [<Literal>]
  let TupleTypeConst = "tuple"

  type TupleType = { Kind: string; Items: Type array }

  type Property = {
    Deprecated: string option
    Documentation: string option
    Name: string
    Optional: bool option
    Proposed: bool option
    Required: bool option
    Since: string option
    Type: Type
  } with

    member x.IsOptional =
      x.Optional
      |> Option.defaultValue false

    member x.NameAsPascalCase = String.toPascalCase x.Name

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse


  [<Literal>]
  let StructureTypeLiteral = "literal"

  type StructureLiteral = {
    Deprecated: string option
    Documentation: string option
    Properties: Property array
    Proposed: bool option
    Since: string option
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

    member x.PropertiesSafe =
      x.Properties
      |> Array.filter Proposed.checkProposed

  type StructureLiteralType = { Kind: string; Value: StructureLiteral }

  [<Literal>]
  let StringLiteralTypeConst = "stringLiteral"

  type StringLiteralType = { Kind: string; Value: string }

  [<Literal>]
  let IntegerLiteralTypeConst = "integerLiteral"

  type IntegerLiteralType = { Kind: string; Value: decimal }

  [<Literal>]
  let BooleanLiteralTypeConst = "booleanLiteral"

  type BooleanLiteralType = { Kind: string; Value: bool }

  [<RequireQualifiedAccess>]
  type Type =
    | BaseType of BaseType
    | ReferenceType of ReferenceType
    | ArrayType of ArrayType
    | MapType of MapType
    | AndType of AndType
    | OrType of OrType
    | TupleType of TupleType
    | StructureLiteralType of StructureLiteralType
    | StringLiteralType of StringLiteralType
    | IntegerLiteralType of IntegerLiteralType
    | BooleanLiteralType of BooleanLiteralType

    member x.isStructureLiteralType =
      match x with
      | StructureLiteralType _ -> true
      | _ -> false


  type Structure = {
    Deprecated: string option
    Documentation: string option
    Extends: Type array option
    Mixins: Type array option
    Name: string
    Properties: Property array option
    Proposed: bool option
    Since: string option
  } with

    member x.ExtendsSafe = Option.Array.toArray x.Extends
    member x.MixinsSafe = Option.Array.toArray x.Mixins

    member x.PropertiesSafe =
      Option.Array.toArray x.Properties
      |> Seq.filter Proposed.checkProposed

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  type TypeAlias = {
    Deprecated: string option
    Documentation: string option
    Name: string
    Proposed: bool option
    Since: string option
    Type: Type
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  [<JsonConverter(typeof<Newtonsoft.Json.Converters.StringEnumConverter>)>]
  type EnumerationTypeNameValues =
    | String = 0
    | Integer = 1
    | Uinteger = 2

  type EnumerationType = { Kind: string; Name: EnumerationTypeNameValues }

  type EnumerationEntry = {
    Deprecated: string option
    Documentation: string option

    Name: string
    Proposed: bool option
    Since: string option
    Value: string
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

  type Enumeration = {
    Deprecated: string option
    Documentation: string option
    Name: string
    Proposed: bool option
    Since: string option
    SupportsCustomValues: bool option
    Type: EnumerationType
    Values: EnumerationEntry array
  } with

    member x.StructuredDocs =
      x.Documentation
      |> Option.map StructuredDocs.parse

    member x.ValuesSafe =
      x.Values
      |> Array.filter Proposed.checkProposed

  type MetaModel = {
    MetaData: MetaData
    Requests: Requests array
    Structures: Structure array
    TypeAliases: TypeAlias array
    Enumerations: Enumeration array
  } with

    member x.StructuresSafe =
      x.Structures
      |> Array.filter Proposed.checkProposed

    member x.TypeAliasesSafe =
      x.TypeAliases
      |> Array.filter Proposed.checkProposed

    member x.EnumerationsSafe =
      x.Enumerations
      |> Array.filter Proposed.checkProposed

  module Converters =

    type MapKeyTypeConverter() =
      inherit JsonConverter<MapKeyType>()

      override _.WriteJson(writer: JsonWriter, value: MapKeyType, serializer: JsonSerializer) : unit =
        failwith "Should never be writing this structure, it comes from Microsoft LSP Spec"

      override _.ReadJson
        (
          reader: JsonReader,
          objectType: System.Type,
          existingValue: MapKeyType,
          hasExistingValue,
          serializer: JsonSerializer
        ) =
        let jobj = JObject.Load(reader)
        let kind = jobj.["kind"].Value<string>()

        match kind with
        | ReferenceTypeConst ->
          let name = jobj.["name"].Value<string>()
          MapKeyType.ReferenceType { Kind = kind; Name = name }
        | "base" ->
          let name = jobj.["name"].Value<string>()
          MapKeyType.Base {| Kind = kind; Name = MapKeyNameEnum.Parse name |}
        | _ -> failwithf "Unknown map key type: %s" kind

    type TypeConverter() =
      inherit JsonConverter<Type>()

      override _.WriteJson(writer: JsonWriter, value: MetaModel.Type, serializer: JsonSerializer) : unit =
        failwith "Should never be writing this structure, it comes from Microsoft LSP Spec"

      override _.ReadJson
        (
          reader: JsonReader,
          objectType: System.Type,
          existingValue: Type,
          hasExistingValue,
          serializer: JsonSerializer
        ) =
        let jobj = JObject.Load(reader)
        let kind = jobj.["kind"].Value<string>()

        match kind with
        | BaseTypeConst ->
          let name = jobj.["name"].Value<string>()
          Type.BaseType { Kind = kind; Name = BaseTypes.Parse name }
        | ReferenceTypeConst ->
          let name = jobj.["name"].Value<string>()
          Type.ReferenceType { Kind = kind; Name = name }
        | ArrayTypeConst ->
          let element = jobj.["element"].ToObject<Type>(serializer)
          Type.ArrayType { Kind = kind; Element = element }
        | MapTypeConst ->
          let key = jobj.["key"].ToObject<MapKeyType>(serializer)
          let value = jobj.["value"].ToObject<Type>(serializer)
          Type.MapType { Kind = kind; Key = key; Value = value }
        | AndTypeConst ->
          let items = jobj.["items"].ToObject<Type[]>(serializer)
          Type.AndType { Kind = kind; Items = items }
        | OrTypeConst ->
          let items = jobj.["items"].ToObject<Type[]>(serializer)
          Type.OrType { Kind = kind; Items = items }
        | TupleTypeConst ->
          let items = jobj.["items"].ToObject<Type[]>(serializer)
          Type.TupleType { Kind = kind; Items = items }
        | StructureTypeLiteral ->
          let value = jobj.["value"].ToObject<StructureLiteral>(serializer)
          Type.StructureLiteralType { Kind = kind; Value = value }
        | StringLiteralTypeConst ->
          let value = jobj.["value"].Value<string>()
          Type.StringLiteralType { Kind = kind; Value = value }
        | IntegerLiteralTypeConst ->
          let value = jobj.["value"].Value<decimal>()
          Type.IntegerLiteralType { Kind = kind; Value = value }
        | BooleanLiteralTypeConst ->
          let value = jobj.["value"].Value<bool>()
          Type.BooleanLiteralType { Kind = kind; Value = value }
        | _ -> failwithf "Unknown type kind: %s" kind


  let metaModelSerializerSettings =
    let settings = JsonSerializerSettings()
    settings.Converters.Add(Converters.TypeConverter() :> JsonConverter)
    settings.Converters.Add(Converters.MapKeyTypeConverter() :> JsonConverter)
    settings.Converters.Add(JsonUtils.OptionConverter() :> JsonConverter)
    settings

module GenerateTests =

  open System.Runtime.CompilerServices


  open System
  open Expecto
  open Fantomas.Core
  open Fantomas.Core.SyntaxOak

  open Fabulous.AST

  open type Fabulous.AST.Ast

  open System.IO
  open Newtonsoft.Json
  open Fantomas.FCS.Syntax
  open Fabulous.AST.StackAllocatedCollections


  type ModuleOrNamespaceExtensions2 =
    [<Extension>]
    static member inline Yield(_: CollectionBuilder<'parent, ModuleDecl>, x: WidgetBuilder<AnonymousModuleNode>) =
      let node = Gen.mkOak x

      let ws =
        node.Declarations
        |> List.map (fun x -> Ast.EscapeHatch(x).Compile())
        |> List.toArray
        |> MutStackArray1.fromArray

      { Widgets = ws }

  let JToken = LongIdent("Newtonsoft.Json.Linq.JToken")

  let createOption (t: WidgetBuilder<Type>) = Ast.OptionPostfix t

  let createDictionary (types: WidgetBuilder<Type> list) = AppPrefix(LongIdent("Map"), types)

  let createErasedUnion (types: WidgetBuilder<Type> array) =
    if types.Length > 1 then
      let duType = LongIdent $"U%d{types.Length}"
      AppPrefix(duType, (Array.toList types))
    else
      types.[0]

  let isNullableType (t: MetaModel.Type) =
    match t with
    | MetaModel.Type.BaseType { Name = MetaModel.BaseTypes.Null } -> true
    | _ -> false

  let appendAugment augment s =
    match augment with
    | Some x -> sprintf "%s %s" s x
    | None -> s

    
  let handleSameShapeStructuredUnions path createField (ts: MetaModel.Type array) =
    if
      ts
      |> Array.forall (fun t -> t.isStructureLiteralType)
    then
      let ts =
        ts
        |> Array.map (fun t ->
          match t with
          | MetaModel.Type.StructureLiteralType s -> s.Value
          | _ -> failwithf "Expected StructureLiteralType %A" t
        )

      let allProperties =
        ts
        |> Array.collect (fun s -> s.PropertiesSafe)
        |> Array.groupBy (fun p -> p.Name)

      if
        allProperties
        |> Array.forall (fun (_, props) ->
          let (_, first) = allProperties.[0]
          props.Length = first.Length
        )
      then
        let fields: (string * WidgetBuilder<_> * _ list) list =
          allProperties
          |> Array.map (fun (name, props) ->
            let prop =
              props
              |> Array.tryFind (fun x -> x.IsOptional) // Prefer optional properties
              |> Option.defaultValue (props.[0])

            let (name, ty, _, _, others) = createField (path @ [prop.NameAsPascalCase]) prop.Type prop 
            name, ty, others
          )
          |> Array.toList
        let others = fields |> Seq.collect(fun (a,b,c) -> c) |> Seq.toList

        let fields = fields |> List.map(fun (n,f,_) -> n,f)

        let (fieldTy, record) =
          let name = String.concat "" path
          LongIdent name,
            Record (name) {
              for (n,f) in fields do Field(n,f)
            }

        Some(fieldTy, record::others)
      else
        None
    else
      None



  let rec createField
    path
    (currentType: MetaModel.Type)
    (currentProperty: MetaModel.Property)
    : string * WidgetBuilder<Type> * string list option * WidgetBuilder<AttributeNode> option * _ list =
    try
      let rec getType path (currentType: MetaModel.Type) : WidgetBuilder<Type> * WidgetBuilder<AttributeNode> option * _ list =
        match currentType with
        | MetaModel.Type.ReferenceType r ->
          let name = r.Name
          LongIdent name, None, []

        | MetaModel.Type.BaseType b ->
          let name = b.Name.ToDotNetType()
          LongIdent name, None, []

        | MetaModel.Type.OrType o ->
          
          match handleSameShapeStructuredUnions  path (createField) o.Items with
          | Some (x: WidgetBuilder<Type>, others) -> x, None, others
          | None ->

          // TS types can have optional properties (myKey?: string)
          // and unions with null (string | null)
          // we need to handle both cases
          let isOptional, items =
            if Array.exists isNullableType o.Items then
              true,
              o.Items
              |> Array.filter (fun x -> not (isNullableType x))
            else
              false, o.Items

          let ts =
            items
            |> Array.mapi ( fun i ->
              getType (path @ [$"C{i+1}"])
            )
          
          let others = ts |> Seq.collect(fun (a,b,c) -> c) |> Seq.toList

          let ts = ts |> Array.map(fun (a,b,c) -> a)
          // if this is already marked as Optional in the schema, ignore the union case
          // as we'll wrap it in an option type near the end
          if
            isOptional
            && not currentProperty.IsOptional
          then
            createOption (createErasedUnion ts),
            Some(
              Attribute "Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Include)"
            ), others
          else
            createErasedUnion ts, None, others

        | MetaModel.Type.ArrayType a ->
          let (t, _, others) =
            getType path a.Element
          Array(
            t,
            1
          ),
          None,
          others
        | MetaModel.Type.StructureLiteralType l ->
          if
            l.Value.PropertiesSafe
            |> Array.isEmpty
          then
            JToken, None, []
          else
            let ts =
              l.Value.PropertiesSafe
              |> Array.map (fun  p ->
                let (name, typ, _, _, others) = createField (path @ [p.NameAsPascalCase]) p.Type p
                name, typ, others
              )
              |> Array.toList
            let fieldTy, record =
              let fields =
                ts
                |> List.map(fun (n,f,_) ->
                  Field(n,f)
                )
              let name = String.concat "" path 
              LongIdent name, 
              Record (String.concat "" path) {
                for f in fields do f
              }

            let others =
              ts
              |> List.collect(fun (a,b,c) -> c)

            fieldTy, None, record :: others

        | MetaModel.Type.MapType m ->
          let key =
            match m.Key with
            | MetaModel.MapKeyType.Base b ->
              b.Name.ToDotNetType()
              |> LongIdent
            | MetaModel.MapKeyType.ReferenceType r -> LongIdent(r.Name)

          let (value, _, others) =
            getType path m.Value

          createDictionary [
            key
            value
          ],
          None,
          others

        | MetaModel.Type.StringLiteralType t ->
          LongIdent("string"), Some(Attribute($"UnionKindAttribute(\"{t.Value}\")")), []
        | MetaModel.Type.TupleType t ->
          
          let ts =
            t.Items
            |> Array.mapi (fun i -> 
              getType (path @ [$"T{i+1}"])
            )
            |> Array.toList
          let tuple =
            ts
            |> List.map(fun (a,b,c) -> a)
            |> Tuple
          let others = ts |> List.collect(fun (a,b,c) -> c)

          tuple, None, others

        | _ -> failwithf $"todo Property %A{currentType}"

      let (t, attribute, others) = getType path currentType
      let t = if currentProperty.IsOptional then createOption t else t
      let name = currentProperty.NameAsPascalCase
      name, t, currentProperty.StructuredDocs, attribute, others
    with e ->
      raise
      <| Exception(sprintf "createField on %A  " currentProperty, e)


  let isUnitStructure (structure: MetaModel.Structure) =

    let isEmptyExtends =
      structure.Extends
      |> Option.Array.isEmpty

    let isEmptyMixins =
      structure.Mixins
      |> Option.Array.isEmpty

    let isEmptyProperties =
      structure.Properties
      |> Option.Array.isEmpty

    isEmptyExtends
    && isEmptyMixins
    && isEmptyProperties

  //HACK: need to add WorkDoneProgressOptions since it's a mixin but really should be an interface
  let extensionsButNotReally = [
    "WorkDoneProgressParams"
    "WorkDoneProgressOptions"
  ]


  let createInterfaceStructures (structure: MetaModel.Structure array) (model: MetaModel.MetaModel) =
    // Scan and find all interfaces
    let interfaceStructures =
      structure
      |> Array.collect (fun s ->
        s.ExtendsSafe
        |> Array.map (fun e ->
          match e with
          | MetaModel.Type.ReferenceType r ->
            match
              model.StructuresSafe
              |> Array.tryFind (fun s -> s.Name = r.Name)
            with
            | Some s -> s
            | None -> failwithf "Could not find structure %s" r.Name
          | _ -> failwithf "todo Extends %A" e
        )
      )
      |> Array.distinctBy (fun x -> x.Name)

      //HACK: need to add additional types since it's a mixin but really should be an interface
      |> Array.append [|
        yield!
          model.StructuresSafe
          |> Array.filter (fun s ->
            extensionsButNotReally
            |> List.contains s.Name
          )
      |]

    interfaceStructures
    |> Array.map (fun s ->
      let widget =
        Interface($"I{s.Name}") {
          let properties = s.PropertiesSafe

          for p in properties do
            let name, t, docs, _, others= createField [s.Name; p.NameAsPascalCase] p.Type p
            let ap = AbstractProperty(name, t)

            yield
              docs
              |> Option.map (ap.xmlDocs)
              |> Option.defaultValue ap

          // MetaModel is incorrect we need to use Mixin instead of extends
          for e in s.MixinsSafe do
            match e with
            | MetaModel.Type.ReferenceType r -> yield Inherit($"I{r.Name}")
            | _ -> ()
        }

      let widget =
        s.StructuredDocs
        |> Option.map (fun docs -> widget.xmlDocs docs)
        |> Option.defaultValue widget

      s, widget
    )


  let createStructure
    (structure: MetaModel.Structure)
    (interfaceStructures: MetaModel.Structure array)
    (model: MetaModel.MetaModel)
    =

    let rec expandFields (structure: MetaModel.Structure) : list<_ * _ * _ * _ * _ * _> = [

      for e in structure.ExtendsSafe do
        match e with
        | MetaModel.Type.ReferenceType r ->
          match
            model.StructuresSafe
            |> Array.tryFind (fun s -> s.Name = r.Name)
          with
          | Some s ->
            for (name, ty, docs, attr, _, others) in expandFields s do
              (name, ty, docs, attr, 10, others)
          | None -> failwithf "Could not find structure %s" r.Name

        | _ -> failwithf "todo Extends %A" e

      // Mixins are inlined fields
      for m in structure.MixinsSafe do
        match m with
        | MetaModel.Type.ReferenceType r ->
          match
            model.StructuresSafe
            |> Array.tryFind (fun s -> s.Name = r.Name)
          with
          | Some s ->
            for p in s.PropertiesSafe do
              let (name, ty, docs, attr, others) = createField [s.Name; p.NameAsPascalCase] p.Type p 
              (name, ty, docs, attr, 1, others)
          | None -> failwithf "Could not find structure %s" r.Name
        | _ -> failwithf "todo Mixins %A" m

      for p in structure.PropertiesSafe do
        let (name, ty, docs, attr, others) = createField [structure.Name; p.NameAsPascalCase] p.Type p 
        (name, ty, docs, attr, 100, others)
    ]

    let rec implementInterface (structure: MetaModel.Structure) = [|


      // Implement interface
      yield!
        interfaceStructures
        |> Array.tryFind (fun s -> s.Name = structure.Name)
        |> Option.map (fun s ->
          let interfaceName = Ast.LongIdent($"I{s.Name}")

          InterfaceMember(interfaceName) {
            for p in s.PropertiesSafe do
              let name = Constant($"x.{p.NameAsPascalCase}")
              let outp = Property(ConstantPat(name), ConstantExpr(name))

              p.StructuredDocs
              |> Option.map (fun docs -> outp.xmlDocs docs)
              |> Option.defaultValue outp
          }

        )
        |> Option.toArray

      for e in structure.ExtendsSafe do
        match e with
        | MetaModel.Type.ReferenceType r ->
          yield!
            interfaceStructures
            |> Array.tryFind (fun s -> s.Name = r.Name)
            |> Option.map implementInterface
            |> Option.Array.toArray
        | _ -> ()

      // hack mixin with `extensionsButNotReally`
      for m in structure.MixinsSafe do
        match m with
        | MetaModel.Type.ReferenceType r ->
          yield!
            interfaceStructures
            |> Array.tryFind (fun s -> s.Name = r.Name)
            |> Option.map implementInterface
            |> Option.Array.toArray
        | _ -> ()
    |]


    try
      let recordFields = 
        expandFields structure
      let others =
        recordFields 
        |> List.collect(fun (_,_,_,_,_,o) -> o)
      [
        yield! others
        Record(structure.Name) {
        yield!
          recordFields
          |> List.groupBy (fun (name, _, _, _, _, _) -> name)
          |> List.map (fun (name, group) ->
            let (name, t, docs, attr, _, _) =
              group
              |> List.maxBy (fun (_, _, _, _, order, _) -> order)

            let f = Field(name, t)

            let f =
              docs
              |> Option.map (f.xmlDocs)
              |> Option.defaultValue f

            let f =
              attr
              |> Option.map (f.attribute)
              |> Option.defaultValue f

            f

          )
      }
      |> fun r ->
        let r =
          structure.StructuredDocs
          |> Option.map (fun docs -> r.xmlDocs docs)
          |> Option.defaultValue r

        match implementInterface structure with
        | [||] -> r
        | interfaces ->
          r.members () {
            for i in interfaces do
              i
          }
      ]

    with e ->
      raise
      <| Exception(sprintf "createStructure on %A" structure, e)



  let createTypeAlias (alias: MetaModel.TypeAlias) =
    let rec getType path (t: MetaModel.Type) =
      if alias.Name = "LSPAny" then
        JToken, []
      else
        match t with
        | MetaModel.Type.ReferenceType r -> LongIdent r.Name, []
        | MetaModel.Type.BaseType b -> LongIdent(b.Name.ToDotNetType()) , []
        | MetaModel.Type.OrType o ->
          match handleSameShapeStructuredUnions path (createField) o.Items with
          | Some (x, others) -> x, others
          | None ->

            let types =
              o.Items
              |> Array.mapi(fun i item ->
                getType (path @ [$"C{i+1}"]) item
              ) 
            let types2 =
              types |> Array.map fst
            
            let others = types |> Seq.collect snd
            let x =
              types2
              |> createErasedUnion
            x, Seq.toList others
        | MetaModel.Type.ArrayType a -> 
          let (types, others) = getType path a.Element
          Array(types, 1), others
        | MetaModel.Type.StructureLiteralType l when Proposed.checkProposed l.Value ->
          if
            l.Value.PropertiesSafe
            |> Array.isEmpty
          then
            JToken, []
          else
            let ts =
              l.Value.PropertiesSafe
              |> Array.map (fun p ->
                let (name, typ: WidgetBuilder<Type>, _, _, others) = createField [alias.Name] p.Type p 
                name, typ
              )
              |> Array.toList
            let name = path |> String.concat ""
            LongIdent(name)
            , [Record (name) { for (n, t) in ts do Field(n, t) }]

        | MetaModel.Type.MapType m ->
          let key =
            match m.Key with
            | MetaModel.MapKeyType.Base b ->
              b.Name.ToDotNetType()
              |> LongIdent
            | MetaModel.MapKeyType.ReferenceType r ->
              r.Name
              |> LongIdent

          let (value, others) = getType path m.Value

          createDictionary [
            key
            value
          ], others

        | MetaModel.Type.StringLiteralType t -> String() , []
        | MetaModel.Type.TupleType t ->
          let types = 

            t.Items
            |> Array.mapi(fun i item ->
              getType (path @ ["$T{i+1}"]) item
            ) 
          let others = 
            types
            |> Seq.collect snd
            |> Seq.toList
          let tuple =
            types
            |> Array.map fst
            |> Array.toList
            |> Tuple
          tuple, others

        | _ -> failwithf "todo Property %A" t

    let (types: WidgetBuilder<Type>, others) = getType [alias.Name] alias.Type

    let getIdent (x : IdentifierOrDot list) =
      x
      |> List.map(function IdentifierOrDot.Ident i -> i.Text | _ -> "")
      |> String.concat ""

    let abbrev =
      match Gen.mkOak types, others |> Seq.tryHead |> Option.map(Gen.mkOak) with
      | Type.LongIdent i, Some r when  (getIdent i.Content) = getIdent ((r :> ITypeDefn).TypeName.Identifier.Content)  ->
        AnonymousModule() {
          let r =
            Record(alias.Name) {
              for f in r.Fields do  f
                
            }
          alias.StructuredDocs
          |> Option.map (fun docs -> r.xmlDocs docs)
          |> Option.defaultValue r
        }
      | _ ->
        AnonymousModule() {
          let abbrev = Abbrev(alias.Name, types)

          let abbrev =
            alias.StructuredDocs
            |> Option.map (fun docs -> abbrev.xmlDocs docs)
            |> Option.defaultValue abbrev
          abbrev
          
          for o in others do o
        }

    AnonymousModule() {
      
        abbrev
      
    }

  let createEnumeration (enumeration: MetaModel.Enumeration) =
    AnonymousModule() {
      match enumeration.Type.Name with
      | MetaModel.EnumerationTypeNameValues.String ->
        match enumeration.SupportsCustomValues with
        | Some true ->

          let ab = Abbrev(enumeration.Name, "string")

          enumeration.StructuredDocs
          |> Option.map (fun docs -> ab.xmlDocs docs)
          |> Option.defaultValue ab

          NestedModule(enumeration.Name) {
            for v in enumeration.ValuesSafe do
              let name = PrettyNaming.NormalizeIdentifierBackticks v.Name
              let l = Value(ConstantPat(Constant(name)), ConstantExpr(String(v.Value))).attribute (Attribute "Literal")
              let l = l.returnType (LongIdent enumeration.Name)

              v.StructuredDocs
              |> Option.map (fun docs -> l.xmlDocs docs)
              |> Option.defaultValue l

          }


        | _ ->
          let enum =
            Enum enumeration.Name {
              for i, v in
                enumeration.ValuesSafe
                |> Array.mapi (fun i x -> i, x) do
                let case = EnumCase(v.Name, string i)

                let case = case.attribute (Attribute($"System.Runtime.Serialization.EnumMember(Value = \"{v.Value}\")"))

                v.StructuredDocs
                |> Option.map (fun docs -> case.xmlDocs docs)
                |> Option.defaultValue case
            }

          let enum =
            enumeration.StructuredDocs
            |> Option.map (fun docs -> enum.xmlDocs docs)
            |> Option.defaultValue enum

          enum.attribute (
            Attribute("Newtonsoft.Json.JsonConverter(typeof<Newtonsoft.Json.Converters.StringEnumConverter>)")
          )

      | MetaModel.EnumerationTypeNameValues.Integer
      | MetaModel.EnumerationTypeNameValues.Uinteger ->
        let enum =
          Enum enumeration.Name {
            for v in enumeration.ValuesSafe do
              let case = EnumCase(String.toPascalCase v.Name, v.Value)

              v.StructuredDocs
              |> Option.map (fun docs -> case.xmlDocs docs)
              |> Option.defaultValue case
          }

        enumeration.StructuredDocs
        |> Option.map (fun docs -> enum.xmlDocs docs)
        |> Option.defaultValue enum
      | _ -> failwithf "todo Enumeration %A" enumeration
    }


  let generateTests =
    testList "Generations" [
      testCaseAsync "Can Parse MetaModel"
      <| async {
        let! metaModel =
          File.ReadAllTextAsync(MetaModel.metaModel)
          |> Async.AwaitTask

        let parsedMetaModel =
          JsonConvert.DeserializeObject<MetaModel.MetaModel>(metaModel, MetaModel.metaModelSerializerSettings)

        let documentUriDocs =
          """
URI’s are transferred as strings. The URI’s format is defined in https://tools.ietf.org/html/rfc3986

See: https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#uri
"""

        let regexpDocs =
          """
Regular expressions are transferred as strings.

See https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#regExp
"""

        let source =
          Ast.Oak() {
            Namespace("Ionide.LanguageServerProtocol.Types") {

              // Simple aliases for types that are not in dotnet
              Abbrev("URI", "string")
                .xmlDocs (
                  documentUriDocs
                  |> StructuredDocs.parse
                )

              Abbrev("DocumentUri", "string")
                .xmlDocs (
                  documentUriDocs
                  |> StructuredDocs.parse
                )

              Abbrev("RegExp", "string")
                .xmlDocs (
                  regexpDocs
                  |> StructuredDocs.parse
                )


              let structures = parsedMetaModel.StructuresSafe

              let (knownInterfaces, interfaceWidgets) =
                createInterfaceStructures structures parsedMetaModel
                |> Array.unzip


              for w in interfaceWidgets do
                w
              
              let records = ResizeArray<_>()
              for s in structures do
                if isUnitStructure s then
                  Abbrev(s.Name, "unit")
                else
                    createStructure s knownInterfaces parsedMetaModel
                    |> List.map(fun r ->
                      let x  = Gen.mkOak r
                      let y :ITypeDefn = x
                      let name =
                        match y.TypeName.Identifier.Content |> List.head with
                        | IdentifierOrDot.Ident x -> x.Text
                        | _ -> ""
                      name, r
                    )
                    |> records.AddRange
              
              for r in records |> Seq.distinctBy fst |> Seq.map snd do r

              for t in parsedMetaModel.TypeAliasesSafe do
                createTypeAlias t
                  


              for e in parsedMetaModel.EnumerationsSafe do
                createEnumeration e


            }
            |> fun x -> x.toRecursive ()
          }


        let writeToFile path contents = File.WriteAllText(path, contents)

        source
        |> Gen.mkOak
        |> CodeFormatter.FormatOakAsync
        |> Async.RunSynchronously
        |> writeToFile (Path.Combine(__SOURCE_DIRECTORY__, "Types.cg.fsx"))
      }
    ]


  [<Tests>]
  let tests = ftestList "Generate" [ generateTests ]
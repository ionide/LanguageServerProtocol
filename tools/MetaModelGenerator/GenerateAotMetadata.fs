namespace MetaModelGenerator

module GenerateAotMetadata =
  open System
  open System.IO
  open System.Reflection
  open System.Text
  open Microsoft.FSharp.Reflection

  let private normalizeMethod (value: string) =
    value.Split(
      '/',
      StringSplitOptions.RemoveEmptyEntries
      ||| StringSplitOptions.TrimEntries
    )
    |> Array.filter ((<>) "$")
    |> Array.map (fun part ->
      Char.ToUpperInvariant(part.[0]).ToString()
      + part.Substring(1)
    )
    |> String.concat ""

  let private csharpTypeName (ty: Type) =
    let rec render (value: Type) =
      if value.IsArray then
        render (value.GetElementType())
        + "[]"
      elif value.IsGenericType then
        let definitionName = value.GetGenericTypeDefinition().FullName
        let tick = definitionName.IndexOf('`')
        let name = definitionName.Substring(0, tick).Replace('+', '.')

        let arguments =
          value.GetGenericArguments()
          |> Array.map render
          |> String.concat ", "

        $"global::{name}<{arguments}>"
      else
        $"global::{value.FullName.Replace('+', '.')}"

    render ty

  let private isClosedProtocolType (ty: Type) =
    not ty.ContainsGenericParameters
    && not ty.IsPointer
    && not ty.IsByRef
    && ty
       <> typeof<Void>
    && ty
       <> typeof<obj>

  let private collectTypes (assembly: Assembly) =
    let optionDefinition = typedefof<option<_>>
    let mapDefinition = typedefof<Map<_, _>>
    let protocolNamespace = "Ionide.LanguageServerProtocol.Types"
    let mutable seen = Set.empty<string>
    let mutable closedSpecials = Map.empty<string, Type>

    let rec visit (ty: Type) =
      if
        isNull ty
        || not (isClosedProtocolType ty)
      then
        ()
      else
        let key = ty.AssemblyQualifiedName

        if
          not (isNull key)
          && not (Set.contains key seen)
        then
          seen <- Set.add key seen

          if ty.IsArray then
            visit (ty.GetElementType())
          elif ty.IsGenericType then
            let definition = ty.GetGenericTypeDefinition()

            if
              definition = optionDefinition
              || definition = mapDefinition
              || (not (isNull definition.FullName)
                  && definition.FullName.StartsWith("Ionide.LanguageServerProtocol.Types.U", StringComparison.Ordinal))
            then
              closedSpecials <- Map.add key ty closedSpecials

            ty.GetGenericArguments()
            |> Array.iter visit

          if
            ty.Namespace = protocolNamespace
            && not ty.IsGenericTypeDefinition
          then
            ty.GetProperties(
              BindingFlags.Public
              ||| BindingFlags.Instance
            )
            |> Array.iter (fun property -> visit property.PropertyType)

    let roots =
      assembly.GetExportedTypes()
      |> Array.filter (fun ty ->
        ty.Namespace = protocolNamespace
        && not ty.IsGenericTypeDefinition
        && not ty.IsInterface
        && not (
          ty.IsAbstract
          && ty.IsSealed
        )
        && not (typeof<Attribute>.IsAssignableFrom ty)
        && not (ty.Name.EndsWith("Converter", StringComparison.Ordinal))
      )

    roots
    |> Array.iter visit

    for contractName in
      [
        "Ionide.LanguageServerProtocol.ILspServer"
        "Ionide.LanguageServerProtocol.ILspClient"
      ] do
      let contract = assembly.GetType(contractName, true)

      for methodInfo in contract.GetMethods() do
        methodInfo.GetParameters()
        |> Array.iter (fun parameter -> visit parameter.ParameterType)

        visit methodInfo.ReturnType

    roots
    |> Array.sortBy _.FullName,
    closedSpecials
    |> Map.toArray
    |> Array.map snd

  let private enumMemberValue (field: FieldInfo) =
    field.CustomAttributes
    |> Seq.tryFind (fun attribute ->
      attribute.AttributeType.FullName = "System.Runtime.Serialization.EnumMemberAttribute"
    )
    |> Option.bind (fun attribute ->
      attribute.NamedArguments
      |> Seq.tryFind (fun argument -> argument.MemberName = "Value")
      |> Option.map (fun argument -> string argument.TypedValue.Value)
    )
    |> Option.defaultValue field.Name

  let private unionKindValue (ty: Type) =
    ty.GetProperties(
      BindingFlags.Public
      ||| BindingFlags.Instance
    )
    |> Array.tryPick (fun property ->
      property.CustomAttributes
      |> Seq.tryFind (fun attribute ->
        attribute.AttributeType.FullName = "Ionide.LanguageServerProtocol.Types.UnionKindAttribute"
      )
      |> Option.bind (fun attribute ->
        attribute.ConstructorArguments
        |> Seq.tryHead
        |> Option.map (fun argument -> string argument.Value)
      )
    )

  let private escape (value: string) = value.Replace("\\", "\\\\").Replace("\"", "\\\"")

  let private generateCSharp (assembly: Assembly) (metaModel: MetaModel.MetaModel) =
    let roots, specials = collectTypes assembly

    let validationTypes =
      let mutable seen = Set.empty<string>
      let collected = ResizeArray<Type>()

      let rec visit (ty: Type) =
        if
          not (isNull ty)
          && isClosedProtocolType ty
        then
          let key = ty.AssemblyQualifiedName

          if
            not (isNull key)
            && not (Set.contains key seen)
          then
            seen <- Set.add key seen
            collected.Add ty

            if ty.IsArray then
              visit (ty.GetElementType())
            elif ty.IsGenericType then
              ty.GetGenericArguments()
              |> Array.iter visit

            if
              FSharpType.IsRecord(
                ty,
                BindingFlags.Public
                ||| BindingFlags.NonPublic
              )
            then
              FSharpType.GetRecordFields(
                ty,
                BindingFlags.Public
                ||| BindingFlags.NonPublic
              )
              |> Array.iter (fun property -> visit property.PropertyType)

      roots
      |> Array.iter visit

      specials
      |> Array.iter visit

      collected
      |> Seq.sortBy csharpTypeName
      |> Seq.toArray

    let enums =
      roots
      |> Array.filter (fun ty ->
        ty.IsEnum
        && ty.GetFields(
             BindingFlags.Public
             ||| BindingFlags.Static
           )
           |> Array.exists (fun field ->
             field.CustomAttributes
             |> Seq.exists (fun attribute ->
               attribute.AttributeType.FullName = "System.Runtime.Serialization.EnumMemberAttribute"
             )
           )
      )

    let optionDefinition = typedefof<option<_>>
    let mapDefinition = typedefof<Map<_, _>>

    let options =
      specials
      |> Array.filter (fun ty ->
        ty.IsGenericType
        && ty.GetGenericTypeDefinition() = optionDefinition
      )

    let maps =
      specials
      |> Array.filter (fun ty ->
        ty.IsGenericType
        && ty.GetGenericTypeDefinition() = mapDefinition
      )

    let unions =
      specials
      |> Array.filter (fun ty ->
        ty.IsGenericType
        && ty
          .GetGenericTypeDefinition()
          .FullName.StartsWith("Ionide.LanguageServerProtocol.Types.U", StringComparison.Ordinal)
      )

    let records =
      validationTypes
      |> Array.filter (fun ty ->
        FSharpType.IsRecord(
          ty,
          BindingFlags.Public
          ||| BindingFlags.NonPublic
        )
      )

    let validationOptions =
      validationTypes
      |> Array.filter (fun ty ->
        ty.IsGenericType
        && ty.GetGenericTypeDefinition() = optionDefinition
      )

    let validationMaps =
      validationTypes
      |> Array.filter (fun ty ->
        ty.IsGenericType
        && ty.GetGenericTypeDefinition() = mapDefinition
        && ty.GetGenericArguments().[0] = typeof<string>
      )

    let validationArrays =
      validationTypes
      |> Array.filter _.IsArray

    let serverRequests =
      metaModel.Requests
      |> Array.filter Proposed.checkProposed
      |> Array.filter (fun request ->
        request.MessageDirection = MetaModel.MessageDirection.ClientToServer
        || request.MessageDirection = MetaModel.MessageDirection.Both
      )

    let serverNotifications =
      metaModel.Notifications
      |> Array.filter Proposed.checkProposed
      |> Array.filter (fun notification ->
        notification.MessageDirection = MetaModel.MessageDirection.ClientToServer
        || notification.MessageDirection = MetaModel.MessageDirection.Both
      )

    let builder = StringBuilder()

    let line (text: string) =
      builder.AppendLine(text)
      |> ignore

    line "// <auto-generated />"
    line "#nullable enable"
    line "using System.Diagnostics.CodeAnalysis;"
    line "using System.Runtime.CompilerServices;"
    line "using System.Text.Json;"
    line "using System.Text.Json.Serialization;"
    line "using System.Text.Json.Serialization.Metadata;"
    line "using Microsoft.FSharp.Collections;"
    line "using Microsoft.FSharp.Core;"
    line "using PolyType;"
    line "using StreamJsonRpc;"
    line "[assembly: InternalsVisibleTo(\"Ionide.LanguageServerProtocol\")]"
    line "namespace Ionide.LanguageServerProtocol.StaticMetadata;"
    line ""
    line "[GenerateShape(IncludeMethods = MethodShapeFlags.PublicInstance)]"
    line "internal abstract partial class ProtocolTargetContract"
    line "{"

    for request in serverRequests do
      let name = normalizeMethod request.Method

      let parameter =
        if Array.isEmpty request.ParamsSafe then
          ""
        else
          "JsonElement request, "

      let singleParameter =
        if Array.isEmpty request.ParamsSafe then
          ""
        else
          ", UseSingleObjectParameterDeserialization = true"

      line $"    [JsonRpcMethod(\"{escape request.Method}\"{singleParameter})]"
      line $"    public abstract Task<JsonElement> {name}Async({parameter}CancellationToken cancellationToken);"

    for notification in serverNotifications do
      let name = normalizeMethod notification.Method

      let parameter =
        if Array.isEmpty notification.ParamsSafe then
          ""
        else
          "JsonElement request, "

      let singleParameter =
        if Array.isEmpty notification.ParamsSafe then
          ""
        else
          ", UseSingleObjectParameterDeserialization = true"

      line $"    [JsonRpcMethod(\"{escape notification.Method}\"{singleParameter})]"
      line $"    public abstract Task {name}Async({parameter}CancellationToken cancellationToken);"

    line "}"
    line ""

    let converterNames = ResizeArray<string>()

    options
    |> Array.iteri (fun index ty ->
      let name = $"OptionConverter{index}"
      converterNames.Add name

      line
        $"internal sealed class {name} : FSharpOptionJsonConverter<{csharpTypeName (ty.GetGenericArguments().[0])}> {{ }}"
    )

    maps
    |> Array.iteri (fun index ty ->
      let args = ty.GetGenericArguments()

      if args.[0] = typeof<string> then
        let name = $"MapConverter{index}"
        converterNames.Add name
        line $"internal sealed class {name} : FSharpMapJsonConverter<{csharpTypeName args.[1]}> {{ }}"
    )

    unions
    |> Array.iteri (fun index ty ->
      let args = ty.GetGenericArguments()
      let name = $"UnionConverter{index}"
      converterNames.Add name

      let kinds =
        args
        |> Array.map unionKindValue
        |> Array.map (
          Option.map (fun value -> $"\"{escape value}\"")
          >> Option.defaultValue "null"
        )
        |> String.concat ", "

      let typeArguments =
        args
        |> Array.map csharpTypeName
        |> String.concat ", "

      let baseName = $"ErasedUnion{args.Length}JsonConverter<{typeArguments}>"

      line
        $"internal sealed class {name} : {baseName} {{ protected override string?[] UnionKinds => new string?[] {{ {kinds} }}; }}"
    )

    enums
    |> Array.iteri (fun index ty ->
      let name = $"EnumConverter{index}"
      converterNames.Add name
      line $"internal sealed class {name} : JsonConverter<{csharpTypeName ty}>"
      line "{"

      line
        $"    public override {csharpTypeName ty} Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString() switch"

      line "    {"

      for field in
        ty.GetFields(
          BindingFlags.Public
          ||| BindingFlags.Static
        ) do
        line $"        \"{escape (enumMemberValue field)}\" => {csharpTypeName ty}.{field.Name},"

      line "        _ => throw new JsonException(\"Unknown protocol enumeration value.\"),"
      line "    };"

      line
        $"    public override void Write(Utf8JsonWriter writer, {csharpTypeName ty} value, JsonSerializerOptions options)"

      line "    {"
      line "        writer.WriteStringValue(value switch"
      line "        {"

      for field in
        ty.GetFields(
          BindingFlags.Public
          ||| BindingFlags.Static
        ) do
        line $"            {csharpTypeName ty}.{field.Name} => \"{escape (enumMemberValue field)}\","

      line "            _ => throw new JsonException(\"Unknown protocol enumeration value.\"),"
      line "        });"
      line "    }"
      line "}"
    )

    converterNames.Add "LspAnyJsonConverter"

    let converters =
      converterNames
      |> Seq.map (fun name -> $"typeof({name})")
      |> String.concat ", "

    line ""

    line
      $"[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, PropertyNameCaseInsensitive = true, RespectRequiredConstructorParameters = false, Converters = new[] {{ {converters} }})]"

    for index, ty in Array.indexed roots do
      line $"[JsonSerializable(typeof({csharpTypeName ty}), TypeInfoPropertyName = \"ProtocolType{index}\")]"

    for index, ty in Array.indexed specials do
      line $"[JsonSerializable(typeof({csharpTypeName ty}), TypeInfoPropertyName = \"ProtocolSpecialType{index}\")]"

    line "[JsonSerializable(typeof(JsonElement))]"
    line "internal partial class ProtocolJsonSerializerContext : JsonSerializerContext;"
    line ""
    line "internal static class ProtocolMetadata"
    line "{"

    line
      "    internal static IJsonTypeInfoResolver Resolver { get; } = ProtocolJsonSerializerContext.Default.WithAddedModifier(static typeInfo =>"

    line "    {"
    line "        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;"
    line "        foreach (JsonPropertyInfo property in typeInfo.Properties) property.IsRequired = false;"
    line "    });"

    line "    private static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();"

    line
      "    internal static RpcTargetMetadata Target { get; } = RpcTargetMetadata.FromShape<ProtocolTargetContract>();"

    line ""
    line "    private static JsonSerializerOptions CreateSerializerOptions()"
    line "    {"
    line "        var options = new JsonSerializerOptions(ProtocolJsonSerializerContext.Default.Options);"
    line "        options.TypeInfoResolver = Resolver;"
    line "        return options;"
    line "    }"

    line ""
    line "    internal static void Configure(JsonSerializerOptions options)"
    line "    {"
    line "        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;"
    line "        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;"
    line "        options.PropertyNameCaseInsensitive = true;"
    line "        options.TypeInfoResolver = Resolver;"

    line
      "        foreach (JsonConverter converter in ProtocolJsonSerializerContext.Default.Options.Converters) options.Converters.Add(converter);"

    line "    }"

    line "    internal static JsonElement Serialize(object? value)"
    line "    {"

    line "        if (value is null) return JsonDocument.Parse(\"null\").RootElement.Clone();"

    line
      "        return JsonSerializer.SerializeToElement(value, SerializerOptions.GetTypeInfo(value.GetType()) ?? throw new JsonException($\"No generated JSON metadata for {value.GetType()}.\"));"

    line "    }"

    line "    internal static T Deserialize<T>(JsonElement value)"
    line "    {"
    line "        ValidateRequired(typeof(T), value);"

    line
      "        return JsonSerializer.Deserialize(value, (JsonTypeInfo<T>)(SerializerOptions.GetTypeInfo(typeof(T)) ?? throw new JsonException($\"No generated JSON metadata for {typeof(T)}.\")))!;"

    line "    }"
    line ""
    line "    internal static void ValidateRequired(Type type, JsonElement value)"
    line "    {"

    for ty in validationOptions do
      let valueType = ty.GetGenericArguments().[0]
      line $"        if (type == typeof({csharpTypeName ty}))"
      line "        {"
      line "            if (value.ValueKind != JsonValueKind.Null)"
      line "            {"
      line $"                ValidateRequired(typeof({csharpTypeName valueType}), value);"
      line "            }"
      line ""
      line "            return;"
      line "        }"

    for ty in validationArrays do
      let elementType = ty.GetElementType()
      line $"        if (type == typeof({csharpTypeName ty}))"
      line "        {"
      line "            if (value.ValueKind == JsonValueKind.Array)"
      line "            {"
      line "                foreach (JsonElement element in value.EnumerateArray())"
      line "                {"
      line $"                    ValidateRequired(typeof({csharpTypeName elementType}), element);"
      line "                }"
      line "            }"
      line ""
      line "            return;"
      line "        }"

    for ty in validationMaps do
      let valueType = ty.GetGenericArguments().[1]
      line $"        if (type == typeof({csharpTypeName ty}))"
      line "        {"
      line "            if (value.ValueKind == JsonValueKind.Object)"
      line "            {"
      line "                foreach (JsonProperty property in value.EnumerateObject())"
      line "                {"
      line $"                    ValidateRequired(typeof({csharpTypeName valueType}), property.Value);"
      line "                }"
      line "            }"
      line ""
      line "            return;"
      line "        }"

    for ty in records do
      let properties =
        FSharpType.GetRecordFields(
          ty,
          BindingFlags.Public
          ||| BindingFlags.NonPublic
        )

      line $"        if (type == typeof({csharpTypeName ty}))"
      line "        {"

      properties
      |> Array.iteri (fun index property ->
        let propertyName = System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(property.Name)

        let isOptional =
          property.PropertyType.IsGenericType
          && property.PropertyType.GetGenericTypeDefinition() = optionDefinition

        if isOptional then
          line $"            if (TryGetProperty(value, \"{escape propertyName}\", out JsonElement property{index}))"

          line "            {"

          line $"                ValidateRequired(typeof({csharpTypeName property.PropertyType}), property{index});"

          line "            }"
        else
          line $"            JsonElement property{index} = RequireProperty(value, \"{escape propertyName}\");"

          line $"            ValidateRequired(typeof({csharpTypeName property.PropertyType}), property{index});"
      )

      line ""
      line "            return;"
      line "        }"

    line "    }"
    line ""
    line "    private static JsonElement RequireProperty(JsonElement value, string name)"
    line "    {"
    line "        if (TryGetProperty(value, name, out JsonElement property)) return property;"
    line "        throw new JsonException(string.Concat(\"Required property '\", name, \"' is missing.\"));"
    line "    }"
    line ""
    line "    private static bool TryGetProperty(JsonElement value, string name, out JsonElement result)"
    line "    {"
    line "        if (value.ValueKind == JsonValueKind.Object)"
    line "        {"
    line "            foreach (JsonProperty property in value.EnumerateObject())"
    line "            {"
    line "                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))"
    line "                {"
    line "                    result = property.Value;"
    line "                    return true;"
    line "                }"
    line "            }"
    line "        }"
    line ""
    line "        result = default;"
    line "        return false;"
    line "    }"

    line
      "    internal static Task NotifyAsync(global::StreamJsonRpc.JsonRpc rpc, string method, JsonElement value) => rpc.NotifyWithParameterObjectAsync(method, NamedArguments(value), NamedArgumentTypes(value));"

    line
      "    internal static Task<JsonElement> InvokeAsync(global::StreamJsonRpc.JsonRpc rpc, string method, JsonElement value) => rpc.InvokeWithParameterObjectAsync<JsonElement>(method, NamedArguments(value), NamedArgumentTypes(value), CancellationToken.None);"

    line ""

    line "    private static IReadOnlyDictionary<string, object?> NamedArguments(JsonElement value)"

    line "    {"

    line
      "        if (value.ValueKind != JsonValueKind.Object) throw new JsonException(\"Protocol parameters must be a JSON object.\");"

    line "        var arguments = new Dictionary<string, object?>();"

    line
      "        foreach (JsonProperty property in value.EnumerateObject()) arguments.Add(property.Name, property.Value);"

    line "        return arguments;"
    line "    }"
    line ""

    line "    private static IReadOnlyDictionary<string, Type> NamedArgumentTypes(JsonElement value)"

    line "    {"

    line
      "        if (value.ValueKind != JsonValueKind.Object) throw new JsonException(\"Protocol parameters must be a JSON object.\");"

    line "        var types = new Dictionary<string, Type>();"

    line
      "        foreach (JsonProperty property in value.EnumerateObject()) types.Add(property.Name, typeof(JsonElement));"

    line "        return types;"
    line "    }"

    line "}"
    line ""
    line "internal static class FormatterFactory"
    line "{"

    line
      "    [UnconditionalSuppressMessage(\"Trimming\", \"IL2026\", Justification = \"The generated JsonSerializerContext and static RPC metadata close the protocol graph.\")]"

    line
      "    [UnconditionalSuppressMessage(\"AOT\", \"IL3050\", Justification = \"The generated JsonSerializerContext and static RPC metadata close the protocol graph.\")]"

    line "    internal static IJsonRpcMessageFormatter Create()"
    line "    {"
    line "        var formatter = new SystemTextJsonFormatter();"
    line "        ProtocolMetadata.Configure(formatter.JsonSerializerOptions);"
    line "        return formatter;"
    line "    }"

    line "}"

    builder.ToString()

  let private generateFSharp (metaModel: MetaModel.MetaModel) =
    let serverRequests =
      metaModel.Requests
      |> Array.filter Proposed.checkProposed
      |> Array.filter (fun request ->
        request.MessageDirection = MetaModel.MessageDirection.ClientToServer
        || request.MessageDirection = MetaModel.MessageDirection.Both
      )

    let serverNotifications =
      metaModel.Notifications
      |> Array.filter Proposed.checkProposed
      |> Array.filter (fun notification ->
        notification.MessageDirection = MetaModel.MessageDirection.ClientToServer
        || notification.MessageDirection = MetaModel.MessageDirection.Both
      )

    let builder = StringBuilder()

    let line (text: string) =
      builder.AppendLine(text)
      |> ignore

    line "// <auto-generated />"
    line "namespace Ionide.LanguageServerProtocol"
    line "open System"
    line "open System.Text.Json"
    line "open System.Threading"
    line "open System.Threading.Tasks"
    line "open Ionide.LanguageServerProtocol.StaticMetadata"
    line ""
    line "type internal StaticProtocolTarget<'server when 'server :> ILspServer>"

    line
      "  (server: 'server, handlings: Map<string, Mappings.ServerRequestHandling<'server>>, onShutdown: Action, onExit: Action) ="

    line "  inherit ProtocolTargetContract()"

    for request in serverRequests do
      let name = normalizeMethod request.Method
      let route = escape request.Method

      if request.Method = "shutdown" then
        line $"  override _.{name}Async(cancellationToken) ="
        line "    onShutdown.Invoke()"

        line
          $"    AotRuntime.invokeWithoutParameter server handlings \"{route}\" cancellationToken (fun () -> server.{name}())"
      elif Array.isEmpty request.ParamsSafe then
        line $"  override _.{name}Async(cancellationToken) ="

        line
          $"    AotRuntime.invokeWithoutParameter server handlings \"{route}\" cancellationToken (fun () -> server.{name}())"
      else
        line $"  override _.{name}Async(request, cancellationToken) ="

        line
          $"    AotRuntime.invokeWithParameter server handlings \"{route}\" request cancellationToken (fun parameter -> server.{name}(parameter))"

    for notification in serverNotifications do
      let name = normalizeMethod notification.Method
      let route = escape notification.Method

      if notification.Method = "exit" then
        line $"  override _.{name}Async(cancellationToken) ="
        line "    onExit.Invoke()"

        line
          $"    AotRuntime.notifyWithoutParameter server handlings \"{route}\" cancellationToken (fun () -> server.{name}())"
      elif Array.isEmpty notification.ParamsSafe then
        line $"  override _.{name}Async(cancellationToken) ="

        line
          $"    AotRuntime.notifyWithoutParameter server handlings \"{route}\" cancellationToken (fun () -> server.{name}())"
      else
        line $"  override _.{name}Async(request, cancellationToken) ="

        line
          $"    AotRuntime.notifyWithParameter server handlings \"{route}\" request cancellationToken (fun parameter -> server.{name}(parameter))"

    builder.ToString()

  let generate
    (metaModel: MetaModel.MetaModel)
    (contractAssemblyPath: string)
    (csharpOutputPath: string)
    (fsharpOutputPath: string)
    =
    async {
      let assembly = Assembly.LoadFrom contractAssemblyPath
      let csharp = generateCSharp assembly metaModel
      let fsharp = generateFSharp metaModel

      Directory.CreateDirectory(Path.GetDirectoryName(csharpOutputPath))
      |> ignore

      Directory.CreateDirectory(Path.GetDirectoryName(fsharpOutputPath))
      |> ignore

      do! FileWriters.writeIfChanged csharpOutputPath csharp
      do! FileWriters.writeIfChanged fsharpOutputPath fsharp
    }
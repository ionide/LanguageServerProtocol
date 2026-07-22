using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Ionide.LanguageServerProtocol.Types;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

namespace Ionide.LanguageServerProtocol.StaticMetadata;

internal class FSharpOptionJsonConverter<T> : JsonConverter<FSharpOption<T>>
{
  public override bool HandleNull => true;

  public override FSharpOption<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    if (reader.TokenType == JsonTokenType.Null)
    {
      return FSharpOption<T>.None;
    }

    JsonTypeInfo<T> typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    T value = JsonSerializer.Deserialize(ref reader, typeInfo)!;
    return FSharpOption<T>.Some(value);
  }

  public override void Write(Utf8JsonWriter writer, FSharpOption<T> value, JsonSerializerOptions options)
  {
    if (FSharpOption<T>.get_IsNone(value))
    {
      writer.WriteNullValue();
      return;
    }

    JsonSerializer.Serialize(writer, value.Value, (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T)));
  }
}

internal class FSharpMapJsonConverter<T> : JsonConverter<FSharpMap<string, T>>
{
  public override FSharpMap<string, T> Read(
    ref Utf8JsonReader reader,
    Type typeToConvert,
    JsonSerializerOptions options
  )
  {
    if (reader.TokenType != JsonTokenType.StartObject)
    {
      throw new JsonException("A protocol map must be a JSON object.");
    }

    var values = new List<Tuple<string, T>>();
    JsonTypeInfo<T> typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
    {
      if (reader.TokenType != JsonTokenType.PropertyName)
      {
        throw new JsonException("Expected a protocol map key.");
      }

      string key = reader.GetString()!;
      if (!reader.Read())
        throw new JsonException("Expected a protocol map value.");
      values.Add(Tuple.Create(key, JsonSerializer.Deserialize(ref reader, typeInfo)!));
    }

    return new FSharpMap<string, T>(values);
  }

  public override void Write(Utf8JsonWriter writer, FSharpMap<string, T> value, JsonSerializerOptions options)
  {
    JsonTypeInfo<T> typeInfo = (JsonTypeInfo<T>)options.GetTypeInfo(typeof(T));
    writer.WriteStartObject();
    foreach (KeyValuePair<string, T> item in value)
    {
      writer.WritePropertyName(item.Key);
      JsonSerializer.Serialize(writer, item.Value, typeInfo);
    }
    writer.WriteEndObject();
  }
}

internal abstract class ErasedUnionJsonConverter<TUnion> : JsonConverter<TUnion>
{
  protected abstract Type[] CandidateTypes { get; }
  protected abstract string?[] UnionKinds { get; }
  protected abstract TUnion Create(int index, object? value);
  protected abstract object? GetValue(TUnion value);

  public override TUnion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  {
    using JsonDocument document = JsonDocument.ParseValue(ref reader);
    JsonElement element = document.RootElement;
    int selected = SelectCandidate(element, options);
    ProtocolMetadata.ValidateRequired(CandidateTypes[selected], element);
    JsonTypeInfo typeInfo = options.GetTypeInfo(CandidateTypes[selected]);
    object? value = JsonSerializer.Deserialize(element, typeInfo);
    return Create(selected, value);
  }

  public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options)
  {
    object? nested = GetValue(value);
    if (nested is null)
    {
      writer.WriteNullValue();
      return;
    }

    JsonSerializer.Serialize(writer, nested, options.GetTypeInfo(nested.GetType()));
  }

  private int SelectCandidate(JsonElement element, JsonSerializerOptions options)
  {
    for (int index = 0; index < CandidateTypes.Length; index++)
    {
      Type candidate = CandidateTypes[index];
      if (
        (element.ValueKind == JsonValueKind.String && candidate == typeof(string))
        || (element.ValueKind == JsonValueKind.Number && IsNumber(candidate))
        || (
          (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False)
          && candidate == typeof(bool)
        )
        || (element.ValueKind == JsonValueKind.Array && candidate.IsArray)
      )
      {
        return index;
      }
    }

    if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("kind", out JsonElement kindElement))
    {
      string? kind = kindElement.GetString();
      for (int index = 0; index < UnionKinds.Length; index++)
      {
        if (UnionKinds[index] == kind)
          return index;
      }
    }

    if (element.ValueKind == JsonValueKind.Object)
    {
      foreach (int index in Enumerable.Range(0, CandidateTypes.Length))
      {
        JsonTypeInfo candidate = options.GetTypeInfo(CandidateTypes[index]);
        if (candidate.Kind != JsonTypeInfoKind.Object)
          continue;
        bool matches = true;
        foreach (JsonProperty property in element.EnumerateObject())
        {
          if (
            !candidate.Properties.Any(candidateProperty =>
              StringComparer.OrdinalIgnoreCase.Equals(candidateProperty.Name, property.Name)
            )
          )
          {
            matches = false;
            break;
          }
        }

        if (matches)
          return index;
      }
    }

    throw new JsonException($"No case of {typeof(TUnion)} accepts this JSON value.");
  }

  private static bool IsNumber(Type type) =>
    type == typeof(byte)
    || type == typeof(sbyte)
    || type == typeof(short)
    || type == typeof(ushort)
    || type == typeof(int)
    || type == typeof(uint)
    || type == typeof(long)
    || type == typeof(ulong)
    || type == typeof(float)
    || type == typeof(double)
    || type == typeof(decimal);
}

internal class ErasedUnion2JsonConverter<T1, T2> : ErasedUnionJsonConverter<U2<T1, T2>>
{
  protected override Type[] CandidateTypes => [typeof(T1), typeof(T2)];
  protected override string?[] UnionKinds => [null, null];

  protected override U2<T1, T2> Create(int index, object? value) =>
    index switch
    {
      0 => U2<T1, T2>.NewC1((T1)value!),
      1 => U2<T1, T2>.NewC2((T2)value!),
      _ => throw new JsonException(),
    };

  protected override object? GetValue(U2<T1, T2> value) =>
    value switch
    {
      U2<T1, T2>.C1 first => first.Item,
      U2<T1, T2>.C2 second => second.Item,
      _ => throw new JsonException(),
    };
}

internal class ErasedUnion3JsonConverter<T1, T2, T3> : ErasedUnionJsonConverter<U3<T1, T2, T3>>
{
  protected override Type[] CandidateTypes => [typeof(T1), typeof(T2), typeof(T3)];
  protected override string?[] UnionKinds => [null, null, null];

  protected override U3<T1, T2, T3> Create(int index, object? value) =>
    index switch
    {
      0 => U3<T1, T2, T3>.NewC1((T1)value!),
      1 => U3<T1, T2, T3>.NewC2((T2)value!),
      2 => U3<T1, T2, T3>.NewC3((T3)value!),
      _ => throw new JsonException(),
    };

  protected override object? GetValue(U3<T1, T2, T3> value) =>
    value switch
    {
      U3<T1, T2, T3>.C1 item => item.Item,
      U3<T1, T2, T3>.C2 item => item.Item,
      U3<T1, T2, T3>.C3 item => item.Item,
      _ => throw new JsonException(),
    };
}

internal class ErasedUnion4JsonConverter<T1, T2, T3, T4> : ErasedUnionJsonConverter<U4<T1, T2, T3, T4>>
{
  protected override Type[] CandidateTypes => [typeof(T1), typeof(T2), typeof(T3), typeof(T4)];
  protected override string?[] UnionKinds => [null, null, null, null];

  protected override U4<T1, T2, T3, T4> Create(int index, object? value) =>
    index switch
    {
      0 => U4<T1, T2, T3, T4>.NewC1((T1)value!),
      1 => U4<T1, T2, T3, T4>.NewC2((T2)value!),
      2 => U4<T1, T2, T3, T4>.NewC3((T3)value!),
      3 => U4<T1, T2, T3, T4>.NewC4((T4)value!),
      _ => throw new JsonException(),
    };

  protected override object? GetValue(U4<T1, T2, T3, T4> value) =>
    value switch
    {
      U4<T1, T2, T3, T4>.C1 item => item.Item,
      U4<T1, T2, T3, T4>.C2 item => item.Item,
      U4<T1, T2, T3, T4>.C3 item => item.Item,
      U4<T1, T2, T3, T4>.C4 item => item.Item,
      _ => throw new JsonException(),
    };
}

internal sealed class LspAnyJsonConverter : JsonConverter<LSPAny>
{
  public override LSPAny Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
    LSPAny.fromJsonElement(JsonElement.ParseValue(ref reader));

  public override void Write(Utf8JsonWriter writer, LSPAny value, JsonSerializerOptions options) =>
    value.JsonElement.WriteTo(writer);
}

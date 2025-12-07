using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Records;

namespace Services;

public class PolymorphicTypeResolver : DefaultJsonTypeInfoResolver
{
    public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

        Type baseMessage = typeof(_Message);
        if (jsonTypeInfo.Type == baseMessage)
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                IgnoreUnrecognizedTypeDiscriminators = true,
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            };

            // Adicione os tipos derivados aqui
            foreach (var derivedType in typeof(_Message).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false)
                         .Cast<JsonDerivedTypeAttribute>())
            {
                jsonTypeInfo.PolymorphismOptions.DerivedTypes.Add(
                    new JsonDerivedType(derivedType.DerivedType, derivedType.TypeDiscriminator.ToString()));
            }
        }

        return jsonTypeInfo;
    }
}
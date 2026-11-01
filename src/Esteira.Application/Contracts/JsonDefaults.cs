using System.Text.Json;
using System.Text.Json.Serialization;

namespace Esteira.Application.Contracts;

public static class JsonDefaults
{
    /// <summary>camelCase, enums como string, sem nulos: o mesmo formato dos contratos em docs/contracts.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        WriteIndented = false,
    };
}

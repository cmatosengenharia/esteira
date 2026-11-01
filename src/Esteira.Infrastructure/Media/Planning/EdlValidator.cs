using System.Text.Json;
using System.Text.Json.Nodes;
using Esteira.Application.Contracts;
using Json.Schema;

namespace Esteira.Infrastructure.Media.Planning;

/// <summary>Valida um EDL contra docs/contracts/edl.schema.json (embutido). Nunca persistir EDL que não passe aqui.</summary>
public static class EdlValidator
{
    private static readonly Lazy<JsonSchema> Schema = new(() => JsonSchema.FromText(ContractResources.Read(ContractResources.EdlSchema)));

    public static IReadOnlyList<string> Validate(string edlJson)
    {
        JsonNode? node;
        try { node = JsonNode.Parse(edlJson); }
        catch (JsonException ex) { return [$"JSON inválido: {ex.Message}"]; }

        var result = Schema.Value.Evaluate(node, new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid) return [];

        var errors = new List<string>();
        foreach (var detail in result.Details.Where(d => d.HasErrors))
            foreach (var (key, message) in detail.Errors!)
                errors.Add($"{detail.InstanceLocation}: {key} {message}");
        return errors.Count == 0 ? ["EDL inválido (sem detalhe do schema)"] : errors;
    }

    public static void EnsureValid(string edlJson)
    {
        var errors = Validate(edlJson);
        if (errors.Count > 0)
            throw new InvalidOperationException("EDL não passa no schema: " + string.Join("; ", errors.Take(10)));
    }
}

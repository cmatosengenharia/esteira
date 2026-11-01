using System.Reflection;

namespace Esteira.Application.Contracts;

/// <summary>Lê os contratos JSON embutidos (docs/contracts).</summary>
public static class ContractResources
{
    public const string EdlSchema = "contracts/edl.schema.json";
    public const string StyleBibleSchema = "contracts/style-bible.schema.json";
    public const string NetworkSpecs = "contracts/network-specs.json";

    public static string Read(string logicalName)
    {
        var asm = typeof(ContractResources).Assembly;
        using var stream = asm.GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Recurso embutido não encontrado: {logicalName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

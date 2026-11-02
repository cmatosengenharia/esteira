namespace Esteira.Application.Contracts;

/// <summary>Nome e tipo de conteúdo de uma saída do job a partir da chave do objeto, para o download pela API.</summary>
public static class OutputFiles
{
    public static string FileName(string objectKey)
    {
        var slash = objectKey.LastIndexOf('/');
        var name = slash >= 0 ? objectKey[(slash + 1)..] : objectKey;
        return name.Length == 0 ? "saida" : name;
    }

    public static string ContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".mp4" or ".m4v" => "video/mp4",
        ".mov" => "video/quicktime",
        ".webm" => "video/webm",
        ".json" => "application/json",
        ".ass" or ".txt" => "text/plain; charset=utf-8",
        ".wav" => "audio/wav",
        _ => "application/octet-stream",
    };
}

namespace Esteira.Domain.Enums;

/// <summary>Proporções de saída. <see cref="AspectRatioExtensions.ToContract"/> dá a string usada nos contratos JSON ("9:16").</summary>
public enum AspectRatio
{
    Vertical9x16,
    Horizontal16x9,
    Square1x1,
    Portrait4x5,
}

public static class AspectRatioExtensions
{
    public static string ToContract(this AspectRatio aspect) => aspect switch
    {
        AspectRatio.Vertical9x16 => "9:16",
        AspectRatio.Horizontal16x9 => "16:9",
        AspectRatio.Square1x1 => "1:1",
        AspectRatio.Portrait4x5 => "4:5",
        _ => throw new ArgumentOutOfRangeException(nameof(aspect), aspect, null),
    };

    public static AspectRatio FromContract(string value) => value switch
    {
        "9:16" => AspectRatio.Vertical9x16,
        "16:9" => AspectRatio.Horizontal16x9,
        "1:1" => AspectRatio.Square1x1,
        "4:5" => AspectRatio.Portrait4x5,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "aspect desconhecido"),
    };

    /// <summary>Largura e altura do master para a proporção, base 1080p.</summary>
    public static (int Width, int Height) ToPixels(this AspectRatio aspect) => aspect switch
    {
        AspectRatio.Vertical9x16 => (1080, 1920),
        AspectRatio.Horizontal16x9 => (1920, 1080),
        AspectRatio.Square1x1 => (1080, 1080),
        AspectRatio.Portrait4x5 => (1080, 1350),
        _ => throw new ArgumentOutOfRangeException(nameof(aspect), aspect, null),
    };
}

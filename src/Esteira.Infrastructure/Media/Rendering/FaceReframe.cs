using System.Globalization;
using Esteira.Infrastructure.Media.Audio;

namespace Esteira.Infrastructure.Media.Rendering;

/// <summary>Centro horizontal do rosto num instante (amostra a 2 fps); null = quadro sem rosto.</summary>
public sealed record FaceSample(double T, double? CenterX);

/// <summary>
/// Reframe que segue o rosto: centro suavizado por média móvel de 1 s dentro de cada cena, passo máximo por amostra,
/// cena sem rosto usa o centro fixo do quadro. Ideia de SamurAIGPT/AI-Youtube-Shorts-Generator (MIT), reescrita.
/// </summary>
public static class FaceReframe
{
    private const double WindowSeconds = 1.0;

    /// <summary>Devolve (t, X) com X = borda esquerda do recorte, já limitada a [0, sourceWidth − cropWidth].</summary>
    public static IReadOnlyList<(double T, double X)> Smooth(
        IReadOnlyList<FaceSample> samples, IReadOnlyList<TimeRange> scenes, int sourceWidth, int cropWidth, double? maxStepPx = null)
    {
        var maxStep = maxStepPx ?? sourceWidth * 0.05;
        var maxX = (double)(sourceWidth - cropWidth);
        var points = new List<(double T, double X)>();

        foreach (var scene in scenes)
        {
            var inScene = samples.Where(s => s.T >= scene.In && s.T < scene.Out).OrderBy(s => s.T).ToList();
            if (inScene.Count == 0) continue;

            var firstFace = inScene.FirstOrDefault(s => s.CenterX is not null)?.CenterX;
            var raw = new double[inScene.Count];
            var last = firstFace ?? sourceWidth / 2.0;
            for (var i = 0; i < inScene.Count; i++)
                raw[i] = last = inScene[i].CenterX ?? last;

            double? prev = null;
            for (var i = 0; i < inScene.Count; i++)
            {
                var t = inScene[i].T;
                var center = Enumerable.Range(0, inScene.Count)
                    .Where(j => Math.Abs(inScene[j].T - t) <= WindowSeconds / 2 + 1e-9)
                    .Average(j => raw[j]);
                if (prev is { } p) center = p + Math.Clamp(center - p, -maxStep, maxStep);
                prev = center;
                points.Add((t, Math.Clamp(center - cropWidth / 2.0, 0, maxX)));
            }
        }

        return points;
    }

    /// <summary>
    /// crop=W:H:'X(t)':0 com X interpolado linearmente entre pontos da mesma cena; entre cenas X segura até o corte.
    /// Sem pontos, recorte central fixo.
    /// </summary>
    public static string BuildCropFilter(
        IReadOnlyList<(double T, double X)> points, IReadOnlyList<TimeRange> scenes, int cropWidth, int cropHeight)
    {
        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        if (points.Count == 0) return $"crop={cropWidth}:{cropHeight}:'(iw-ow)/2':0";

        var expr = F(points[^1].X);
        for (var i = points.Count - 2; i >= 0; i--)
        {
            var (t0, x0) = points[i];
            var (t1, x1) = points[i + 1];
            var sameScene = scenes.Any(s => s.In <= t0 && t1 < s.Out);
            var segment = sameScene ? $"{F(x0)}+({F(x1)}-{F(x0)})*(t-{F(t0)})/{F(t1 - t0)}" : F(x0);
            expr = $"if(lt(t,{F(t1)}),{segment},{expr})";
        }

        return $"crop={cropWidth}:{cropHeight}:'{expr}':0";
    }
}

using Esteira.Infrastructure.Media.Audio;
using Esteira.Infrastructure.Media.Rendering;

namespace Esteira.UnitTests.Media;

public class FaceReframeTests
{
    // fonte 1920x1080 → recorte 9:16 de 608x1080; X é a borda esquerda do recorte (centro − 304)
    private const int SourceWidth = 1920;
    private const int CropWidth = 608;

    /// <summary>Amostras a 2 fps de 0 até endSeconds (exclusivo), com o centro do rosto dado por tempo.</summary>
    private static List<FaceSample> Samples(double endSeconds, Func<double, double?> centerAt) =>
        Enumerable.Range(0, (int)(endSeconds * 2)).Select(i => i * 0.5).Select(t => new FaceSample(t, centerAt(t))).ToList();

    [Fact]
    public void Still_face_keeps_crop_constant_on_face_center()
    {
        var points = FaceReframe.Smooth(Samples(4, _ => 1200), [new TimeRange(0, 4)], SourceWidth, CropWidth);

        Assert.Equal(8, points.Count);
        Assert.All(points, p => Assert.Equal(1200 - 304, p.X, 3));
    }

    [Fact]
    public void Jump_is_smoothed_by_moving_average_and_limited_by_max_step()
    {
        // rosto salta 400 px em t=2; passo máximo padrão = 5% de 1920 = 96 px por amostra
        var points = FaceReframe.Smooth(Samples(10, t => t < 2 ? 960 : 1360), [new TimeRange(0, 10)], SourceWidth, CropWidth);

        for (var i = 1; i < points.Count; i++)
            Assert.True(Math.Abs(points[i].X - points[i - 1].X) <= 96 + 1e-9, $"passo {i} maior que 96 px");
        Assert.True(points[3].X > 656 && points[3].X < 1056); // em t=1.5 a média de 1 s já começa a andar
        Assert.Equal(1360 - 304, points[^1].X, 3);            // converge para o rosto
    }

    [Fact]
    public void Scene_without_face_falls_back_to_fixed_scene_center()
    {
        var points = FaceReframe.Smooth(Samples(3, _ => null), [new TimeRange(0, 3)], SourceWidth, CropWidth);

        Assert.All(points, p => Assert.Equal(960 - 304, p.X, 3));
    }

    [Fact]
    public void Missing_face_inside_scene_holds_last_center()
    {
        var points = FaceReframe.Smooth(Samples(4, t => t is >= 1 and < 3 ? null : 1200), [new TimeRange(0, 4)], SourceWidth, CropWidth);

        Assert.All(points, p => Assert.Equal(1200 - 304, p.X, 3));
    }

    [Fact]
    public void Smoothing_does_not_cross_scene_boundary()
    {
        var points = FaceReframe.Smooth(
            Samples(4, t => t < 2 ? 700 : 1500),
            [new TimeRange(0, 2), new TimeRange(2, 4)],
            SourceWidth, CropWidth);

        Assert.Equal(700 - 304, points[3].X, 3);  // t=1.5 não vê a cena seguinte
        Assert.Equal(1500 - 304, points[4].X, 3); // t=2.0 corta seco, sem herdar passo da cena anterior
    }

    [Fact]
    public void Crop_never_leaves_the_frame()
    {
        var left = FaceReframe.Smooth(Samples(2, _ => 50), [new TimeRange(0, 2)], SourceWidth, CropWidth);
        var right = FaceReframe.Smooth(Samples(2, _ => 1900), [new TimeRange(0, 2)], SourceWidth, CropWidth);

        Assert.All(left, p => Assert.Equal(0, p.X, 3));
        Assert.All(right, p => Assert.Equal(SourceWidth - CropWidth, p.X, 3));
    }

    [Fact]
    public void Crop_filter_is_piecewise_linear_expression_of_time()
    {
        var filter = FaceReframe.BuildCropFilter([(0.0, 100.0), (0.5, 150.5), (1.0, 200.0)], [new TimeRange(0, 2)], CropWidth, 1080);

        Assert.Equal("crop=608:1080:'if(lt(t,0.5),100+(150.5-100)*(t-0)/0.5,if(lt(t,1),150.5+(200-150.5)*(t-0.5)/0.5,200))':0", filter);
    }

    [Fact]
    public void Crop_filter_with_single_point_is_constant()
    {
        Assert.Equal("crop=608:1080:'656':0", FaceReframe.BuildCropFilter([(0.0, 656.0)], [new TimeRange(0, 2)], CropWidth, 1080));
    }

    [Fact]
    public void Crop_filter_cuts_hard_at_scene_boundary_instead_of_sliding()
    {
        // último ponto da cena 1 (t=1.5, X=396) segura até o corte; em t=2 salta para a cena 2 (X=1196)
        var filter = FaceReframe.BuildCropFilter([(1.0, 396.0), (1.5, 396.0), (2.0, 1196.0)], [new TimeRange(0, 2), new TimeRange(2, 4)], CropWidth, 1080);

        Assert.Equal("crop=608:1080:'if(lt(t,1.5),396+(396-396)*(t-1)/0.5,if(lt(t,2),396,1196))':0", filter);
    }

    [Fact]
    public void Crop_filter_without_samples_falls_back_to_center()
    {
        var points = FaceReframe.Smooth([], [new TimeRange(0, 2)], SourceWidth, CropWidth);

        Assert.Equal("crop=608:1080:'(iw-ow)/2':0", FaceReframe.BuildCropFilter(points, [new TimeRange(0, 2)], CropWidth, 1080));
    }
}

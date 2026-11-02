using Esteira.Application.Contracts;

namespace Esteira.UnitTests.Contracts;

public class OutputFilesTests
{
    [Theory]
    [InlineData("t/jobs/j/master-9x16.mp4", "master-9x16.mp4", "video/mp4")]
    [InlineData("t/jobs/j/clip-2-9x16.mp4", "clip-2-9x16.mp4", "video/mp4")]
    [InlineData("t/jobs/j/edl.json", "edl.json", "application/json")]
    [InlineData("t/jobs/j/captions.ass", "captions.ass", "text/plain; charset=utf-8")]
    [InlineData("semBarra.bin", "semBarra.bin", "application/octet-stream")]
    public void File_name_is_the_last_segment_and_content_type_follows_the_extension(string key, string name, string type)
    {
        Assert.Equal(name, OutputFiles.FileName(key));
        Assert.Equal(type, OutputFiles.ContentType(name));
    }
}

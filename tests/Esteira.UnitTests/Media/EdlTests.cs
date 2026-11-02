using Esteira.Application.Abstractions;
using Esteira.Domain.Entities;
using Esteira.Domain.Enums;
using Esteira.Infrastructure.Media.Audio;
using Esteira.Infrastructure.Media.Planning;
using Esteira.Infrastructure.Media.Rendering;
using Microsoft.Extensions.Logging.Abstractions;

namespace Esteira.UnitTests.Media;

public class EdlTests
{
    [Fact]
    public async Task Silence_planner_produces_a_schema_valid_edl_from_synthetic_wav_and_fixed_transcript()
    {
        var wav = SyntheticAudio.Build((true, 2.7), (false, 1.2), (true, 2.0));
        var path = Path.Combine(Path.GetTempPath(), $"esteira-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, WavFile.Write(wav.SampleRate, wav.Samples));
        try
        {
            var job = new VideoJob { OriginalFileName = "a.mp4", SourceObjectKey = "t/jobs/1/source.mp4", DurationSeconds = 5.9, Fps = 30 };
            var transcript = new Transcript("pt", CaptionTests.Fixed());
            var planner = new SilenceEditPlanner(NullLogger<SilenceEditPlanner>.Instance);

            var json = await planner.PlanAsync(new PlanRequest(job, transcript, path, null), CancellationToken.None);

            Assert.Empty(EdlValidator.Validate(json));
            var edl = EdlDocument.Parse(json);
            Assert.Equal(2, edl.Keep.Count);
            Assert.Equal("t/jobs/1/source.mp4", edl.SourceObjectKey);
            Assert.NotNull(edl.Captions);
            Assert.Equal("phrase", edl.Captions!.Mode);
            Assert.Equal(1200, edl.Captions.CombineWithinMs);
            Assert.Equal(AspectRatio.Vertical9x16, edl.Outputs.Single().Aspect);
            Assert.True(edl.TargetDurationSeconds is > 4.5 and < 5.2, $"target {edl.TargetDurationSeconds}");
            Assert.Contains("\"firstWord\": \"Olá\"", json);
            Assert.Contains("\"lastWord\": \"legenda.\"", json);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task Planner_cuts_repeated_take_and_hesitation_from_keep()
    {
        var wav = SyntheticAudio.Build((true, 6.6));
        var path = Path.Combine(Path.GetTempPath(), $"esteira-{Guid.NewGuid():N}.wav");
        await File.WriteAllBytesAsync(path, WavFile.Write(wav.SampleRate, wav.Samples));
        try
        {
            var job = new VideoJob { OriginalFileName = "a.mp4", SourceObjectKey = "t/jobs/1/source.mp4", DurationSeconds = 6.6, Fps = 30 };
            var transcript = new Transcript("pt", SpeechErrorDetectorTests.WithErrors());
            var planner = new SilenceEditPlanner(NullLogger<SilenceEditPlanner>.Instance);

            var json = await planner.PlanAsync(new PlanRequest(job, transcript, path, null), CancellationToken.None);

            Assert.Empty(EdlValidator.Validate(json));
            var edl = EdlDocument.Parse(json);
            Assert.Equal([new TimeRange(1.72, 4.60), new TimeRange(4.90, 6.6)], edl.Keep);
            Assert.Contains("1 repetição(ões)", edl.Strategy);
            Assert.Contains("1 hesitação(ões)", edl.Strategy);
            Assert.Contains("\"firstWord\": \"vamos\"", json);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Clips_round_trip_through_json_and_pass_the_schema()
    {
        var doc = new EdlDocument
        {
            SourceObjectKey = "t/jobs/1/source.mp4", SourceDurationSeconds = 180, SourceFps = 30, Strategy = "teste",
            Keep = [new TimeRange(0, 180)], Params = new EdlParams(), Outputs = [(AspectRatio.Vertical9x16, "center")],
            Clips = [new EdlClip("Por que 3 erros somem", 30.0, 72.5, 85, "Por que 3 erros de fala somem?"), new EdlClip("Frase 20", 100, 140, 50, null)],
        };

        var json = doc.ToJson();

        Assert.Empty(EdlValidator.Validate(json));
        var back = EdlDocument.Parse(json);
        Assert.Equal(doc.Clips, back.Clips);
        Assert.Contains("\"hookSentence\": \"Por que 3 erros de fala somem?\"", json);
    }

    [Fact]
    public void Validator_rejects_edl_that_violates_the_contract()
    {
        var missing = """{ "version": 1, "source": { "objectKey": "x" }, "keep": [], "layers": [], "params": {}, "outputs": [] }""";
        Assert.NotEmpty(EdlValidator.Validate(missing));

        var badAspect = """
        { "version": 1, "source": { "objectKey": "x", "durationSeconds": 1, "fps": 30 }, "keep": [{ "in": 0, "out": 1 }],
          "layers": [], "params": {}, "outputs": [{ "aspect": "3:2" }] }
        """;
        Assert.Contains(EdlValidator.Validate(badAspect), e => e.Contains("aspect"));

        var ok = """
        { "version": 1, "source": { "objectKey": "x", "durationSeconds": 1, "fps": 30 }, "keep": [{ "in": 0, "out": 1 }],
          "layers": [{ "type": "captions", "style": { "mode": "phrase" } }], "params": {}, "outputs": [{ "aspect": "9:16" }] }
        """;
        Assert.Empty(EdlValidator.Validate(ok));
        Assert.NotEmpty(EdlValidator.Validate("{ not json"));
    }

    [Fact]
    public void Ffmpeg_filter_uses_select_concat_center_crop_and_subtitles()
    {
        var select = FfmpegRenderer.BuildSelect([new TimeRange(0, 1.5), new TimeRange(2.25, 3)]);
        Assert.Equal("between(t,0,1.5)+between(t,2.25,3)", select);

        var filter = FfmpegRenderer.BuildFilter(select, 1080, 1920, "captions-9x16.ass");
        Assert.Contains("[0:v]select='between(t,0,1.5)+between(t,2.25,3)',setpts=N/FRAME_RATE/TB,crop=", filter);
        Assert.Contains("0.5625", filter);
        Assert.Contains("scale=1080:1920", filter);
        Assert.Contains(",subtitles=captions-9x16.ass[v];[0:a]aselect='between(t,0,1.5)+between(t,2.25,3)',asetpts=N/SR/TB[a]", filter);
        Assert.DoesNotContain("subtitles", FfmpegRenderer.BuildFilter(select, 1920, 1080, null));
    }
}

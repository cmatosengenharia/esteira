using Esteira.Infrastructure.Media.Tools;

namespace Esteira.UnitTests.Media;

public class WhisperParseTests
{
    [Fact]
    public void Parses_full_json_with_one_word_per_segment()
    {
        const string json = """
        {
          "result": { "language": "pt" },
          "transcription": [
            { "timestamps": { "from": "00:00:00,000", "to": "00:00:00,300" }, "offsets": { "from": 0, "to": 300 }, "text": " Olá",
              "tokens": [ { "text": "[_BEG_]", "p": 0.99 }, { "text": " Ol", "p": 0.8 }, { "text": "á", "p": 0.6 } ] },
            { "timestamps": { "from": "00:00:00,300", "to": "00:00:00,900" }, "offsets": { "from": 300, "to": 900 }, "text": " pessoal",
              "tokens": [ { "text": " pessoal", "p": 0.95 } ] },
            { "timestamps": { "from": "00:00:00,900", "to": "00:00:00,900" }, "offsets": { "from": 900, "to": 900 }, "text": "", "tokens": [] }
          ]
        }
        """;
        var t = WhisperCppTranscriber.Parse(json);
        Assert.Equal("pt", t.Language);
        Assert.Equal(2, t.Words.Count);
        Assert.Equal("Olá", t.Words[0].Word);
        Assert.Equal(0.0, t.Words[0].Start);
        Assert.Equal(0.3, t.Words[0].End);
        Assert.Equal(0.7, t.Words[0].Score, 3);
        Assert.Equal("Olá pessoal", t.Text);
        Assert.Equal(0.9, t.DurationSeconds);
    }

    [Fact]
    public void Parses_frame_rate_fractions()
    {
        Assert.Equal(29.97, FfprobeInspector.ParseRate("30000/1001"), 2);
        Assert.Equal(30, FfprobeInspector.ParseRate("30/1"));
        Assert.Equal(0, FfprobeInspector.ParseRate("0/0"));
    }
}

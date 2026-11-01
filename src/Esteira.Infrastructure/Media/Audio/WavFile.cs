using System.Buffers.Binary;

namespace Esteira.Infrastructure.Media.Audio;

/// <summary>Leitor mínimo de WAV PCM 16-bit (o único formato que a esteira produz). Multi-canal vira mono por média.</summary>
public sealed record WavFile(int SampleRate, short[] Samples)
{
    public double DurationSeconds => (double)Samples.Length / SampleRate;

    public static WavFile Read(string path) => Parse(File.ReadAllBytes(path));

    public static WavFile Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || data[..4].SequenceEqual("RIFF"u8) is false || data[8..12].SequenceEqual("WAVE"u8) is false)
            throw new InvalidDataException("Não é um arquivo WAV (RIFF/WAVE)");

        int channels = 0, sampleRate = 0, bitsPerSample = 0, format = 0;
        var pos = 12;
        while (pos + 8 <= data.Length)
        {
            var id = data.Slice(pos, 4);
            var size = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(pos + 4, 4));
            var body = pos + 8;
            if (id.SequenceEqual("fmt "u8))
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(body + 2, 2));
                sampleRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(body + 4, 4));
                bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(body + 14, 2));
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (format != 1 && format != 0xFFFE) throw new InvalidDataException($"WAV com formato {format} não suportado (só PCM)");
                if (bitsPerSample != 16) throw new InvalidDataException($"WAV com {bitsPerSample} bits não suportado (só 16)");
                if (channels <= 0 || sampleRate <= 0) throw new InvalidDataException("WAV sem chunk fmt antes de data");
                var available = Math.Min(size, data.Length - body);
                var frames = available / (2 * channels);
                var samples = new short[frames];
                for (var f = 0; f < frames; f++)
                {
                    var acc = 0;
                    for (var c = 0; c < channels; c++)
                        acc += BinaryPrimitives.ReadInt16LittleEndian(data.Slice(body + (f * channels + c) * 2, 2));
                    samples[f] = (short)(acc / channels);
                }
                return new WavFile(sampleRate, samples);
            }
            pos = body + size + (size & 1);
        }
        throw new InvalidDataException("WAV sem chunk data");
    }

    /// <summary>Gera um WAV mono 16-bit em memória (usado em testes e no vídeo de demonstração).</summary>
    public static byte[] Write(int sampleRate, ReadOnlySpan<short> samples)
    {
        var dataBytes = samples.Length * 2;
        var buffer = new byte[44 + dataBytes];
        var s = buffer.AsSpan();
        "RIFF"u8.CopyTo(s[..4]);
        BinaryPrimitives.WriteUInt32LittleEndian(s[4..8], (uint)(36 + dataBytes));
        "WAVE"u8.CopyTo(s[8..12]);
        "fmt "u8.CopyTo(s[12..16]);
        BinaryPrimitives.WriteUInt32LittleEndian(s[16..20], 16);
        BinaryPrimitives.WriteUInt16LittleEndian(s[20..22], 1);
        BinaryPrimitives.WriteUInt16LittleEndian(s[22..24], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(s[24..28], (uint)sampleRate);
        BinaryPrimitives.WriteUInt32LittleEndian(s[28..32], (uint)(sampleRate * 2));
        BinaryPrimitives.WriteUInt16LittleEndian(s[32..34], 2);
        BinaryPrimitives.WriteUInt16LittleEndian(s[34..36], 16);
        "data"u8.CopyTo(s[36..40]);
        BinaryPrimitives.WriteUInt32LittleEndian(s[40..44], (uint)dataBytes);
        for (var i = 0; i < samples.Length; i++)
            BinaryPrimitives.WriteInt16LittleEndian(s.Slice(44 + i * 2, 2), samples[i]);
        return buffer;
    }
}

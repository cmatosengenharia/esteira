namespace Esteira.Infrastructure.Media;

public sealed class MediaOptions
{
    public const string Section = "Media";
    /// <summary>Raiz do storage local (IObjectStorage da Fase 0).</summary>
    public string StorageRoot { get; set; } = "./data/storage";
    public string FfmpegPath { get; set; } = "ffmpeg";
    public string FfprobePath { get; set; } = "ffprobe";
    /// <summary>Vazio = procura whisper-cli e depois whisper-cpp no PATH (ou ESTEIRA_WHISPER_BIN).</summary>
    public string WhisperPath { get; set; } = string.Empty;
    public string WhisperModelPath { get; set; } = "./models/ggml-small.bin";
    public int WhisperThreads { get; set; } = 4;
    /// <summary>Idioma padrão da transcrição ("auto" detecta). Fixar "pt" melhora muito em áudio ruidoso.</summary>
    public string WhisperLanguage { get; set; } = "auto";
    /// <summary>Diretório de trabalho para arquivos temporários do render (ASS, listas).</summary>
    public string WorkRoot { get; set; } = "./data/work";
}

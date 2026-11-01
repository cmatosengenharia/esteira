using Esteira.Application.Abstractions;
using Esteira.Infrastructure.Media.Planning;
using Esteira.Infrastructure.Media.Rendering;
using Esteira.Infrastructure.Media.Storage;
using Esteira.Infrastructure.Media.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Esteira.Infrastructure.Media;

public static class MediaServiceCollectionExtensions
{
    /// <summary>Storage local, ffmpeg/ffprobe, whisper.cpp, planner por silêncio e render ffmpeg (Fase 0).</summary>
    public static IServiceCollection AddEsteiraMedia(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.Section));
        services.AddSingleton<ProcessRunner>();
        services.AddSingleton<IObjectStorage, LocalObjectStorage>();
        services.AddSingleton<IMediaInspector, FfprobeInspector>();
        services.AddSingleton<IAudioExtractor, FfmpegAudioExtractor>();
        services.AddSingleton<ITranscriber, WhisperCppTranscriber>();
        services.AddSingleton<IEditPlanner, SilenceEditPlanner>();
        services.AddSingleton<IRenderer, FfmpegRenderer>();
        return services;
    }
}

using Esteira.Domain.Entities;
using Esteira.Domain.Enums;

namespace Esteira.Application.Contracts;

/// <summary>Convenção de chaves no storage: sempre prefixadas pelo tenant.</summary>
public static class ObjectKeys
{
    public static string Source(Guid tenantId, Guid jobId, string extension) => $"{tenantId}/jobs/{jobId}/source{extension}";
    public static string Audio(VideoJob job) => $"{job.TenantId}/jobs/{job.Id}/audio.wav";
    public static string Master(VideoJob job, AspectRatio aspect) => $"{job.TenantId}/jobs/{job.Id}/master-{aspect.ToContract().Replace(':', 'x')}.mp4";
    public static string Clip(VideoJob job, int index) => $"{job.TenantId}/jobs/{job.Id}/clip-{index}-9x16.mp4";
    public static string Edl(VideoJob job) => $"{job.TenantId}/jobs/{job.Id}/edl.json";
    public static string Transcript(VideoJob job) => $"{job.TenantId}/jobs/{job.Id}/transcript.json";
    public static string Captions(VideoJob job) => $"{job.TenantId}/jobs/{job.Id}/captions.ass";
}

namespace SV_SOUL.Models;
/// <summary>Immutable, main-thread capture. Safe to pass to background request processing.</summary>
public sealed record WorldContextSnapshot(string Json, double CaptureMilliseconds);

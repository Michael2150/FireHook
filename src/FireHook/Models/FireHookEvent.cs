namespace FireHook.Models;

public sealed class FireHookEvent
{
    public long Id { get; init; }
    public string EventName { get; init; } = string.Empty;
    public string? Payload { get; init; }
    public DateTime FiredAt { get; init; }
}

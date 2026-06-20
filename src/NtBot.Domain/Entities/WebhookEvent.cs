namespace NtBot.Domain.Entities;

public class WebhookEvent
{
    public Guid Id { get; set; }
    public string Gateway { get; set; } = "stripe";
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? Payload { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

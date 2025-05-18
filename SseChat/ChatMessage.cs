namespace SseChat;

public class ChatMessage
{
    public Guid Id { get; }
    public DateTime Timestamp { get; }
    public string? UserName { get; }
    public string Text { get; }

    public ChatMessage(string? userName, string text)
    {
        Id = Guid.CreateVersion7();
        Timestamp = DateTime.UtcNow;
        UserName = userName;
        Text = text;
    }

    public ChatMessage(Guid id, DateTime timestamp, string? userName, string text)
    {
        Id = id;
        Timestamp = timestamp;
        UserName = userName;
        Text = text;
    }
}

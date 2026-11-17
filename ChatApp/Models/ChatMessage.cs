namespace ChatApp.Models;

public enum ChatMessageKind
{
    System,
    Broadcast,
    Private
}

/// <summary>
/// İstemci ve sunucu arasında iletilen tek bir sohbet mesajını temsil eder.
/// </summary>
public sealed class ChatMessage
{
    public DateTime Timestamp { get; }
    public ChatMessageKind Kind { get; }
    public string Sender { get; }
    public string Recipient { get; }
    public string Content { get; }

    public ChatMessage(
        DateTime timestamp,
        ChatMessageKind kind,
        string sender,
        string recipient,
        string content)
    {
        Timestamp = timestamp;
        Kind = kind;
        Sender = sender;
        Recipient = string.IsNullOrWhiteSpace(recipient) ? "Herkes" : recipient;
        Content = content;
    }

    public string ToFormattedDisplay()
    {
        return Kind switch
        {
            ChatMessageKind.System => $"[{Timestamp:HH:mm:ss}] [SİSTEM] {Content}",
            ChatMessageKind.Private => $"[{Timestamp:HH:mm:ss}] [ÖZEL: {Sender} -> {Recipient}] {Content}",
            _ => $"[{Timestamp:HH:mm:ss}] {Sender}: {Content}"
        };
    }

    public string ToWireProtocol()
    {
        string safeContent = Content.Replace("|", "¦").Replace("\r", " ").Replace("\n", " ");
        return $"MSG|{Timestamp:HH:mm:ss}|{Kind}|{Sender}|{Recipient}|{safeContent}";
    }

    public static ChatMessage? TryParseWire(string rawLine)
    {
        if (string.IsNullOrWhiteSpace(rawLine))
        {
            return null;
        }

        string[] parts = rawLine.Split('|', 6);
        if (parts.Length < 6 || parts[0] != "MSG")
        {
            return new ChatMessage(DateTime.Now, ChatMessageKind.System, "Sunucu", "Herkes", rawLine);
        }

        if (!DateTime.TryParse(parts[1], out DateTime parsedTime))
        {
            parsedTime = DateTime.Now;
        }

        if (!Enum.TryParse(parts[2], out ChatMessageKind kind))
        {
            kind = ChatMessageKind.Broadcast;
        }

        return new ChatMessage(parsedTime, kind, parts[3], parts[4], parts[5]);
    }
}

/// <summary>
/// Sunucuya bağlı bir istemcinin temel bağlantı bilgilerini tutar.
/// </summary>
public sealed class ConnectedClientInfo
{
    public string Username { get; }
    public string RemoteEndPoint { get; }
    public DateTime ConnectedAt { get; }

    public ConnectedClientInfo(string username, string remoteEndPoint, DateTime connectedAt)
    {
        Username = username;
        RemoteEndPoint = remoteEndPoint;
        ConnectedAt = connectedAt;
    }

    public override string ToString()
    {
        return $"● {Username}  ({RemoteEndPoint})  —  Giriş: {ConnectedAt:HH:mm:ss}";
    }
}

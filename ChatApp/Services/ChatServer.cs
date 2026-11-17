using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ChatApp.Models;

namespace ChatApp.Services;

/// <summary>
/// TcpListener kullanarak birden fazla istemcinin aynı odaya bağlanmasını,
/// genel ve özel mesajlaşmasını asenkron (async/await) olarak yöneten sunucu sınıfı.
/// </summary>
public sealed class ChatServer
{
    private sealed class ClientSession
    {
        public required ConnectedClientInfo Info { get; init; }
        public required TcpClient Client { get; init; }
        public required StreamWriter Writer { get; init; }
    }

    private readonly ConcurrentDictionary<string, ClientSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning { get; private set; }
    public int Port { get; private set; } = 5050;

    public event Action<string>? ServerLogReceived;
    public event Action<IReadOnlyList<ConnectedClientInfo>>? ConnectedClientsChanged;

    public void Start(int port)
    {
        if (IsRunning)
        {
            return;
        }

        Port = port;
        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        IsRunning = true;

        Log($"Sunucu {port} portunda başlatıldı. İstemci bağlantıları bekleniyor...");
        _ = AcceptClientsLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        try
        {
            _cts?.Cancel();
            _listener?.Stop();
        }
        catch
        {
            // Kapatma sırasındaki soket hataları yoksayılır
        }

        foreach (ClientSession session in _sessions.Values)
        {
            try
            {
                session.Client.Close();
            }
            catch
            {
                // Yoksay
            }
        }

        _sessions.Clear();
        NotifyClientsChanged();
        Log("Sunucu durduruldu ve tüm istemci bağlantıları kapatıldı.");
    }

    private async Task AcceptClientsLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null)
        {
            try
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync(token);
                _ = HandleClientSessionAsync(tcpClient, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (SocketException) when (!IsRunning)
            {
                break;
            }
            catch (Exception ex)
            {
                if (IsRunning)
                {
                    Log($"İstemci kabul hatası: {ex.Message}");
                }
            }
        }
    }

    private async Task HandleClientSessionAsync(TcpClient tcpClient, CancellationToken token)
    {
        string remoteEp = tcpClient.Client.RemoteEndPoint?.ToString() ?? "127.0.0.1";
        string username = string.Empty;

        try
        {
            using NetworkStream stream = tcpClient.GetStream();
            using StreamReader reader = new(stream, Encoding.UTF8);
            using StreamWriter writer = new(stream, Encoding.UTF8) { AutoFlush = true };

            // İlk satır: JOIN|KullaniciAdi
            string? joinLine = await reader.ReadLineAsync(token);
            if (string.IsNullOrWhiteSpace(joinLine) || !joinLine.StartsWith("JOIN|"))
            {
                tcpClient.Close();
                return;
            }

            string requestedName = joinLine.Substring(5).Trim();
            if (string.IsNullOrWhiteSpace(requestedName))
            {
                requestedName = "Misafir";
            }

            username = EnsureUniqueUsername(requestedName);
            ConnectedClientInfo info = new(username, remoteEp, DateTime.Now);
            ClientSession session = new()
            {
                Info = info,
                Client = tcpClient,
                Writer = writer
            };

            _sessions[username] = session;
            NotifyClientsChanged();
            Log($"İstemci bağlandı: {username} ({remoteEp})");

            ChatMessage welcomeMsg = new(
                DateTime.Now,
                ChatMessageKind.System,
                "Sunucu",
                "Herkes",
                $"'{username}' sohbet odasına katıldı. (Aktif kişi: {_sessions.Count})");
            await BroadcastMessageAsync(welcomeMsg);
            await BroadcastUserListAsync();

            while (!token.IsCancellationRequested && tcpClient.Connected)
            {
                string? rawLine = await reader.ReadLineAsync(token);
                if (rawLine == null)
                {
                    break;
                }

                if (rawLine.StartsWith("LEAVE|"))
                {
                    break;
                }

                ChatMessage? incoming = ChatMessage.TryParseWire(rawLine);
                if (incoming == null)
                {
                    continue;
                }

                if (incoming.Kind == ChatMessageKind.Private &&
                    !string.Equals(incoming.Recipient, "Herkes", StringComparison.OrdinalIgnoreCase))
                {
                    await RoutePrivateMessageAsync(incoming);
                }
                else
                {
                    await BroadcastMessageAsync(incoming);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException)
        {
            // İstemci bağlantıyı kapattı
        }
        finally
        {
            if (!string.IsNullOrEmpty(username) && _sessions.TryRemove(username, out _))
            {
                NotifyClientsChanged();
                Log($"İstemci ayrıldı: {username} ({remoteEp})");

                if (IsRunning)
                {
                    ChatMessage leaveMsg = new(
                        DateTime.Now,
                        ChatMessageKind.System,
                        "Sunucu",
                        "Herkes",
                        $"'{username}' sohbetten ayrıldı.");
                    await BroadcastMessageAsync(leaveMsg);
                    await BroadcastUserListAsync();
                }
            }

            try
            {
                tcpClient.Close();
            }
            catch
            {
                // Yoksay
            }
        }
    }

    private async Task RoutePrivateMessageAsync(ChatMessage message)
    {
        Log($"[Özel Mesaj] {message.Sender} -> {message.Recipient}: {message.Content}");
        string wire = message.ToWireProtocol();

        if (_sessions.TryGetValue(message.Recipient, out ClientSession? targetSession))
        {
            await SafeSendAsync(targetSession, wire);
        }

        if (_sessions.TryGetValue(message.Sender, out ClientSession? senderSession) &&
            !string.Equals(message.Sender, message.Recipient, StringComparison.OrdinalIgnoreCase))
        {
            await SafeSendAsync(senderSession, wire);
        }
    }

    public async Task BroadcastMessageAsync(ChatMessage message)
    {
        Log(message.ToFormattedDisplay());
        string wire = message.ToWireProtocol();

        foreach (ClientSession session in _sessions.Values)
        {
            await SafeSendAsync(session, wire);
        }
    }

    private async Task BroadcastUserListAsync()
    {
        string userListCsv = string.Join(",", _sessions.Keys.OrderBy(k => k));
        string wire = $"USERS|{userListCsv}";
        foreach (ClientSession session in _sessions.Values)
        {
            await SafeSendAsync(session, wire);
        }
    }

    private static async Task SafeSendAsync(ClientSession session, string wireLine)
    {
        try
        {
            await session.Writer.WriteLineAsync(wireLine);
        }
        catch
        {
            // Gönderim başarısızsa oturum döngüsü temizleyecektir
        }
    }

    private string EnsureUniqueUsername(string baseName)
    {
        string candidate = baseName;
        int suffix = 2;
        while (_sessions.ContainsKey(candidate))
        {
            candidate = $"{baseName}_{suffix++}";
        }
        return candidate;
    }

    private void NotifyClientsChanged()
    {
        List<ConnectedClientInfo> snapshot = _sessions.Values
            .Select(s => s.Info)
            .OrderBy(i => i.ConnectedAt)
            .ToList();
        ConnectedClientsChanged?.Invoke(snapshot);
    }

    private void Log(string text)
    {
        ServerLogReceived?.Invoke($"[{DateTime.Now:HH:mm:ss}] {text}");
    }
}

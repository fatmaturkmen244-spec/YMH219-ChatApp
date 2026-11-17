using System.Net.Sockets;
using System.Text;
using ChatApp.Models;

namespace ChatApp.Services;

/// <summary>
/// TcpClient ve StreamReader/StreamWriter kullanarak sunucuya asenkron bağlanan istemci sınıfı.
/// </summary>
public sealed class ChatClient
{
    private TcpClient? _tcpClient;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _cts;

    public bool IsConnected => _tcpClient?.Connected == true;
    public string Username { get; private set; } = string.Empty;

    public event Action<ChatMessage>? MessageReceived;
    public event Action<IReadOnlyList<string>>? OnlineUsersUpdated;
    public event Action<string>? Disconnected;

    public async Task ConnectAsync(string host, int port, string username)
    {
        if (IsConnected)
        {
            return;
        }

        Username = username.Trim();
        _tcpClient = new TcpClient();
        using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(4));
        await _tcpClient.ConnectAsync(host, port, timeoutCts.Token);

        NetworkStream stream = _tcpClient.GetStream();
        _reader = new StreamReader(stream, Encoding.UTF8);
        _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

        await _writer.WriteLineAsync($"JOIN|{Username}");

        _cts = new CancellationTokenSource();
        _ = ReceiveLoopAsync(_cts.Token);
    }

    public async Task SendMessageAsync(string content, string recipient = "Herkes")
    {
        if (!IsConnected || _writer == null)
        {
            throw new InvalidOperationException("Sunucuya aktif bir bağlantı bulunmuyor.");
        }

        ChatMessageKind kind = string.Equals(recipient, "Herkes", StringComparison.OrdinalIgnoreCase)
            ? ChatMessageKind.Broadcast
            : ChatMessageKind.Private;

        ChatMessage msg = new(DateTime.Now, kind, Username, recipient, content);
        await _writer.WriteLineAsync(msg.ToWireProtocol());
    }

    public async Task DisconnectAsync()
    {
        if (_tcpClient == null)
        {
            return;
        }

        try
        {
            if (_writer != null && _tcpClient.Connected)
            {
                await _writer.WriteLineAsync($"LEAVE|{Username}");
            }
        }
        catch
        {
            // Yoksay
        }

        Cleanup("Bağlantı kullanıcı tarafından sonlandırıldı.");
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _reader != null)
            {
                string? rawLine = await _reader.ReadLineAsync(token);
                if (rawLine == null)
                {
                    break;
                }

                if (rawLine.StartsWith("USERS|"))
                {
                    string csv = rawLine.Substring(6);
                    string[] users = string.IsNullOrWhiteSpace(csv)
                        ? []
                        : csv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    OnlineUsersUpdated?.Invoke(users);
                    continue;
                }

                ChatMessage? msg = ChatMessage.TryParseWire(rawLine);
                if (msg != null)
                {
                    MessageReceived?.Invoke(msg);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Cleanup($"Sunucu ile bağlantı kesildi ({ex.Message}).");
            return;
        }

        Cleanup("Sunucu bağlantıyı kapattı.");
    }

    private void Cleanup(string reason)
    {
        if (_tcpClient == null)
        {
            return;
        }

        try
        {
            _cts?.Cancel();
            _tcpClient.Close();
        }
        catch
        {
            // Yoksay
        }
        finally
        {
            _tcpClient = null;
            _reader = null;
            _writer = null;
        }

        Disconnected?.Invoke(reason);
    }
}

/// <summary>
/// Sohbet geçmişini .txt dosyasına kaydeder.
/// </summary>
public sealed class ChatHistoryService
{
    public void ExportHistory(string filePath, IEnumerable<string> formattedMessages)
    {
        using StreamWriter writer = new(filePath, append: false, Encoding.UTF8);
        writer.WriteLine("======================================================================");
        writer.WriteLine(" YMH 219 — İSTEMCİ-SUNUCU SOHBET GEÇMİŞİ KAYDI");
        writer.WriteLine($" Dışa Aktarım Zamanı: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        writer.WriteLine("======================================================================");
        writer.WriteLine();
        foreach (string line in formattedMessages)
        {
            writer.WriteLine(line);
        }
    }
}

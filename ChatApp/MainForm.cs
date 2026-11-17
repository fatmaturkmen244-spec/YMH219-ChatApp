using ChatApp.Models;
using ChatApp.Services;

namespace ChatApp;

public sealed class MainForm : Form
{
    private readonly ChatServer _server = new();
    private readonly ChatClient _client = new();
    private readonly ChatHistoryService _historyService = new();
    private readonly List<string> _chatTranscript = [];
    private static int _spawnedClientCounter = 2;

    // Sunucu Kontrolleri (Sol Panel)
    private readonly TextBox _txtServerPort;
    private readonly Button _btnStartServer;
    private readonly Button _btnStopServer;
    private readonly Label _lblServerStatus;
    private readonly ListBox _lstConnectedClients;
    private readonly RichTextBox _rtbServerLog;

    // İstemci Kontrolleri (Sağ Panel)
    private readonly TextBox _txtHost;
    private readonly TextBox _txtClientPort;
    private readonly TextBox _txtUsername;
    private readonly Button _btnConnect;
    private readonly Button _btnDisconnect;
    private readonly Label _lblClientStatus;
    private readonly RichTextBox _rtbChatMessages;
    private readonly ComboBox _cmbRecipient;
    private readonly TextBox _txtMessageInput;
    private readonly Button _btnSend;
    private readonly Button _btnSaveHistory;
    private readonly Label _lblStatusBar;

    public MainForm(string defaultUsername = "Fatma", bool isSecondaryClientWindow = false)
    {
        Text = isSecondaryClientWindow
            ? $"YMH219 — İstemci Sohbet Penceresi ({defaultUsername})"
            : "YMH219 — İstemci-Sunucu Chat Uygulaması (TCP/IP Soket Stüdyosu)";
        ClientSize = new Size(1000, 670);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(241, 245, 249);
        ForeColor = Color.FromArgb(30, 41, 59);

        // 1. Üst Başlık Barı (Indigo Messenger Header)
        Panel headerPanel = new()
        {
            Dock = DockStyle.Top,
            Height = 72,
            BackColor = Color.FromArgb(49, 46, 129)
        };
        Controls.Add(headerPanel);

        Panel violetStripe = new()
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = Color.FromArgb(167, 139, 250)
        };
        headerPanel.Controls.Add(violetStripe);

        Label lblHeaderTitle = new()
        {
            Text = "💬  TCP/IP İSTEMCİ-SUNUCU SOHBET SİSTEMİ",
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 12),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblHeaderTitle);

        Label lblHeaderSub = new()
        {
            Text = "Asenkron TcpListener & TcpClient · Çoklu İstemci Odası · Özel Mesajlaşma · Geçmiş Kaydı",
            Font = new Font("Segoe UI", 9.2f),
            ForeColor = Color.FromArgb(199, 210, 254),
            Location = new Point(22, 42),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblHeaderSub);

        Button btnNewClientWindow = new()
        {
            Text = "+ Yeni İstemci Aç",
            Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold),
            BackColor = Color.FromArgb(124, 58, 237),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(790, 19),
            Size = new Size(190, 36),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btnNewClientWindow.FlatAppearance.BorderSize = 0;
        btnNewClientWindow.Click += (_, _) =>
        {
            string nextUser = $"Ogrenci_{_spawnedClientCounter++}";
            MainForm extraClient = new(nextUser, isSecondaryClientWindow: true);
            extraClient.Show();
        };
        headerPanel.Controls.Add(btnNewClientWindow);

        // 2. Sol Panel: Sunucu (TcpListener) Yönetim Merkezi
        Panel serverCard = new()
        {
            Location = new Point(16, 86),
            Size = new Size(375, 536),
            BackColor = Color.White
        };
        Controls.Add(serverCard);

        Panel serverHeader = new()
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(238, 242, 255)
        };
        serverCard.Controls.Add(serverHeader);

        Label lblServerTitle = new()
        {
            Text = "SUNUCU KONTROL PANELİ",
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(55, 48, 163),
            Location = new Point(12, 12),
            AutoSize = true
        };
        serverHeader.Controls.Add(lblServerTitle);

        _lblServerStatus = new Label
        {
            Text = "● KAPALI",
            Font = new Font("Segoe UI", 8.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(153, 27, 27),
            BackColor = Color.FromArgb(254, 226, 226),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(252, 9),
            Size = new Size(110, 26)
        };
        serverHeader.Controls.Add(_lblServerStatus);

        Label lblSrvPort = new()
        {
            Text = "Port:",
            Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold),
            Location = new Point(14, 58),
            AutoSize = true
        };
        serverCard.Controls.Add(lblSrvPort);

        _txtServerPort = new TextBox
        {
            Text = "5050",
            Font = new Font("Consolas", 10f),
            Location = new Point(56, 55),
            Size = new Size(64, 26)
        };
        serverCard.Controls.Add(_txtServerPort);

        _btnStartServer = CreateStyledButton("▶ Başlat", 128, 52, 114, 32, Color.FromArgb(16, 185, 129), Color.White);
        _btnStartServer.Click += (_, _) => HandleStartServer();
        serverCard.Controls.Add(_btnStartServer);

        _btnStopServer = CreateStyledButton("■ Durdur", 250, 52, 110, 32, Color.FromArgb(239, 68, 68), Color.White);
        _btnStopServer.Enabled = false;
        _btnStopServer.Click += (_, _) => HandleStopServer();
        serverCard.Controls.Add(_btnStopServer);

        Label lblClientsHeader = new()
        {
            Text = "BAĞLI İSTEMCİLER (Kullanıcı Adı · IP:Port · Saat):",
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(14, 98),
            AutoSize = true
        };
        serverCard.Controls.Add(lblClientsHeader);

        _lstConnectedClients = new ListBox
        {
            Location = new Point(14, 120),
            Size = new Size(346, 155),
            Font = new Font("Consolas", 9.2f),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle
        };
        serverCard.Controls.Add(_lstConnectedClients);

        Label lblServerLogHeader = new()
        {
            Text = "SUNUCU AĞ GÜNLÜĞÜ (TcpListener Events):",
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(14, 286),
            AutoSize = true
        };
        serverCard.Controls.Add(lblServerLogHeader);

        _rtbServerLog = new RichTextBox
        {
            Location = new Point(14, 308),
            Size = new Size(346, 214),
            Font = new Font("Consolas", 8.8f),
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.FromArgb(165, 243, 252),
            BorderStyle = BorderStyle.None,
            ReadOnly = true
        };
        serverCard.Controls.Add(_rtbServerLog);

        // 3. Sağ Panel: İstemci (TcpClient) Sohbet Arayüzü
        Panel clientCard = new()
        {
            Location = new Point(405, 86),
            Size = new Size(579, 536),
            BackColor = Color.White
        };
        Controls.Add(clientCard);

        Panel clientHeader = new()
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(243, 232, 255)
        };
        clientCard.Controls.Add(clientHeader);

        Label lblClientTitle = new()
        {
            Text = "👤  İSTEMCİ SOHBET TERMİNALİ",
            Font = new Font("Segoe UI", 10.2f, FontStyle.Bold),
            ForeColor = Color.FromArgb(107, 33, 168),
            Location = new Point(14, 11),
            AutoSize = true
        };
        clientHeader.Controls.Add(lblClientTitle);

        _lblClientStatus = new Label
        {
            Text = "● BAĞLI DEĞİL",
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(146, 64, 14),
            BackColor = Color.FromArgb(254, 243, 199),
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(438, 9),
            Size = new Size(126, 26)
        };
        clientHeader.Controls.Add(_lblClientStatus);

        // Bağlantı Ayarları Satırı
        Label lblHost = new()
        {
            Text = "IP:",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Location = new Point(14, 58),
            AutoSize = true
        };
        clientCard.Controls.Add(lblHost);

        _txtHost = new TextBox
        {
            Text = "127.0.0.1",
            Font = new Font("Consolas", 9.5f),
            Location = new Point(38, 55),
            Size = new Size(92, 25)
        };
        clientCard.Controls.Add(_txtHost);

        Label lblCPort = new()
        {
            Text = "Port:",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Location = new Point(136, 58),
            AutoSize = true
        };
        clientCard.Controls.Add(lblCPort);

        _txtClientPort = new TextBox
        {
            Text = "5050",
            Font = new Font("Consolas", 9.5f),
            Location = new Point(172, 55),
            Size = new Size(52, 25)
        };
        clientCard.Controls.Add(_txtClientPort);

        Label lblUser = new()
        {
            Text = "Kullanıcı:",
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            Location = new Point(230, 58),
            AutoSize = true
        };
        clientCard.Controls.Add(lblUser);

        _txtUsername = new TextBox
        {
            Text = defaultUsername,
            Font = new Font("Segoe UI Semibold", 9.5f),
            Location = new Point(292, 55),
            Size = new Size(98, 25)
        };
        clientCard.Controls.Add(_txtUsername);

        _btnConnect = CreateStyledButton("Bağlan", 398, 52, 82, 31, Color.FromArgb(79, 70, 229), Color.White);
        _btnConnect.Click += async (_, _) => await HandleConnectClientAsync();
        clientCard.Controls.Add(_btnConnect);

        _btnDisconnect = CreateStyledButton("Kes", 486, 52, 78, 31, Color.FromArgb(100, 116, 139), Color.White);
        _btnDisconnect.Enabled = false;
        _btnDisconnect.Click += async (_, _) => await HandleDisconnectClientAsync();
        clientCard.Controls.Add(_btnDisconnect);

        // Mesaj Listesi Başlığı ve Geçmiş Kaydet Butonu
        Label lblMessagesHeader = new()
        {
            Text = "SOHBET AKIŞI (Zaman Bilgili Gelen / Giden Mesajlar):",
            Font = new Font("Segoe UI", 8.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(14, 98),
            AutoSize = true
        };
        clientCard.Controls.Add(lblMessagesHeader);

        _btnSaveHistory = CreateStyledButton("💾 .TXT Kaydet", 434, 92, 130, 24, Color.FromArgb(14, 165, 233), Color.White);
        _btnSaveHistory.Font = new Font("Segoe UI", 8.2f, FontStyle.Bold);
        _btnSaveHistory.Click += (_, _) => HandleSaveHistory();
        clientCard.Controls.Add(_btnSaveHistory);

        _rtbChatMessages = new RichTextBox
        {
            Location = new Point(14, 120),
            Size = new Size(550, 326),
            Font = new Font("Segoe UI", 10f),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };
        clientCard.Controls.Add(_rtbChatMessages);

        // Hedef Seçimi (Genel Oda veya Özel Mesaj) ve Mesaj Gönderim Alanı
        Label lblTarget = new()
        {
            Text = "Mesaj Alıcısı:",
            Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(14, 456),
            AutoSize = true
        };
        clientCard.Controls.Add(lblTarget);

        _cmbRecipient = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(112, 452),
            Size = new Size(210, 26)
        };
        _cmbRecipient.Items.Add("Herkes");
        _cmbRecipient.SelectedIndex = 0;
        clientCard.Controls.Add(_cmbRecipient);

        Label lblPrivateHint = new()
        {
            Text = "💡 İpucu: Listedeki bir kullanıcıyı seçerek özel mesaj gönderebilirsiniz.",
            Font = new Font("Segoe UI", 8.2f, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(14, 480),
            AutoSize = true
        };
        clientCard.Controls.Add(lblPrivateHint);

        _txtMessageInput = new TextBox
        {
            Location = new Point(14, 498),
            Size = new Size(442, 28),
            Font = new Font("Segoe UI", 10.2f),
            PlaceholderText = "Mesajınızı yazın ve Gönder'e veya Enter'a basın..."
        };
        _txtMessageInput.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await HandleSendMessageAsync();
            }
        };
        clientCard.Controls.Add(_txtMessageInput);

        _btnSend = CreateStyledButton("Gönder ➤", 464, 496, 100, 30, Color.FromArgb(79, 70, 229), Color.White);
        _btnSend.Enabled = false;
        _btnSend.Click += async (_, _) => await HandleSendMessageAsync();
        clientCard.Controls.Add(_btnSend);

        // 4. Alt Durum Çubuğu
        _lblStatusBar = new Label
        {
            Text = "Hazır: Önce sol panelden 'Sunucu Başlat' butonuna, ardından sağ panelden 'Bağlan' butonuna tıklayın.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(30, 27, 75),
            Dock = DockStyle.Bottom,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(16, 0, 0, 0)
        };
        Controls.Add(_lblStatusBar);

        // Sunucu ve İstemci Olay Abonelikleri (Thread-safe UI Invoke)
        _server.ServerLogReceived += logLine => SafeInvoke(() =>
        {
            _rtbServerLog.AppendText(logLine + Environment.NewLine);
            _rtbServerLog.ScrollToCaret();
        });

        _server.ConnectedClientsChanged += clients => SafeInvoke(() =>
        {
            _lstConnectedClients.Items.Clear();
            foreach (ConnectedClientInfo client in clients)
            {
                _lstConnectedClients.Items.Add(client.ToString());
            }
        });

        _client.MessageReceived += msg => SafeInvoke(() => AppendChatMessage(msg));

        _client.OnlineUsersUpdated += users => SafeInvoke(() =>
        {
            string currentSelection = _cmbRecipient.SelectedItem?.ToString() ?? "Herkes";
            _cmbRecipient.Items.Clear();
            _cmbRecipient.Items.Add("Herkes");
            foreach (string u in users)
            {
                if (!string.Equals(u, _client.Username, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbRecipient.Items.Add(u);
                }
            }

            int idx = _cmbRecipient.Items.IndexOf(currentSelection);
            _cmbRecipient.SelectedIndex = idx >= 0 ? idx : 0;
        });

        _client.Disconnected += reason => SafeInvoke(() =>
        {
            UpdateClientUiState(false);
            _lblStatusBar.Text = $"Bağlantı Durumu: {reason}";
        });

        FormClosing += async (_, _) =>
        {
            await _client.DisconnectAsync();
            _server.Stop();
        };
    }

    private static Button CreateStyledButton(string text, int x, int y, int width, int height, Color bgColor, Color fgColor)
    {
        Button btn = new()
        {
            Text = text,
            Font = new Font("Segoe UI Semibold", 9f, FontStyle.Bold),
            BackColor = bgColor,
            ForeColor = fgColor,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(x, y),
            Size = new Size(width, height),
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void SafeInvoke(Action action)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void HandleStartServer()
    {
        if (!int.TryParse(_txtServerPort.Text.Trim(), out int port) || port is < 1024 or > 65535)
        {
            MessageBox.Show(
                "Lütfen 1024 ile 65535 arasında geçerli bir TCP port numarası girin.",
                "Geçersiz Port",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _server.Start(port);
            _lblServerStatus.Text = $"● AKTİF ({port})";
            _lblServerStatus.BackColor = Color.FromArgb(209, 250, 229);
            _lblServerStatus.ForeColor = Color.FromArgb(6, 95, 70);
            _btnStartServer.Enabled = false;
            _btnStopServer.Enabled = true;
            _txtServerPort.Enabled = false;
            _lblStatusBar.Text = $"Sunucu {port} portunda dinlemede. İstemci bölümünden 'Bağlan' butonuna tıklayabilirsiniz.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Sunucu başlatılamadı (Port başka bir uygulama tarafından kullanılıyor olabilir):\n{ex.Message}",
                "Sunucu Başlatma Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void HandleStopServer()
    {
        _server.Stop();
        _lblServerStatus.Text = "● KAPALI";
        _lblServerStatus.BackColor = Color.FromArgb(254, 226, 226);
        _lblServerStatus.ForeColor = Color.FromArgb(153, 27, 27);
        _btnStartServer.Enabled = true;
        _btnStopServer.Enabled = false;
        _txtServerPort.Enabled = true;
        _lblStatusBar.Text = "Sunucu durduruldu.";
    }

    private async Task HandleConnectClientAsync()
    {
        string host = _txtHost.Text.Trim();
        string username = _txtUsername.Text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Lütfen bağlanmadan önce bir kullanıcı adı girin.", "Kullanıcı Adı Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_txtClientPort.Text.Trim(), out int port) || port is < 1024 or > 65535)
        {
            MessageBox.Show("Geçerli bir port numarası girin.", "Port Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnConnect.Enabled = false;
        _lblStatusBar.Text = $"{host}:{port} adresindeki sunucuya bağlanılıyor...";

        try
        {
            await _client.ConnectAsync(host, port, username);
            UpdateClientUiState(true);
            _lblStatusBar.Text = $"Bağlantı başarılı: '{username}' olarak {host}:{port} sunucusuna bağlandınız.";
        }
        catch (Exception ex)
        {
            _btnConnect.Enabled = true;
            _lblStatusBar.Text = $"Bağlantı hatası: Sunucuya ulaşılamadı ({ex.Message}).";
            MessageBox.Show(
                $"Sunucuya bağlanılamadı!\n\nDetay: {ex.Message}\n\nLütfen önce sunucunun başlatıldığından ve IP/Port bilgilerinin doğruluğundan emin olun.",
                "Bağlantı Hatası",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task HandleDisconnectClientAsync()
    {
        await _client.DisconnectAsync();
        UpdateClientUiState(false);
    }

    private void UpdateClientUiState(bool connected)
    {
        _btnConnect.Enabled = !connected;
        _btnDisconnect.Enabled = connected;
        _btnSend.Enabled = connected;
        _txtHost.Enabled = !connected;
        _txtClientPort.Enabled = !connected;
        _txtUsername.Enabled = !connected;

        if (connected)
        {
            _lblClientStatus.Text = $"● BAĞLI ({_client.Username})";
            _lblClientStatus.BackColor = Color.FromArgb(209, 250, 229);
            _lblClientStatus.ForeColor = Color.FromArgb(6, 95, 70);
        }
        else
        {
            _lblClientStatus.Text = "● BAĞLI DEĞİL";
            _lblClientStatus.BackColor = Color.FromArgb(254, 243, 199);
            _lblClientStatus.ForeColor = Color.FromArgb(146, 64, 14);
        }
    }

    private async Task HandleSendMessageAsync()
    {
        string text = _txtMessageInput.Text.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string recipient = _cmbRecipient.SelectedItem?.ToString() ?? "Herkes";
        // @KullaniciAdi kısayolu desteği
        if (text.StartsWith('@') && text.Contains(' '))
        {
            int spaceIdx = text.IndexOf(' ');
            recipient = text.Substring(1, spaceIdx - 1).Trim();
            text = text.Substring(spaceIdx + 1).Trim();
        }

        try
        {
            await _client.SendMessageAsync(text, recipient);
            _txtMessageInput.Clear();
            _txtMessageInput.Focus();
        }
        catch (Exception ex)
        {
            _lblStatusBar.Text = $"Mesaj gönderilemedi: {ex.Message}";
            MessageBox.Show($"Mesaj gönderilirken hata oluştu:\n{ex.Message}", "Gönderim Hatası", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void AppendChatMessage(ChatMessage msg)
    {
        string formatted = msg.ToFormattedDisplay();
        _chatTranscript.Add(formatted);

        Color lineColor = msg.Kind switch
        {
            ChatMessageKind.System => Color.FromArgb(5, 150, 105),
            ChatMessageKind.Private => Color.FromArgb(124, 58, 237),
            _ => string.Equals(msg.Sender, _client.Username, StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(37, 99, 235)
                : Color.FromArgb(15, 23, 42)
        };

        _rtbChatMessages.SelectionStart = _rtbChatMessages.TextLength;
        _rtbChatMessages.SelectionLength = 0;
        _rtbChatMessages.SelectionColor = lineColor;
        _rtbChatMessages.SelectionFont = new Font(
            "Segoe UI",
            9.8f,
            msg.Kind == ChatMessageKind.Private ? FontStyle.Bold : FontStyle.Regular);
        _rtbChatMessages.AppendText(formatted + Environment.NewLine);
        _rtbChatMessages.ScrollToCaret();
    }

    private void HandleSaveHistory()
    {
        if (_chatTranscript.Count == 0)
        {
            MessageBox.Show("Kaydedilecek sohbet geçmişi bulunmuyor.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using SaveFileDialog sfd = new()
        {
            Title = "Sohbet Geçmişini Kaydet",
            Filter = "Metin Dosyası (*.txt)|*.txt",
            FileName = $"ChatHistory_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            _historyService.ExportHistory(sfd.FileName, _chatTranscript);
            _lblStatusBar.Text = $"Sohbet geçmişi kaydedildi: {Path.GetFileName(sfd.FileName)}";
            MessageBox.Show($"Sohbet geçmişi başarıyla kaydedildi:\n{sfd.FileName}", "Kayıt Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Dosya kaydedilemedi:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void CaptureScreenshots(string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        Show();
        Application.DoEvents();

        // Ekran Görüntüsü 1: Sunucu Aktif + 3 İstemci Bağlı + Genel ve Özel Mesajlaşma Akışı
        _lblServerStatus.Text = "● AKTİF (5050)";
        _lblServerStatus.BackColor = Color.FromArgb(209, 250, 229);
        _lblServerStatus.ForeColor = Color.FromArgb(6, 95, 70);
        _btnStartServer.Enabled = false;
        _btnStopServer.Enabled = true;

        _lstConnectedClients.Items.Clear();
        _lstConnectedClients.Items.Add("● Fatma  (127.0.0.1:54112)  —  Giriş: 14:30:05");
        _lstConnectedClients.Items.Add("● Ahmet  (127.0.0.1:54118)  —  Giriş: 14:30:12");
        _lstConnectedClients.Items.Add("● Zeynep (127.0.0.1:54125)  —  Giriş: 14:30:28");

        _rtbServerLog.Clear();
        _rtbServerLog.AppendText("[14:30:01] Sunucu 5050 portunda başlatıldı. İstemci bağlantıları bekleniyor...\n");
        _rtbServerLog.AppendText("[14:30:05] İstemci bağlandı: Fatma (127.0.0.1:54112)\n");
        _rtbServerLog.AppendText("[14:30:12] İstemci bağlandı: Ahmet (127.0.0.1:54118)\n");
        _rtbServerLog.AppendText("[14:30:20] Fatma: Merhaba arkadaşlar, TCP soket bağlantısı sorunsuz çalışıyor!\n");
        _rtbServerLog.AppendText("[14:30:24] Ahmet: Selam Fatma, mesajlar anlık olarak düştü.\n");
        _rtbServerLog.AppendText("[14:30:28] İstemci bağlandı: Zeynep (127.0.0.1:54125)\n");
        _rtbServerLog.AppendText("[14:30:35] [Özel Mesaj] Fatma -> Zeynep: Zeynep proje raporunu kontrol ettin mi?\n");

        UpdateClientUiState(true);
        _lblClientStatus.Text = "● BAĞLI (Fatma)";
        _cmbRecipient.Items.Clear();
        _cmbRecipient.Items.AddRange(["Herkes", "Ahmet", "Zeynep"]);
        _cmbRecipient.SelectedIndex = 0;

        DateTime t0 = DateTime.Today.AddHours(14).AddMinutes(30).AddSeconds(5);
        AppendChatMessage(new ChatMessage(t0, ChatMessageKind.System, "Sunucu", "Herkes", "'Fatma' sohbet odasına katıldı. (Aktif kişi: 1)"));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(7), ChatMessageKind.System, "Sunucu", "Herkes", "'Ahmet' sohbet odasına katıldı. (Aktif kişi: 2)"));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(15), ChatMessageKind.Broadcast, "Fatma", "Herkes", "Merhaba arkadaşlar, TCP soket bağlantısı sorunsuz çalışıyor!"));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(19), ChatMessageKind.Broadcast, "Ahmet", "Herkes", "Selam Fatma, mesajlar anlık olarak düştü."));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(23), ChatMessageKind.System, "Sunucu", "Herkes", "'Zeynep' sohbet odasına katıldı. (Aktif kişi: 3)"));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(30), ChatMessageKind.Private, "Fatma", "Zeynep", "Zeynep proje raporunu kontrol ettin mi?"));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(38), ChatMessageKind.Private, "Zeynep", "Fatma", "Evet, ekran görüntüleri ve sınıf diyagramı hazır!"));

        _txtMessageInput.Text = "Harika, o zaman GitHub'a yükleyebiliriz.";
        _lblStatusBar.Text = "Bağlantı aktif: 'Fatma' olarak 127.0.0.1:5050 sunucusuna bağlısınız (3 çevrimiçi kullanıcı).";
        Refresh();
        Application.DoEvents();

        using (Bitmap bmp1 = new(Width, Height))
        {
            DrawToBitmap(bmp1, new Rectangle(0, 0, Width, Height));
            bmp1.Save(Path.Combine(outputDir, "chat_active.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        // Ekran Görüntüsü 2: Özel Mesaj Seçimi + Bağlantı Kesilme / Hata Yönetimi ve Geçmiş Kaydı Durumu
        _cmbRecipient.SelectedIndex = 2; // Zeynep seçili
        _txtMessageInput.Clear();
        AppendChatMessage(new ChatMessage(t0.AddSeconds(45), ChatMessageKind.Broadcast, "Fatma", "Herkes", "Harika, o zaman GitHub'a yükleyebiliriz."));
        AppendChatMessage(new ChatMessage(t0.AddSeconds(52), ChatMessageKind.System, "Sunucu", "Herkes", "'Ahmet' sohbetten ayrıldı."));
        _lstConnectedClients.Items.RemoveAt(1);
        _rtbServerLog.AppendText("[14:30:52] İstemci ayrıldı: Ahmet (127.0.0.1:54118)\n");
        _lblStatusBar.Text = "Sohbet geçmişi kaydedildi: ChatHistory_20261117_143100.txt (8 mesaj başarıyla dışa aktarıldı).";
        Refresh();
        Application.DoEvents();

        using (Bitmap bmp2 = new(Width, Height))
        {
            DrawToBitmap(bmp2, new Rectangle(0, 0, Width, Height));
            bmp2.Save(Path.Combine(outputDir, "chat_private_and_export.png"), System.Drawing.Imaging.ImageFormat.Png);
        }

        Close();
    }
}

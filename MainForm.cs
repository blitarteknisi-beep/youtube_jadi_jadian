using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Net.Http;
using System.Text.Json;

namespace YouTubeAudioPlayer;

public sealed class MainForm : Form
{
    private readonly TextBox searchBox = new() { PlaceholderText = "Cari judul lagu atau nama artis…", Dock = DockStyle.Fill };
    private readonly TextBox apiKeyBox = new() { PlaceholderText = "YouTube Data API key (wajib untuk pencarian)", Width = 260, UseSystemPasswordChar = true };
    private readonly Button searchButton = ButtonOf("Cari", 72);
    private readonly Button saveKeyButton = ButtonOf("Simpan API key", 112);
    private readonly Button playButton = ButtonOf("▶  Play", 82);
    private readonly Button pauseButton = ButtonOf("Ⅱ  Pause", 82);
    private readonly Button previousButton = ButtonOf("|◀", 52);
    private readonly Button nextButton = ButtonOf("▶|", 52);
    private readonly Button volumeButton = ButtonOf("Volume", 72);
    private readonly TrackBar volume = new() { Minimum = 0, Maximum = 100, Value = 75, TickStyle = TickStyle.None, Width = 130 };
    private readonly ListBox results = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly Label nowPlaying = new() { Text = "Belum ada lagu dipilih", AutoEllipsis = true, Dock = DockStyle.Fill, ForeColor = Color.White, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Label status = new() { Text = "Siap", AutoSize = true, ForeColor = Color.Silver, Padding = new Padding(8, 5, 4, 0) };
    private readonly WebView2 playerView = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer progressTimer = new() { Interval = 1200 };
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
    private readonly string profileDir;
    private readonly string keyPath;
    private readonly List<VideoItem> queue = new();
    private int currentIndex = -1;
    private bool webReady;
    private bool isSeeking;

    private static readonly string[] BlockedHosts = {
        "doubleclick.net", "googlesyndication.com", "googleadservices.com",
        "adservice.google.com", "adnxs.com", "adsrvr.org", "adform.net",
        "taboola.com", "outbrain.com"
    };

    private sealed record VideoItem(string Id, string Title, string Channel);

    public MainForm()
    {
        profileDir = Path.Combine(AppContext.BaseDirectory, "Data", "WebViewProfile");
        keyPath = Path.Combine(dataDir, "youtube-api-key.txt");
        Text = "YouTube Audio Player";
        Width = 1000; Height = 690; MinimumSize = new Size(820, 570);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(23, 25, 28);
        ForeColor = Color.White;
        BuildUi();
        LoadApiKey();
        Shown += async (_, _) => await InitializePlayerAsync();
        progressTimer.Tick += async (_, _) => await UpdateProgressAsync();
        FormClosed += (_, _) => { progressTimer.Stop(); http.Dispose(); };
    }

    private static Button ButtonOf(string text, int width) => new()
    {
        Text = text, Width = width, Height = 32, FlatStyle = FlatStyle.Flat,
        ForeColor = Color.White, BackColor = Color.FromArgb(57, 62, 68),
        FlatAppearance = { BorderSize = 0 }, Margin = new Padding(3)
    };

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(12), BackColor = BackColor };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0, 5, 0, 5) };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8));
        searchPanel.Controls.Add(searchBox, 0, 0);
        searchPanel.Controls.Add(searchButton, 1, 0);
        searchButton.Click += async (_, _) => await SearchAsync();
        searchBox.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) { await SearchAsync(); e.SuppressKeyPress = true; } };

        var center = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        var left = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 0, 12, 0) };
        left.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var resultsLabel = new Label { Text = "HASIL PENCARIAN", ForeColor = Color.Silver, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        left.Controls.Add(resultsLabel, 0, 0);
        left.Controls.Add(results, 0, 1);
        results.DoubleClick += async (_, _) => await PlaySelectedAsync();

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        right.Controls.Add(new Label { Text = "PEMUTAR RESMI YOUTUBE", ForeColor = Color.Silver, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        right.Controls.Add(playerView, 0, 1);
        right.Controls.Add(nowPlaying, 0, 2);
        center.Controls.Add(left, 0, 0);
        center.Controls.Add(right, 1, 0);

        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = new Padding(0, 14, 0, 0) };
        controls.Controls.Add(previousButton); controls.Controls.Add(playButton); controls.Controls.Add(pauseButton); controls.Controls.Add(nextButton);
        controls.Controls.Add(new Label { Text = "Volume", ForeColor = Color.Silver, AutoSize = true, Padding = new Padding(10, 9, 0, 0) });
        controls.Controls.Add(volume);
        previousButton.Click += async (_, _) => await PlayAtAsync(currentIndex - 1);
        playButton.Click += async (_, _) => await PlayerCommandAsync("player.playVideo()");
        pauseButton.Click += async (_, _) => await PlayerCommandAsync("player.pauseVideo()");
        nextButton.Click += async (_, _) => await PlayAtAsync(currentIndex + 1);
        volume.Scroll += async (_, _) => await PlayerCommandAsync($"player.setVolume({volume.Value})");

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118));
        footer.Controls.Add(status, 0, 0);
        footer.Controls.Add(apiKeyBox, 1, 0);
        footer.Controls.Add(saveKeyButton, 2, 0);
        saveKeyButton.Click += (_, _) => SaveApiKey();

        root.Controls.Add(searchPanel, 0, 0);
        root.Controls.Add(center, 0, 1);
        root.Controls.Add(controls, 0, 2);
        root.Controls.Add(footer, 0, 3);
        Controls.Add(root);
    }

    private void LoadApiKey()
    {
        try { if (File.Exists(keyPath)) apiKeyBox.Text = File.ReadAllText(keyPath).Trim(); } catch { }
    }

    private void SaveApiKey()
    {
        try {
            Directory.CreateDirectory(dataDir);
            File.WriteAllText(keyPath, apiKeyBox.Text.Trim());
            status.Text = "API key disimpan di folder Data";
        } catch (Exception ex) { status.Text = "Gagal menyimpan API key: " + ex.Message; }
    }

    private async Task InitializePlayerAsync()
    {
        try
        {
            Directory.CreateDirectory(profileDir);
            var env = await CoreWebView2Environment.CreateAsync(null, profileDir,
                new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
            await playerView.EnsureCoreWebView2Async(env);
            var settings = playerView.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;

            foreach (var host in BlockedHosts)
                playerView.CoreWebView2.AddWebResourceRequestedFilter($"*://*.{host}/*", CoreWebView2WebResourceContext.All);
            playerView.CoreWebView2.WebResourceRequested += (_, e) =>
            {
                try {
                    var host = new Uri(e.Request.Uri).Host.ToLowerInvariant();
                    if (BlockedHosts.Any(d => host == d || host.EndsWith("." + d, StringComparison.Ordinal)))
                        e.Response = playerView.CoreWebView2.Environment.CreateWebResourceResponse(
                            new MemoryStream(), 204, "Blocked", "Content-Type: text/plain");
                } catch { }
            };
            webReady = true;
            playerView.CoreWebView2.NavigateToString(PlayerHtml);
            status.Text = "Player siap. Masukkan API key untuk mencari lagu.";
            progressTimer.Start();
        }
        catch (Exception ex)
        {
            status.Text = "WebView2 Runtime belum tersedia";
            MessageBox.Show("Aplikasi memerlukan Microsoft Edge WebView2 Runtime.\n" + ex.Message,
                "WebView2 belum tersedia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task SearchAsync()
    {
        var query = searchBox.Text.Trim();
        var key = apiKeyBox.Text.Trim();
        if (query.Length == 0) return;
        if (key.Length == 0) {
            status.Text = "Masukkan YouTube Data API key terlebih dahulu.";
            return;
        }

        searchButton.Enabled = false;
        status.Text = "Mencari di YouTube…";
        results.Items.Clear();
        queue.Clear();
        currentIndex = -1;
        try
        {
            var url = "https://www.googleapis.com/youtube/v3/search?part=snippet&type=video&maxResults=20&q=" +
                      Uri.EscapeDataString(query) + "&key=" + Uri.EscapeDataString(key);
            using var response = await http.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) {
                status.Text = "Pencarian gagal: periksa API key / kuota API.";
                return;
            }
            using var doc = JsonDocument.Parse(json);
            foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                var id = item.GetProperty("id").GetProperty("videoId").GetString();
                var snippet = item.GetProperty("snippet");
                var title = snippet.GetProperty("title").GetString() ?? "Tanpa judul";
                var channel = snippet.GetProperty("channelTitle").GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(id)) {
                    queue.Add(new VideoItem(id, title, channel));
                    results.Items.Add(title + "  —  " + channel);
                }
            }
            status.Text = queue.Count == 0 ? "Tidak ada hasil." : $"{queue.Count} hasil ditemukan. Klik dua kali untuk memutar.";
        }
        catch (Exception ex) { status.Text = "Kesalahan pencarian: " + ex.Message; }
        finally { searchButton.Enabled = true; }
    }

    private async Task PlaySelectedAsync()
    {
        if (results.SelectedIndex >= 0)
            await PlayAtAsync(results.SelectedIndex);
    }

    private async Task PlayAtAsync(int index)
    {
        if (index < 0 || index >= queue.Count) return;
        currentIndex = index;
        results.SelectedIndex = index;
        var item = queue[index];
        nowPlaying.Text = item.Title;
        status.Text = "Memuat: " + item.Title;
        if (webReady)
            await PlayerCommandAsync($"pendingVideoId='{item.Id}'; if(player && player.loadVideoById) player.loadVideoById('{item.Id}')");
    }

    private async Task PlayerCommandAsync(string command)
    {
        if (!webReady || playerView.CoreWebView2 is null) return;
        try { await playerView.CoreWebView2.ExecuteScriptAsync(
            "if(typeof player!=='undefined' && player){" + command + ";}"); }
        catch { status.Text = "Kontrol player belum siap."; }
    }

    private async Task UpdateProgressAsync()
    {
        if (!webReady || playerView.CoreWebView2 is null || currentIndex < 0) return;
        try
        {
            var result = await playerView.CoreWebView2.ExecuteScriptAsync(
                "JSON.stringify({t:player.getCurrentTime()||0,d:player.getDuration()||0,s:player.getPlayerState()})");
            if (string.IsNullOrWhiteSpace(result) || result == "null") return;
            using var doc = JsonDocument.Parse(result.Trim('"').Replace("\\\"", "\""));
            var state = doc.RootElement.GetProperty("s").GetInt32();
            if (state == 1) status.Text = "Sedang diputar";
        }
        catch { }
    }

    private const string PlayerHtml = """
    <!doctype html>
    <html><head><meta name="viewport" content="width=device-width,initial-scale=1">
    <style>
      html,body{margin:0;padding:0;background:#17191c;color:#fff;font-family:Segoe UI,sans-serif;overflow:hidden}
      #player{width:100%;aspect-ratio:16/9;background:#000}
      iframe{width:100%;height:100%;border:0}
      .hint{font-size:11px;color:#aeb4bc;padding:5px 2px}
    </style></head><body>
      <div id="player"></div>
      <div class="hint">Pemutar YouTube resmi • area video tetap terlihat</div>
      <script>
        var player;
        var pendingVideoId='';
        var tag=document.createElement('script'); tag.src='https://www.youtube.com/iframe_api';
        document.head.appendChild(tag);
        function onYouTubeIframeAPIReady(){
          player=new YT.Player('player',{
            width:'100%',height:'100%',videoId:'',
            playerVars:{autoplay:0,controls:1,rel:0,playsinline:1,fs:0,iv_load_policy:3},
            events:{onReady:function(){
              player.setVolume(75);
              if(pendingVideoId) player.loadVideoById(pendingVideoId);
            }}
          });
        }
      </script>
    </body></html>
    """;
}

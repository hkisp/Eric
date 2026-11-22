using System.Diagnostics;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace IDCXray;
public sealed class MainWindow : Window
{
    private readonly MacStore store = new();
    private readonly ListBox nodes = new();
    private readonly ListBox manageNodes = new();
    private bool syncingNodes;
    private readonly ListBox subscriptions = new();
    private TrayIcon? tray;
    private readonly TextBox logBox = new() { IsReadOnly = true, AcceptsReturn = true, Height = 220 };
    private readonly TextBox socks = new() { Text = "10808", Width = 90 };
    private readonly TextBox http = new() { Text = "10809", Width = 90 };
    private readonly CheckBox systemProxy = new() { Content = "设置 macOS 系统代理" };
    private readonly TextBlock state = new() { Text = "未连接" };
    private Process? core;
    private bool exiting;
    private readonly string logPath = Path.Combine(MacStore.DirectoryPath, "app.log");

    public MainWindow()
    {
        Title = "Eric agent"; Width = 900; Height = 680; MinWidth = 720; MinHeight = 520;
        store.Load(); socks.Text = store.Socks.ToString(); http.Text = store.Http.ToString(); systemProxy.IsChecked = store.SystemProxy;
        nodes.SelectionChanged += (_, _) => SyncNodes(nodes, manageNodes);
        manageNodes.SelectionChanged += (_, _) => SyncNodes(manageNodes, nodes);
        RefreshNodes(); if (store.Nodes.Count > 0) nodes.SelectedIndex = 0;
        subscriptions.ItemsSource = store.Subscriptions;
        Content = BuildUi();
        BuildTray();
        Closing += (_, e) => { if (!exiting) { e.Cancel = true; Hide(); } else Disconnect(); };
        Log("Eric agent 已启动。");
    }

    private Control BuildUi()
    {
        var tabs = new TabControl();
        tabs.Items.Add(new TabItem { Header = "连接", Content = new StackPanel { Spacing = 12, Margin = new Thickness(18), Children = { state, nodes, Row(Button("连接 / 断开", async () => await Toggle()), Button("测试延迟", async () => await TestLatency())) } } });
        tabs.Items.Add(new TabItem { Header = "节点管理", Content = BuildNodes() });
        tabs.Items.Add(new TabItem { Header = "订阅", Content = BuildSubscriptions() });
        tabs.Items.Add(new TabItem { Header = "设置", Content = BuildSettings() });
        tabs.Items.Add(new TabItem { Header = "日志", Content = logBox });
        return new DockPanel { Children = { tabs } };
    }

    private void RefreshNodes()
    {
        syncingNodes = true;
        try { nodes.ItemsSource = null; nodes.ItemsSource = store.Nodes; manageNodes.ItemsSource = null; manageNodes.ItemsSource = store.Nodes; manageNodes.SelectedIndex = nodes.SelectedIndex; }
        finally { syncingNodes = false; }
    }
    private void SyncNodes(ListBox from, ListBox to)
    {
        if (syncingNodes) return;
        syncingNodes = true;
        try { to.SelectedIndex = from.SelectedIndex; } finally { syncingNodes = false; }
    }

    private Control BuildNodes() => new StackPanel { Spacing = 8, Margin = new Thickness(14), Children = {
        manageNodes,
        Row(Button("从剪贴板导入", ImportClipboard), Button("导入 .conf 文件", ImportConf), Button("删除选中", DeleteNode))
    } };

    private readonly TextBox subscriptionName = new() { Watermark = "订阅名称", Width = 180 };
    private readonly TextBox subscriptionUrl = new() { Watermark = "订阅 URL", Width = 360 };
    private Control BuildSubscriptions() => new StackPanel { Spacing = 8, Margin = new Thickness(14), Children = {
        subscriptions,
        Row(subscriptionName, subscriptionUrl, Button("添加订阅", AddSubscription)),
        Button("更新选中", async () => await UpdateSubscriptions(true)), Button("更新全部", async () => await UpdateSubscriptions(false)), Button("删除选中", DeleteSubscription)
    } };

    private Control BuildSettings() => new StackPanel { Spacing = 12, Margin = new Thickness(20), Children = {
        Row(new TextBlock { Text = "本地 SOCKS 端口" }, socks), Row(new TextBlock { Text = "本地 HTTP 端口" }, http),
        systemProxy, Button("保存设置", SaveSettings), Button("显示日志", () => { logBox.Focus(); })
    } };

    private static StackPanel Row(params Control[] controls) => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 }.Also(p => { foreach (var c in controls) p.Children.Add(c); });
    private static Button Button(string title, Action click) { var b = new Button { Content = title, Padding = new Thickness(12, 7) }; b.Click += (_, _) => click(); return b; }
    private static Button Button(string title, Func<Task> click) { var b = new Button { Content = title, Padding = new Thickness(12, 7) }; b.Click += async (_, _) => await click(); return b; }

    private async void ImportClipboard()
    {
        try { var clipboard = TopLevel.GetTopLevel(this)?.Clipboard; var text = clipboard is null ? null : await clipboard.TryGetTextAsync(); if (!string.IsNullOrWhiteSpace(text)) ImportText(text); }
        catch (Exception ex) { Log("读取剪贴板失败：" + ex.Message); }
    }
    private async void ImportConf()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("WireGuard 配置") { Patterns = new[] { "*.conf" } } } });
        if (files.Count == 0) return;
        try { ImportText(await File.ReadAllTextAsync(files[0].Path.LocalPath)); } catch (Exception ex) { Log("读取配置文件失败：" + ex.Message); }
    }
    private void ImportText(string text)
    {
        var result = LinkParser.ParseMany(text);
        foreach (var n in result.Nodes) { if (n.Name.Contains("WireGuard", StringComparison.OrdinalIgnoreCase)) n.Name = "WireGuard 节点"; store.Nodes.Add(n); }
        RefreshNodes(); if (store.Nodes.Count > 0) nodes.SelectedIndex = store.Nodes.Count - 1;
        store.Save(); Log($"已导入 {result.Nodes.Count} 个节点；错误 {result.Errors.Count} 条。");
        foreach (var error in result.Errors) Log("导入错误：" + error);
    }
    private void DeleteNode() { if (nodes.SelectedItem is Node n) { store.Nodes.Remove(n); RefreshNodes(); store.Save(); } }

    private void AddSubscription()
    {
        if (!Uri.TryCreate(subscriptionUrl.Text, UriKind.Absolute, out var url) || (url.Scheme != "https" && url.Scheme != "http")) { Log("订阅 URL 无效。"); return; }
        store.Subscriptions.Add(new Subscription { Name = string.IsNullOrWhiteSpace(subscriptionName.Text) ? url.Host : subscriptionName.Text, Url = url.ToString() }); store.Save(); Log("订阅已添加。");
    }
    private async Task UpdateSubscriptions(bool selectedOnly)
    {
        var list = selectedOnly ? (subscriptions.SelectedItem is Subscription chosen ? new[] { chosen } : Array.Empty<Subscription>()) : store.Subscriptions.ToArray();
        using var client = new HttpClient();
        foreach (var sub in list)
        {
            try { var body = await client.GetStringAsync(sub.Url); var result = SubscriptionDecoder.DecodeDetailed(body); var sync = SubscriptionSynchronizer.Sync(store.Nodes, sub.Id, result.Nodes); sub.LastUpdated = DateTime.Now; Log($"订阅已更新：新增 {sync.Added}，更新 {sync.Updated}，移除 {sync.Deleted}，跳过 {sync.Skipped}。"); }
            catch (Exception ex) { Log("订阅更新失败：" + ex.Message); }
        }
        RefreshNodes(); subscriptions.ItemsSource = null; subscriptions.ItemsSource = store.Subscriptions; store.Save();
    }
    private void DeleteSubscription() { if (subscriptions.SelectedItem is Subscription selected) { store.Subscriptions.Remove(selected); store.Nodes.RemoveAll(n => n.SubscriptionId == selected.Id); RefreshNodes(); subscriptions.ItemsSource = null; subscriptions.ItemsSource = store.Subscriptions; store.Save(); } }

    private void SaveSettings()
    {
        if (!int.TryParse(socks.Text, out var s) || !int.TryParse(http.Text, out var h) || s is < 1 or > 65535 || h is < 1 or > 65535 || s == h) { Log("端口设置无效。"); return; }
        store.Socks = s; store.Http = h; store.SystemProxy = systemProxy.IsChecked == true; store.Save();
        if (core is { HasExited: false }) _ = SetSystemProxy(store.SystemProxy);
        Log("设置已保存。");
    }
    private async Task Toggle() { if (core is { HasExited: false }) Disconnect(); else await Connect(); }
    private async Task Connect()
    {
        if (nodes.SelectedItem is not Node node) { Log("请先选择节点。"); return; }
        SaveSettings();
        var path = Path.Combine(AppContext.BaseDirectory, "core", "xray");
        if (!File.Exists(path)) { Log("找不到 Xray 核心：请将 xray 放到应用程序 Contents/MacOS/core/xray。"); return; }
        try
        {
            var mode = File.GetUnixFileMode(path); if ((mode & UnixFileMode.UserExecute) == 0) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            var config = Path.Combine(MacStore.DirectoryPath, "config.json"); File.WriteAllText(config, XrayConfig.Build(node, store.Socks, store.Http)); File.SetUnixFileMode(config, MacStore.FilePermissions);
            core = Process.Start(new ProcessStartInfo(path) { ArgumentList = { "run", "-config", config }, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true, CreateNoWindow = true });
            if (core is null) throw new InvalidOperationException("无法启动 Xray 核心。");
            core.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Log("Xray 核心输出了一条诊断信息。"); }; core.BeginErrorReadLine();
            await Task.Delay(1200);
            if (core.HasExited) { Log("Xray 启动失败，请查看日志。"); core = null; return; }
            state.Text = "已连接"; Log("连接成功。");
            if (store.SystemProxy) await SetSystemProxy(true);
        }
        catch (Exception ex) { Log("连接失败：" + ex.Message); }
    }
    private void Disconnect()
    {
        _ = SetSystemProxy(false);
        try { if (core is { HasExited: false }) { core.Kill(true); core.WaitForExit(3000); } } catch (Exception ex) { Log("停止 Xray 失败：" + ex.Message); }
        core?.Dispose(); core = null; state.Text = "未连接"; Log("已断开连接。");
    }
    private async Task TestLatency()
    {
        if (nodes.SelectedItem is not Node n) return;
        try { var sw = Stopwatch.StartNew(); using var tcp = new TcpClient(); await tcp.ConnectAsync(n.Host, n.Port).WaitAsync(TimeSpan.FromSeconds(5)); Log($"TCP 延迟 {sw.ElapsedMilliseconds} ms。"); }
        catch { Log("延迟测试失败。"); }
    }

    private async Task SetSystemProxy(bool enabled)
    {
        try
        {
            var services = await RunNetworkSetup("-listallnetworkservices");
            foreach (var service in services.Split('\n').Skip(1).Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith('*') && !x.StartsWith("An asterisk", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var (kind, port) in new[] { ("-setwebproxy", store.Http), ("-setsecurewebproxy", store.Http), ("-setsocksfirewallproxy", store.Socks) })
                {
                    if (enabled) await RunNetworkSetup(kind, service, "127.0.0.1", port.ToString());
                    await RunNetworkSetup(enabled ? kind + "state" : kind + "state", service, enabled ? "on" : "off");
                }
            }
            Log(enabled ? "已设置 macOS 系统代理。" : "已关闭 macOS 系统代理。");
        }
        catch (Exception ex) { Log("设置系统代理失败：" + ex.Message); }
    }
    private void BuildTray()
    {
        var menu = new NativeMenu();
        var show = new NativeMenuItem("显示 / 隐藏窗口") { Command = new RelayCommand(() => { if (IsVisible) Hide(); else Show(); }) };
        var connect = new NativeMenuItem("连接 / 断开") { Command = new RelayCommand(() => _ = Toggle()) };
        var quit = new NativeMenuItem("退出") { Command = new RelayCommand(ExitApp) };
        menu.Items.Add(show); menu.Items.Add(connect); menu.Items.Add(quit);
        using var iconStream = typeof(MainWindow).Assembly.GetManifestResourceStream("IDCXray.Assets.eric-agent.ico");
        tray = new TrayIcon { ToolTipText = "Eric agent", Icon = iconStream is null ? null : new WindowIcon(iconStream), Menu = menu, IsVisible = true };
        if (Application.Current is { } app) TrayIcon.SetIcons(app, new TrayIcons { tray });
    }
    private static async Task<string> RunNetworkSetup(params string[] args)
    {
        using var p = new Process { StartInfo = new ProcessStartInfo("/usr/sbin/networksetup") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) p.StartInfo.ArgumentList.Add(arg);
        p.Start(); var stdout = await p.StandardOutput.ReadToEndAsync(); var stderr = await p.StandardError.ReadToEndAsync(); await p.WaitForExitAsync();
        if (p.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "networksetup 命令失败。" : stderr.Trim()); return stdout;
    }
    private void Log(string message)
    {
        Directory.CreateDirectory(MacStore.DirectoryPath);
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        try { File.AppendAllText(logPath, line + Environment.NewLine); File.SetUnixFileMode(logPath, UnixFileMode.UserRead | UnixFileMode.UserWrite); } catch { }
        Dispatcher.UIThread.Post(() => logBox.Text += line + Environment.NewLine);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Hide();
        base.OnKeyDown(e);
    }
    public void ExitApp()
    {
        exiting = true; if (tray is not null) tray.IsVisible = false; Disconnect();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
    }
}

internal static class ControlExtensions
{
    public static T Also<T>(this T value, Action<T> action) { action(value); return value; }
}

internal sealed class RelayCommand(Action execute) : System.Windows.Input.ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute();
}

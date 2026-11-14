using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaColor=System.Windows.Media.Color;
using MediaBrushes=System.Windows.Media.Brushes;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
using Application=System.Windows.Application;
using Clipboard=System.Windows.Clipboard;
using MessageBox=System.Windows.MessageBox;
using WpfButton=System.Windows.Controls.Button;

namespace IDCXray;
public partial class MainWindow : Window
{
    private readonly Store store=new(); private Process? xray; private Forms.NotifyIcon? tray; private DateTime started; private string connectionState="Disconnected";
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(1)}; private bool shuttingDown; private bool initializing=true;
    [DllImport("wininet.dll")] private static extern bool InternetSetOption(IntPtr h,int option,IntPtr buffer,int length);
    private static string PidPath=>Path.Combine(Store.Dir,"xray.pid"); private static string LogPath=>Path.Combine(Store.Dir,"app.log");
    public MainWindow()
    {
        InitializeComponent(); store.Load(); RecoverPreviousProxy(); KillTrackedOrphan();
        SocksPort.Text=store.Socks.ToString(); HttpPort.Text=store.Http.ToString(); ProxyToggle.IsChecked=store.SystemProxy; AutoConnectToggle.IsChecked=store.AutoConnect; StartupToggle.IsChecked=store.StartWithWindows; MinimizedToggle.IsChecked=store.StartMinimized; AiToggle.IsChecked=store.ChatAi;
        RouteMode.SelectedIndex=store.Mode switch{"Global"=>1,"Direct"=>2,_=>0}; LanguageBox.SelectedIndex=store.Language=="English"?1:0;
        NodeList.ItemsSource=store.Nodes; RuleList.ItemsSource=store.Rules; if(store.Nodes.Count>0)NodeList.SelectedIndex=0;
        BuildTray(); timer.Tick+=(_,_)=>TickStatus(); timer.Start(); Loaded+=OnLoaded;
        Closing+=(_,e)=>{if(!shuttingDown){e.Cancel=true;Hide();}else{Disconnect();if(tray!=null)tray.Visible=false;}};
        StateChanged+=(_,_)=>{if(WindowState==WindowState.Minimized)Hide();}; initializing=false; ApplyLanguage();
    }
    private async void OnLoaded(object sender,RoutedEventArgs e){if(store.StartMinimized){WindowState=WindowState.Minimized;Hide();}if(store.AutoConnect&&store.Nodes.Count>0){NodeList.SelectedIndex=Math.Clamp(NodeList.SelectedIndex,0,store.Nodes.Count-1);await Task.Delay(500);await Connect();}}
    private void BuildTray()
    {
        tray=new Forms.NotifyIcon{Text="Eric agent",Icon=System.Drawing.SystemIcons.Application,Visible=true,ContextMenuStrip=new Forms.ContextMenuStrip()};
        tray.ContextMenuStrip.Items.Add("显示窗口",null,(_,_)=>ShowWindow()); tray.ContextMenuStrip.Items.Add("连接 / 断开",null,async(_,_)=>await Toggle());
        var switchMenu=new Forms.ToolStripMenuItem("切换节点"); tray.ContextMenuStrip.Items.Add(switchMenu); tray.ContextMenuStrip.Opening+=(_,_)=>{switchMenu.DropDownItems.Clear();foreach(var n in store.Nodes){var item=new Forms.ToolStripMenuItem(n.Name){Checked=NodeList.SelectedItem==n};item.Click+=async(_,_)=>{NodeList.SelectedItem=n;if(xray is{HasExited:false}){Disconnect();await Connect();}};switchMenu.DropDownItems.Add(item);}};
        tray.ContextMenuStrip.Items.Add("退出",null,(_,_)=>ExitApp());tray.DoubleClick+=(_,_)=>ShowWindow();
    }
    private void ShowWindow(){Show();WindowState=WindowState.Normal;Activate();}
    private void ExitApp(){shuttingDown=true;Disconnect();if(tray!=null)tray.Visible=false;Application.Current.Shutdown();}
    private void TitleBarMouseDown(object sender,MouseButtonEventArgs e){if(e.LeftButton!=MouseButtonState.Pressed)return;DependencyObject? current=e.OriginalSource as DependencyObject;while(current is not null&&current!=sender){if(current is System.Windows.Controls.Primitives.ButtonBase)return;current=System.Windows.Media.VisualTreeHelper.GetParent(current);}DragMove();}
    private void MinimizeWindow(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void CloseWindow(object sender,RoutedEventArgs e)=>ExitApp();
    private void Nav(object sender,RoutedEventArgs e){var key=(string)((WpfButton)sender).Tag;HomePage.Visibility=key=="Home"?Visibility.Visible:Visibility.Collapsed;NodesPage.Visibility=key=="Nodes"?Visibility.Visible:Visibility.Collapsed;RoutingPage.Visibility=key=="Routing"?Visibility.Visible:Visibility.Collapsed;SettingsPage.Visibility=key=="Settings"?Visibility.Visible:Visibility.Collapsed;PageTitle.Text=key switch{"Home"=>IsEnglish?"Connection":"连接","Nodes"=>IsEnglish?"Configurations":"配置管理","Routing"=>IsEnglish?"Routing":"路由","Settings"=>IsEnglish?"Settings":"设置",_=>key};var selected=new SolidColorBrush(MediaColor.FromRgb(224,238,255));foreach(var b in new[]{NavHome,NavNodes,NavRouting,NavSettings})b.Background=ReferenceEquals(b,sender)?selected:MediaBrushes.Transparent;NavHomeIndicator.Visibility=key=="Home"?Visibility.Visible:Visibility.Collapsed;NavNodesIndicator.Visibility=key=="Nodes"?Visibility.Visible:Visibility.Collapsed;NavRoutingIndicator.Visibility=key=="Routing"?Visibility.Visible:Visibility.Collapsed;NavSettingsIndicator.Visibility=key=="Settings"?Visibility.Visible:Visibility.Collapsed;}
    private bool IsEnglish=>store.Language=="English";
    private void ApplyLanguage(){var zh=new Dictionary<string,string>{{"首页","Home"},{"配置管理","Configurations"},{"路由","Routing"},{"设置","Settings"},{"连接","Connection"},{"当前节点","Current node"},{"尚未选择节点","No node selected"},{"从剪贴板导入","Import clipboard"},{"订阅 URL","Subscription URL"},{"编辑链接","Edit link"},{"重命名","Rename"},{"删除","Delete"},{"TCP 延迟","TCP latency"},{"路由模式","Routing mode"},{"OpenAI / ChatGPT / Claude 走代理","Route OpenAI / ChatGPT / Claude via proxy"},{"本地端口","Local ports"},{"设置 Windows 系统代理","Set Windows system proxy"},{"启动时自动连接上次节点","Auto-connect last node"},{"随 Windows 启动","Start with Windows"},{"启动后最小化到托盘","Start minimized to tray"},{"界面语言","Language"},{"打开配置文件夹","Open config folder"},{"查看日志","View logs"},{"保存设置","Save settings"},{"添加规则","Add rule"},{"删除所选","Delete selected"},{"自定义规则（域名或 IP）","Custom rules (domain or IP)"},{"规则模式将局域网和中国大陆地址直连，其余流量走代理。","Rule mode bypasses LAN and mainland China; other traffic uses proxy."},{"本地运行 · 无遥测","Local · No telemetry"}};var map=IsEnglish?zh:zh.ToDictionary(p=>p.Value,p=>p.Key);TranslateTree(this,map);SetState(connectionState);}
    private static void TranslateTree(DependencyObject node,IReadOnlyDictionary<string,string> map){if(node is System.Windows.Controls.Primitives.ButtonBase b&&b.Content is string c&&map.TryGetValue(c,out var bc))b.Content=bc;if(node is TextBlock t&&map.TryGetValue(t.Text,out var tx))t.Text=tx;for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(node);i++)TranslateTree(System.Windows.Media.VisualTreeHelper.GetChild(node,i),map);}
    private void SetState(string state){connectionState=state;StatusText.Text=state switch{"Disconnected"=>(IsEnglish?"Disconnected":"未连接"),"Connecting"=>(IsEnglish?"Connecting":"连接中"),"Connected"=>(IsEnglish?"Connected":"已连接"),_=>IsEnglish?"Error":"错误"};if(state=="Connected"){ConnectButton.Content=IsEnglish?"Disconnect":"断开";ConnectButton.Background=new LinearGradientBrush(MediaColor.FromRgb(0,122,255),MediaColor.FromRgb(52,199,89),45);ConnectButton.Foreground=MediaBrushes.White;ConnectButton.BorderBrush=MediaBrushes.Transparent;OuterGlow.Fill=new SolidColorBrush(MediaColor.FromArgb(74,0,122,255));OuterGlow.Opacity=.75;ConnectedGlow.Opacity=.55;}else{ConnectButton.Content=state=="Connecting"?(IsEnglish?"Connecting…":"连接中…"):(IsEnglish?"Connect":"连接");ConnectButton.Background=new LinearGradientBrush(MediaColor.FromRgb(255,255,255),MediaColor.FromRgb(237,241,246),45);ConnectButton.Foreground=new SolidColorBrush(MediaColor.FromRgb(71,84,103));ConnectButton.BorderBrush=new SolidColorBrush(MediaColor.FromRgb(221,227,236));OuterGlow.Fill=new SolidColorBrush(MediaColor.FromArgb(28,71,122,190));OuterGlow.Opacity=.38;ConnectedGlow.Opacity=0;}}
    private void SelectNode(object sender,SelectionChangedEventArgs e){if(NodeList.SelectedItem is Node n){CurrentNode.Text=n.Name;CurrentProtocol.Text=n.Summary;CurrentLatency.Text="延迟 —";}}
    private async void ToggleConnect(object sender,RoutedEventArgs e)=>await Toggle(); private async Task Toggle(){if(xray is{HasExited:false})Disconnect();else await Connect();}
    private async Task Connect()
    {
        if(NodeList.SelectedItem is not Node node){MessageBox.Show(IsEnglish?"Import and select a node first.":"请先导入并选择节点。");return;}
        try{
            if(!int.TryParse(SocksPort.Text,out var socks)||socks is<1 or>65535||!int.TryParse(HttpPort.Text,out var http)||http is<1 or>65535||socks==http)throw new InvalidOperationException("SOCKS / HTTP 端口无效。");
            store.Socks=socks;store.Http=http;if(!PortFree(socks)||!PortFree(http))throw new InvalidOperationException("本地端口已被占用，请更换端口后重试。");
            Directory.CreateDirectory(Store.Dir);var cfg=Path.Combine(Store.Dir,"config.json");File.WriteAllText(cfg,XrayConfig.Build(node,socks,http,store.Mode,store.ChatAi,store.Rules));
            var exe=Path.Combine(AppContext.BaseDirectory,"core","xray.exe");if(!File.Exists(exe))throw new FileNotFoundException("未找到 core\\xray.exe");
            xray=Process.Start(new ProcessStartInfo{FileName=exe,ArgumentList={"run","-c",cfg},UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(exe)!})??throw new InvalidOperationException("无法启动 Xray。");
            File.WriteAllText(PidPath,xray.Id.ToString());SetState("Connecting");SafeLog("Xray child process started.");
            for(var i=0;i<48;i++){await Task.Delay(250);if(xray.HasExited)throw new InvalidOperationException("Xray exited; check the selected node.");if(await Probe(socks)){SetState("Connected");started=DateTime.Now;if(ProxyToggle.IsChecked==true)SetSystemProxy(true,http);return;}}
            throw new TimeoutException("Xray 已启动，但经本地代理的测试请求未成功。");
        }catch(Exception ex){Disconnect();SetState("Error");SafeLog("Connection attempt failed: "+ex.GetType().Name);MessageBox.Show(ex.Message,"Eric agent",MessageBoxButton.OK,MessageBoxImage.Error);}
    }
    private void TickStatus(){if(xray is{HasExited:false}){var t=DateTime.Now-started;TimerText.Text=$"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";}else if(connectionState is "Connected" or "Connecting")Disconnect();}
    private static bool PortFree(int p){try{using var l=new TcpListener(IPAddress.Loopback,p);l.Start();return true;}catch{return false;}}
    private static async Task<bool> Probe(int p){try{using var h=new HttpClient(new HttpClientHandler{Proxy=new WebProxy($"http://127.0.0.1:{p}"),UseProxy=true}){Timeout=TimeSpan.FromSeconds(3)};using var r=await h.GetAsync("https://www.msftconnecttest.com/connecttest.txt");return r.IsSuccessStatusCode;}catch{return false;}}
    private void Disconnect(){var proc=xray;try{if(proc is{HasExited:false}){proc.Kill(true);proc.WaitForExit(2500);}}catch{}proc?.Dispose();xray=null;try{if(File.Exists(PidPath))File.Delete(PidPath);}catch{}if(store.ProxyWasCaptured)SetSystemProxy(false,store.Http);SetState("Disconnected");TimerText.Text="";SpeedText.Text="";}
    private void SetSystemProxy(bool enabled,int port)
    {
        try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings",true)!;
            if(enabled){if(!store.ProxyWasCaptured){store.PreviousProxyEnabled=Convert.ToInt32(key.GetValue("ProxyEnable",0))!=0;store.PreviousProxyServer=key.GetValue("ProxyServer","")?.ToString()??"";store.ProxyWasCaptured=true;store.Save();}key.SetValue("ProxyEnable",1);key.SetValue("ProxyServer",$"http=127.0.0.1:{port};https=127.0.0.1:{port}");}
            else if(store.ProxyWasCaptured){key.SetValue("ProxyEnable",store.PreviousProxyEnabled?1:0);if(store.PreviousProxyServer.Length>0)key.SetValue("ProxyServer",store.PreviousProxyServer);else key.DeleteValue("ProxyServer",false);store.ProxyWasCaptured=false;store.Save();}
            InternetSetOption(IntPtr.Zero,39,IntPtr.Zero,0);InternetSetOption(IntPtr.Zero,37,IntPtr.Zero,0);
        }catch{SafeLog("System proxy update failed.");}
    }
    private void RecoverPreviousProxy(){if(store.ProxyWasCaptured)SetSystemProxy(false,store.Http);}
    private void SettingsChanged(object sender,RoutedEventArgs e){if(initializing)return;store.SystemProxy=ProxyToggle.IsChecked==true;store.AutoConnect=AutoConnectToggle.IsChecked==true;store.StartWithWindows=StartupToggle.IsChecked==true;store.StartMinimized=MinimizedToggle.IsChecked==true;ConfigureStartup(store.StartWithWindows);store.Save();if(StatusText.Text=="Connected")SetSystemProxy(store.SystemProxy,store.Http);}
    private void PortsChanged(object sender,RoutedEventArgs e){if(int.TryParse(SocksPort.Text,out var s)&&s is>0 and<65536)store.Socks=s;if(int.TryParse(HttpPort.Text,out var h)&&h is>0 and<65536)store.Http=h;store.Save();}
    private void ChangeMode(object sender,SelectionChangedEventArgs e){if(RouteMode?.SelectedItem is ComboBoxItem i){store.Mode=i.Content?.ToString()??"Rule-based";if(!initializing)store.Save();}}
    private void AiChanged(object sender,RoutedEventArgs e){if(initializing)return;store.ChatAi=AiToggle.IsChecked==true;store.Save();}
    private void LanguageChanged(object sender,SelectionChangedEventArgs e){if(LanguageBox?.SelectedItem is ComboBoxItem i){store.Language=i.Content?.ToString()??"中文";ApplyLanguage();if(!initializing)store.Save();}}
    private void SaveSettings(object sender,RoutedEventArgs e){SettingsChanged(sender,e);PortsChanged(sender,e);MessageBox.Show(IsEnglish?"Settings saved.":"设置已保存。");}
    private void ConfigureStartup(bool enable){try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)!;if(enable){key.SetValue("Eric agent",$"\"{Environment.ProcessPath}\"");key.DeleteValue("IDCXray",false);}else{key.DeleteValue("Eric agent",false);key.DeleteValue("IDCXray",false);}}catch{SafeLog("Startup registration failed.");}}
    private void ImportNode(object sender,RoutedEventArgs e){var text=Clipboard.ContainsText()?Clipboard.GetText():"";var raw=Prompt("导入节点","粘贴分享链接",text);if(string.IsNullOrWhiteSpace(raw))return;try{var n=LinkParser.Parse(raw);store.Nodes.Add(n);store.Save();NodeList.Items.Refresh();NodeList.SelectedItem=n;RefreshTray();}catch(Exception ex){MessageBox.Show("链接解析失败："+ex.Message);}}
    private void EditNode(object sender,RoutedEventArgs e){if(NodeList.SelectedItem is not Node old){MessageBox.Show("请先选择节点。");return;}var raw=Prompt("编辑节点","粘贴新的分享链接（密钥不会显示在列表中）","");if(string.IsNullOrWhiteSpace(raw))return;try{var replacement=LinkParser.Parse(raw);replacement.Name=old.Name;var index=store.Nodes.IndexOf(old);store.Nodes[index]=replacement;store.Save();NodeList.Items.Refresh();NodeList.SelectedItem=replacement;RefreshTray();}catch(Exception ex){MessageBox.Show("链接解析失败："+ex.Message);}}
    private void RenameNode(object sender,RoutedEventArgs e){if(NodeList.SelectedItem is not Node n)return;var name=Prompt("重命名","输入节点显示名称",n.Name);if(string.IsNullOrWhiteSpace(name))return;n.Name=name.Trim();store.Save();NodeList.Items.Refresh();RefreshTray();}
    private async void UpdateSubscription(object sender,RoutedEventArgs e){var url=Prompt("更新订阅","输入 HTTP(S) 订阅 URL","");if(!Uri.TryCreate(url,UriKind.Absolute,out var u)||u.Scheme is not("http" or "https")){MessageBox.Show("请输入有效的 HTTP 或 HTTPS URL。");return;}try{using var client=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};var body=await client.GetStringAsync(u);var nodes=SubscriptionDecoder.Decode(body);var count=0;foreach(var node in nodes)if(!store.Nodes.Any(x=>x.Protocol==node.Protocol&&x.Host==node.Host&&x.Port==node.Port&&x.Id==node.Id)){store.Nodes.Add(node);count++;}store.Save();NodeList.Items.Refresh();RefreshTray();MessageBox.Show($"已导入 {count} 个节点。");}catch(Exception ex){MessageBox.Show("订阅更新失败："+ex.Message);}}
    private async void LatencyTest(object sender,RoutedEventArgs e){if(NodeList.SelectedItem is not Node n)return;var sw=Stopwatch.StartNew();try{using var c=new TcpClient();await c.ConnectAsync(n.Host,n.Port).WaitAsync(TimeSpan.FromSeconds(5));sw.Stop();CurrentLatency.Text=$"延迟 {sw.ElapsedMilliseconds} ms";MessageBox.Show($"{n.Name}: {sw.ElapsedMilliseconds} ms (TCP)","TCP 延迟");}catch{CurrentLatency.Text="延迟 超时";MessageBox.Show($"{n.Name}: 超时或无法连接","TCP 延迟");}}
    private void DeleteNode(object sender,RoutedEventArgs e){if(NodeList.SelectedItem is not Node n)return;if(MessageBox.Show($"删除节点“{n.Name}”？","确认",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;store.Nodes.Remove(n);store.Save();NodeList.Items.Refresh();CurrentNode.Text="尚未选择节点";RefreshTray();}
    private void AddRule(object sender,RoutedEventArgs e){var val=RuleValue.Text.Trim();if(val.Length==0)return;var type=((ComboBoxItem?)RuleType.SelectedItem)?.Content?.ToString()??"domain";var action=((ComboBoxItem?)RuleAction.SelectedItem)?.Content?.ToString()??"proxy";if(type=="domain"&&!val.Contains('.')){MessageBox.Show("请输入有效域名。");return;}if(type=="ip"&&!System.Net.IPAddress.TryParse(val,out _)){MessageBox.Show("请输入有效 IP 地址或 CIDR。");return;}store.Rules.Add(new RouteRule(type,val,action));store.Save();RuleList.Items.Refresh();RuleValue.Clear();}
    private void DeleteRule(object sender,RoutedEventArgs e){if(RuleList.SelectedItem is RouteRule r){store.Rules.Remove(r);store.Save();RuleList.Items.Refresh();}}
    private void RefreshTray(){if(tray?.ContextMenuStrip is null)return;}
    private static string Prompt(string title,string label,string initial){var w=new Window{Title=title,Width=560,Height=190,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=Application.Current.MainWindow,ResizeMode=ResizeMode.NoResize,Background=System.Windows.Media.Brushes.DarkSlateGray};var grid=new Grid{Margin=new Thickness(14)};grid.RowDefinitions.Add(new RowDefinition());grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var box=new System.Windows.Controls.TextBox{Text=initial,Margin=new Thickness(2,8,2,10)};grid.Children.Add(new StackPanel{Children={new TextBlock{Text=label,Foreground=System.Windows.Media.Brushes.White},box}});var buttons=new StackPanel{Orientation=System.Windows.Controls.Orientation.Horizontal,HorizontalAlignment=System.Windows.HorizontalAlignment.Right};var ok=new WpfButton{Content="确定",IsDefault=true,MinWidth=80};var cancel=new WpfButton{Content="取消",IsCancel=true,MinWidth=80};buttons.Children.Add(ok);buttons.Children.Add(cancel);Grid.SetRow(buttons,1);grid.Children.Add(buttons);w.Content=grid;ok.Click+=(_,_)=>w.DialogResult=true;return w.ShowDialog()==true?box.Text:"";}
    private void OpenData(object sender,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo("explorer.exe",Store.Dir){UseShellExecute=true});
    private void OpenLog(object sender,RoutedEventArgs e){Directory.CreateDirectory(Store.Dir);if(!File.Exists(LogPath))File.WriteAllText(LogPath,"Eric agent local log.\n");Process.Start(new ProcessStartInfo("notepad.exe",LogPath){UseShellExecute=true});}
    private static void SafeLog(string message){try{Directory.CreateDirectory(Store.Dir);File.AppendAllText(LogPath,$"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\n");}catch{}}
    private static void KillTrackedOrphan(){try{if(!File.Exists(PidPath))return;if(int.TryParse(File.ReadAllText(PidPath),out var pid)){var p=Process.GetProcessById(pid);if(p.ProcessName.Equals("xray",StringComparison.OrdinalIgnoreCase)){var expected=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"core","xray.exe"));var actual=Path.GetFullPath(p.MainModule?.FileName??"");if(actual.Equals(expected,StringComparison.OrdinalIgnoreCase))p.Kill(true);}}File.Delete(PidPath);}catch{try{File.Delete(PidPath);}catch{}}}
}


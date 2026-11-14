# Eric agent Windows 客户端

基于 C# / .NET 8 WPF。Xray-core 作为子进程运行；节点和设置写入 `%LOCALAPPDATA%\IDCXray\nodes.dpapi`，并使用当前 Windows 用户 DPAPI 加密。应用没有遥测或远程控制。

## 构建与测试

```powershell
dotnet test .\tests\IDCXray.Tests.csproj
dotnet publish .\src\IDCXray.csproj -c Release -r win-x64 --self-contained -o .\publish
```

发布目录包含 `Eric agent.exe` 以及 `core\xray.exe`、`core\geoip.dat`、`core\geosite.dat`。如果在没有联网的环境执行自包含发布，SDK 需要先缓存对应的 .NET 8 Windows runtime packs。`publish.ps1` 可继续生成`Eric-agent-release.zip` 和桌面快捷方式。

## 使用

1. 在“配置管理”中从剪贴板导入 VLESS、VMess、Trojan 或 Shadowsocks 分享链接，也可以填写订阅 URL。
2. 节点支持替换链接、重命名、删除和 TCP 延迟测试。节点列表不显示密码或 UUID。
3. 首页连接必须同时满足 Xray 子进程存活和经本地 SOCKS 的 HTTPS 测试请求成功。默认 SOCKS/HTTP 端口为 10808/10809。
4. 路由包含 Global、Rule-based、Direct。规则模式默认将私网和中国大陆流量直连，其余走代理；可设置 OpenAI/Claude 域名规则以及自定义域名/IP 的 proxy、direct、block 规则。
5. 系统代理写入当前用户 Internet Settings，启用前会保存原值，并在断开、正常退出或下次启动恢复。托盘菜单可连接、断开、切换节点、显示窗口或退出。
6. 设置支持开机启动、启动最小化、自动连接、端口和中英文界面。

## 发布环境限制

当前容器已安装 .NET SDK 8.0.428，但本次自包含发布需要的 runtime packs 尚未缓存，访问 `https://api.nuget.org/v3/index.json` 返回 NU1301。因此 framework-dependent 发布可以构建并用于本机启动（本机已有 .NET 8 Desktop Runtime），但不能将其称为 self-contained。不要把真实节点链接、密钥或订阅写进源码或测试。

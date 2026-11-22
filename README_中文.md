# Eric agent

基于 C# / .NET 8。Windows 版为 WPF，macOS 版为 Avalonia。当前版本 **1.6.0**。Xray-core 作为子进程运行；Windows 上将节点和设置写入 `%LOCALAPPDATA%\IDCXray\nodes.dpapi`（当前用户 DPAPI 加密）。应用没有遥测或远程控制。

预编译安装包（含完整版精简包和 1.6.0 限定版）发布在：<https://blog.idcai.net/down/>

源码里的首次启动内置节点只是占位示例（文档用地址和假密钥），不能连上真实服务器。请自行导入或订阅节点。不要把分享链接、UUID、密码或密钥提交进仓库。

## 构建与测试

默认构建是**完整客户端**（可选 Rule-based / Global / Direct，以及自定义规则）：

```powershell
dotnet test .\tests\IDCXray.Tests.csproj
dotnet publish .\src\IDCXray.csproj -c Release -r win-x64 --self-contained -o .\publish
```

发布目录包含 `Eric agent.exe` 以及 `core\xray.exe`、`core\geoip.dat`、`core\geosite.dat`。`publish.ps1` 可继续生成 `Eric-agent-release.zip`。

### 限定版（只代理指定 AI / Google / Cloudflare 域名）

限定版只把 Cloudflare、OpenAI/Codex/ChatGPT、Claude/Anthropic、Gemini/Google 相关域名走代理，其余直连。节点仍可导入、编辑、订阅和连接，但用户不能修改系统分流规则。编译方法：

```powershell
dotnet publish .\src\IDCXray.csproj -c Release -r win-x64 --self-contained false -p:EricAiOnly=true -o .\publish-ai-only
```

`-p:EricAiOnly=true` 会定义 `ERIC_AI_ONLY`，把产品名设为「Eric agent 1.6.0 限定版」，InformationalVersion 为 `1.6.0-ai-only`。FileVersion 仍为 `1.6.0.0`。默认构建不受影响。

macOS：

```powershell
dotnet test .\mac\tests\Mac.Tests.csproj
.\mac\publish-mac.cmd
```

## 使用

1. 在「配置管理」中可一次从剪贴板导入多条 VLESS、VMess、Trojan、Shadowsocks 或 `wireguard://` 分享链接，也可导入 WireGuard `.conf`，以及保存并更新订阅 URL。
2. 节点支持替换链接、重命名、删除和 TCP 延迟测试。界面不显示密码或 UUID。
3. 默认 SOCKS/HTTP 端口为 10808/10809。可选择设置系统代理（浏览器走本地端口）。
4. 完整版路由包含 Global、Rule-based、Direct。规则模式默认将私网和中国大陆流量直连，其余走代理；可设置 AI 域名规则以及自定义域名/IP 的 proxy、direct、block 规则。限定版中这些规则写死且不可改。
5. 系统代理写入当前用户 Internet Settings，启用前会保存原值，并在断开、正常退出或下次启动恢复。托盘菜单可连接、断开、切换节点、显示窗口或退出。
6. 设置支持开机启动、启动最小化、自动连接、端口和中英文界面。

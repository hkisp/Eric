# Eric agent

Windows (WPF, .NET 8) and macOS (Avalonia) client for Xray. Version **1.6.0**.

Downloads (prebuilt packages, including the optional 限定版): <https://blog.idcai.net/down/>

This repository is **source only**. Built-in first-run node profiles in the tree are **placeholders** (documentation addresses and dummy credentials) and will not connect to a real server. Import or subscribe your own nodes. Do not commit share links, UUIDs, passwords, or keys.

## Build and test (Windows)

Default build is the **full client** (user-selectable routing: Rule-based / Global / Direct, plus custom rules).

```powershell
dotnet test .\tests\IDCXray.Tests.csproj
dotnet publish .\src\IDCXray.csproj -c Release -r win-x64 --self-contained -o .\publish
```

The publish folder should contain `Eric agent.exe` plus `core\xray.exe`, `core\geoip.dat`, and `core\geosite.dat`. `publish.ps1` can also zip a self-contained release.

### 限定版 (AI-only split tunnel)

The locked edition proxies only Cloudflare, OpenAI/Codex/ChatGPT, Claude/Anthropic, and Gemini/Google related domains; everything else is direct. Nodes can still be imported, edited, subscribed, and connected. Routing rules are not user-editable. Build it with:

```powershell
dotnet publish .\src\IDCXray.csproj -c Release -r win-x64 --self-contained false -p:EricAiOnly=true -o .\publish-ai-only
```

`EricAiOnly=true` defines `ERIC_AI_ONLY`, sets Product/Title to "Eric agent 1.6.0 限定版", and InformationalVersion to `1.6.0-ai-only`. FileVersion stays `1.6.0.0`.

## macOS

```powershell
dotnet test .\mac\tests\Mac.Tests.csproj
.\mac\publish-mac.cmd
```

See `README_中文.md` for usage notes.

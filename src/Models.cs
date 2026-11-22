using System.Text;
using System.Text.Json;
namespace IDCXray;

public sealed class Node
{
    public string Name { get; set; } = ""; public string Protocol { get; set; } = ""; public string Host { get; set; } = ""; public int Port { get; set; }
    public string Id { get; set; } = ""; public string Password { get; set; } = ""; public string Method { get; set; } = ""; public string Flow { get; set; } = "";
    public string Network { get; set; } = "tcp"; public string Security { get; set; } = "none"; public string Sni { get; set; } = ""; public string Fingerprint { get; set; } = "";
    public string PublicKey { get; set; } = ""; public string ShortId { get; set; } = ""; public string Path { get; set; } = ""; public string ServiceName { get; set; } = "";
    public string AlterId { get; set; } = "0";
    public string WgAddress { get; set; } = "";
    public string AllowedIPs { get; set; } = "";
    public string PresharedKey { get; set; } = "";
    public int Mtu { get; set; }
    public int KeepAlive { get; set; }
    public bool IsBuiltInOracle { get; set; }
    public string? SubscriptionId { get; set; }
    public string Summary => string.Equals(Protocol, "wireguard", StringComparison.OrdinalIgnoreCase)
        ? "WIREGUARD · UDP"
        : $"{(Protocol ?? "unknown").ToUpperInvariant()} · {(string.IsNullOrWhiteSpace(Network) || Network.Equals("raw", StringComparison.OrdinalIgnoreCase) ? "TCP" : Network).ToUpperInvariant()}";
}

public sealed class Subscription
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public DateTime? LastUpdated { get; set; }
}

public sealed record ImportBatchResult(IReadOnlyList<Node> Nodes, IReadOnlyList<string> Errors);
public sealed record SubscriptionSyncResult(int Added, int Updated, int Deleted, int Skipped, IReadOnlyList<string> Errors);

public static class SubscriptionSynchronizer
{
    public static SubscriptionSyncResult Sync(List<Node> existing, string subscriptionId, IReadOnlyList<Node> incoming)
    {
        int added=0, updated=0, deleted=0, skipped=0; var errors=new List<string>(); var candidates=new Dictionary<string,Node>(StringComparer.OrdinalIgnoreCase);
        foreach(var n in incoming) { try { LinkParser.ValidateNode(n); var key=LinkParser.Identity(n); if(candidates.ContainsKey(key)){skipped++;errors.Add("订阅中有重复节点，已跳过重复项。");}else candidates[key]=n; } catch(FormatException ex){skipped++;errors.Add(ex.Message);} }
        if(candidates.Count==0) throw new InvalidOperationException("订阅中没有有效节点。");
        foreach(var n in existing.Where(n=>n.SubscriptionId==subscriptionId&&!n.IsBuiltInOracle).ToList()) { var key=LinkParser.Identity(n); if(candidates.Remove(key,out var fresh)){fresh.SubscriptionId=subscriptionId;existing[existing.IndexOf(n)]=fresh;updated++;}else{existing.Remove(n);deleted++;} }
        foreach(var n in candidates.Values){n.SubscriptionId=subscriptionId;existing.Add(n);added++;}
        return new(added,updated,deleted,skipped,errors);
    }
}

public sealed record RouteRule(string Type, string Value, string Action);

public static class LinkParser
{
    public static bool LooksLikeWireGuard(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var t = raw;
        return t.Contains("[Interface]", StringComparison.OrdinalIgnoreCase)
            && t.Contains("PrivateKey", StringComparison.OrdinalIgnoreCase);
    }

    public static Node Parse(string raw)
    {
        if (LooksLikeWireGuard(raw)) return ParseWireGuardConf(raw);
        var s = string.Concat(raw.Where(c => !char.IsWhiteSpace(c)));
        if(s.StartsWith("vmess://",StringComparison.OrdinalIgnoreCase))return ParseVmess(s);
        if (!Uri.TryCreate(s, UriKind.Absolute, out var u)) throw new FormatException("链接格式无效。");
        var q = System.Web.HttpUtility.ParseQueryString(u.Query);
        var name = Decode(u.Fragment.TrimStart('#')); var host = u.Host; var port = u.IsDefaultPort ? 0 : u.Port;
        Node n = u.Scheme.ToLowerInvariant() switch
        {
            "vless" => new Node { Protocol="vless", Host=host, Port=port, Id=Decode(u.UserInfo), Flow=q["flow"]??"", Network=Norm(q["type"]??"tcp"), Security=q["security"]??"none", Sni=q["sni"]??q["serverName"]??"", Fingerprint=q["fp"]??"", PublicKey=q["pbk"]??q["password"]??"", ShortId=q["sid"]??"", Path=q["path"]??"", ServiceName=q["serviceName"]??"" },
            "trojan" => new Node { Protocol="trojan", Host=host, Port=port, Password=Decode(u.UserInfo), Network=Norm(q["type"]??"tcp"), Security=q["security"]??"tls", Sni=q["sni"]??q["peer"]??q["host"]??"", Path=q["path"]??"", ServiceName=q["serviceName"]??"" },
            "ss" => ParseShadowsocks(s,u),
            "wireguard" => ParseWireGuardLink(u,q,name),
            _ => throw new FormatException("支持 vless://、vmess://、trojan://、ss:// 链接，以及 WireGuard .conf。")
        };
        n.Name = name.Length == 0 ? $"{n.Host}:{n.Port}" : name;
        Validate(n); return n;
    }

    public static ImportBatchResult ParseMany(string raw)
    {
        var parsed=new List<Node>();var errors=new List<string>();
        if(LooksLikeWireGuard(raw)){try{parsed.Add(ParseWireGuardConf(raw));}catch(Exception ex){errors.Add(ex.Message);}return new(parsed,errors);}
        var lines=raw.Replace("\r\n","\n").Split('\n',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        for(int i=0;i<lines.Length;i++)try{parsed.Add(Parse(lines[i]));}catch(Exception ex){errors.Add($"第 {i+1} 行：{ex.Message}");}
        return new(parsed,errors);
    }
    private static Node ParseWireGuardLink(Uri u,System.Collections.Specialized.NameValueCollection q,string name)
    {
        var secret=Decode(u.UserInfo);if(string.IsNullOrWhiteSpace(secret))throw new FormatException("wireguard:// 缺少 userinfo 私钥。");
        var pub=q["publickey"]??q["publicKey"];if(string.IsNullOrWhiteSpace(pub))throw new FormatException("wireguard:// 缺少 publickey 参数。");
        var address=q["address"];if(string.IsNullOrWhiteSpace(address))throw new FormatException("wireguard:// 缺少 address 参数。");
        var endpoint=q["endpoint"];
        if(string.IsNullOrWhiteSpace(endpoint)){var h=q["host"]??u.Host;var p=u.IsDefaultPort?0:u.Port;if(string.IsNullOrWhiteSpace(h)||p==0)throw new FormatException("wireguard:// 缺少 endpoint 或 host:port。");endpoint=$"{h}:{p}";}
        ParseEndpoint(endpoint,out var host,out var port);
        var n=new Node{Protocol="wireguard",Name=string.IsNullOrWhiteSpace(name)?$"WireGuard {host}":name,Host=host,Port=port,Password=secret,PublicKey=pub,Network="udp",Security="none",WgAddress=address,AllowedIPs=q["allowedips"]??"0.0.0.0/0",PresharedKey=q["presharedkey"]??"",Mtu=int.TryParse(q["mtu"],out var mtu)?mtu:1420,KeepAlive=int.TryParse(q["keepalive"],out var ka)?ka:0};Validate(n);return n;
    }
    public static string Identity(Node n)=>$"{n.Protocol}|{n.Host}|{n.Port}";
    public static void ValidateNode(Node n)=>Validate(n);

    public static Node ParseWireGuardConf(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) throw new FormatException("配置为空。");
        string section = "";
        var iface = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var peer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var peerCount = 0;
        foreach (var line0 in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var line = line0.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';')) continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line[1..^1].Trim();
                if (section.Equals("Peer", StringComparison.OrdinalIgnoreCase))
                {
                    peerCount++;
                    if (peerCount > 1) throw new FormatException("当前仅支持包含一个 [Peer] 的 WireGuard 配置。");
                    peer.Clear();
                }
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (section.Equals("Interface", StringComparison.OrdinalIgnoreCase)) iface[key] = value;
            else if (section.Equals("Peer", StringComparison.OrdinalIgnoreCase)) peer[key] = value;
        }
        if (peerCount != 1) throw new FormatException("配置需要包含一个 [Peer] 段。");
        if (!iface.TryGetValue("PrivateKey", out var secret) || string.IsNullOrWhiteSpace(secret)) throw new FormatException("缺少 Interface PrivateKey。");
        if (!peer.TryGetValue("PublicKey", out var pub) || string.IsNullOrWhiteSpace(pub)) throw new FormatException("缺少 Peer PublicKey。");
        if (!peer.TryGetValue("Endpoint", out var endpoint) || string.IsNullOrWhiteSpace(endpoint)) throw new FormatException("缺少 Peer Endpoint。");
        ParseEndpoint(endpoint, out var host, out var port);
        var address = iface.GetValueOrDefault("Address") ?? "";
        if (string.IsNullOrWhiteSpace(address)) throw new FormatException("缺少 Interface Address。");
        int.TryParse(iface.GetValueOrDefault("MTU"), out var mtu);
        int.TryParse(peer.GetValueOrDefault("PersistentKeepalive"), out var keepAlive);
        var n = new Node
        {
            Protocol = "wireguard",
            Name = $"WireGuard {host}",
            Host = host,
            Port = port,
            Password = secret,
            PublicKey = pub,
            Network = "udp",
            Security = "none",
            WgAddress = address,
            AllowedIPs = peer.GetValueOrDefault("AllowedIPs") ?? "0.0.0.0/0",
            PresharedKey = peer.GetValueOrDefault("PresharedKey") ?? "",
            Mtu = mtu > 0 ? mtu : 1420,
            KeepAlive = keepAlive
        };
        Validate(n);
        return n;
    }

    private static void ParseEndpoint(string endpoint, out string host, out int port)
    {
        endpoint = endpoint.Trim();
        if (endpoint.StartsWith('['))
        {
            var close = endpoint.IndexOf(']');
            if (close < 0 || close + 2 >= endpoint.Length || endpoint[close + 1] != ':') throw new FormatException("Endpoint 格式无效。");
            host = endpoint[1..close];
            if (!int.TryParse(endpoint[(close + 2)..], out port) || port is < 1 or > 65535) throw new FormatException("Endpoint 端口无效。");
            return;
        }
        var idx = endpoint.LastIndexOf(':');
        if (idx <= 0) throw new FormatException("Endpoint 需要包含 host:port。");
        host = endpoint[..idx];
        if (!int.TryParse(endpoint[(idx + 1)..], out port) || port is < 1 or > 65535) throw new FormatException("Endpoint 端口无效。");
    }

    private static Node ParseVmess(string s)
    {
        var encoded=s[8..].Split('#','?')[0].TrimEnd('/'); using var d=JsonDocument.Parse(Encoding.UTF8.GetString(Decode64(encoded))); var x=d.RootElement;
        string V(string k,string df="")=>x.TryGetProperty(k,out var v)?v.ToString():df;
        var n=new Node { Name=Decode(s.Contains('#')?s[(s.IndexOf('#')+1)..]:"") is {Length:>0} nm?nm:V("ps",V("add")), Protocol="vmess", Host=V("add"), Port=int.TryParse(V("port"),out var p)?p:0, Id=V("id"), AlterId=V("aid","0"), Security=V("tls") is "" or "none"?"none":"tls", Sni=V("sni",V("host")), Network=Norm(V("net","tcp")), Path=V("path"), ServiceName=V("path") };
        Validate(n); return n;
    }
    private static Node ParseShadowsocks(string s,Uri u)
    {
        var user=Decode(u.UserInfo); string auth;
        try { auth=Encoding.UTF8.GetString(Decode64(user)); } catch { auth=user; }
        var host=u.Host; var port=u.Port;
        var at=auth.LastIndexOf('@'); if(at>=0){var endpoint=auth[(at+1)..];auth=auth[..at];var ep=endpoint.Split(':');if(ep.Length==2){host=ep[0];int.TryParse(ep[1],out port);}}
        var pair=auth.Split(':',2); if(pair.Length!=2)throw new FormatException("Shadowsocks 用户信息无效。");
        var n=new Node{Name=Decode(u.Fragment.TrimStart('#')),Protocol="shadowsocks",Host=host,Port=port,Method=pair[0],Password=Decode(pair[1]),Security="none"}; if(n.Name.Length==0)n.Name=$"{host}:{port}";Validate(n);return n;
    }
    private static void Validate(Node n)
    {
        if(string.IsNullOrWhiteSpace(n.Host)||n.Port is <1 or >65535)throw new FormatException("节点地址或端口无效。");
        if(n.Protocol is "vless" or "vmess" && string.IsNullOrWhiteSpace(n.Id))throw new FormatException("节点 ID 不能为空。");
        if(n.Protocol=="trojan"&&string.IsNullOrEmpty(n.Password))throw new FormatException("Trojan 密码不能为空。");
        if(n.Protocol=="shadowsocks"&&(string.IsNullOrEmpty(n.Method)||string.IsNullOrEmpty(n.Password)))throw new FormatException("Shadowsocks 方法和密码不能为空。");
        if(n.Protocol=="wireguard")
        {
            if(string.IsNullOrWhiteSpace(n.Password))throw new FormatException("WireGuard 私钥不能为空。");
            if(string.IsNullOrWhiteSpace(n.PublicKey))throw new FormatException("WireGuard 对端公钥不能为空。");
            if(string.IsNullOrWhiteSpace(n.WgAddress))throw new FormatException("WireGuard 地址不能为空。");
        }
    }
    private static string Norm(string s)=>s.Equals("raw",StringComparison.OrdinalIgnoreCase)?"tcp":s.ToLowerInvariant();
    private static string Decode(string s){try{return Uri.UnescapeDataString(s);}catch{return s;}}
    private static byte[] Decode64(string s){s=s.Replace('-','+').Replace('_','/');s+=new string('=',(4-s.Length%4)%4);return Convert.FromBase64String(s);}
}

public static class SubscriptionDecoder
{
    public static IReadOnlyList<Node> Decode(string body)
    {
        var text=body.Trim(); try{text=Encoding.UTF8.GetString(Convert.FromBase64String(text));}catch(FormatException){}
        return LinkParser.ParseMany(text).Nodes;
    }
    public static ImportBatchResult DecodeDetailed(string body)
    {
        if(string.IsNullOrWhiteSpace(body))return new(Array.Empty<Node>(),new[]{"订阅内容为空。"});
        var text=body.Trim();try{text=Encoding.UTF8.GetString(Convert.FromBase64String(text));}catch(FormatException){}
        return LinkParser.ParseMany(text);
    }
}

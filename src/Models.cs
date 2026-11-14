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
    public bool IsBuiltInOracle { get; set; }
    public string Summary => $"{Protocol.ToUpperInvariant()} · {Network.ToUpperInvariant()}";
}

public sealed record RouteRule(string Type, string Value, string Action);

public static class LinkParser
{
    public static Node Parse(string raw)
    {
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
            _ => throw new FormatException("支持 vless://、vmess://、trojan://、ss:// 链接。")
        };
        n.Name = name.Length == 0 ? $"{n.Host}:{n.Port}" : name;
        Validate(n); return n;
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
        var result=new List<Node>(); foreach(var line in text.Split('\n',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)){if(!line.Contains("://",StringComparison.Ordinal))continue;result.Add(LinkParser.Parse(line));} return result;
    }
}

using System.Text.Json;
namespace IDCXray;
public static class XrayConfig
{
    private static readonly string[] AiOnlyDomains =
    {
        "geosite:cloudflare", "geosite:openai", "geosite:anthropic", "geosite:google",
        "geosite:google-gemini", "geosite:google-deepmind",
        "domain:cloudflare.com", "domain:cloudflare.net", "domain:cloudflare-dns.com", "domain:workers.dev", "domain:pages.dev", "domain:trycloudflare.com", "domain:r2.dev", "domain:cloudflareinsights.com",
        "domain:openai.com", "domain:chatgpt.com", "domain:oaistatic.com", "domain:oaiusercontent.com",
        "domain:anthropic.com", "domain:claude.ai", "domain:claude.com", "domain:claudeusercontent.com",
        "domain:google.com", "domain:googleapis.com", "domain:gstatic.com", "domain:googleusercontent.com", "domain:google", "domain:gemini.google.com", "domain:aistudio.google.com", "domain:generativelanguage.googleapis.com", "domain:ai.google.dev", "domain:deepmind.google", "domain:accounts.google.com"
    };

    public static string Build(Node n,int socks=10808,int http=10809,string mode="Rule-based",bool ai=true,IEnumerable<RouteRule>? custom=null)
        => BuildCore(n,socks,http,mode,ai,custom,false);

    public static string BuildAiOnly(Node n,int socks=10808,int http=10809,string mode="Rule-based",bool ai=true,IEnumerable<RouteRule>? custom=null)
        => BuildCore(n,socks,http,mode,ai,custom,true);

    private static string BuildCore(Node n,int socks,int http,string mode,bool ai,IEnumerable<RouteRule>? custom,bool aiOnly)
    {
        object settings=n.Protocol switch
        {
            "vless" => new {vnext=new[]{new {address=n.Host,port=n.Port,users=new[]{new {id=n.Id,encryption="none",flow=n.Flow}}}}},
            "vmess" => new {vnext=new[]{new {address=n.Host,port=n.Port,users=new[]{new {id=n.Id,alterId=int.TryParse(n.AlterId,out var aid)?aid:0,security="auto"}}}}},
            "trojan" => new {servers=new[]{new {address=n.Host,port=n.Port,password=n.Password}}},
            "wireguard" => BuildWireGuardSettings(n),
            _ => new {servers=new[]{new {address=n.Host,port=n.Port,method=n.Method,password=n.Password}}}
        };
        var network=string.IsNullOrWhiteSpace(n.Network)||string.Equals(n.Network,"raw",StringComparison.OrdinalIgnoreCase)?"tcp":n.Network.ToLowerInvariant();
        var security=string.IsNullOrWhiteSpace(n.Security)?"none":n.Security;
        var stream=new Dictionary<string,object>{{"network",network},{"security",security}};
        if(n.Security=="reality")stream["realitySettings"]=new {show=false,fingerprint=n.Fingerprint,serverName=EmptyTo(n.Sni,n.Host),publicKey=n.PublicKey,shortId=n.ShortId,spiderX=""};
        else if(n.Security=="tls")stream["tlsSettings"]=new {serverName=EmptyTo(n.Sni,n.Host),fingerprint=EmptyTo(n.Fingerprint,"chrome"),allowInsecure=false};
        if(network=="ws")stream["wsSettings"]=new {path=EmptyTo(n.Path,"/"),headers=new {Host=EmptyTo(n.Sni,n.Host)}};
        if(network=="grpc")stream["grpcSettings"]=new {serviceName=n.ServiceName, multiMode=false};
        if(network=="http")stream["httpSettings"]=new {path=EmptyTo(n.Path,"/"),host=new[]{EmptyTo(n.Sni,n.Host)}};
        var rules=new List<object>();
        if(aiOnly)
        {
            rules.Add(new {type="field",outboundTag="direct",ip=new[]{"geoip:private"}});
            rules.Add(new {type="field",outboundTag="proxy",domain=AiOnlyDomains});
            rules.Add(new {type="field",outboundTag="direct",network="tcp,udp"});
        }
        else
        {
        if(mode=="Direct") rules.Add(new {type="field",outboundTag="direct",network="tcp,udp"});
        if(mode=="Rule-based")
        {
            if(n.IsBuiltInOracle)
            {
                rules.Add(new {type="field",outboundTag="proxy",ip="1.1.1.1",port="53"});
                rules.Add(new {type="field",outboundTag="direct",ip="8.8.8.8",port="53"});
                rules.Add(new {type="field",outboundTag="direct",domain=new[]{"geosite:private","geosite:apple"}});
                rules.Add(new {type="field",outboundTag="direct",ip=new[]{"223.5.5.5","223.6.6.6","119.29.29.29","1.12.12.12","120.53.53.53","180.76.76.76","114.114.114.114"}});
                rules.Add(new {type="field",outboundTag="direct",domain=new[]{"domain:alidns.com","domain:doh.pub","domain:dot.pub","domain:360.cn","domain:onedns.net"}});
            }
            // Evaluate AI destinations before China/LAN bypass rules so Gemini stays proxied.
            if(ai)rules.Add(new {type="field",outboundTag="proxy",domain=new[]{"domain:chatgpt.com","domain:openai.com","domain:oaistatic.com","domain:oaiusercontent.com","domain:claude.ai","domain:claude.com","domain:anthropic.com","domain:googleapis.com","domain:gemini.google.com","domain:aistudio.google.com","domain:generativelanguage.googleapis.com"}});
            if(n.IsBuiltInOracle)rules.Add(new {type="field",outboundTag="proxy",domain=new[]{"geosite:google"}});
            rules.Add(new {type="field",outboundTag="direct",ip=new[]{"geoip:private","geoip:cn"}});
            rules.Add(new {type="field",outboundTag="direct",domain=new[]{"geosite:cn"}});
            if(custom!=null)foreach(var r in custom){if(r is null) continue; var action=(r.Action??"").ToLowerInvariant() switch{"direct"=>"direct","block"=>"block",_=>"proxy"}; var typ=r.Type??""; rules.Add(new {type="field",outboundTag=action,domain=typ=="domain"?new[]{"domain:"+(r.Value??"")}:null,ip=typ=="ip"?new[]{r.Value??""}:null});}
        }
        }
        object proxyOutbound = n.Protocol == "wireguard"
            ? new {tag="proxy",protocol="wireguard",settings}
            : new {tag="proxy",protocol=n.Protocol,settings,streamSettings=stream};
        var root=new Dictionary<string,object?> { ["log"]=new {loglevel="warning"}, ["inbounds"]=new object[]{
            new {tag="socks-in",listen="127.0.0.1",port=socks,protocol="socks",settings=new {udp=true,auth="noauth"},sniffing=new {enabled=true,destOverride=new[]{"http","tls","quic"}}},
            new {tag="http-in",listen="127.0.0.1",port=http,protocol="http",settings=new {},sniffing=new {enabled=true,destOverride=new[]{"http","tls","quic"}}}
        }, ["outbounds"]=aiOnly ? new object[]{new {tag="direct",protocol="freedom",settings=new{}},proxyOutbound,new {tag="block",protocol="blackhole",settings=new{}}} : new object[]{proxyOutbound,new {tag="direct",protocol="freedom",settings=new{}},new {tag="block",protocol="blackhole",settings=new{}}}, ["routing"]=new {domainStrategy="IPIfNonMatch",rules}, ["stats"]=new {}};
        if(n.IsBuiltInOracle)
        {
            root["dns"] = new
            {
                disableCache=false,
                disableFallback=false,
                disableFallbackIfMatch=false,
                queryStrategy="UseIP",
                servers=new[]{"1.1.1.1"},
                hosts=new Dictionary<string,object>
                {
                    ["cloudflare-dns.com"]=new[]{"104.16.248.249","104.16.249.249","2606:4700::6810:f8f9","2606:4700::6810:f9f9"},
                    ["common.dot.dns.yandex.net"]=new[]{"77.88.8.8","77.88.8.1","2a02:6b8::feed:0ff","2a02:6b8:0:1::feed:0ff"},
                    ["dns.alidns.com"]=new[]{"223.5.5.5","223.6.6.6","2400:3200::1","2400:3200:baba::1"},
                    ["dns.cloudflare.com"]=new[]{"104.16.132.229","104.16.133.229","2606:4700::6810:84e5","2606:4700::6810:85e5"},
                    ["dns.google"]=new[]{"8.8.8.8","8.8.4.4","2001:4860:4860::8888","2001:4860:4860::8844"},
                    ["dns.quad9.net"]=new[]{"9.9.9.9","149.112.112.112","2620:fe::fe","2620:fe::9"},
                    ["domain:googleapis.cn"]="googleapis.com",
                    ["dot.pub"]=new[]{"1.12.12.12","120.53.53.53"},
                    ["one.one.one.one"]=new[]{"1.1.1.1","1.0.0.1","2606:4700:4700::1111","2606:4700:4700::1001"}
                }
            };
            root["policy"] = new
            {
                levels=new Dictionary<string,object>{{"8",new {bufferSize=0,connIdle=30,downlinkOnly=1,handshake=4,statsUserDownlink=false,statsUserUplink=false,uplinkOnly=1}}},
                system=new {statsOutboundDownlink=true,statsOutboundUplink=true}
            };
        }
        return JsonSerializer.Serialize(root,new JsonSerializerOptions{WriteIndented=true});
    }

    private static object BuildWireGuardSettings(Node n)
    {
        var address = SplitList(n.WgAddress);
        if (address.Length == 0) address = new[] { "10.66.66.2/32" };
        var allowed = SplitList(string.IsNullOrWhiteSpace(n.AllowedIPs) ? "0.0.0.0/0, ::/0" : n.AllowedIPs);
        var peer = new Dictionary<string, object?>
        {
            ["publicKey"] = n.PublicKey,
            ["endpoint"] = $"{n.Host}:{n.Port}",
            ["allowedIPs"] = allowed
        };
        if (!string.IsNullOrWhiteSpace(n.PresharedKey)) peer["preSharedKey"] = n.PresharedKey;
        if (n.KeepAlive > 0) peer["keepAlive"] = n.KeepAlive;
        return new
        {
            secretKey = n.Password,
            address,
            peers = new[] { peer },
            mtu = n.Mtu > 0 ? n.Mtu : 1420,
            domainStrategy = "ForceIPv4"
        };
    }

    private static string[] SplitList(string value) =>
        (value ?? "").Split(new[] { ',', '\n', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string EmptyTo(string value,string fallback)=>string.IsNullOrWhiteSpace(value)?fallback:value;
}

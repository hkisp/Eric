using System.Text;
using System.Text.Json;
using IDCXray;
var count=0;
void Check(bool ok,string name){if(!ok)throw new Exception("FAIL: "+name);count++;Console.WriteLine("PASS: "+name);}
const string fakeId="11111111-2222-3333-4444-555555555555";
var v=LinkParser.Parse($"vless://{fakeId}@example.com:443?security=reality&flow=xtls-rprx-vision&type=raw&sni=example.org&fp=chrome&pbk=FAKE_PUBLIC_KEY&sid=abcd#Fake");Check(v.Protocol=="vless"&&v.Network=="tcp"&&v.Flow=="xtls-rprx-vision","VLESS parsing and raw normalization");
var vmJson=JsonSerializer.Serialize(new{v="2",ps="Fake VMess",add="example.com",port="443",id=fakeId,aid="0",net="ws",host="example.com",path="/fake",tls="tls"});var vm=LinkParser.Parse("vmess://"+Convert.ToBase64String(Encoding.UTF8.GetBytes(vmJson)));Check(vm.Protocol=="vmess"&&vm.Network=="ws"&&vm.Port==443,"VMess parsing");
var tr=LinkParser.Parse("trojan://FAKE_PASSWORD@example.com:443?security=tls&type=grpc&serviceName=fake#Fake");Check(tr.Protocol=="trojan"&&tr.Network=="grpc"&&tr.Password=="FAKE_PASSWORD","Trojan parsing");
var ssAuth=Convert.ToBase64String(Encoding.UTF8.GetBytes("aes-128-gcm:FAKE_PASSWORD"));var ss=LinkParser.Parse($"ss://{ssAuth}@example.com:8388#Fake");Check(ss.Protocol=="shadowsocks"&&ss.Method=="aes-128-gcm"&&ss.Port==8388,"Shadowsocks parsing");
var sub=Convert.ToBase64String(Encoding.UTF8.GetBytes($"vless://{fakeId}@example.com:443#Fake\ntrojan://FAKE_PASSWORD@example.org:443#Other"));Check(SubscriptionDecoder.Decode(sub).Count==2,"Base64 subscription decoding");
using var doc=JsonDocument.Parse(XrayConfig.Build(v));var root=doc.RootElement;Check(root.GetProperty("inbounds").GetArrayLength()==2&&root.GetProperty("outbounds")[0].GetProperty("protocol").GetString()=="vless","VLESS config generation");
using var td=JsonDocument.Parse(XrayConfig.Build(tr));using var sd=JsonDocument.Parse(XrayConfig.Build(ss));Check(td.RootElement.GetProperty("outbounds")[0].GetProperty("settings").GetProperty("servers")[0].TryGetProperty("password",out _)&&sd.RootElement.GetProperty("outbounds")[0].GetProperty("settings").GetProperty("servers")[0].TryGetProperty("method",out _),"Trojan and Shadowsocks config shapes");
Console.WriteLine($"All {count} unit checks passed.");

using IDCXray;

var passed = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception("FAIL: " + label); passed++; }
var wg = LinkParser.Parse("wireguard://SAFE_FAKE_KEY@example.test:51820?publickey=SAFE_FAKE_PUBLIC&address=10.1.0.2%2F32#WG");
Check(wg.Protocol == "wireguard" && wg.Port == 51820, "WireGuard link parse");
var conf = "[Interface]\nPrivateKey = SAFE_FAKE_KEY\nAddress = 10.1.0.2/32\n[Peer]\nPublicKey = SAFE_FAKE_PUBLIC\nEndpoint = example.test:51820\n";
Check(LinkParser.ParseMany(conf).Nodes.Single().Protocol == "wireguard", "WireGuard conf parse");
var vm = LinkParser.ParseMany("vless://SAFE_FAKE_ID@example.test:443#node\ntrojan://SAFE_FAKE_PASS@example.test:8443#node2");
Check(vm.Nodes.Count == 2 && vm.Errors.Count == 0, "multi protocol import");
Check(SubscriptionDecoder.Decode(Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("vless://SAFE_FAKE_ID@example.test:443#sub"))).Count == 1, "subscription decode");
Check(MacStore.DirectoryPath.EndsWith(Path.Combine("Library", "Application Support", "Eric agent"), StringComparison.Ordinal), "mac application support path");
Check(MacStore.FilePath.EndsWith(Path.Combine("Eric agent", "nodes.json"), StringComparison.Ordinal), "mac node storage filename");
Check(MacStore.DirectoryPermissions == (UnixFileMode)448, "directory permission mask");
Check(MacStore.FilePermissions == (UnixFileMode)384, "file permission mask");
Console.WriteLine($"All {passed} mac checks passed.");

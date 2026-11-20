using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AppRoutingManager;
public static class RoutingTests {
 static int passed;
 static void Assert(bool value,string name){if(!value)throw new Exception("FAILED: "+name);passed++;Console.WriteLine("PASS "+name);}
 static bool HostEntryRule(Dictionary<string,object> r){return Json.Str(r,"Remarks").EndsWith("（分流助手）");}
 static void Throws(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Assert(failed,name);}
 public static int Main(string[] args){try{
  string root=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.Combine(root,"guiConfigs"));File.WriteAllText(Path.Combine(root,"v2rayN.exe"),"fixture");string dbPath=Path.Combine(root,"guiConfigs","guiNDB.db");
  using(var db=new Sqlite(dbPath,false,true)){db.Exec("CREATE TABLE RoutingItem (Id TEXT PRIMARY KEY,Remarks TEXT,RuleSet TEXT,RuleNum INTEGER,CustomRulesetPath4Singbox TEXT,Enabled INTEGER,Locked INTEGER,IsActive INTEGER,Sort INTEGER)");db.Exec("INSERT INTO RoutingItem VALUES ('base','original','[]',0,NULL,1,0,1,0)");}
  var store=new RoutingStore(root);string source="{\"version\":3,\"rules\":[{\"type\":\"logical\",\"mode\":\"and\",\"rules\":[{\"process_name\":[\"msedge.exe\"]},{\"domain_suffix\":[\"baidu.com\",\"qq.com\"]}]}]}";
  string support=store.Support;Directory.CreateDirectory(support);string sourcePath=Path.Combine(support,"existing.json"),mappingPath=Path.Combine(support,"mapping.json");Json.Atomic(sourcePath,source);Json.Atomic(mappingPath,Json.Text(new object[]{new Dictionary<string,object>{{"tag",RoutingStore.Tag},{"type","local"},{"format","source"},{"path",sourcePath}}}));
  var originalRules=RoutingStore.BuildRules(null,new List<AppEntry>{new AppEntry("QQ.exe")},new List<AppEntry>{new AppEntry("msedge.exe")});using(var db=new Sqlite(dbPath)){db.Exec("UPDATE RoutingItem SET RuleSet=?,RuleNum=?,CustomRulesetPath4Singbox=? WHERE Id='base'",Json.Text(originalRules),originalRules.Count,mappingPath);}
  var snap=store.Load(store.Profiles().First());Assert(snap.Split.Count==1&&snap.Split[0].Match=="msedge.exe","load preserves manual split-list edits");Assert(snap.Direct.Count==1&&snap.Direct[0].Match=="QQ.exe","load direct apps from native database");
  var saved=store.Save(snap,"Renamed rule",new List<AppEntry>{new AppEntry("Weixin.exe")},new List<AppEntry>{new AppEntry("chrome.exe"),new AppEntry(@"D:\Other\msedge.exe")},false);
  Assert(saved.Profile.Id=="base"&&saved.Profile.Name=="Renamed rule","rename updates existing rule ID");Assert(saved.Direct.Count==1&&saved.Direct[0].Match=="Weixin.exe","remove and add direct applications");Assert(saved.Split.Count==2&&saved.Split.Any(x=>x.ByPath),"save name and full-path split matches together");
  var savedRules=Json.Arr(Json.Serializer().DeserializeObject(saved.Profile.RawRules));Assert(savedRules.Count==6&&savedRules.All(x=>Json.Obj(x).Count>0&&Json.Get(Json.Obj(x),"value")==null),"native route list is flat and retains original rules");Assert(Json.Str(Json.Obj(savedRules.Last()),"outboundTag")=="proxy","unlisted traffic has final proxy fallback");Assert(savedRules.Any(x=>Json.Str(Json.Obj(x),"network")=="udp")&&savedRules.Any(x=>Json.Arr(Json.Get(Json.Obj(x),"ip")).Contains("geoip:private")),"UDP policy and LAN exceptions preserved");
  var empty=store.Save(saved,"Empty lists",new List<AppEntry>(),new List<AppEntry>(),false);Assert(empty.Direct.Count==0&&empty.Split.Count==0,"empty lists round trip without wildcard direct rule");Assert(!Json.Arr(Json.Serializer().DeserializeObject(empty.Profile.RawRules)).Any(x=>Json.Str(Json.Obj(x),"outboundTag")=="direct"&&Json.Get(Json.Obj(x),"process")!=null),"empty direct list cannot turn into catch-all direct");
  Throws(()=>store.Save(empty,"duplicate",new List<AppEntry>{new AppEntry("QQ.exe")},new List<AppEntry>{new AppEntry("qq.exe")},false),"conflicting lists rejected before mutation");
  using(var db=new Sqlite(dbPath)){db.Exec("UPDATE RoutingItem SET Remarks='external edit' WHERE Id='base'");}
  string before=File.ReadAllText(empty.SourcePath);Throws(()=>store.Save(empty,"stale edit",new List<AppEntry>(),new List<AppEntry>{new AppEntry("test.exe")},false),"concurrent native edits detected");Assert(File.ReadAllText(empty.SourcePath)==before,"concurrency failure does not change source file");
  var fresh=store.Load(store.Profiles().First());Json.Atomic(fresh.SourcePath,before+" ");Throws(()=>store.Save(fresh,"external source",new List<AppEntry>(),new List<AppEntry>{new AppEntry("test.exe")},false),"concurrent source edits detected");
  var malformed=store.Load(store.Profiles().First());malformed.SourceText="{\"rules\":[]}";string previousRules=malformed.Profile.RawRules;Throws(()=>store.Save(malformed,"invalid",new List<AppEntry>(),new List<AppEntry>(),false),"malformed country source rejected");Assert(store.Profiles().First().RawRules==previousRules,"validation failure leaves native rules unchanged");
  Assert(Directory.GetDirectories(Path.Combine(support,"backups")).Length>=2,"automatic local backups created");Assert(AppEntry.Normalize("D:/Apps/Browser.exe")==@"D:\Apps\Browser.exe","portable path normalization");
  var nativeStore=new RoutingStore(args[1]);var nativeSnapshot=nativeStore.Load(nativeStore.Profiles().First(p=>p.Managed&&p.Active));nativeStore.ValidateSource(nativeSnapshot.SourcePath);Assert(nativeSnapshot.Split.Count>0,"live source loaded read-only and validated by installed sing-box");
  string emptyNative=Path.Combine(nativeStore.Support,"test-empty-"+Guid.NewGuid().ToString("N")+".json");try{Json.Atomic(emptyNative,Json.Text(RoutingStore.BuildSource(nativeSnapshot.SourceText,new List<AppEntry>())));nativeStore.ValidateSource(emptyNative);Assert(true,"empty-list rule also passes actual sing-box validation");}finally{File.Delete(emptyNative);}
  Directory.CreateDirectory(Path.Combine(root,"bin","sing_box"));Directory.CreateDirectory(Path.Combine(root,"bin","srss"));File.Copy(Path.Combine(args[1],"bin","sing_box","sing-box.exe"),Path.Combine(root,"bin","sing_box","sing-box.exe"));foreach(string country in new[]{"geosite-cn","geosite-geolocation-cn","geoip-cn"}){string raw=Path.Combine(args[1],"bin","srss",country+".srs");if(File.Exists(raw))File.Copy(raw,Path.Combine(root,"bin","srss",country+".srs"));}
  var portable=store.Save(store.Load(null),"Portable fresh setup",new List<AppEntry>{new AppEntry("localapp.exe")},new List<AppEntry>{new AppEntry("msedge.exe")},true);Assert(portable.Profile.Name=="Portable fresh setup"&&portable.Profile.Id!="base","fresh machine bootstrap creates a separate native profile");Assert(!portable.Profile.Active&&store.Profiles().First(x=>x.Id=="base").Active,"bootstrap does not silently switch the original active profile");Assert(portable.Split.Count==1&&portable.Direct.Count==1,"bootstrap creates local source and native lists without bundled personal configuration");
  Throws(()=>store.Save(store.Load(null),"Portable fresh setup",new List<AppEntry>(),new List<AppEntry>(),false),"duplicate profile names rejected");
  // 规则集重命名 / 删除
  string pid=portable.Profile.Id;store.Rename(pid,"Renamed portable");Assert(store.Profiles().First(p=>p.Id==pid).Name=="Renamed portable","rename profile only changes its name");
  string otherName=store.Profiles().First(p=>p.Id!=pid).Name;Throws(()=>store.Rename(pid,otherName),"rename to existing name rejected");Throws(()=>store.Rename(pid,"  "),"empty profile name rejected");
  var activeProfile=store.Profiles().FirstOrDefault(p=>p.Active);if(activeProfile!=null&&activeProfile.Id!=pid){Throws(()=>store.Delete(activeProfile.Id),"active profile cannot be deleted");Assert(store.Profiles().Any(p=>p.Id==activeProfile.Id),"active profile kept after refused delete");}
  string pMapping=store.Profiles().First(p=>p.Id==pid).MappingPath,pSource=portable.SourcePath;store.Delete(pid);
  Assert(!store.Profiles().Any(p=>p.Id==pid),"profile deleted from database");Assert(!File.Exists(pMapping)&&!File.Exists(pSource),"deleted profile files removed");
  Assert(Directory.GetDirectories(Path.Combine(support,"backups"),"deleted-*").Any(d=>File.Exists(Path.Combine(d,"routing.json"))),"deleted profile backed up");
  // 域名 / IP 名单
  Assert(HostEntry.Parse("https://www.Example.com:8443/a?b").Value=="domain:example.com","url is reduced to its main domain");
  Assert(HostEntry.Parse("http://news.bbc.co.uk/x").Value=="domain:bbc.co.uk"&&HostEntry.Parse("www.sina.com.cn/news/1.html").Value=="domain:sina.com.cn","main domain keeps two-level public suffixes");
  Assert(HostEntry.Parse("https://1.2.3.4:8443/path?q").Value=="1.2.3.4/32"&&HostEntry.Parse("http://[2001:db8::1]/x").Value=="2001:db8::1/128","url with ip host becomes that ip");
  Assert(HostEntry.Parse("www.example.com").Value=="domain:www.example.com"&&HostEntry.Parse("full:www.example.com").Text=="full:www.example.com","typed host without url parts is kept as-is");
  Throws(()=>HostEntry.Parse("localhost"),"single-label typo rejected without prefix");Assert(HostEntry.Parse("domain:localhost").Value=="domain:localhost","single label allowed with explicit prefix");
  Assert(HostEntry.Parse("*.example.com").Value=="domain:example.com"&&HostEntry.Parse("full:A.example.com").Value=="full:a.example.com","wildcard and full domain forms");
  Assert(HostEntry.Parse("10.1.2.3/8").Value=="10.0.0.0/8"&&HostEntry.Parse("1.2.3.4").Value=="1.2.3.4/32"&&HostEntry.Parse("1.2.3.4").IsIp,"ip and cidr normalized");
  Assert(HostEntry.Parse("2001:db8::1/32").Value=="2001:db8::/32"&&HostEntry.Parse("[::1]").Value=="::1/128","ipv6 normalized");
  Assert(HostEntry.Parse("中文.com").Value=="domain:xn--fiq228c.com","internationalized domain to punycode");
  Assert(HostEntry.Parse("geoip:Telegram").Value=="geoip:telegram"&&HostEntry.Parse("geoip:telegram").IsIp&&!HostEntry.Parse("geosite:google").IsIp,"geo rule libraries");
  foreach(string bad in new[]{"bad domain.com","exa_mple..com","1.2.3.4/33","regexp:(","keyword:a b","geosite:","-x.com"})Throws(()=>HostEntry.Parse(bad),"invalid host rejected: "+bad);
  string problem;var parsed=HostEntry.ParseMany("# comment\r\nexample.com, 1.1.1.1\r\n\r\nregexp:^a, b$\r\nexample.com",out problem);Assert(problem==null&&parsed.Count==3&&parsed[2].Value=="regexp:^a, b$","editor parses lines, separators, comments, regexp and duplicates");
  HostEntry.ParseMany("ok.com\r\n1.2.3.4/40",out problem);Assert(problem!=null&&problem.StartsWith("第 2 行"),"editor reports the failing line");
  var hostSnap=store.Load(store.Profiles().First(x=>x.Id=="base"));
  var dh=new List<HostEntry>{HostEntry.Parse("baidu.com"),HostEntry.Parse("192.168.50.0/24")};var ph=new List<HostEntry>{HostEntry.Parse("full:api.openai.com"),HostEntry.Parse("keyword:google")};
  var withHosts=store.Save(hostSnap,"Hosts",hostSnap.Direct,hostSnap.Split,true,dh,ph);
  Assert(withHosts.DirectHosts.Select(x=>x.Value).SequenceEqual(dh.Select(x=>x.Value))&&withHosts.ProxyHosts.Select(x=>x.Value).SequenceEqual(ph.Select(x=>x.Value)),"host lists round trip through v2rayN rules");
  var hostRules=Json.Arr(Json.Serializer().DeserializeObject(withHosts.Profile.RawRules)).Select(Json.Obj).ToList();
  Assert(Json.Str(hostRules[0],"id")=="app-manager-host-direct-domain"&&Json.Str(hostRules[1],"id")=="app-manager-host-direct-ip"&&Json.Str(hostRules[2],"outboundTag")=="proxy"&&hostRules.Count(x=>Json.Str(x,"id").StartsWith("app-manager-host"))==3,"host rules come first, split by domain and ip");
  var again=store.Save(withHosts,"Hosts",withHosts.Direct,withHosts.Split,false);Assert(Json.Arr(Json.Serializer().DeserializeObject(again.Profile.RawRules)).Count==hostRules.Count&&again.ProxyHosts.Count==2,"re-saving does not duplicate host rules");
  // 模拟 v2rayN 重写规则（大写字段、新 id），仍能按备注识别
  var rewritten=hostRules.Select(x=>{var r=new Dictionary<string,object>();foreach(var kv in x)r[char.ToUpper(kv.Key[0])+kv.Key.Substring(1)]=kv.Value;if(Json.Str(r,"Id").StartsWith("app-manager-host"))r["Id"]=Guid.NewGuid().ToString();return (object)r;}).ToList();
  using(var db=new Sqlite(dbPath)){db.Exec("UPDATE RoutingItem SET RuleSet=? WHERE Id='base'",Json.Text(rewritten));}
  var reread=store.Load(store.Profiles().First(x=>x.Id=="base"));Assert(reread.DirectHosts.Count==2&&reread.ProxyHosts.Count==2,"host rules recognised after v2rayN rewrites ids");
  var cleared=store.Save(reread,"Hosts",reread.Direct,reread.Split,false,new List<HostEntry>(),new List<HostEntry>{HostEntry.Parse("x.com")});
  var clearedRules=Json.Arr(Json.Serializer().DeserializeObject(cleared.Profile.RawRules)).Select(Json.Obj).ToList();Assert(cleared.DirectHosts.Count==0&&cleared.ProxyHosts.Count==1&&clearedRules.Count(x=>HostEntryRule(x))==1,"emptied host list removes its rules");
  Throws(()=>store.Save(cleared,"Hosts",cleared.Direct,cleared.Split,false,new List<HostEntry>{HostEntry.Parse("x.com")},new List<HostEntry>{HostEntry.Parse("domain:X.com")}),"same host in both lists rejected");
  Throws(()=>store.Save(cleared,"Hosts",cleared.Direct,cleared.Split,false,new List<HostEntry>{HostEntry.Parse("geosite:not-installed-xyz")},null),"missing local geosite rejected");
  var geo=store.Save(cleared,"Hosts",cleared.Direct,cleared.Split,true,new List<HostEntry>{HostEntry.Parse("geosite:cn")},null);Assert(geo.DirectHosts.Count==1&&geo.DirectHosts[0].IsGeo,"installed geosite accepted");
  Throws(()=>store.ValidateHosts(new List<HostEntry>{HostEntry.Parse("regexp:^(?=a)b")}),"sing-box rejects regex syntax unsupported by Go");
  // 运行模式检测：用本地假 Clash API，不触碰真实 v2rayN
  Assert(ClashMode.Query(root)==null,"no running config means no mode warning");
  var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();int port=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;string seenAuth=null;
  var server=new System.Threading.Thread(()=>{for(int i=0;i<2;i++){using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){var reader=new StreamReader(stream);string line;while(!string.IsNullOrEmpty(line=reader.ReadLine()))if(line.StartsWith("Authorization:"))seenAuth=line.Substring(14).Trim();string body="{\"mode\":\""+(i==0?"Global":"Rule")+"\"}";var bytes=System.Text.Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n"+body);stream.Write(bytes,0,bytes.Length);}}}){IsBackground=true};server.Start();
  Json.Atomic(Path.Combine(root,"binConfigs","config.json"),Json.Text(new Dictionary<string,object>{{"experimental",new Dictionary<string,object>{{"clash_api",new Dictionary<string,object>{{"external_controller","0.0.0.0:"+port},{"secret","s3"}}}}}}));
  string mode=ClashMode.Query(root);Assert(mode=="Global"&&seenAuth=="Bearer s3"&&ClashMode.Warning(mode)!=null,"global mode detected through clash api with secret");
  mode=ClashMode.Query(root);Assert(mode=="Rule"&&ClashMode.Warning(mode)==null,"rule mode shows no warning");listener.Stop();
  Assert(ClashMode.Warning("Direct")!=null,"direct mode warns too");
  Console.WriteLine("ALL "+passed+" CHECKS PASSED");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace AppRoutingManager {
sealed class TimedWebClient : WebClient {
 protected override WebRequest GetWebRequest(Uri address){var request=base.GetWebRequest(address);request.Timeout=20000;var http=request as HttpWebRequest;if(http!=null)http.ReadWriteTimeout=20000;return request;}
}
public static class Json {
 public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=int.MaxValue,RecursionLimit=512};}
 public static object Read(string path){return Serializer().DeserializeObject(File.ReadAllText(path,Encoding.UTF8));}
 public static string Text(object value){return Serializer().Serialize(value);}
 public static Dictionary<string,object> Obj(object value){return value as Dictionary<string,object> ?? new Dictionary<string,object>();}
 public static List<object> Arr(object value){var a=value as IEnumerable;return a==null||value is string?new List<object>():a.Cast<object>().ToList();}
 public static object Get(Dictionary<string,object> d,string key){foreach(var p in d)if(string.Equals(p.Key,key,StringComparison.OrdinalIgnoreCase))return p.Value;return null;}
 public static string Str(Dictionary<string,object> d,string key){return Convert.ToString(Get(d,key));}
 public static bool Flag(Dictionary<string,object> d,string key,bool fallback){object value=Get(d,key);return value==null?fallback:Convert.ToBoolean(value);}
 public static void Set(Dictionary<string,object> d,string key,object value){var old=d.Keys.FirstOrDefault(k=>string.Equals(k,key,StringComparison.OrdinalIgnoreCase));d[old??key]=value;}
 public static void Atomic(string path,string text){Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+".new-"+Guid.NewGuid().ToString("N");File.WriteAllText(tmp,text,new UTF8Encoding(false));if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
 public static string Hash(string text){using(var h=SHA256.Create())return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(text??"")));}
}

public sealed class Sqlite : IDisposable {
 IntPtr db;
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_open_v2(byte[] path,out IntPtr handle,int flags,IntPtr vfs);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_close(IntPtr handle);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_busy_timeout(IntPtr handle,int ms);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_prepare_v2(IntPtr handle,byte[] sql,int length,out IntPtr stmt,IntPtr tail);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_step(IntPtr stmt);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_finalize(IntPtr stmt);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_text(IntPtr stmt,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_bytes(IntPtr stmt,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_count(IntPtr stmt);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_column_name(IntPtr stmt,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_column_type(IntPtr stmt,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_text(IntPtr stmt,int index,byte[] text,int length,IntPtr destructor);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_bind_null(IntPtr stmt,int index);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern int sqlite3_changes(IntPtr handle);
 [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]static extern IntPtr sqlite3_errmsg(IntPtr handle);
 static byte[] U(string s){return Encoding.UTF8.GetBytes(s+"\0");}
 static string Utf(IntPtr p,int length=-1){if(p==IntPtr.Zero)return null;if(length<0){length=0;while(Marshal.ReadByte(p,length)!=0)length++;}byte[] bytes=new byte[length];if(length>0)Marshal.Copy(p,bytes,0,length);return Encoding.UTF8.GetString(bytes);}
 public Sqlite(string path,bool readOnly=false,bool create=false){int rc=sqlite3_open_v2(U(path),out db,readOnly?1:(create?6:2),IntPtr.Zero);if(rc!=0){string m=Utf(sqlite3_errmsg(db));Dispose();throw new IOException("无法打开 v2rayN 数据库："+m);}sqlite3_busy_timeout(db,5000);}
 IntPtr Prepare(string sql,object[] args){IntPtr stmt;int rc=sqlite3_prepare_v2(db,U(sql),-1,out stmt,IntPtr.Zero);if(rc!=0)throw new IOException("数据库操作失败："+Utf(sqlite3_errmsg(db)));try{for(int i=0;i<args.Length;i++){if(args[i]==null)rc=sqlite3_bind_null(stmt,i+1);else{byte[] b=U(Convert.ToString(args[i]));rc=sqlite3_bind_text(stmt,i+1,b,b.Length-1,new IntPtr(-1));}if(rc!=0)throw new IOException("数据库参数写入失败。");}return stmt;}catch{sqlite3_finalize(stmt);throw;}}
 public List<Dictionary<string,object>> Query(string sql,params object[] args){IntPtr stmt=Prepare(sql,args);try{var rows=new List<Dictionary<string,object>>();int rc;while((rc=sqlite3_step(stmt))==100){var row=new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<sqlite3_column_count(stmt);i++)row[Utf(sqlite3_column_name(stmt,i))]=sqlite3_column_type(stmt,i)==5?null:Utf(sqlite3_column_text(stmt,i),sqlite3_column_bytes(stmt,i));rows.Add(row);}if(rc!=101)throw new IOException("数据库读取失败："+Utf(sqlite3_errmsg(db)));return rows;}finally{sqlite3_finalize(stmt);}}
 public int Exec(string sql,params object[] args){IntPtr stmt=Prepare(sql,args);try{if(sqlite3_step(stmt)!=101)throw new IOException("数据库保存失败："+Utf(sqlite3_errmsg(db)));return sqlite3_changes(db);}finally{sqlite3_finalize(stmt);}}
 public void Dispose(){if(db!=IntPtr.Zero){sqlite3_close(db);db=IntPtr.Zero;}}
}

public sealed class AppEntry {
 public string Match;
 public string Description;
 public bool ByPath{get{return Match.Contains("\\");}}
 public string Name{get{return Path.GetFileName(Match);}}
 public AppEntry(string match){Match=Normalize(match);Description=Describe(ByPath?Match:null,Name);}
 public static string Normalize(string value){value=(value??"").Trim().Trim('"').Replace('/','\\');if(value.Length==0||!value.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new ArgumentException("请选择 EXE 文件，或输入正确的 EXE 名称。");if(value.Contains('\\')&&!Path.IsPathRooted(value))throw new ArgumentException("使用完整 EXE 路径，或只填写进程名称。");return value;}
 public static string Describe(string path,string fallback){try{if(path!=null&&File.Exists(path)){var v=FileVersionInfo.GetVersionInfo(path);if(!string.IsNullOrWhiteSpace(v.FileDescription))return v.FileDescription;}}catch{}return fallback;}
}
public sealed class RouteProfile {
 public string Id,Name,RawRules,MappingPath;
 public bool Active,Managed;
 public Dictionary<string,object> Raw;
 public override string ToString(){return Name+(Active?"  · 正在使用":"");}
}
public sealed class RouteSnapshot {
 public RouteProfile Profile;
 public string Root,SourcePath,SourceText,MappingText;
 public List<AppEntry> Direct=new List<AppEntry>(),Split=new List<AppEntry>();
 public bool IsNew;
}

public sealed class RoutingStore {
 public const string Tag="geosite-app-split-cn";
 public string Root{get;private set;}
 public string DbPath{get{return Path.Combine(Root,"guiConfigs","guiNDB.db");}}
 public string Support{get{return Path.Combine(Root,"guiConfigs","AppRouting");}}
 public RoutingStore(string root){Root=Path.GetFullPath(root).TrimEnd('\\');if(!IsRoot(Root))throw new IOException("这个文件夹中没有找到 v2rayN.exe 和它的配置数据库。请先运行一次 v2rayN，再选择它。");using(var db=new Sqlite(DbPath,true)){var columns=db.Query("PRAGMA table_info(RoutingItem)").Select(x=>Json.Str(x,"name")).ToList();foreach(string c in new[]{"Id","Remarks","RuleSet","RuleNum","CustomRulesetPath4Singbox","Enabled","IsActive"})if(!columns.Contains(c))throw new IOException("此版本的 v2rayN 配置格式暂不兼容，请使用支持 sing-box 自定义规则集的 v2rayN 7.x。");}}
 public static bool IsRoot(string root){try{return File.Exists(Path.Combine(root,"v2rayN.exe"))&&File.Exists(Path.Combine(root,"guiConfigs","guiNDB.db"));}catch{return false;}}
 public List<RouteProfile> Profiles(){using(var db=new Sqlite(DbPath,true))return db.Query("SELECT * FROM RoutingItem ORDER BY Sort").Select(ToProfile).ToList();}
 static RouteProfile ToProfile(Dictionary<string,object> row){string raw=Json.Str(row,"RuleSet");bool managed=false;try{managed=Json.Arr(Json.Serializer().DeserializeObject(raw)).Any(x=>Json.Arr(Json.Get(Json.Obj(x),"Domain")).Any(y=>Convert.ToString(y)=="geosite:app-split-cn"));}catch{}return new RouteProfile{Id=Json.Str(row,"Id"),Name=Json.Str(row,"Remarks"),RawRules=raw,MappingPath=Json.Str(row,"CustomRulesetPath4Singbox"),Active=Json.Str(row,"IsActive")=="1",Managed=managed,Raw=row};}
 public RouteSnapshot Load(RouteProfile profile){
  var snapshot=new RouteSnapshot{Profile=profile,Root=Root,IsNew=profile==null};
  if(profile==null){snapshot.Split.Add(new AppEntry("msedge.exe"));snapshot.Split.Add(new AppEntry("chrome.exe"));return snapshot;}
  var rules=Json.Arr(Json.Serializer().DeserializeObject(profile.RawRules));
  foreach(var o in rules){var r=Json.Obj(o);if(Json.Str(r,"OutboundTag")=="direct"&&Json.Flag(r,"Enabled",true))foreach(var p in Json.Arr(Json.Get(r,"Process")))AddUnique(snapshot.Direct,new AppEntry(Convert.ToString(p)));}
  if(profile.Managed){
   if(string.IsNullOrEmpty(profile.MappingPath)||!File.Exists(profile.MappingPath))throw new IOException("此规则的本地规则集映射文件丢失，请先恢复该文件。");
   snapshot.MappingText=File.ReadAllText(profile.MappingPath,Encoding.UTF8);
   var definition=Json.Arr(Json.Serializer().DeserializeObject(snapshot.MappingText)).Select(Json.Obj).FirstOrDefault(d=>Json.Str(d,"tag")==Tag);
   if(definition==null)throw new IOException("找不到应用国内外分流规则集的文件位置。");
   snapshot.SourcePath=Json.Str(definition,"path");if(!Path.IsPathRooted(snapshot.SourcePath))snapshot.SourcePath=Path.Combine(Root,snapshot.SourcePath);
   if(!File.Exists(snapshot.SourcePath))throw new IOException("应用分流规则文件丢失："+snapshot.SourcePath);
   if(Json.Str(definition,"format")=="binary")snapshot.SourceText=Decompile(snapshot.SourcePath);else snapshot.SourceText=File.ReadAllText(snapshot.SourcePath,Encoding.UTF8);
   var document=Json.Obj(Json.Serializer().DeserializeObject(snapshot.SourceText));var outer=Json.Arr(Json.Get(document,"rules"));
   if(outer.Count>0){var conjunction=Json.Arr(Json.Get(Json.Obj(outer[0]),"rules"));if(conjunction.Count<2)throw new IOException("应用分流规则结构无法识别，未进行修改。");CollectApps(Json.Obj(conjunction[0]),snapshot.Split);}
  }
  return snapshot;
 }
 static void CollectApps(Dictionary<string,object> rule,List<AppEntry> result){foreach(string key in new[]{"process_name","process_path"})foreach(var a in Json.Arr(Json.Get(rule,key))){string v=Convert.ToString(a);if(!v.StartsWith("__app_routing_empty_",StringComparison.Ordinal))AddUnique(result,new AppEntry(v));}foreach(var child in Json.Arr(Json.Get(rule,"rules")))CollectApps(Json.Obj(child),result);}
 public static void AddUnique(List<AppEntry> target,AppEntry value){if(!target.Any(x=>string.Equals(x.Match,value.Match,StringComparison.OrdinalIgnoreCase)))target.Add(value);}
 string CorePath{get{string core=Path.Combine(Root,"bin","sing_box","sing-box.exe");if(!File.Exists(core))throw new IOException("没有找到 sing-box 内核。请先在 v2rayN 中安装并使用 sing-box / TUN 模式。");return core;}}
 string RunCore(string arguments){var start=new ProcessStartInfo(CorePath,arguments){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Root};using(var process=Process.Start(start)){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(30000))throw new IOException("sing-box 校验超时，配置没有保存。");string text=output.Result+error.Result;if(process.ExitCode!=0)throw new IOException("sing-box 校验未通过，配置没有保存。\r\n"+text);return text;}}
 public static string Quote(string text){var b=new StringBuilder("\"");int n=0;foreach(char c in text){if(c=='\\'){n++;continue;}b.Append('\\',c=='"'?n*2+1:n);n=0;b.Append(c);}b.Append('\\',n*2);return b.Append('"').ToString();}
 string Decompile(string input){Directory.CreateDirectory(Support);string path=Path.Combine(Support,"read-"+Guid.NewGuid().ToString("N")+".json");try{RunCore("rule-set decompile --output "+Quote(path)+" "+Quote(input));return File.ReadAllText(path,Encoding.UTF8);}finally{if(File.Exists(path))File.Delete(path);}}
  Dictionary<string,object> NewSource(){var regions=new List<object>();foreach(string name in new[]{"geosite-cn","geosite-geolocation-cn","geoip-cn"}){string path=Path.Combine(Root,"bin","srss",name+".srs");if(!File.Exists(path)&&name!="geosite-geolocation-cn")path=DownloadCountrySet(name);if(File.Exists(path))regions.AddRange(Json.Arr(Json.Get(Json.Obj(Json.Serializer().DeserializeObject(Decompile(path))),"rules")));}
  if(regions.Count==0)throw new IOException("还没有国内规则库。请先在 v2rayN 中更新 Geo 文件，然后再保存。");
  return Json.Obj(new Dictionary<string,object>{{"version",3},{"rules",new object[]{new Dictionary<string,object>{{"type","logical"},{"mode","and"},{"rules",new object[]{new Dictionary<string,object>(),new Dictionary<string,object>{{"type","logical"},{"mode","or"},{"rules",regions}}}}}}}});
 }
 string DownloadCountrySet(string name){
  string dir=Path.Combine(Support,"country");Directory.CreateDirectory(dir);string target=Path.Combine(dir,name+".srs");if(File.Exists(target))return target;
  string temporary=target+".download-"+Guid.NewGuid().ToString("N");string repository=name.StartsWith("geosite-")?"sing-geosite":"sing-geoip";
  try{ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;using(var web=new TimedWebClient()){web.Headers["User-Agent"]="AppRoutingManager/1.0";web.DownloadFile("https://raw.githubusercontent.com/SagerNet/"+repository+"/rule-set/"+name+".srs",temporary);}if(new FileInfo(temporary).Length>16*1024*1024)throw new IOException("国内规则文件大小异常。");Decompile(temporary);File.Move(temporary,target);return target;}
  catch(Exception e){throw new IOException("首次使用需要国内规则库，但没有下载成功。请连接网络或先开启 v2rayN，再保存一次。\r\n"+e.Message);}
  finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
 public static Dictionary<string,object> BuildSource(string original,List<AppEntry> entries){var source=Json.Obj(Json.Serializer().DeserializeObject(original));var rules=Json.Arr(Json.Get(source,"rules"));if(rules.Count!=1)throw new IOException("国内规则文件格式无法识别，未进行修改。");var outer=Json.Obj(rules[0]);var conditions=Json.Arr(Json.Get(outer,"rules"));if(Json.Str(outer,"mode")!="and"||conditions.Count<2)throw new IOException("国内规则文件缺少应用与地区组合条件。");
  var matches=new List<object>();var names=entries.Where(x=>!x.ByPath).Select(x=>x.Match).ToArray();var paths=entries.Where(x=>x.ByPath).Select(x=>x.Match).ToArray();if(names.Length>0)matches.Add(new Dictionary<string,object>{{"process_name",names}});if(paths.Length>0)matches.Add(new Dictionary<string,object>{{"process_path",paths}});
   if(matches.Count==0){
    var neverA=new Dictionary<string,object>{{"process_name",new[]{"__app_routing_empty_a__.exe"}}};
    var neverB=new Dictionary<string,object>{{"process_name",new[]{"__app_routing_empty_b__.exe"}}};
    conditions[0]=new Dictionary<string,object>{{"type","logical"},{"mode","and"},{"rules",new object[]{neverA,neverB}}};
   }
  else conditions[0]=new Dictionary<string,object>{{"type","logical"},{"mode","or"},{"rules",matches}};
  outer["rules"]=conditions;return source;
 }
 public static List<object> BuildRules(string oldRules,List<AppEntry> direct,List<AppEntry> split){var rules=new List<object>();if(direct.Count>0)rules.Add(new Dictionary<string,object>{{"id","app-manager-direct"},{"remarks","全部直连应用"},{"outboundTag","direct"},{"process",direct.Select(x=>x.Match).ToArray()},{"enabled",true}});rules.Add(new Dictionary<string,object>{{"id","app-manager-split"},{"remarks","指定应用国内直连，国外代理"},{"outboundTag","direct"},{"domain",new[]{"geosite:app-split-cn"}},{"enabled",split.Count>0}});
  if(!string.IsNullOrEmpty(oldRules)){foreach(var value in Json.Arr(Json.Serializer().DeserializeObject(oldRules))){var r=Json.Obj(value);if(r.Count==0||Json.Get(r,"value")!=null)throw new IOException("现有路由结构异常，请先备份并修复。");if(Json.Get(r,"Process")!=null&&Json.Str(r,"OutboundTag")=="direct")continue;if(Json.Arr(Json.Get(r,"Domain")).Any(x=>Convert.ToString(x)=="geosite:app-split-cn"))continue;if(Json.Str(r,"OutboundTag")=="proxy"&&Json.Str(r,"Port")=="0-65535")continue;rules.Add(r);}}
  else{rules.Add(new Dictionary<string,object>{{"remarks","阻断 UDP 443"},{"outboundTag","block"},{"port","443"},{"network","udp"},{"enabled",true}});rules.Add(new Dictionary<string,object>{{"remarks","局域网直连"},{"outboundTag","direct"},{"ip",new[]{"geoip:private"}},{"enabled",true}});rules.Add(new Dictionary<string,object>{{"remarks","局域网域名直连"},{"outboundTag","direct"},{"domain",new[]{"geosite:private"}},{"enabled",true}});}
  rules.Add(new Dictionary<string,object>{{"id","app-manager-fallback"},{"remarks","其余应用全部代理"},{"outboundTag","proxy"},{"port","0-65535"},{"enabled",true}});return rules;
 }
 public void ValidateSource(string sourcePath){
  string check=Path.Combine(Support,"check-"+Guid.NewGuid().ToString("N")+".json");
  try{
   var outbound=new Dictionary<string,object>{{"type","direct"},{"tag","direct"}};
   var rule=new Dictionary<string,object>{{"rule_set",new[]{Tag}},{"outbound","direct"}};
   var set=new Dictionary<string,object>{{"tag",Tag},{"type","local"},{"format","source"},{"path",sourcePath}};
   var route=new Dictionary<string,object>{{"rules",new object[]{rule}},{"rule_set",new object[]{set}}};
   var config=new Dictionary<string,object>{{"outbounds",new object[]{outbound}},{"route",route}};
   Json.Atomic(check,Json.Text(config));RunCore("check -c "+Quote(check));
  }finally{if(File.Exists(check))File.Delete(check);}
 }
 public RouteSnapshot Save(RouteSnapshot snapshot,string name,List<AppEntry> direct,List<AppEntry> split,bool validate=true){
  name=(name??"").Trim();if(name.Length==0||name.Length>100)throw new ArgumentException("请填写规则集名称（1—100 个字）。");
  if(Profiles().Any(p=>p.Name==name&&(snapshot.IsNew||p.Id!=snapshot.Profile.Id)))throw new ArgumentException("已经有同名规则集，请换一个名称。");
  foreach(var x in direct)if(split.Any(y=>string.Equals(x.Match,y.Match,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("同一项不能同时放在两个名单中："+x.Match);
  if(!string.Equals(snapshot.Root,Root,StringComparison.OrdinalIgnoreCase))throw new IOException("v2rayN 位置已经变化，请重新读取。");
  Directory.CreateDirectory(Support);string id=snapshot.IsNew?DateTime.UtcNow.Ticks.ToString():snapshot.Profile.Id;
  string sourcePath=snapshot.IsNew?Path.Combine(Support,"apps-"+id+".json"):snapshot.SourcePath;
  if(string.IsNullOrEmpty(sourcePath)||sourcePath.EndsWith(".srs",StringComparison.OrdinalIgnoreCase))sourcePath=Path.Combine(Support,"apps-"+id+".json");
  string mappingPath=snapshot.IsNew?Path.Combine(Support,"sets-"+id+".json"):snapshot.Profile.MappingPath;
  if(string.IsNullOrEmpty(mappingPath))mappingPath=Path.Combine(Support,"sets-"+id+".json");
  string oldSource=File.Exists(sourcePath)?File.ReadAllText(sourcePath,Encoding.UTF8):null;
  string oldMapping=File.Exists(mappingPath)?File.ReadAllText(mappingPath,Encoding.UTF8):null;
  string template=snapshot.SourceText??Json.Text(NewSource());var source=BuildSource(template,split);string sourceText=Json.Text(source);
  string staged=Path.Combine(Support,"stage-"+Guid.NewGuid().ToString("N")+".json");
  Json.Atomic(staged,sourceText);try{if(validate)ValidateSource(staged);}catch{File.Delete(staged);throw;}
  var definitions=string.IsNullOrEmpty(oldMapping)?new List<object>():Json.Arr(Json.Serializer().DeserializeObject(oldMapping));definitions=definitions.Where(x=>Json.Str(Json.Obj(x),"tag")!=Tag).ToList();definitions.Add(new Dictionary<string,object>{{"tag",Tag},{"type","local"},{"format","source"},{"path",sourcePath}});
  var rules=BuildRules(snapshot.IsNew?null:snapshot.Profile.RawRules,direct,split);string rulesText=Json.Text(rules);string mappingText=Json.Text(definitions);
  string backup=Path.Combine(Support,"backups","manager-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(backup);
  if(snapshot.Profile!=null)Json.Atomic(Path.Combine(backup,"routing.json"),Json.Text(snapshot.Profile.Raw));if(oldSource!=null)File.WriteAllText(Path.Combine(backup,"source.json"),oldSource,Encoding.UTF8);if(oldMapping!=null)File.WriteAllText(Path.Combine(backup,"mapping.json"),oldMapping,Encoding.UTF8);
  bool filesChanged=false;using(var db=new Sqlite(DbPath)){db.Exec("BEGIN IMMEDIATE");try{
    if(!snapshot.IsNew){var current=db.Query("SELECT * FROM RoutingItem WHERE Id=?",id).FirstOrDefault();if(current==null||Json.Str(current,"RuleSet")!=snapshot.Profile.RawRules||Json.Str(current,"Remarks")!=snapshot.Profile.Name||Json.Str(current,"CustomRulesetPath4Singbox")!=snapshot.Profile.MappingPath)throw new IOException("v2rayN 中的规则刚刚被其他窗口修改了，请先点重新读取，避免覆盖。");if(snapshot.MappingText!=null&&File.ReadAllText(snapshot.Profile.MappingPath,Encoding.UTF8)!=snapshot.MappingText)throw new IOException("规则集映射刚刚被其他程序修改，请先重新读取。");if(snapshot.SourceText!=null&&File.Exists(snapshot.SourcePath)&&!snapshot.SourcePath.EndsWith(".srs",StringComparison.OrdinalIgnoreCase)&&File.ReadAllText(snapshot.SourcePath,Encoding.UTF8)!=snapshot.SourceText)throw new IOException("本地分流文件已被其他程序修改，请重新读取后再保存。");}
    filesChanged=true;Json.Atomic(sourcePath,sourceText);Json.Atomic(mappingPath,mappingText);
    if(snapshot.IsNew)db.Exec("INSERT INTO RoutingItem (Id,Remarks,RuleSet,RuleNum,CustomRulesetPath4Singbox,Enabled,Locked,IsActive,Sort) VALUES (?,?,?,?,?,1,0,0,(SELECT COALESCE(MAX(Sort),0)+1 FROM RoutingItem))",id,name,rulesText,rules.Count,mappingPath);
    else if(db.Exec("UPDATE RoutingItem SET Remarks=?,RuleSet=?,RuleNum=?,CustomRulesetPath4Singbox=? WHERE Id=?",name,rulesText,rules.Count,mappingPath,id)!=1)throw new IOException("所选规则集不存在，未保存。");
    db.Exec("COMMIT");
   }catch{try{db.Exec("ROLLBACK");}catch{}if(filesChanged){if(oldSource!=null)Json.Atomic(sourcePath,oldSource);else if(File.Exists(sourcePath))File.Delete(sourcePath);if(oldMapping!=null)Json.Atomic(mappingPath,oldMapping);else if(File.Exists(mappingPath))File.Delete(mappingPath);}throw;}
   finally{if(File.Exists(staged))File.Delete(staged);}}
  return Load(Profiles().First(x=>x.Id==id));
 }
}

public static class Discovery {
 public static string SettingsPath{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AppRoutingManager","settings.json");}}
 public static string SavedRoot(){try{return Json.Str(Json.Obj(Json.Read(SettingsPath)),"v2rayNFolder");}catch{return null;}}
 public static void Remember(string root){try{Json.Atomic(SettingsPath,Json.Text(new Dictionary<string,object>{{"v2rayNFolder",root}}));}catch{}}
 public static string Find(){var candidates=new List<string>();string saved=SavedRoot();if(saved!=null)candidates.Add(saved);
  foreach(var p in Process.GetProcessesByName("v2rayN")){using(p){try{candidates.Add(Path.GetDirectoryName(p.MainModule.FileName));}catch{}}}
  string self=AppDomain.CurrentDomain.BaseDirectory;for(int i=0;i<3&&self!=null;i++){candidates.Add(self);self=Path.GetDirectoryName(self.TrimEnd('\\'));}
  foreach(var c in candidates)if(RoutingStore.IsRoot(c))return c;
  var roots=new List<string>{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads"),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)};
  foreach(var drive in DriveInfo.GetDrives().Where(d=>d.IsReady&&d.DriveType==DriveType.Fixed)){roots.Add(Path.Combine(drive.RootDirectory.FullName,"Tools"));roots.Add(Path.Combine(drive.RootDirectory.FullName,"Common"));roots.Add(Path.Combine(drive.RootDirectory.FullName,"Apps"));roots.Add(drive.RootDirectory.FullName);}
  int budget=1000;var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var queue=new Queue<Tuple<string,int>>();foreach(string root in roots)if(Directory.Exists(root))queue.Enqueue(Tuple.Create(root,0));
  while(queue.Count>0&&budget-->0){var current=queue.Dequeue();if(!visited.Add(current.Item1))continue;if(RoutingStore.IsRoot(current.Item1))return current.Item1;if(current.Item2>=3)continue;try{foreach(string dir in Directory.GetDirectories(current.Item1)){string name=Path.GetFileName(dir);if(name.StartsWith("$")||name.Equals("Windows",StringComparison.OrdinalIgnoreCase)||name.Equals("node_modules",StringComparison.OrdinalIgnoreCase)||name.Equals("AppData",StringComparison.OrdinalIgnoreCase))continue;if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0)continue;queue.Enqueue(Tuple.Create(dir,current.Item2+1));}}catch{}}
  return null;
 }
}
}

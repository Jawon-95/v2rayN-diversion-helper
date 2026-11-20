using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace AppRoutingManager {
/// <summary>一条域名或 IP 规则，Value 为 v2rayN 原生写法（domain:/full:/keyword:/regexp:/geosite:/geoip: 或 CIDR）。</summary>
public sealed class HostEntry {
 public readonly string Value,Kind,Text;
 public readonly bool IsIp;
 HostEntry(string value,bool ip,string kind,string text){Value=value;IsIp=ip;Kind=kind;Text=text;}
 static readonly Regex Label=new Regex("^[a-z0-9_]([a-z0-9_-]*[a-z0-9_])?$"),GeoName=new Regex("^[a-z0-9][a-z0-9_.!@-]*$");
 public bool IsGeo{get{return Value.StartsWith("geosite:")||Value.StartsWith("geoip:");}}

 /// <summary>解析用户输入：支持网址、*.example.com、带前缀的写法、IP 与 IP 段。</summary>
 public static HostEntry Parse(string input){
  string s=(input??"").Trim();
  if(s.Length==0)throw new ArgumentException("内容为空。");
  if(s.IndexOfAny(new[]{'\r','\n','\0','"'})>=0)throw new ArgumentException("包含无效字符。");
  string lower=s.ToLowerInvariant();
  foreach(string p in new[]{"domain:","full:","keyword:","regexp:","geosite:","geoip:"}){
   if(!lower.StartsWith(p))continue;string rest=s.Substring(p.Length).Trim();
   if(rest.Length==0)throw new ArgumentException("「"+p+"」后面缺少内容。");
   switch(p){
    case "domain:":return Domain(rest,false,true);
    case "full:":return Domain(rest,true,true);
    case "keyword:":rest=rest.ToLowerInvariant();if(rest.Any(char.IsWhiteSpace)||rest.Contains(','))throw new ArgumentException("关键字不能包含空格或逗号。");return new HostEntry("keyword:"+rest,false,"域名关键字","keyword:"+rest);
    case "regexp:":try{new Regex(rest);}catch{throw new ArgumentException("正则表达式无效："+rest);}return new HostEntry("regexp:"+rest,false,"域名正则","regexp:"+rest);
    default:rest=rest.ToLowerInvariant();if(!GeoName.IsMatch(rest))throw new ArgumentException("规则库名称无效："+rest);bool ip=p=="geoip:";return new HostEntry(p+rest,ip,ip?"IP 规则库":"域名规则库",p+rest);
   }
  }
  string cidr;if(TryIp(s,out cidr))return Ip(cidr);
  return Domain(s,false,false);
 }

 /// <summary>读取 v2rayN 里已保存的值；无法识别的写法原样保留。</summary>
 public static HostEntry FromStored(string value,bool ip){
  value=(value??"").Trim();
  bool known=ip||new[]{"domain:","full:","keyword:","regexp:","geosite:"}.Any(p=>value.StartsWith(p,StringComparison.OrdinalIgnoreCase));
  if(known)try{var e=Parse(value);if(e.IsIp==ip)return e;}catch{}
  return new HostEntry(value,ip,"其他",value);
 }

 static HostEntry Ip(string cidr){
  int slash=cidr.IndexOf('/');string address=cidr.Substring(0,slash);int bits=int.Parse(cidr.Substring(slash+1));
  bool single=bits==(address.Contains(':')?128:32);
  return new HostEntry(cidr,true,single?"IP 地址":"IP 段",single?address:cidr);
 }

 /// <summary>prefixed：用户显式写了 domain:/full:。粘贴的网址（带协议、路径、端口）会归并到主域名。</summary>
 static HostEntry Domain(string s,bool full,bool prefixed){
  string host=s.Trim();bool url=false;
  if(host.Contains("://")){Uri uri;if(!Uri.TryCreate(host,UriKind.Absolute,out uri)||string.IsNullOrEmpty(uri.Host))throw new ArgumentException("无法识别的网址："+s);host=uri.Host;url=true;}
  else{
   int cut=host.IndexOfAny(new[]{'/','?','#'});
   if(cut>=0){string tail=host.Substring(cut+1);host=host.Substring(0,cut);url=true;string ip;if(s.Trim()[cut]=='/'&&tail.Length>0&&tail.All(char.IsDigit)&&TryIp(host,out ip))throw new ArgumentException("IP 段无效："+s);}
   int at=host.LastIndexOf('@');if(at>=0){host=host.Substring(at+1);url=true;}
   int colon=host.LastIndexOf(':');if(colon>0&&host.IndexOf(':')==colon&&host.Substring(colon+1).All(char.IsDigit)){host=host.Substring(0,colon);url=true;}
  }
  string cidr;if(!full&&TryIp(host,out cidr))return Ip(cidr);
  if(host.StartsWith("*."))host=host.Substring(2);
  host=host.Trim('.');
  try{host=new IdnMapping().GetAscii(host);}catch{throw new ArgumentException("域名无效："+s);}
  host=host.ToLowerInvariant();
  if(host.Length==0||host.Length>253||!host.Split('.').All(l=>l.Length<=63&&Label.IsMatch(l)))throw new ArgumentException("域名无效："+s);
  if(!prefixed&&!host.Contains('.'))throw new ArgumentException("请填写完整域名，例如 example.com："+s);
  if(url&&!full)host=MainDomain(host);
  return full?new HostEntry("full:"+host,false,"完整域名","full:"+host):new HostEntry("domain:"+host,false,"域名（含子域名）",host);
 }

 // 常见的二级公共后缀，如 com.cn、co.uk、com.hk
 static readonly HashSet<string> SecondLevel=new HashSet<string>{"com","net","org","gov","edu","ac","co","or","ne","go","mil","ltd","plc","nom","gen","info","biz"};
 /// <summary>取主域名：www.news.example.com → example.com，www.bbc.co.uk → bbc.co.uk。</summary>
 public static string MainDomain(string host){var l=host.Split('.');if(l.Length<=2)return host;int keep=l[l.Length-1].Length==2&&SecondLevel.Contains(l[l.Length-2])?3:2;return string.Join(".",l.Skip(l.Length-keep));}

 /// <summary>解析多条输入：按行，行内可用空格、逗号、分号分隔；regexp: 行整行算一条；# 开头为注释。</summary>
 public static List<HostEntry> ParseMany(string text,out string problem){
  problem=null;var result=new List<HostEntry>();var lines=(text??"").Replace("\r\n","\n").Split('\n');bool multi=lines.Count(x=>x.Trim().Length>0)>1;
  for(int i=0;i<lines.Length;i++){string line=lines[i].Trim();if(line.Length==0||line.StartsWith("#"))continue;
   bool regex=line.StartsWith("regexp:",StringComparison.OrdinalIgnoreCase);
   foreach(string part in regex?new[]{line}:line.Split(new[]{' ','\t',';',',','，','；','、'},StringSplitOptions.RemoveEmptyEntries)){
    try{AddUnique(result,Parse(part));}catch(Exception ex){problem=(multi?"第 "+(i+1)+" 行：":"")+ex.Message;return null;}
   }
  }
  return result;
 }

 /// <summary>识别 IP 或 CIDR，并规范成「网络地址/前缀长度」。</summary>
 public static bool TryIp(string s,out string cidr){
  cidr=null;s=s.Trim();if(s.StartsWith("[")){int end=s.IndexOf(']');if(end<0)return false;s=s.Substring(1,end-1)+s.Substring(end+1);}
  string address=s,prefix=null;int slash=s.IndexOf('/');if(slash>=0){address=s.Substring(0,slash);prefix=s.Substring(slash+1);}
  bool v6=address.Contains(':');
  if(!v6){var parts=address.Split('.');if(parts.Length!=4||parts.Any(p=>p.Length==0||p.Length>3||!p.All(char.IsDigit)||int.Parse(p)>255))return false;}
  IPAddress ip;if(!IPAddress.TryParse(address,out ip))return false;
  if(v6!=(ip.AddressFamily==AddressFamily.InterNetworkV6))return false;
  int max=v6?128:32,bits=max;
  if(prefix!=null&&(!int.TryParse(prefix,NumberStyles.None,CultureInfo.InvariantCulture,out bits)||bits<0||bits>max))return false;
  if(v6)ip.ScopeId=0;
  byte[] bytes=ip.GetAddressBytes();for(int i=0;i<bytes.Length;i++){int keep=Math.Max(0,Math.Min(8,bits-i*8));bytes[i]&=(byte)(0xFF<<(8-keep));}
  cidr=new IPAddress(bytes)+"/"+bits;return true;
 }

 public static void AddUnique(List<HostEntry> target,HostEntry value){if(!target.Any(x=>Same(x,value)))target.Add(value);}
 public static bool Same(HostEntry a,HostEntry b){return string.Equals(a.Value,b.Value,a.Value.StartsWith("regexp:")?StringComparison.Ordinal:StringComparison.OrdinalIgnoreCase);}
}
}

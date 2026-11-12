using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace AppRoutingManager {
static class Native {
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
 public struct PROCESSENTRY32 {
  public uint dwSize,cntUsage,th32ProcessID;
  public IntPtr th32DefaultHeapID;
  public uint th32ModuleID,cntThreads,th32ParentProcessID;
  public int pcPriClassBase;
  public uint dwFlags;
  [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string szExeFile;
 }
 [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; public Rectangle ToRectangle(){return Rectangle.FromLTRB(Left,Top,Right,Bottom);} }
 public delegate bool EnumProc(IntPtr hwnd,IntPtr lParam);

 [DllImport("kernel32.dll",SetLastError=true)] public static extern IntPtr CreateToolhelp32Snapshot(uint flags,uint pid);
 [DllImport("kernel32.dll",EntryPoint="Process32FirstW",CharSet=CharSet.Unicode)] public static extern bool Process32First(IntPtr snapshot,ref PROCESSENTRY32 entry);
 [DllImport("kernel32.dll",EntryPoint="Process32NextW",CharSet=CharSet.Unicode)] public static extern bool Process32Next(IntPtr snapshot,ref PROCESSENTRY32 entry);
 [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
 [DllImport("kernel32.dll")] public static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode)] public static extern bool QueryFullProcessImageName(IntPtr process,int flags,StringBuilder name,ref int size);
 [DllImport("kernel32.dll")] public static extern bool GetProcessTimes(IntPtr process,out long creation,out long exit,out long kernel,out long user);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback,IntPtr lParam);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent,EnumProc callback,IntPtr lParam);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd,uint cmd);
 [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd,int index);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowTextLength(IntPtr hwnd);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int max);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd,StringBuilder text,int max);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out int pid);
 [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT point);
 [DllImport("user32.dll")] public static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out int value,int size);
 [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd,int attribute,out RECT value,int size);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] public static extern uint ExtractIconEx(string file,int index,IntPtr[] large,IntPtr[] small,uint count);
 [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hwnd,int msg,IntPtr wParam,string lParam);

 public static string Text(IntPtr h){int n=GetWindowTextLength(h);if(n<=0)return null;var b=new StringBuilder(n+1);GetWindowText(h,b,b.Capacity);return b.ToString();}
 public static string ClassOf(IntPtr h){var b=new StringBuilder(256);GetClassName(h,b,b.Capacity);return b.ToString();}
 public static bool Cloaked(IntPtr h){int v;try{return DwmGetWindowAttribute(h,14,out v,4)==0&&v!=0;}catch{return false;}}
 public static Rectangle Bounds(IntPtr h){RECT r;try{if(DwmGetWindowAttribute(h,9,out r,Marshal.SizeOf(typeof(RECT)))==0)return r.ToRectangle();}catch{}GetWindowRect(h,out r);return r.ToRectangle();}
 public static int RawPid(IntPtr h){int pid;GetWindowThreadProcessId(h,out pid);return pid;}
 /// <summary>窗口所属进程；UWP 应用的 ApplicationFrameHost 外框会解析到真正的应用进程。</summary>
 public static int PidOf(IntPtr h){int pid=RawPid(h);if(ClassOf(h)!="ApplicationFrameWindow")return pid;int real=pid;EnumChildWindows(h,(c,l)=>{int p=RawPid(c);if(p!=pid){real=p;return false;}return true;},IntPtr.Zero);return real;}
 public static bool IsShell(IntPtr h){string c=ClassOf(h);return c=="Progman"||c=="WorkerW"||c=="Shell_TrayWnd"||c=="Shell_SecondaryTrayWnd";}
 /// <summary>与任务栏/Alt+Tab 相同的“应用窗口”判断。</summary>
 public static bool IsAppWindow(IntPtr h){if(!IsWindowVisible(h)||GetWindow(h,4)!=IntPtr.Zero)return false;int ex=GetWindowLong(h,-20);if((ex&0x80)!=0&&(ex&0x40000)==0)return false;if(GetWindowTextLength(h)==0||Cloaked(h))return false;return !IsShell(h);}
}

public sealed class ProcInfo {
 public int Pid,ParentPid;
 public long Created;
 public string Name,Path,Description,Title;
}

public sealed class ProcGroup {
 public string Key,Match,Name,Path,Description;
 public bool Selectable;
 public int Section;
 public Bitmap Image;
 public ProcGroup Parent;
 public readonly List<ProcInfo> Items=new List<ProcInfo>();
 public readonly List<ProcGroup> Children=new List<ProcGroup>();
 public string Display{get{return string.IsNullOrEmpty(Description)?System.IO.Path.GetFileNameWithoutExtension(Name):Description;}}
 public int Total{get{return Items.Count+Children.Sum(c=>c.Total);}}
 public bool HasWindow{get{return Items.Any(i=>i.Title!=null)||Children.Any(c=>c.HasWindow);}}
}

public static class ProcessScanner {
 static readonly object gate=new object();
 static readonly Dictionary<string,Bitmap> icons=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);
 static readonly Dictionary<string,string> descriptions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 static Bitmap fallback;
 // 这些系统宿主进程启动的程序不归到它们名下，否则几乎所有应用都会挂在 explorer / svchost 下面。
 static readonly HashSet<string> Hosts=new HashSet<string>(new[]{"explorer.exe","svchost.exe","services.exe","wininit.exe","winlogon.exe","csrss.exe","smss.exe","System","Registry","sihost.exe","userinit.exe","RuntimeBroker.exe","dllhost.exe","taskhostw.exe","ApplicationFrameHost.exe","WmiPrvSE.exe","sihost.exe"},StringComparer.OrdinalIgnoreCase);

 public static List<ProcInfo> Scan(){
  var titles=new Dictionary<int,string>();
  Native.EnumWindows((h,l)=>{if(Native.IsAppWindow(h)){int pid=Native.PidOf(h);if(!titles.ContainsKey(pid))titles[pid]=Native.Text(h);}return true;},IntPtr.Zero);
  int self;using(var me=Process.GetCurrentProcess())self=me.Id;
  var list=new List<ProcInfo>();
  IntPtr snap=Native.CreateToolhelp32Snapshot(2,0);
  if(snap==IntPtr.Zero||snap==new IntPtr(-1))throw new IOException("无法读取进程列表。");
  try{
   var e=new Native.PROCESSENTRY32();e.dwSize=(uint)Marshal.SizeOf(typeof(Native.PROCESSENTRY32));
   if(Native.Process32First(snap,ref e))do{
    int pid=(int)e.th32ProcessID;if(pid==0||pid==self)continue;
    var p=new ProcInfo{Pid=pid,ParentPid=(int)e.th32ParentProcessID,Name=e.szExeFile};
    Query(p);string t;if(titles.TryGetValue(pid,out t))p.Title=t;p.Description=Describe(p.Path);IconFor(p.Path);if(p.Path!=null)lock(gate){if(!namePaths.ContainsKey(p.Name))namePaths[p.Name]=p.Path;}list.Add(p);
   }while(Native.Process32Next(snap,ref e));
  }finally{Native.CloseHandle(snap);}
  return list;
 }
 static void Query(ProcInfo p){IntPtr h=Native.OpenProcess(0x1000,false,p.Pid);if(h==IntPtr.Zero)return;try{var b=new StringBuilder(1024);int size=b.Capacity;if(Native.QueryFullProcessImageName(h,0,b,ref size))p.Path=b.ToString(0,size);long c,x,k,u;if(Native.GetProcessTimes(h,out c,out x,out k,out u))p.Created=c;}finally{Native.CloseHandle(h);}}
 static readonly Dictionary<string,string> namePaths=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
 /// <summary>按进程名找一个正在运行（或最近扫描到）的程序路径，用于显示图标和描述。</summary>
 public static string PathForName(string exe){
  if(string.IsNullOrEmpty(exe))return null;lock(gate){string p;if(namePaths.TryGetValue(exe,out p))return p;}
  string found=null;try{foreach(var pr in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe))){using(pr){if(found==null){var info=new ProcInfo{Pid=pr.Id};Query(info);found=info.Path;}}}}catch{}
  if(found!=null)lock(gate)namePaths[exe]=found;return found;
 }
 public static string NameOf(int pid){var p=new ProcInfo{Pid=pid};Query(p);if(p.Path!=null)return Path.GetFileName(p.Path);try{using(var x=Process.GetProcessById(pid))return x.ProcessName+".exe";}catch{return "PID "+pid;}}
 static string Describe(string path){if(path==null)return null;lock(gate){string d;if(descriptions.TryGetValue(path,out d))return d;try{d=FileVersionInfo.GetVersionInfo(path).FileDescription;}catch{}d=string.IsNullOrWhiteSpace(d)?null:d.Trim();descriptions[path]=d;return d;}}
 public static Bitmap IconFor(string path){
  lock(gate){
   Bitmap b;if(path!=null&&icons.TryGetValue(path,out b))return b;b=null;
   if(path!=null){try{var small=new IntPtr[1];Native.ExtractIconEx(path,0,null,small,1);if(small[0]!=IntPtr.Zero){using(var i=Icon.FromHandle(small[0]))b=new Bitmap(i.ToBitmap());Native.DestroyIcon(small[0]);}}catch{}}
   if(b==null){if(fallback==null)fallback=SystemIcons.Application.ToBitmap();b=fallback;}
   if(path!=null)icons[path]=b;return b;
  }
 }

 /// <summary>按程序（进程名或完整路径）分组，并把由某个应用启动的其他程序归到该应用下面，类似任务管理器。</summary>
 public static List<ProcGroup> Group(List<ProcInfo> procs,bool byPath){
  var groups=new Dictionary<string,ProcGroup>(StringComparer.OrdinalIgnoreCase);var owner=new Dictionary<int,ProcGroup>();var byPid=new Dictionary<int,ProcInfo>();
  foreach(var p in procs){
   byPid[p.Pid]=p;string match=byPath?p.Path:p.Name;string key=byPath?(p.Path??("?"+p.Name)):p.Name;ProcGroup g;
   if(!groups.TryGetValue(key,out g)){g=new ProcGroup{Key=key,Match=match,Name=p.Name,Path=p.Path,Description=p.Description,Image=IconFor(p.Path)};g.Selectable=match!=null&&match.EndsWith(".exe",StringComparison.OrdinalIgnoreCase);groups[key]=g;}
   if(g.Path==null&&p.Path!=null){g.Path=p.Path;g.Description=p.Description;g.Image=IconFor(p.Path);}
   g.Items.Add(p);owner[p.Pid]=g;
  }
  foreach(var g in groups.Values){
   g.Items.Sort((a,b)=>a.Created.CompareTo(b.Created));
   ProcGroup parent=null;bool ok=true;
   foreach(var p in g.Items){
    ProcInfo pp;if(!byPid.TryGetValue(p.ParentPid,out pp)){ok=false;break;}
    var pg=owner[pp.Pid];if(pg==g)continue;
    if(pp.Created!=0&&p.Created!=0&&pp.Created>p.Created){ok=false;break;} // PID 被复用
    if(parent==null)parent=pg;else if(parent!=pg){ok=false;break;}
   }
   if(!ok||parent==null||Hosts.Contains(parent.Name))continue;
   for(var a=parent;a!=null;a=a.Parent)if(a==g){ok=false;break;}
   if(ok)g.Parent=parent;
  }
  foreach(var g in groups.Values)if(g.Parent!=null)g.Parent.Children.Add(g);
  var tops=groups.Values.Where(g=>g.Parent==null).ToList();
  string win=Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd('\\')+"\\";
  foreach(var g in tops)g.Section=g.HasWindow?0:(g.Path==null||g.Path.StartsWith(win,StringComparison.OrdinalIgnoreCase)?2:1);
  Sort(tops);return tops;
 }
 static void Sort(List<ProcGroup> list){list.Sort((a,b)=>string.Compare(a.Display,b.Display,StringComparison.CurrentCultureIgnoreCase));foreach(var g in list)Sort(g.Children);}
}
}

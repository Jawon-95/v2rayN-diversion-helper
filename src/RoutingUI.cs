using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly:AssemblyTitle("应用分流助手")]
[assembly:AssemblyDescription("适用于 v2rayN 的独立应用分流配置工具")]
[assembly:AssemblyVersion("1.1.0.0")]
[assembly:AssemblyFileVersion("1.1.0.0")]
namespace AppRoutingManager {
static class Program {
 [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [DllImport("user32.dll")]static extern bool AllowSetForegroundWindow(int processId);
 // 只允许运行一个实例：再次启动时通知已有窗口显示到最前，然后退出。
 [STAThread]static void Main(string[] args){
  bool first;using(var mutex=new System.Threading.Mutex(true,@"Local\AppRoutingManager.Instance",out first))
  using(var signal=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,@"Local\AppRoutingManager.Show")){
   if(!first){try{AllowSetForegroundWindow(-1);}catch{}signal.Set();return;}
   try{try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
    var form=new MainForm(args.Length>0?args[0]:null);
    var wait=System.Threading.ThreadPool.RegisterWaitForSingleObject(signal,(s,t)=>{try{if(form.IsHandleCreated)form.BeginInvoke(new Action(form.Reveal));}catch{}},null,-1,false);
    Application.Run(form);wait.Unregister(null);
   }catch(Exception ex){MessageBox.Show(ex.Message,"应用分流助手",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  }
 }
}

/// <summary>名单卡片的公共部分：标题栏、悬停操作按钮、右键菜单、拖放、空状态。</summary>
public abstract class ListCard : Card {
 const string DragFormat="AppRoutingManager.ListCard";
 public readonly DataGridView Grid=Theme.Grid();
 public readonly string Title;public readonly Color Accent;
 public ListCard Partner;
 public event Action Changed;
 protected readonly Panel Header=new Panel{Dock=DockStyle.Top,BackColor=Theme.Surface};
 protected readonly FlowLayoutPanel Tools=new FlowLayoutPanel{Dock=DockStyle.Right,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,AutoSize=true,BackColor=Theme.Surface,Padding=Padding.Empty,Margin=Padding.Empty};
 readonly Label titleLabel,counter;
 int hover=-1,hoverAction=-1,pressAction=-1,pressRow=-1,dragRow=-1;Point dragStart;
 protected abstract string EmptyText{get;}
 protected abstract string EmptyGlyph{get;}
 protected abstract void RequestMove();
 public abstract void RemoveSelected();
 protected abstract void PaintContent(Graphics g,int row,int column,Rectangle bounds,object value);
 protected virtual bool AcceptFiles(string[] paths){return false;}
 protected virtual void DropFiles(string[] paths){}
 protected virtual bool AcceptText{get{return false;}}
 protected virtual void DropText(string text){}
 protected virtual void BuildMenu(ToolStripItemCollection items){}
 /// <summary>悬停时左侧操作按钮（默认是「移到另一侧」）。</summary>
 protected virtual string PrimaryGlyph{get{return Theme.IconSwitch;}}
 protected virtual string PrimaryText{get{return MoveText;}}
 protected virtual void MenuClosed(){}

 protected ListCard(string title,string tip,Color accent){
  Title=title;Accent=accent;HighlightColor=accent;Dock=DockStyle.Fill;Margin=new Padding(Theme.S(6));
  Header.Height=Theme.S(46);
  titleLabel=Theme.Label(title,12,true);titleLabel.BackColor=Theme.Surface;titleLabel.Cursor=Cursors.Help;Header.Controls.Add(titleLabel);if(!string.IsNullOrEmpty(tip))Theme.Tips.SetToolTip(titleLabel,tip);
  counter=Theme.Label("0",10);counter.ForeColor=Theme.Faint;counter.BackColor=Theme.Surface;Header.Controls.Add(counter);
  Header.Controls.Add(Tools);
  Header.Paint+=(s,e)=>{int d=Theme.S(10);var old=e.Graphics.SmoothingMode;e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using(var b=new SolidBrush(Accent))e.Graphics.FillEllipse(b,0,titleLabel.Top+(titleLabel.Height-d)/2f+Theme.S(1),d,d);e.Graphics.SmoothingMode=old;};
  Action place=()=>{titleLabel.Location=new Point(Theme.S(18),(Header.Height-Theme.S(10)-titleLabel.Height)/2);counter.Location=new Point(titleLabel.Right+Theme.S(6),titleLabel.Bottom-counter.Height-Theme.S(2));foreach(Control c in Tools.Controls)c.Margin=new Padding(Theme.S(6),(Header.Height-Theme.S(10)-c.Height)/2,0,0);};
  Header.Resize+=(s,e)=>place();titleLabel.SizeChanged+=(s,e)=>place();Tools.ControlAdded+=(s,e)=>place();place();
  Grid.ShowCellToolTips=false;
  Grid.CellPainting+=PaintCell;
  Grid.CellMouseEnter+=(s,e)=>SetHover(e.RowIndex);Grid.MouseLeave+=(s,e)=>{SetHover(-1);SetAction(-1);};
  Grid.MouseMove+=GridMouseMove;Grid.MouseDown+=GridMouseDown;Grid.MouseUp+=GridMouseUp;
  Grid.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete&&!Grid.IsCurrentCellInEditMode){RemoveSelected();e.Handled=true;}};
  Grid.Paint+=PaintEmpty;Grid.Resize+=(s,e)=>{if(Grid.RowCount==0)Grid.Invalidate();};
  foreach(Control c in new Control[]{this,Grid,Header}){c.AllowDrop=true;c.DragEnter+=DragOverCard;c.DragOver+=DragOverCard;c.DragLeave+=(s,e)=>Highlight=false;c.DragDrop+=DropOnCard;}
  Controls.Add(Grid);Controls.Add(Header);Grid.BringToFront();
  var outside=new OutsideClick(Grid);Grid.CellBeginEdit+=(s,e)=>Application.AddMessageFilter(outside);Grid.CellEndEdit+=(s,e)=>Application.RemoveMessageFilter(outside);Grid.Disposed+=(s,e)=>Application.RemoveMessageFilter(outside);
 }
 /// <summary>就地编辑时，在输入框以外的任何地方按下鼠标都结束编辑（空白处、卡片背景、标题栏等不会抢焦点的位置也算）。</summary>
 sealed class OutsideClick : IMessageFilter {
  [DllImport("user32.dll")]static extern bool IsChild(IntPtr parent,IntPtr child);
  readonly DataGridView grid;public OutsideClick(DataGridView grid){this.grid=grid;}
  public bool PreFilterMessage(ref Message m){
   int msg=m.Msg;if(msg!=0x201&&msg!=0x204&&msg!=0x207&&msg!=0xA1&&msg!=0xA4&&msg!=0xA7)return false;
   var editor=grid.EditingControl;if(editor==null||!grid.IsCurrentCellInEditMode)return false;
   if(m.HWnd==editor.Handle||IsChild(editor.Handle,m.HWnd))return false;
   grid.EndEdit();return false;
  }
 }
 /// <summary>在标题栏下方追加一块内容（例如输入框）。</summary>
 protected void AddTop(Control c){Controls.Add(c);Header.SendToBack();Grid.BringToFront();}
 protected void SetCount(int n){counter.Text=n.ToString();}
 protected void RaiseChanged(){if(Changed!=null)Changed();}
 public string MoveText{get{return Partner!=null?"移到「"+Partner.Title+"」":"移到另一侧";}}

 // ---------- 悬停操作按钮（移动 / 删除） ----------
 Rectangle ActionRect(int row,int action){var cell=Grid.GetCellDisplayRectangle(Grid.ColumnCount-1,row,false);int size=Theme.S(30);int x=cell.Right-Theme.S(10)-size*(2-action)-Theme.S(2)*(1-action);return new Rectangle(x,cell.Y+(cell.Height-size)/2,size,size);}
 protected int ActionAt(Point p,out int row){var hit=Grid.HitTest(p.X,p.Y);row=hit.RowIndex;if(row<0||Grid.IsCurrentCellInEditMode&&Grid.CurrentCell!=null&&Grid.CurrentCell.RowIndex==row)return -1;for(int a=0;a<2;a++)if(ActionRect(row,a).Contains(p))return a;return -1;}
 void SetHover(int index){if(index==hover)return;int old=hover;hover=index;if(old>=0&&old<Grid.RowCount)Grid.InvalidateRow(old);if(index>=0&&index<Grid.RowCount)Grid.InvalidateRow(index);}
 void SetAction(int action){if(action==hoverAction)return;hoverAction=action;if(hover>=0&&hover<Grid.RowCount)Grid.InvalidateRow(hover);Grid.Cursor=action>=0?Cursors.Hand:Cursors.Default;Theme.Tips.SetToolTip(Grid,action==0?PrimaryText:action==1?"删除":null);}
 void GridMouseMove(object sender,MouseEventArgs e){
  int row;int action=ActionAt(e.Location,out row);if(row!=hover)SetHover(row);SetAction(action);
  if(e.Button==MouseButtons.Left&&dragRow>=0&&!Grid.IsCurrentCellInEditMode){var d=SystemInformation.DragSize;if(Math.Abs(e.X-dragStart.X)>d.Width||Math.Abs(e.Y-dragStart.Y)>d.Height){dragRow=-1;Grid.DoDragDrop(new DataObject(DragFormat,this),DragDropEffects.Move);}}
 }
 void GridMouseDown(object sender,MouseEventArgs e){
  int row;pressAction=ActionAt(e.Location,out row);pressRow=row;dragRow=-1;
  if(e.Button==MouseButtons.Left&&pressAction<0&&row>=0){dragRow=row;dragStart=e.Location;}
  if(e.Button==MouseButtons.Right&&row>=0&&!Grid.Rows[row].Selected){Grid.ClearSelection();Grid.CurrentCell=Grid.Rows[row].Cells[0];Grid.Rows[row].Selected=true;}
 }
 void GridMouseUp(object sender,MouseEventArgs e){
  dragRow=-1;int row;int action=ActionAt(e.Location,out row);
  if(e.Button==MouseButtons.Left&&action>=0&&action==pressAction&&row==pressRow){Grid.ClearSelection();Grid.Rows[row].Selected=true;if(action==0)RequestMove();else RemoveSelected();}
  else if(e.Button==MouseButtons.Right&&row>=0)ShowMenu(e.Location);
  pressAction=-1;
 }
 void ShowMenu(Point at){var m=Theme.Menu();BuildMenu(m.Items);Theme.Item(m.Items,PrimaryText,RequestMove);var del=Theme.Item(m.Items,"删除",RemoveSelected);del.ShortcutKeyDisplayString="Del";m.Closed+=(s,e)=>BeginInvoke(new Action(()=>{m.Dispose();MenuClosed();}));m.Show(Grid,at);}

 // ---------- 绘制 ----------
 void PaintCell(object sender,DataGridViewCellPaintingEventArgs e){
  if(e.RowIndex<0||e.ColumnIndex<0)return;bool selected=(e.State&DataGridViewElementStates.Selected)!=0;
  Theme.PaintRow(Grid,e,selected?Theme.SelectedBack:e.RowIndex==hover?Theme.HoverBack:Theme.Surface);
  var b=e.CellBounds;int right=b.Right;
  if(e.ColumnIndex==Grid.ColumnCount-1&&e.RowIndex==hover&&!(Grid.IsCurrentCellInEditMode&&Grid.CurrentCell!=null&&Grid.CurrentCell.RowIndex==e.RowIndex)){
   for(int a=0;a<2;a++){var r=ActionRect(e.RowIndex,a);bool on=a==hoverAction;if(on)Theme.FillRound(e.Graphics,a==1?Color.FromArgb(254,226,226):Theme.Tint(Partner!=null?Partner.Accent:Theme.Blue,0.14),r,Theme.S(6));
    Theme.Glyph(e.Graphics,a==0?PrimaryGlyph:Theme.IconDelete,10,on?(a==1?Theme.Red:Partner!=null?Partner.Accent:Theme.Blue):Theme.Muted,r);}
   right=ActionRect(e.RowIndex,0).Left-Theme.S(4);
  }
  PaintContent(e.Graphics,e.RowIndex,e.ColumnIndex,new Rectangle(b.X,b.Y,Math.Max(0,right-b.X),b.Height),e.Value);
  e.Handled=true;
 }
 void PaintEmpty(object sender,PaintEventArgs e){
  if(Grid.RowCount>0)return;var r=Grid.ClientRectangle;int top=r.Height/2-Theme.S(44);
  Theme.Glyph(e.Graphics,EmptyGlyph,22,Theme.LineStrong,new Rectangle(0,top,r.Width,Theme.S(44)));
  TextRenderer.DrawText(e.Graphics,EmptyText,Theme.Font(9.5f),new Rectangle(Theme.S(16),top+Theme.S(52),r.Width-Theme.S(32),Theme.S(48)),Theme.Faint,TextFormatFlags.HorizontalCenter|TextFormatFlags.Top|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
 }

 // ---------- 拖放 ----------
 DragDropEffects EffectOf(IDataObject data){
  var source=data.GetData(DragFormat) as ListCard;if(source!=null)return source!=this&&source==Partner?DragDropEffects.Move:DragDropEffects.None;
  if(data.GetDataPresent(DataFormats.FileDrop)){var files=data.GetData(DataFormats.FileDrop) as string[];return files!=null&&AcceptFiles(files)?DragDropEffects.Copy:DragDropEffects.None;}
  if(AcceptText&&data.GetDataPresent(DataFormats.UnicodeText))return DragDropEffects.Copy;
  return DragDropEffects.None;
 }
 void DragOverCard(object sender,DragEventArgs e){e.Effect=EffectOf(e.Data);Highlight=e.Effect!=DragDropEffects.None;}
 void DropOnCard(object sender,DragEventArgs e){
  Highlight=false;if(EffectOf(e.Data)==DragDropEffects.None)return;
  var source=e.Data.GetData(DragFormat) as ListCard;if(source!=null){source.RequestMove();return;}
  if(e.Data.GetDataPresent(DataFormats.FileDrop)){var files=(string[])e.Data.GetData(DataFormats.FileDrop);BeginInvoke(new Action(()=>DropFiles(files)));return;}
  string text=Convert.ToString(e.Data.GetData(DataFormats.UnicodeText));BeginInvoke(new Action(()=>DropText(text)));
 }
}

public sealed class AppPane : ListCard {
 public readonly List<AppEntry> Entries=new List<AppEntry>();
 public event Action<AppPane,bool> AddRequested;
 public event Action<AppPane,string[]> FilesDropped;
 public event Action<AppPane> MoveRequested;
 /// <summary>添加应用时按进程名还是完整路径匹配（由主窗口提供）。</summary>
 public Func<bool> GetByPath;public Action<bool> SetByPath;
 readonly FlatButton add;
 readonly Dictionary<string,Bitmap> icons=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);
 protected override string EmptyText{get{return "把程序或快捷方式拖到这里，或点右上角「添加」";}}
 protected override string EmptyGlyph{get{return Theme.IconApps;}}
 public AppPane(string title,string tip,Color accent):base(title,tip,accent){
  add=new FlatButton("添加",ButtonKind.Secondary,Theme.IconAdd){Dropdown=true,Accent=accent};add.Refit();Tools.Controls.Add(add);
  add.Click+=(s,e)=>{var m=Theme.Menu();Theme.Item(m.Items,"从正在运行的程序中选择…",()=>{if(AddRequested!=null)AddRequested(this,true);});Theme.Item(m.Items,"浏览 .exe 文件…",()=>{if(AddRequested!=null)AddRequested(this,false);});
   if(GetByPath!=null&&SetByPath!=null){bool path=GetByPath();m.Items.Add(new ToolStripSeparator());Theme.Section(m.Items,"匹配方式");Theme.Item(m.Items,"按进程名（所有同名程序）",()=>SetByPath(false),!path);Theme.Item(m.Items,"按完整路径（只匹配这个位置）",()=>SetByPath(true),path);}
   m.Closed+=(a,b)=>BeginInvoke(new Action(m.Dispose));m.Show(add,new Point(add.Width,add.Height+Theme.S(4)),ToolStripDropDownDirection.BelowLeft);};
  Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="app",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=48});
  Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="match",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=52});
  foreach(DataGridViewColumn c in Grid.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
 }
 protected override void RequestMove(){if(MoveRequested!=null)MoveRequested(this);}
 Bitmap IconOf(AppEntry a){
  Bitmap b;if(icons.TryGetValue(a.Match,out b))return b;
  string path=a.ByPath?a.Match:ProcessScanner.PathForName(a.Name);
  if(path!=null&&string.Equals(a.Description,a.Name,StringComparison.OrdinalIgnoreCase))a.Description=AppEntry.Describe(path,a.Name);
  b=ProcessScanner.IconFor(path!=null&&File.Exists(path)?path:null);if(path!=null)icons[a.Match]=b;return b;
 }
 protected override void PaintContent(Graphics g,int row,int column,Rectangle b,object value){
  var a=Grid.Rows[row].Tag as AppEntry;if(a==null)return;int x=b.X+Theme.S(14);
  if(column==0){var icon=IconOf(a);int size=Theme.S(20);if(icon!=null){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(icon,new Rectangle(x,b.Y+(b.Height-size)/2,size,size));}x+=size+Theme.S(10);
   TextRenderer.DrawText(g,Convert.ToString(value),Theme.Font(10),new Rectangle(x,b.Y,b.Right-x-Theme.S(4),b.Height),Theme.Ink,Theme.TextFlags);}
  else TextRenderer.DrawText(g,Convert.ToString(value),Theme.Font(9),new Rectangle(b.X+Theme.S(4),b.Y,b.Right-b.X-Theme.S(8),b.Height),Theme.Muted,Theme.TextFlags|TextFormatFlags.PathEllipsis);
 }
 protected override bool AcceptFiles(string[] paths){return paths.Any(p=>p.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase));}
 protected override void DropFiles(string[] paths){if(FilesDropped!=null)FilesDropped(this,paths);}
 /// <summary>把 .exe 或指向 .exe 的快捷方式解析成程序路径。</summary>
 public static string ResolveExe(string path){
  if(path==null)return null;
  if(path.EndsWith(".lnk",StringComparison.OrdinalIgnoreCase)){try{var t=Type.GetTypeFromProgID("WScript.Shell");object shell=Activator.CreateInstance(t);object link=t.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});path=Convert.ToString(link.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null));}catch{return null;}}
  return path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)?path:null;
 }
 public void Set(IEnumerable<AppEntry> values){Entries.Clear();Entries.AddRange(values);RefreshRows();}
 public void RefreshRows(){Grid.Rows.Clear();foreach(var a in Entries){IconOf(a);int index=Grid.Rows.Add(a.Description,a.ByPath?a.Match:a.Name);Grid.Rows[index].Tag=a;}SetCount(Entries.Count);Grid.ClearSelection();Grid.Invalidate();}
 public List<AppEntry> Selected(){return Grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r=>r.Index).Select(r=>(AppEntry)r.Tag).ToList();}
 public override void RemoveSelected(){var values=Selected();if(values.Count==0)return;foreach(var a in values)Entries.Remove(a);RefreshRows();RaiseChanged();}
 public void Added(){RefreshRows();RaiseChanged();}
}

public sealed class MainForm : Form {
 readonly AppPane direct=new AppPane("全部直连","名单里的应用：访问国内、国外网站都直连",Theme.Green),split=new AppPane("国内外分流","名单里的应用：国内网站直连，国外网站走代理",Theme.Indigo);
 readonly HostPane directHosts=new HostPane("直连","访问这些域名 / IP 时直连\r\n对所有应用生效，优先于应用名单",Theme.Green),proxyHosts=new HostPane("走代理","访问这些域名 / IP 时走代理\r\n对所有应用生效，优先于应用名单",Theme.Indigo);
 readonly Segmented nav=new Segmented("按应用","按域名 / IP");
 readonly ProfilePane profilePane=new ProfilePane();readonly Panel side=new Panel{Dock=DockStyle.Left,BackColor=Theme.Background};
 readonly Pill modePill=new Pill();readonly Label saveState=Theme.Label("",9);readonly Banner banner=new Banner();
 readonly Panel pathBar=new Panel{Dock=DockStyle.Top,BackColor=Theme.Background};readonly InputBox pathBox=new InputBox("没有找到 v2rayN：启动 v2rayN 后点「刷新」，或点「选择」",Theme.IconFolder){Dock=DockStyle.Fill};readonly ToolTip pathError=new ToolTip();
 readonly Panel top=new Panel{Dock=DockStyle.Top,BackColor=Theme.Surface},content=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Background},setup=new Panel{Dock=DockStyle.Fill,BackColor=Theme.Background,Visible=false};
 readonly Control appsPage,hostsPage;
 readonly Timer autoSave=new Timer{Interval=350},modeTimer=new Timer{Interval=3000};
 RoutingStore store;RouteSnapshot snapshot;List<RouteProfile> profiles=new List<RouteProfile>();
 string requestedRoot,profileName,mode,activeId;bool dirty,loading,busy,pickerOpen,checkingMode,byPath,closing;int version;

 public MainForm(string root){
  ConfirmDelete=AskDelete;requestedRoot=root;byPath=Discovery.Pref("matchByPath")=="1";
  Text="应用分流助手";try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
  Font=Theme.Font(10);BackColor=Theme.Background;Size=new Size(Theme.S(1120),Theme.S(740));MinimumSize=new Size(Theme.S(860),Theme.S(540));StartPosition=FormStartPosition.CenterScreen;
  BuildTop();BuildPathBar();
  appsPage=Columns(direct,split);hostsPage=Columns(directHosts,proxyHosts);hostsPage.Visible=false;
  content.Padding=new Padding(Theme.S(14),Theme.S(10),Theme.S(14),Theme.S(14));side.Width=Theme.S(250);side.Padding=new Padding(Theme.S(6));side.Controls.Add(profilePane);
  content.Controls.Add(hostsPage);content.Controls.Add(appsPage);content.Controls.Add(setup);content.Controls.Add(side);BuildSetup();
  Controls.Add(content);Controls.Add(banner);Controls.Add(pathBar);Controls.Add(top);
  nav.Changed+=()=>{appsPage.Visible=nav.SelectedIndex==0;hostsPage.Visible=nav.SelectedIndex==1;};
  direct.Partner=split;split.Partner=direct;directHosts.Other=proxyHosts;proxyHosts.Other=directHosts;
  profilePane.SwitchRequested+=async x=>{var p=profiles.FirstOrDefault(y=>y.Id==x.Id);if(p!=null)await SwitchProfile(p);};profilePane.AddRequested+=NewProfile;profilePane.DeleteRequested+=DeleteProfile;profilePane.Renamer=RenameProfile;
  foreach(var p in new[]{direct,split}){p.GetByPath=()=>byPath;p.SetByPath=v=>ByPath=v;p.AddRequested+=AddApps;p.FilesDropped+=AddFiles;p.MoveRequested+=MoveApps;p.Changed+=()=>MarkDirty();}
  foreach(var p in new[]{directHosts,proxyHosts}){p.MoveRequested+=MoveHosts;p.Changed+=()=>MarkDirty();}
  autoSave.Tick+=async(s,e)=>await AutoSave();modeTimer.Tick+=async(s,e)=>await CheckMode();
  FormClosing+=OnClosing;FormClosed+=(s,e)=>{modeTimer.Stop();autoSave.Stop();};
  Shown+=async(s,e)=>{await Detect(false);modeTimer.Start();};
  UpdateTop();
 }
 static Control Columns(Control left,Control right){var t=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Theme.Background};t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));t.RowStyles.Add(new RowStyle(SizeType.Percent,100));t.Controls.Add(left,0,0);t.Controls.Add(right,1,0);return t;}
 void BuildTop(){
  top.Height=Theme.S(64);top.Paint+=(s,e)=>{using(var p=new Pen(Theme.Line))e.Graphics.DrawLine(p,0,top.Height-1,top.Width,top.Height-1);};
  var logo=new PictureBox{Size=new Size(Theme.S(28),Theme.S(28)),SizeMode=PictureBoxSizeMode.Zoom,BackColor=Theme.Surface};try{logo.Image=new Icon(Icon,Theme.S(32),Theme.S(32)).ToBitmap();}catch{}
  var title=Theme.Label("应用分流助手",13,true);title.BackColor=Theme.Surface;
  saveState.ForeColor=Theme.Faint;saveState.BackColor=Theme.Surface;
  foreach(Control c in new Control[]{logo,title,nav,saveState,modePill})top.Controls.Add(c);
  Action place=()=>{int h=top.Height-1;logo.Location=new Point(Theme.S(20),(h-logo.Height)/2);title.Location=new Point(logo.Right+Theme.S(10),(h-title.Height)/2);nav.Location=new Point(title.Right+Theme.S(28),(h-nav.Height)/2);
   int x=top.Width-Theme.S(20);
   modePill.Location=new Point(x-modePill.Width,(h-modePill.Height)/2);if(modePill.Visible)x=modePill.Left-Theme.S(14);saveState.Location=new Point(x-saveState.Width,(h-saveState.Height)/2);};
  top.Resize+=(s,e)=>place();foreach(Control c in new Control[]{modePill,saveState,title})c.SizeChanged+=(s,e)=>place();modePill.VisibleChanged+=(s,e)=>place();place();
 }
 // 第一行：v2rayN 位置。启动时自动检测正在运行的 v2rayN，「刷新」重新检测，「选择」手动指定。
 void BuildPathBar(){
  pathBar.Height=Theme.S(52);pathBar.Padding=new Padding(Theme.S(16),Theme.S(8),Theme.S(14),Theme.S(8));
  pathBar.Paint+=(s,e)=>{using(var p=new Pen(Theme.Line))e.Graphics.DrawLine(p,0,pathBar.Height-1,pathBar.Width,pathBar.Height-1);};
  var label=new Label{Text="v2rayN 位置",Dock=DockStyle.Left,AutoSize=false,Width=Theme.S(92),TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Muted,Font=Theme.Font(9.5f),BackColor=Theme.Background};
  var refresh=new FlatButton("刷新",ButtonKind.Secondary,Theme.IconRefresh){Dock=DockStyle.Right};Theme.Tips.SetToolTip(refresh,"重新检测正在运行的 v2rayN 并读取规则");refresh.Click+=async(s,e)=>await Detect(true);
  var open=new FlatButton("选择…",ButtonKind.Secondary,Theme.IconFolder){Dock=DockStyle.Right};Theme.Tips.SetToolTip(open,"手动选择 v2rayN.exe");open.Click+=(s,e)=>ChooseFolder();
  Func<Control> gap=()=>new Panel{Dock=DockStyle.Right,Width=Theme.S(8),BackColor=Theme.Background};
  pathBar.Controls.Add(pathBox);pathBar.Controls.Add(gap());pathBar.Controls.Add(refresh);pathBar.Controls.Add(gap());pathBar.Controls.Add(open);pathBar.Controls.Add(label);
  pathBox.Box.KeyDown+=(s,e)=>{
   if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;UsePath(pathBox.Box.Text);}
   else if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;pathBox.Box.Text=store!=null?store.Root:"";pathBox.Box.SelectAll();}
  };
  pathBox.Box.TextChanged+=(s,e)=>pathError.Hide(pathBox);
  pathBox.Box.Leave+=(s,e)=>{if(store!=null&&!string.Equals(pathBox.Box.Text.Trim(),store.Root,StringComparison.OrdinalIgnoreCase))pathBox.Box.Text=store.Root;};
 }
 /// <summary>把已运行实例的窗口显示到最前（再次启动程序时调用）。</summary>
 internal void Reveal(){if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Show();Activate();var owned=OwnedForms.FirstOrDefault(f=>f.Visible);if(owned!=null)owned.Activate();}
 void BuildSetup(){
  var title=Theme.Label("没有找到 v2rayN",14,true);var detail=Theme.Label("启动 v2rayN 后点上方「刷新」，或手动选择 v2rayN.exe（支持 7.x）",10);detail.ForeColor=Theme.Muted;
  var choose=new FlatButton("选择 v2rayN.exe",ButtonKind.Primary,Theme.IconFolder);choose.Height=Theme.S(38);choose.Refit();choose.Click+=(s,e)=>ChooseFolder();
  setup.Controls.Add(title);setup.Controls.Add(detail);setup.Controls.Add(choose);
  setup.Resize+=(s,e)=>{int y=setup.Height/2-Theme.S(70);title.Location=new Point((setup.Width-title.Width)/2,y);detail.Location=new Point((setup.Width-detail.Width)/2,title.Bottom+Theme.S(8));choose.Location=new Point((setup.Width-choose.Width)/2,detail.Bottom+Theme.S(22));};
 }
 void ShowPages(bool connected){setup.Visible=!connected;side.Visible=connected;appsPage.Visible=connected&&nav.SelectedIndex==0;hostsPage.Visible=connected&&nav.SelectedIndex==1;nav.Visible=connected;UpdateTop();}

 // ---------- 顶栏、菜单与提示条 ----------
 void UpdateTop(){
  RefreshProfilePane();
  modePill.Visible=store!=null;
  if(mode==null)modePill.Set("v2rayN 未运行",Theme.Faint);
  else if(ClashMode.Warning(mode)!=null)modePill.Set(string.Equals(mode,"Direct",StringComparison.OrdinalIgnoreCase)?"直连模式":"全局模式",Theme.Red);
  else modePill.Set("规则模式",Theme.Green);
  Theme.Tips.SetToolTip(modePill,mode==null?"没有检测到正在运行的 v2rayN":ClashMode.Warning(mode)??"v2rayN 正在使用规则模式，分流规则会生效");
  string warning=store==null?null:ClashMode.Warning(mode);
  if(warning!=null)banner.Set(warning,"在 v2rayN 左侧「当前代理」页，把「规则模式」切换为 Rule");
  else if(store!=null&&mode!=null&&activeId!=null&&snapshot!=null&&snapshot.Profile!=null&&activeId!=snapshot.Profile.Id)banner.Set("v2rayN 没有在使用「"+snapshot.Profile.Name+"」","在 v2rayN 主界面底部「路由」中选择它");
  else banner.Set(null,null);
 }
 void RefreshProfilePane(){
  if(store==null){profilePane.Set(new ProfilePane.Item[0],null);return;}
  var items=profiles.Select(p=>new ProfilePane.Item{Id=p.Id,Name=snapshot!=null&&snapshot.Profile!=null&&snapshot.Profile.Id==p.Id?profileName??p.Name:p.Name,Active=activeId!=null?p.Id==activeId:p.Active}).ToList();
  if(snapshot!=null&&snapshot.IsNew)items.Add(new ProfilePane.Item{Name=profileName,Unsaved=true});
  profilePane.Set(items,snapshot!=null&&!snapshot.IsNew?snapshot.Profile.Id:null);
 }
 internal bool ByPath{get{return byPath;}set{byPath=value;Discovery.SetPref("matchByPath",value?"1":"0");}}

 // ---------- 保存 ----------
 void MarkDirty(){if(loading)return;dirty=true;version++;SetSaveState("有修改，即将保存…");ScheduleSave();}
 // 主窗口的修改都会自动保存；进程选择窗口打开期间由它的「保存」按钮统一提交。
 void ScheduleSave(){if(loading||pickerOpen||!dirty||store==null)return;autoSave.Stop();autoSave.Start();}
 async Task AutoSave(){autoSave.Stop();if(!dirty||store==null||pickerOpen)return;if(busy||directHosts.Grid.IsCurrentCellInEditMode||proxyHosts.Grid.IsCurrentCellInEditMode){autoSave.Start();return;}await Save();}
 void SetSaveState(string text){saveState.Text=text;}
 internal bool IsDirty{get{return dirty;}}
 internal async Task<bool> Save(bool notify=true){
  if(store==null||snapshot==null){ChooseFolder();return false;}
  string newName=profileName;var d=direct.Entries.ToList();var s=split.Entries.ToList();var dh=directHosts.Entries.ToList();var ph=proxyHosts.Entries.ToList();int v=version;
  SetBusy(true);SetSaveState("正在保存…");
  try{
   var result=await Task.Run(()=>store.Save(snapshot,newName,d,s,true,dh,ph));
   bool unchanged=v==version;loading=true;try{LoadProfiles(result.Profile.Id,unchanged);}finally{loading=false;}
   if(unchanged)dirty=false;else ScheduleSave();
   SetSaveState("✓ 已保存，重启 v2rayN 后生效");
   if(notify)Toast.Notify(this,"已保存","重启 v2rayN 后生效",true);
   return true;
  }catch(Exception ex){SetSaveState("保存失败");ShowError(ex);return false;}
  finally{SetBusy(false);}
 }
 void SetBusy(bool value){busy=value;}
 void OnClosing(object sender,FormClosingEventArgs e){
  if(busy){e.Cancel=true;return;}
  if(!dirty||closing)return;
  e.Cancel=true;closing=true;autoSave.Stop();
  BeginInvoke(new Action(async()=>{if(await Save(false))Close();else if(MessageBox.Show(this,"修改没有保存成功，仍要关闭吗？","应用分流助手",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes)Close();else closing=false;}));
 }

 // ---------- 连接 v2rayN 与规则集 ----------
 /// <summary>检测 v2rayN：优先正在运行的进程。启动时找不到会再查上次的位置与常见目录；手动刷新只认正在运行的。</summary>
 async Task Detect(bool manual){
  if(busy)return;if(manual&&dirty&&store!=null&&!await Save(false))return;
  SetBusy(true);string asked=manual?null:requestedRoot;string found;
  try{found=await Task.Run(()=>asked!=null&&RoutingStore.IsRoot(asked)?asked:manual?Discovery.Running():Discovery.Find());}finally{SetBusy(false);}
  if(IsDisposed)return;
  if(found!=null)Connect(found,found!=asked);
  else if(!manual)ShowPages(false);
  else Toast.Notify(this,"没有检测到正在运行的 v2rayN","请先启动 v2rayN，或点「选择」手动指定",false);
 }
 void ChooseFolder(){if(busy)return;using(var d=new OpenFileDialog{Title="选择 v2rayN.exe",Filter="v2rayN 程序 (v2rayN.exe)|v2rayN.exe",CheckFileExists=true}){if(store!=null)d.InitialDirectory=store.Root;if(d.ShowDialog(this)==DialogResult.OK)UsePath(d.FileName);}}
 /// <summary>使用手动指定的位置（v2rayN 目录或 v2rayN.exe）。</summary>
 async void UsePath(string text){
  string path=(text??"").Trim().Trim('"');if(path.Length==0)return;
  if(path.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))path=Path.GetDirectoryName(path);
  if(!RoutingStore.IsRoot(path)){pathError.Show("这里没有 v2rayN（需要包含 v2rayN.exe 和 guiConfigs 文件夹）",pathBox,0,pathBox.Height+Theme.S(4),4000);return;}
  if(busy)return;if(dirty&&store!=null&&!await Save(false))return;Connect(path,true);
 }
 void Connect(string root,bool remember){
  try{var st=new RoutingStore(root);string keep=store!=null&&string.Equals(store.Root,st.Root,StringComparison.OrdinalIgnoreCase)&&snapshot!=null&&snapshot.Profile!=null?snapshot.Profile.Id:null;store=st;if(remember)Discovery.Remember(root);dirty=false;pathBox.Box.Text=st.Root;LoadProfiles(keep,true);ShowPages(true);var task=CheckMode();}
  catch(Exception ex){ShowError(ex);}
 }
 void LoadProfiles(string preferred,bool apply){
  bool was=loading;loading=true;
  try{
   profiles=store.Profiles().Where(x=>x.Managed).ToList();
   var selected=profiles.FirstOrDefault(x=>x.Id==preferred)??profiles.FirstOrDefault(x=>x.Active)??profiles.FirstOrDefault();
   snapshot=store.Load(selected);var active=store.Profiles().FirstOrDefault(x=>x.Active);activeId=active!=null?active.Id:null;
   if(apply)ApplySnapshot();else profileName=snapshot.IsNew?profileName:snapshot.Profile.Name;
  }finally{loading=was;}
  UpdateTop();
 }
 void ApplySnapshot(){profileName=snapshot.IsNew?"全局 + 应用分流":snapshot.Profile.Name;direct.Set(snapshot.Direct);split.Set(snapshot.Split);directHosts.Set(snapshot.DirectHosts);proxyHosts.Set(snapshot.ProxyHosts);dirty=false;UpdateTop();}
 async Task SwitchProfile(RouteProfile profile){if(busy||store==null||dirty&&!await Save(false)){profilePane.ShowCurrent();return;}try{LoadProfiles(profile.Id,true);}catch(Exception ex){ShowError(ex);}}
 /// <summary>新建规则集：立即保存到 v2rayN，然后进入重命名。</summary>
 async void NewProfile(){
  if(busy||store==null)return;if(dirty&&!await Save(false))return;
  var names=profiles.Select(x=>x.Name).ToList();string name="新规则集";for(int i=2;names.Contains(name);i++)name="新规则集 "+i;
  var st=store;SetBusy(true);SetSaveState("正在新建…");
  try{var fresh=st.Load(null);var result=await Task.Run(()=>st.Save(fresh,name,fresh.Direct,fresh.Split,true,new List<HostEntry>(),new List<HostEntry>()));
   if(st!=store)return;dirty=false;LoadProfiles(result.Profile.Id,true);SetSaveState("✓ 已新建，可直接输入名称");
   int index=profilePane.Items.FindIndex(x=>x.Id==result.Profile.Id);profilePane.Edit(index);}
  catch(Exception ex){SetSaveState("");ShowError(ex);}
  finally{SetBusy(false);}
 }
 /// <summary>重命名规则集；返回错误信息或 null。</summary>
 string RenameProfile(ProfilePane.Item item,string name){
  if(store==null)return null;name=name.Trim();
  if(profiles.Any(p=>p.Name==name&&p.Id!=item.Id))return "已经有同名规则集";
  if(item.Unsaved){if(name.Length>100)return "名称最多 100 个字";profileName=name;UpdateTop();MarkDirty();return null;}
  if(busy)return "正在保存，请稍后再试";
  try{store.Rename(item.Id,name);}catch(Exception ex){return ex.Message;}
  if(snapshot!=null&&snapshot.Profile!=null&&snapshot.Profile.Id==item.Id){snapshot.Profile.Name=name;Json.Set(snapshot.Profile.Raw,"Remarks",name);profileName=name;}
  RefreshProfileList();SetSaveState("✓ 已重命名");return null;
 }
 /// <summary>删除前确认（测试时可替换）。</summary>
 internal Func<string,bool> ConfirmDelete;
 bool AskDelete(string name){return MessageBox.Show(this,"删除规则集「"+name+"」？\r\n\r\n它的应用和域名名单会一起删除（会自动备份）。","删除规则集",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes;}
 async void DeleteProfile(ProfilePane.Item item){
  if(store==null||busy)return;
  if(item.Unsaved){Toast.Notify(this,"这个规则集还没有保存","修改任意名单后会自动保存",false);return;}
  if(item.Active){Toast.Notify(this,"不能删除正在使用的规则集","先在 v2rayN 主界面底部「路由」中换用其他规则",false);return;}
  if(!ConfirmDelete(item.Name))return;
  bool current=snapshot!=null&&snapshot.Profile!=null&&snapshot.Profile.Id==item.Id;
  if(!current&&dirty&&!await Save(false))return;
  if(current){autoSave.Stop();dirty=false;}
  var st=store;SetBusy(true);
  try{await Task.Run(()=>st.Delete(item.Id));if(st!=store)return;if(current)LoadProfiles(null,true);else RefreshProfileList();SetSaveState("✓ 已删除「"+item.Name+"」");}
  catch(Exception ex){ShowError(ex);if(current)LoadProfiles(item.Id,true);}
  finally{SetBusy(false);}
 }
 void RefreshProfileList(){try{profiles=store.Profiles().Where(x=>x.Managed).ToList();}catch{}UpdateTop();}
 // 定期只读查询 v2rayN 的运行模式与正在使用的规则集；全局 / 直连模式下分流不生效，需要醒目提示。
 internal async Task CheckMode(){
  if(checkingMode||store==null||IsDisposed)return;checkingMode=true;
  try{var st=store;var r=await Task.Run(()=>{string m=ClashMode.Query(st.Root);string a=null;try{var p=st.Profiles().FirstOrDefault(x=>x.Active);a=p!=null?p.Id:null;}catch{}return Tuple.Create(m,a);});
   if(IsDisposed||st!=store)return;mode=r.Item1;if(r.Item2!=null)activeId=r.Item2;UpdateTop();}
  finally{checkingMode=false;}
 }

 // ---------- 名单操作 ----------
 void AddApps(AppPane target,bool running){
  if(store==null){ChooseFolder();return;}
  if(running){autoSave.Stop();pickerOpen=true;try{using(var p=new ProcessPicker(this,target,target==direct?split:direct,byPath)){p.ShowDialog(this);if(p.ByPath!=byPath)ByPath=p.ByPath;}}finally{pickerOpen=false;}ScheduleSave();return;}
  using(var f=new OpenFileDialog{Title="选择要加入「"+target.Title+"」的程序",Filter="应用程序 (*.exe)|*.exe",Multiselect=true}){if(f.ShowDialog(this)==DialogResult.OK)AddFiles(target,f.FileNames);}
 }
 void AddFiles(AppPane target,string[] paths){
  if(store==null){ChooseFolder();return;}int added=0;
  foreach(string raw in paths){string path=AppPane.ResolveExe(raw);if(path==null)continue;try{var entry=new AppEntry(byPath?path:Path.GetFileName(path));entry.Description=AppEntry.Describe(path,entry.Name);Insert(target,entry);added++;}catch{}}
  if(added==0)Toast.Notify(this,"没有可添加的程序","只能添加 .exe 程序或指向它的快捷方式",false);
 }
 internal void Insert(AppPane target,AppEntry entry){AppPane other=target==direct?split:direct;other.Entries.RemoveAll(x=>string.Equals(x.Match,entry.Match,StringComparison.OrdinalIgnoreCase));RoutingStore.AddUnique(target.Entries,entry);other.RefreshRows();target.Added();}
 void MoveApps(AppPane source){var chosen=source.Selected();if(chosen.Count==0)return;AppPane destination=source==direct?split:direct;foreach(var entry in chosen){source.Entries.Remove(entry);RoutingStore.AddUnique(destination.Entries,entry);}source.RefreshRows();destination.Added();}
 void MoveHosts(HostPane source){var chosen=source.Selected();if(chosen.Count==0)return;HostPane destination=source==directHosts?proxyHosts:directHosts;foreach(var x in chosen){source.Entries.Remove(x);HostEntry.AddUnique(destination.Entries,x);}source.RefreshRows();destination.Added();}
 void ShowError(Exception ex){MessageBox.Show(Form.ActiveForm??this,ex.Message,"应用分流助手",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
}
}

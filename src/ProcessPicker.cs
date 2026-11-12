using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace AppRoutingManager {
/// <summary>任务管理器风格的进程选择窗口：分组、图标、可展开子进程，勾选即加入名单，可直接保存。</summary>
public sealed class ProcessPicker : Form {
 sealed class Row { public int Kind,Section,Depth,Count; public ProcGroup Group; public ProcInfo Proc; } // Kind: 0 分组标题 1 程序 2 单个进程
 sealed class Change { public AppEntry Entry; public bool Add; }
 struct Parts { public Rectangle Chevron,Check,Icon; public int TextX; }
 static readonly string[] SectionNames={"应用","后台进程","Windows 进程"};
 static readonly Color SelectedBack=Theme.SelectedBack,HoverBack=Theme.HoverBack,PendingBack=Color.FromArgb(255,248,225);

 readonly MainForm host;readonly AppPane target,other;bool byPath,saving;
 readonly DataGridView grid=Theme.Grid();readonly TextBox search=new TextBox();readonly ComboBox mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};
 readonly Label status;readonly Button save,close,refresh;readonly WindowFinder finder=new WindowFinder();
 List<ProcInfo> processes=new List<ProcInfo>();List<ProcGroup> tops=new List<ProcGroup>();readonly List<Row> rows=new List<Row>();
 readonly HashSet<string> expanded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 readonly Dictionary<string,Change> pending=new Dictionary<string,Change>(StringComparer.OrdinalIgnoreCase);
 readonly float scale;readonly Size glyph;readonly Font sectionFont,boldFont;string filter="";int hover=-1;Task scanTask;

 int S(double v){return (int)Math.Round(v*scale);}

 public ProcessPicker(MainForm host,AppPane target,AppPane other,bool path){
  this.host=host;this.target=target;this.other=other;byPath=path;
  Text="从进程列表选择 · "+target.Title;Font=new Font("Microsoft YaHei UI",10);BackColor=Theme.Background;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;KeyPreview=true;AllowTransparency=true;
  using(var g=CreateGraphics()){scale=g.DpiX/96f;glyph=CheckBoxRenderer.GetGlyphSize(g,CheckBoxState.UncheckedNormal);}
  try{Icon=host.Icon;}catch{}
  Size=new Size(S(1060),S(740));MinimumSize=new Size(S(820),S(520));
  sectionFont=new Font("Microsoft YaHei UI",11.5f);boldFont=new Font("Microsoft YaHei UI",10,FontStyle.Bold);

  // 顶部：标题、说明、搜索 / 匹配方式 / 准星 / 刷新
  var top=new Panel{Dock=DockStyle.Top,Height=S(122),Padding=new Padding(S(16),S(10),S(16),S(10))};
  var title=Theme.Label("勾选要加入「"+target.Title+"」的应用",13,true);title.ForeColor=target.Accent;title.Location=new Point(S(14),S(10));top.Controls.Add(title);
  var sub=Theme.Label("勾选或取消后，点右下角「保存」直接写入 v2rayN。也可以按住右侧的准星图标，拖到目标窗口上松开，即可在列表中定位到该进程。",9);sub.ForeColor=Theme.Muted;sub.Location=new Point(S(16),S(42));top.Controls.Add(sub);
  var tools=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=S(44),ColumnCount=5,RowCount=1,Margin=Padding.Empty};
  tools.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(200)));tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(48)));tools.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));tools.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(100)));
  search.Dock=DockStyle.Fill;search.Margin=new Padding(0,S(8),S(10),0);search.HandleCreated+=(s,e)=>Native.SendMessage(search.Handle,0x1501,(IntPtr)1,"搜索名称、进程名、PID、窗口标题或路径");tools.Controls.Add(search,0,0);
  mode.Items.AddRange(new object[]{"按进程名称匹配","按完整路径匹配"});mode.SelectedIndex=byPath?1:0;mode.Dock=DockStyle.Fill;mode.Margin=new Padding(0,S(8),S(10),0);tools.Controls.Add(mode,1,0);
  finder.Size=new Size(S(40),S(40));finder.Margin=new Padding(0,S(2),S(6),0);tools.Controls.Add(finder,2,0);
  var finderText=Theme.Label("拖到窗口",9);finderText.ForeColor=Theme.Muted;finderText.Margin=new Padding(0,S(13),S(12),0);tools.Controls.Add(finderText,3,0);
  refresh=Theme.Button("刷新");refresh.Dock=DockStyle.Fill;refresh.Margin=new Padding(0,S(4),0,S(2));tools.Controls.Add(refresh,4,0);
  top.Controls.Add(tools);Controls.Add(top);
  var tip=new ToolTip();tip.SetToolTip(finder,"按住并拖动到任意窗口上，松开即可选中该窗口所属的进程");

  // 底部：状态 + 关闭 / 保存
  var bottom=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=S(60),ColumnCount=3,RowCount=1,Padding=new Padding(S(16),S(10),S(16),S(12))};
  bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(110)));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,S(150)));
  status=Theme.Label("正在读取进程…",9);status.ForeColor=Theme.Muted;status.AutoSize=false;status.Dock=DockStyle.Fill;status.TextAlign=System.Drawing.ContentAlignment.MiddleLeft;status.AutoEllipsis=true;bottom.Controls.Add(status,0,0);
  close=Theme.Button("关闭");close.Dock=DockStyle.Fill;bottom.Controls.Add(close,1,0);
  save=Theme.Button("保存",true);save.Dock=DockStyle.Fill;save.EnabledChanged+=(s,e)=>{save.BackColor=save.Enabled?Theme.Blue:Color.FromArgb(203,213,225);save.FlatAppearance.BorderColor=save.BackColor;};save.Enabled=false;bottom.Controls.Add(save,2,0);
  Controls.Add(bottom);

  // 进程树
  grid.VirtualMode=true;grid.MultiSelect=false;grid.CellBorderStyle=DataGridViewCellBorderStyle.None;grid.ShowCellToolTips=true;grid.RowTemplate.Height=S(30);grid.ColumnHeadersHeight=S(34);grid.StandardTab=true;
  AddColumn("名称",0,44);AddColumn("分流状态",S(150),0);AddColumn("PID",S(76),0);AddColumn("进程",S(170),0);AddColumn("路径",0,56);
  grid.CellValueNeeded+=(s,e)=>{if(e.RowIndex<rows.Count)e.Value=Value(rows[e.RowIndex],e.ColumnIndex);};
  grid.RowHeightInfoNeeded+=(s,e)=>{if(e.RowIndex<rows.Count&&rows[e.RowIndex].Kind==0)e.Height=S(38);};
  grid.CellToolTipTextNeeded+=(s,e)=>{if(e.RowIndex>=0&&e.RowIndex<rows.Count&&e.ColumnIndex==0){var r=rows[e.RowIndex];if(r.Kind==1)e.ToolTipText=r.Group.Path??r.Group.Name;else if(r.Kind==2)e.ToolTipText=r.Proc.Path??r.Proc.Name;}};
  grid.CellPainting+=PaintCell;grid.CellMouseDown+=CellMouseDown;grid.CellMouseDoubleClick+=CellDoubleClick;grid.KeyDown+=GridKey;
  grid.CellMouseEnter+=(s,e)=>SetHover(e.RowIndex);grid.MouseLeave+=(s,e)=>SetHover(-1);
  Controls.Add(grid);grid.BringToFront();

  search.TextChanged+=(s,e)=>Render();
  mode.SelectedIndexChanged+=(s,e)=>{byPath=mode.SelectedIndex==1;Regroup();};
  refresh.Click+=async(s,e)=>await Reload();
  save.Click+=async(s,e)=>await SaveChanges(this);
  close.Click+=(s,e)=>Close();CancelButton=close;
  finder.Hovering+=t=>status.Text=t;
  finder.DragChanged+=on=>{Opacity=on?0.4:1;if(!on)UpdateState();};
  finder.Picked+=async pid=>await Pick(pid);
  FormClosing+=OnClosing;
  Shown+=async(s,e)=>{search.Focus();await Reload();};
  FormClosed+=(s,e)=>{sectionFont.Dispose();boldFont.Dispose();};
 }

 void AddColumn(string header,int width,int fill){var c=new DataGridViewTextBoxColumn{HeaderText=header,SortMode=DataGridViewColumnSortMode.NotSortable};if(fill>0){c.AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill;c.FillWeight=fill;c.MinimumWidth=S(160);}else c.Width=width;grid.Columns.Add(c);}

 // ---------- 数据 ----------
 Task Reload(){if(scanTask!=null&&!scanTask.IsCompleted)return scanTask;scanTask=DoReload();return scanTask;}
 async Task DoReload(){
  refresh.Enabled=false;status.Text="正在读取进程…";
  try{processes=await Task.Run(()=>ProcessScanner.Scan());}
  catch(Exception ex){status.Text="读取进程失败："+ex.Message;}
  finally{if(!IsDisposed)refresh.Enabled=true;}
  if(!IsDisposed)Regroup();
 }
 void Regroup(){tops=ProcessScanner.Group(processes,byPath);Render();UpdateState();}

 static bool Has(string text,string f){return text!=null&&text.IndexOf(f,StringComparison.OrdinalIgnoreCase)>=0;}
 bool Matches(ProcGroup g){return Has(g.Display,filter)||Has(g.Name,filter)||Has(g.Path,filter)||g.Items.Any(i=>i.Pid.ToString()==filter||Has(i.Title,filter));}
 bool IsShown(ProcGroup g){return filter.Length==0||Matches(g)||g.Children.Any(IsShown);}
 static bool Expandable(ProcGroup g){return g.Items.Count>1||g.Children.Count>0;}
 bool IsOpen(ProcGroup g){return expanded.Contains(g.Key)||(filter.Length>0&&g.Children.Any(IsShown));}

 void Render(){
  filter=search.Text.Trim();
  var cur=Current();ProcGroup selGroup=cur!=null?cur.Group:null;int selPid=cur!=null&&cur.Proc!=null?cur.Proc.Pid:0;int selSection=cur!=null&&cur.Kind==0?cur.Section:-1;
  int first=grid.RowCount>0?grid.FirstDisplayedScrollingRowIndex:0;
  rows.Clear();
  for(int s=0;s<SectionNames.Length;s++){var list=tops.Where(g=>g.Section==s&&IsShown(g)).ToList();if(list.Count==0)continue;rows.Add(new Row{Kind=0,Section=s,Count=list.Count});foreach(var g in list)AddGroup(g,0);}
  hover=-1;grid.RowCount=0;grid.RowCount=rows.Count;
  int index=rows.FindIndex(r=>selPid!=0?(r.Proc!=null&&r.Proc.Pid==selPid):selGroup!=null?(r.Kind==1&&selGroup!=null&&r.Group.Key==selGroup.Key):(r.Kind==0&&r.Section==selSection));
  if(rows.Count>0&&first>=0)grid.FirstDisplayedScrollingRowIndex=Math.Min(first,rows.Count-1);
  if(index>=0)Select(index,false);else grid.ClearSelection();
  grid.Invalidate();
 }
 void AddGroup(ProcGroup g,int depth){
  rows.Add(new Row{Kind=1,Group=g,Depth=depth,Section=g.Section});
  if(!Expandable(g)||!IsOpen(g))return;
  if(g.Items.Count>1)foreach(var p in g.Items)rows.Add(new Row{Kind=2,Group=g,Proc=p,Depth=depth+1});
  foreach(var c in g.Children)if(IsShown(c))AddGroup(c,depth+1);
 }
 Row Current(){var c=grid.CurrentCell;return c!=null&&c.RowIndex>=0&&c.RowIndex<rows.Count?rows[c.RowIndex]:null;}
 void Select(int index,bool reveal){
  if(reveal){int visible=grid.DisplayedRowCount(false);int first=grid.FirstDisplayedScrollingRowIndex;if(index<first+1||index>=first+visible-1)grid.FirstDisplayedScrollingRowIndex=Math.Max(0,index-visible/2);}
  grid.CurrentCell=grid[0,index];grid.Rows[index].Selected=true;
 }

 object Value(Row r,int col){
  if(r.Kind==0)return col==0?SectionNames[r.Section]+" ("+r.Count+")":null;
  if(r.Kind==1){var g=r.Group;switch(col){case 0:return g.Display+(g.Total>1?" ("+g.Total+")":"");case 1:Color c;return StatusOf(g,out c);case 2:return g.Items.Count==1?(object)g.Items[0].Pid:null;case 3:return g.Name;default:return g.Path??"（路径无法读取）";}}
  var p=r.Proc;switch(col){case 0:return p.Title??r.Group.Display;case 2:return p.Pid;case 3:return p.Name;case 4:return p.Path;default:return null;}
 }

 // ---------- 勾选 ----------
 static bool InList(AppPane pane,string match){return match!=null&&pane.Entries.Any(x=>string.Equals(x.Match,match,StringComparison.OrdinalIgnoreCase));}
 bool IsChecked(ProcGroup g){Change c;if(g.Match!=null&&pending.TryGetValue(g.Match,out c))return c.Add;return InList(target,g.Match);}
 string StatusOf(ProcGroup g,out Color color){
  color=Theme.Muted;
  if(!g.Selectable)return byPath&&g.Path==null?"路径无法读取":"";
  bool changed=pending.ContainsKey(g.Match);
  if(IsChecked(g)){color=target.Accent;return "✓ "+target.Title+(changed?" · 待保存":"");}
  if(InList(other,g.Match)){color=other.Accent;return other.Title;}
  foreach(var pane in new[]{target,other}){var alt=pane.Entries.FirstOrDefault(x=>x.ByPath!=byPath&&string.Equals(x.Name,g.Name,StringComparison.OrdinalIgnoreCase));if(alt!=null){color=pane.Accent;return pane.Title+(alt.ByPath?"（按路径）":"（按名称）");}}
  return changed?"待移除":"";
 }
 void Toggle(ProcGroup g){
  if(!g.Selectable){Toast.Notify(this,"这个进程无法按路径分流","它的路径读取不到，请把匹配方式切换为「按进程名称匹配」",false);return;}
  SetChecked(g,!IsChecked(g));
 }
 void SetChecked(ProcGroup g,bool value){
  if(value==InList(target,g.Match))pending.Remove(g.Match);
  else{AppEntry entry;try{entry=new AppEntry(g.Match);}catch(Exception ex){Toast.Notify(this,"无法加入名单",ex.Message,false);return;}if(!string.IsNullOrEmpty(g.Description))entry.Description=g.Description;pending[g.Match]=new Change{Entry=entry,Add=value};}
  grid.Invalidate();UpdateState();
 }
 void UpdateState(){
  int n=pending.Count;save.Enabled=!saving&&(n>0||host.IsDirty);save.Text=n>0?"保存 ("+n+")":"保存";
  if(finder.Dragging)return;
  int total=processes.Count,checkedCount=AllGroups(tops).Count(g=>g.Selectable&&IsChecked(g));
  string text=total+" 个进程 · 已勾选 "+checkedCount+" 个 · "+(byPath?"按完整路径匹配（仅这个程序副本）":"按进程名称匹配（所有同名程序）");
  if(n>0)text="● "+n+" 项更改未保存    "+text;else if(host.IsDirty)text="● 主窗口有未保存的修改    "+text;
  status.Text=text;
 }
 static IEnumerable<ProcGroup> AllGroups(IEnumerable<ProcGroup> list){foreach(var g in list){yield return g;foreach(var c in AllGroups(g.Children))yield return c;}}

 async Task<bool> SaveChanges(Form toastOwner){
  if(saving)return false;if(pending.Count==0&&!host.IsDirty)return true;
  saving=true;UpdateState();UseWaitCursor=true;status.Text="正在校验并保存到 v2rayN…";
  try{
   foreach(var c in pending.Values.ToList()){if(c.Add)host.Insert(target,c.Entry);else target.Entries.RemoveAll(x=>string.Equals(x.Match,c.Entry.Match,StringComparison.OrdinalIgnoreCase));}
   target.Added();other.RefreshRows();pending.Clear();
   bool ok=await host.Save(false);
   if(ok)Toast.Notify(toastOwner,"已保存到 v2rayN","从托盘退出并重新打开 v2rayN 后生效",true);
   return ok;
  }finally{saving=false;UseWaitCursor=false;if(!IsDisposed){grid.Invalidate();UpdateState();}}
 }
 void OnClosing(object sender,FormClosingEventArgs e){
  if(saving||finder.Dragging){e.Cancel=true;return;}
  if(pending.Count==0)return;
  var answer=MessageBox.Show(this,"还有 "+pending.Count+" 项勾选更改没有保存，要现在保存吗？","未保存的更改",MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);
  if(answer==DialogResult.No)return;
  e.Cancel=true;
  if(answer==DialogResult.Yes)BeginInvoke(new Action(async()=>{if(await SaveChanges(host))Close();}));
 }

 // ---------- 拖动准星 ----------
 static ProcGroup GroupOf(IEnumerable<ProcGroup> list,int pid){foreach(var g in list){if(g.Items.Any(i=>i.Pid==pid))return g;var c=GroupOf(g.Children,pid);if(c!=null)return c;}return null;}
 async Task Pick(int pid){
  var g=GroupOf(tops,pid);
  if(g==null){await Reload();g=GroupOf(tops,pid);}
  if(g==null){Toast.Notify(this,"没有找到这个窗口的进程","它可能已经退出，或者属于本工具自身",false);return;}
  if(search.Text.Length>0)search.Text="";
  for(var a=g.Parent;a!=null;a=a.Parent)expanded.Add(a.Key);
  Render();int index=rows.FindIndex(r=>r.Kind==1&&r.Group==g);if(index>=0)Select(index,true);
  // 只负责定位并选中该进程，勾选与保存由用户自己决定
  Activate();grid.Focus();UpdateState();
  status.Text="已定位到："+g.Display+"（"+g.Name+"）"+(IsChecked(g)?" · 已在名单中":" · 未勾选")+"    "+status.Text;
 }

 // ---------- 绘制与交互 ----------
 Parts PartsOf(Row r,int height){
  var p=new Parts();int x=S(8)+r.Depth*S(22);int mid=height/2;int box=S(16);
  p.Chevron=new Rectangle(x,mid-box/2,box,box);x+=S(20);
  if(r.Kind==1){p.Check=new Rectangle(x,mid-glyph.Height/2,glyph.Width,glyph.Height);x+=glyph.Width+S(8);}
  p.Icon=new Rectangle(x,mid-box/2,box,box);p.TextX=x+box+S(8);return p;
 }
 void PaintCell(object sender,DataGridViewCellPaintingEventArgs e){
  if(e.RowIndex<0||e.RowIndex>=rows.Count||e.ColumnIndex<0)return;
  var r=rows[e.RowIndex];var g=e.Graphics;var b=e.CellBounds;bool selected=(e.State&DataGridViewElementStates.Selected)!=0;
  Color back=Color.White;
  if(r.Kind!=0){if(selected)back=SelectedBack;else if(e.RowIndex==hover)back=HoverBack;else if(r.Kind==1&&r.Group.Match!=null&&pending.ContainsKey(r.Group.Match))back=PendingBack;}
  using(var brush=new SolidBrush(back))g.FillRectangle(brush,b);
  e.Handled=true;
  var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
  if(r.Kind==0){if(e.ColumnIndex==0)TextRenderer.DrawText(g,Convert.ToString(e.Value),sectionFont,new Rectangle(b.X+S(12),b.Y+S(4),b.Width*2,b.Height-S(4)),Theme.Blue,flags);return;}
  if(e.ColumnIndex==0){
   var parts=PartsOf(r,b.Height);
   if(r.Kind==1&&Expandable(r.Group))DrawChevron(g,Offset(parts.Chevron,b),IsOpen(r.Group));
   if(r.Kind==1){var state=!r.Group.Selectable?CheckBoxState.UncheckedDisabled:IsChecked(r.Group)?CheckBoxState.CheckedNormal:CheckBoxState.UncheckedNormal;CheckBoxRenderer.DrawCheckBox(g,Offset(parts.Check,b).Location,state);}
   var image=r.Kind==1?r.Group.Image:ProcessScanner.IconFor(r.Proc.Path);
   if(image!=null){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,Offset(parts.Icon,b));}
   bool bold=r.Kind==1&&r.Group.Match!=null&&pending.ContainsKey(r.Group.Match);
   TextRenderer.DrawText(g,Convert.ToString(e.Value),bold?boldFont:grid.Font,new Rectangle(b.X+parts.TextX,b.Y,b.Width-parts.TextX-S(4),b.Height),r.Kind==2?Theme.Muted:Theme.Ink,flags);
   return;
  }
  Color color=e.ColumnIndex==3?Theme.Ink:Theme.Muted;
  if(e.ColumnIndex==1&&r.Kind==1)StatusOf(r.Group,out color);
  TextRenderer.DrawText(g,Convert.ToString(e.Value),grid.Font,new Rectangle(b.X+S(8),b.Y,b.Width-S(12),b.Height),color,flags);
 }
 static Rectangle Offset(Rectangle r,Rectangle cell){r.Offset(cell.Location);return r;}
 void DrawChevron(Graphics g,Rectangle r,bool open){
  var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;float cx=r.X+r.Width/2f,cy=r.Y+r.Height/2f,k=S(4);
  using(var pen=new Pen(Theme.Muted,Math.Max(1.4f,scale*1.4f))){pen.StartCap=pen.EndCap=LineCap.Round;
   if(open)g.DrawLines(pen,new[]{new PointF(cx-k,cy-k/2),new PointF(cx,cy+k/2),new PointF(cx+k,cy-k/2)});
   else g.DrawLines(pen,new[]{new PointF(cx-k/2,cy-k),new PointF(cx+k/2,cy),new PointF(cx-k/2,cy+k)});}
  g.SmoothingMode=old;
 }
 void SetHover(int index){if(index==hover)return;int old=hover;hover=index;if(old>=0&&old<grid.RowCount)grid.InvalidateRow(old);if(index>=0&&index<grid.RowCount)grid.InvalidateRow(index);}
 void ToggleExpand(ProcGroup g){if(!expanded.Remove(g.Key))expanded.Add(g.Key);Render();}
 bool Hit(Rectangle r,Point p){r.Inflate(S(4),S(4));return r.Contains(p);}
 void CellMouseDown(object sender,DataGridViewCellMouseEventArgs e){
  if(e.Button!=MouseButtons.Left||e.RowIndex<0||e.RowIndex>=rows.Count||e.ColumnIndex!=0)return;
  var r=rows[e.RowIndex];if(r.Kind!=1)return;var parts=PartsOf(r,grid.GetCellDisplayRectangle(0,e.RowIndex,false).Height);
  if(Expandable(r.Group)&&Hit(parts.Chevron,e.Location)){ToggleExpand(r.Group);return;}
  if(Hit(parts.Check,e.Location))Toggle(r.Group);
 }
 void CellDoubleClick(object sender,DataGridViewCellMouseEventArgs e){
  if(e.RowIndex<0||e.RowIndex>=rows.Count)return;var r=rows[e.RowIndex];if(r.Kind!=1||!Expandable(r.Group))return;
  if(e.ColumnIndex==0){var parts=PartsOf(r,grid.GetCellDisplayRectangle(0,e.RowIndex,false).Height);if(Hit(parts.Chevron,e.Location)||Hit(parts.Check,e.Location))return;}
  ToggleExpand(r.Group);
 }
 void GridKey(object sender,KeyEventArgs e){
  var r=Current();if(r==null)return;
  if(e.KeyCode==Keys.Space&&r.Kind==1){Toggle(r.Group);e.Handled=true;}
  else if(e.KeyCode==Keys.Right&&r.Kind==1&&Expandable(r.Group)&&!IsOpen(r.Group)){ToggleExpand(r.Group);e.Handled=true;}
  else if(e.KeyCode==Keys.Left&&r.Kind==1&&expanded.Contains(r.Group.Key)){ToggleExpand(r.Group);e.Handled=true;}
 }
}
}

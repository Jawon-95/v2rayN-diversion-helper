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
[assembly:AssemblyVersion("1.0.0.0")]
[assembly:AssemblyFileVersion("1.0.0.0")]
namespace AppRoutingManager {
static class Program {
 [DllImport("user32.dll")]static extern bool SetProcessDpiAwarenessContext(IntPtr value);
 [STAThread]static void Main(string[] args){try{try{SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new MainForm(args.Length>0?args[0]:null));}catch(Exception ex){MessageBox.Show(ex.Message,"应用分流助手",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
}
public static class Theme {
 public static Color Ink=Color.FromArgb(28,43,65),Muted=Color.FromArgb(99,113,132),Blue=Color.FromArgb(43,100,222),Background=Color.FromArgb(245,247,251),Line=Color.FromArgb(226,232,240),SelectedBack=Color.FromArgb(204,232,255),HoverBack=Color.FromArgb(229,243,255);
 public static Button Button(string text,bool primary=false){var b=new Button{Text=text,AutoSize=false,Height=36,FlatStyle=FlatStyle.Flat,BackColor=primary?Blue:Color.White,ForeColor=primary?Color.White:Ink,Cursor=Cursors.Hand,Margin=new Padding(4)};b.FlatAppearance.BorderColor=primary?Blue:Line;b.FlatAppearance.BorderSize=1;return b;}
 public static Label Label(string text,int size=10,bool bold=false){return new Label{Text=text,AutoSize=true,ForeColor=Ink,Font=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,0,0,6)};}
 public static DataGridView Grid(){var g=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AutoGenerateColumns=false,MultiSelect=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,EnableHeadersVisualStyles=false,GridColor=Line,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,ColumnHeadersHeight=35};g.DefaultCellStyle.Font=new Font("Microsoft YaHei UI",10);g.DefaultCellStyle.SelectionBackColor=Color.FromArgb(227,237,255);g.DefaultCellStyle.SelectionForeColor=Ink;g.ColumnHeadersDefaultCellStyle.BackColor=Color.FromArgb(248,250,253);g.ColumnHeadersDefaultCellStyle.ForeColor=Muted;g.ColumnHeadersDefaultCellStyle.Font=new Font("Microsoft YaHei UI",9);g.RowTemplate.Height=37;return g;}
}
public sealed class AppPane : Panel {
 public readonly List<AppEntry> Entries=new List<AppEntry>();
 public readonly DataGridView Grid=Theme.Grid();
 public readonly string Title;public readonly Color Accent;
 public event Action<AppPane,bool> AddRequested;
 public event Action<AppPane> MoveRequested;
 public event Action Changed;
 readonly Label counter;
 public AppPane(string title,string subtitle,Color accent){Title=title;Accent=accent;BackColor=Color.White;Padding=new Padding(16);Dock=DockStyle.Fill;Margin=new Padding(6);var header=new Panel{Dock=DockStyle.Top,Height=80};var h=Theme.Label(title,15,true);h.ForeColor=accent;h.Location=new Point(0,0);header.Controls.Add(h);counter=Theme.Label("0 个应用",9);counter.ForeColor=Theme.Muted;counter.Location=new Point(0,34);header.Controls.Add(counter);var sub=Theme.Label(subtitle,9);sub.ForeColor=Theme.Muted;sub.Location=new Point(0,54);header.Controls.Add(sub);Controls.Add(header);
  var bar=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=44,ColumnCount=4,RowCount=1};for(int i=0;i<4;i++)bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
  string[] labels={"从进程选择","选择 EXE","移到另一侧","删除选中"};for(int i=0;i<4;i++){int action=i;var b=Theme.Button(labels[i]);b.Dock=DockStyle.Fill;b.Click+=(s,e)=>{if(action<2){if(AddRequested!=null)AddRequested(this,action==0);}else if(action==2){if(MoveRequested!=null)MoveRequested(this);}else RemoveSelected();};bar.Controls.Add(b,i,0);}Controls.Add(bar);
  Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="app",HeaderText="应用",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=35});Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="match",HeaderText="进程名称 / 完整路径",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=65});Grid.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Delete){RemoveSelected();e.Handled=true;}};Controls.Add(Grid);Grid.BringToFront();
  // 与进程列表一致的样式：紧凑行高、图标、无网格线、悬停高亮
  Grid.RowTemplate.Height=30;Grid.ColumnHeadersHeight=34;Grid.CellBorderStyle=DataGridViewCellBorderStyle.None;
  foreach(DataGridViewColumn c in Grid.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
  Grid.CellPainting+=PaintCell;Grid.CellMouseEnter+=(s,e)=>SetHover(e.RowIndex);Grid.MouseLeave+=(s,e)=>SetHover(-1);
 }
 int hover=-1;
 readonly Dictionary<string,Bitmap> icons=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);
 Bitmap IconOf(AppEntry a){
  Bitmap b;if(icons.TryGetValue(a.Match,out b))return b;
  string path=a.ByPath?a.Match:ProcessScanner.PathForName(a.Name);
  if(path!=null&&string.Equals(a.Description,a.Name,StringComparison.OrdinalIgnoreCase))a.Description=AppEntry.Describe(path,a.Name);
  b=ProcessScanner.IconFor(path!=null&&System.IO.File.Exists(path)?path:null);if(path!=null)icons[a.Match]=b;return b;
 }
 void SetHover(int index){if(index==hover)return;int old=hover;hover=index;if(old>=0&&old<Grid.RowCount)Grid.InvalidateRow(old);if(index>=0&&index<Grid.RowCount)Grid.InvalidateRow(index);}
 void PaintCell(object sender,DataGridViewCellPaintingEventArgs e){
  if(e.RowIndex<0||e.ColumnIndex<0)return;var a=Grid.Rows[e.RowIndex].Tag as AppEntry;if(a==null)return;
  var g=e.Graphics;var b=e.CellBounds;bool selected=(e.State&DataGridViewElementStates.Selected)!=0;
  using(var brush=new SolidBrush(selected?Theme.SelectedBack:e.RowIndex==hover?Theme.HoverBack:Color.White))g.FillRectangle(brush,b);
  var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;int x=b.X+10;
  if(e.ColumnIndex==0){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(IconOf(a),new Rectangle(x,b.Y+(b.Height-16)/2,16,16));x+=24;}
  TextRenderer.DrawText(g,Convert.ToString(e.Value),Grid.Font,new Rectangle(x,b.Y,b.Right-x-4,b.Height),e.ColumnIndex==0?Theme.Ink:Theme.Muted,flags);
  e.Handled=true;
 }
 public void Set(IEnumerable<AppEntry> values){Entries.Clear();Entries.AddRange(values);RefreshRows();}
 public void RefreshRows(){hover=-1;Grid.Rows.Clear();foreach(var a in Entries){IconOf(a);int index=Grid.Rows.Add(a.Description,a.Match);Grid.Rows[index].Tag=a;Grid.Rows[index].Cells[1].ToolTipText=a.ByPath?"仅这个完整路径下的程序":"所有同名进程";}counter.Text=Entries.Count+" 个应用";Grid.ClearSelection();}
 public List<AppEntry> Selected(){return Grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r=>r.Index).Select(r=>(AppEntry)r.Tag).ToList();}
 public void RemoveSelected(){var values=Selected();if(values.Count==0)return;foreach(var a in values)Entries.Remove(a);RefreshRows();if(Changed!=null)Changed();}
 public void Added(){RefreshRows();if(Changed!=null)Changed();}
}
public sealed class MainForm : Form {
 readonly TextBox folder=new TextBox{ReadOnly=true},name=new TextBox();readonly ComboBox profiles=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList},scope=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};readonly Label status=Theme.Label("正在寻找 v2rayN…",10);readonly Timer autoSave=new Timer{Interval=350};bool pickerOpen;readonly AppPane direct=new AppPane("全部直连","这个应用访问国内、国外网站都直连",Color.FromArgb(21,133,101)),split=new AppPane("国内外分流","国内网站直连，国外网站走代理",Theme.Blue);RoutingStore store;RouteSnapshot snapshot;bool dirty,loading,busy;string requestedRoot;
 public MainForm(string root){requestedRoot=root;Text="应用分流助手 · v2rayN";try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}Size=new Size(1160,780);MinimumSize=new Size(1000,650);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Theme.Background;
  var top=new Panel{Dock=DockStyle.Top,Height=224,Width=ClientSize.Width,Padding=new Padding(20)};var title=Theme.Label("应用分流助手",22,true);title.Location=new Point(20,14);top.Controls.Add(title);var intro=Theme.Label("两份名单，清楚分流。没有加入名单的应用，访问互联网默认全部走代理。",10);intro.ForeColor=Theme.Muted;intro.Location=new Point(23,57);top.Controls.Add(intro);
  folder.SetBounds(22,94,900,29);folder.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;folder.BackColor=Color.White;top.Controls.Add(folder);var browse=Theme.Button("选择 v2rayN");browse.SetBounds(936,90,180,36);browse.Anchor=AnchorStyles.Top|AnchorStyles.Right;browse.Click+=(s,e)=>ChooseFolder();top.Controls.Add(browse);
  var pl=Theme.Label("管理的规则集",9);pl.Location=new Point(22,139);top.Controls.Add(pl);profiles.SetBounds(117,134,346,30);profiles.SelectedIndexChanged+=(s,e)=>SelectProfile();top.Controls.Add(profiles);var create=Theme.Button("新建规则集");create.SetBounds(475,132,120,34);create.Click+=(s,e)=>NewProfile();top.Controls.Add(create);var nl=Theme.Label("名称",9);nl.Location=new Point(615,139);top.Controls.Add(nl);name.SetBounds(660,134,455,29);name.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;name.TextChanged+=(s,e)=>MarkDirty(false);name.Leave+=(s,e)=>ScheduleSave();name.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;ScheduleSave();}};top.Controls.Add(name);
  var sl=Theme.Label("添加应用时",9);sl.Location=new Point(22,186);top.Controls.Add(sl);scope.SetBounds(117,180,345,30);scope.Items.AddRange(new object[]{"按进程名称 · 所有同名副本","按完整路径 · 仅这个程序副本"});scope.SelectedIndex=0;top.Controls.Add(scope);var hint=Theme.Label("同一项放进另一侧时，会自动从原名单移走。",9);hint.ForeColor=Theme.Muted;hint.Location=new Point(477,186);top.Controls.Add(hint);Controls.Add(top);
  var bottom=new Panel{Dock=DockStyle.Bottom,Height=112,Width=ClientSize.Width,Padding=new Padding(20,8,20,15)};var note=Theme.Label("未配置的应用 → 全部代理    ·    局域网保留直连    ·    修改后自动保存并备份",10);note.Location=new Point(22,8);note.ForeColor=Theme.Muted;bottom.Controls.Add(note);status.Location=new Point(22,51);status.AutoSize=false;status.Size=new Size(675,46);status.Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Right;bottom.Controls.Add(status);
  var reload=Theme.Button("重新读取");reload.SetBounds(852,49,120,36);reload.Anchor=AnchorStyles.Right|AnchorStyles.Top;reload.Click+=(s,e)=>Reload();bottom.Controls.Add(reload);var open=Theme.Button("打开 v2rayN");open.SetBounds(978,49,138,36);open.Anchor=AnchorStyles.Right|AnchorStyles.Top;open.Click+=(s,e)=>OpenProxy();bottom.Controls.Add(open);autoSave.Tick+=async(s,e)=>await AutoSave();Controls.Add(bottom);
  var lists=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(14,0,14,0)};lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));lists.Controls.Add(direct,0,0);lists.Controls.Add(split,1,0);Controls.Add(lists);lists.BringToFront();
  direct.AddRequested+=AddApps;split.AddRequested+=AddApps;direct.MoveRequested+=MoveApps;split.MoveRequested+=MoveApps;direct.Changed+=()=>MarkDirty(true);split.Changed+=()=>MarkDirty(true);
  FormClosing+=(s,e)=>{if(busy){e.Cancel=true;return;}if(dirty&&MessageBox.Show(this,"有尚未保存的修改，确定关闭吗？","未保存",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)e.Cancel=true;};Shown+=async(s,e)=>await FindProxy();
 }
 void MarkDirty(bool save){if(loading)return;dirty=true;status.Text=save?"正在自动保存…":"名称修改后按回车或点其他位置即自动保存。";if(save)ScheduleSave();}
 // 主窗口的任何修改都自动保存；进程列表窗口打开期间由它自己的「保存」按钮统一提交。
 void ScheduleSave(){if(loading||pickerOpen||!dirty||store==null)return;autoSave.Stop();autoSave.Start();}
 async Task AutoSave(){autoSave.Stop();if(!dirty||store==null||pickerOpen)return;if(busy){autoSave.Start();return;}await Save();}
 bool Discard(){return !dirty||MessageBox.Show(this,"放弃尚未保存的修改，重新读取吗？","重新读取",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
 async Task FindProxy(){SetBusy(true);string found=await Task.Run(()=>RoutingStore.IsRoot(requestedRoot??"")?requestedRoot:Discovery.Find());SetBusy(false);if(IsDisposed)return;if(found!=null)Connect(found);else{status.Text="没有找到 v2rayN。点右上角选择它的 EXE 文件。";folder.Text="未选择 v2rayN";}}
 void ChooseFolder(){if(busy||!Discard())return;using(var d=new OpenFileDialog{Title="选择 v2rayN.exe",Filter="v2rayN 程序 (v2rayN.exe)|v2rayN.exe",CheckFileExists=true}){if(d.ShowDialog(this)==DialogResult.OK)Connect(Path.GetDirectoryName(d.FileName));}}
 void Connect(string root){try{store=new RoutingStore(root);Discovery.Remember(root);folder.Text=root;dirty=false;LoadProfiles(null);}catch(Exception ex){ShowError(ex);}}
 void LoadProfiles(string preferred){loading=true;try{var all=store.Profiles();var managed=all.Where(x=>x.Managed).ToList();profiles.Items.Clear();foreach(var p in managed)profiles.Items.Add(p);var selected=managed.FirstOrDefault(x=>x.Id==preferred)??managed.FirstOrDefault(x=>x.Active)??managed.FirstOrDefault();if(selected!=null){profiles.SelectedItem=selected;snapshot=store.Load(selected);}else snapshot=store.Load(null);ApplySnapshot();}finally{loading=false;}}
 void SelectProfile(){if(loading||store==null)return;var profile=profiles.SelectedItem as RouteProfile;if(profile==null)return;if(!Discard()){loading=true;profiles.SelectedItem=profiles.Items.Cast<RouteProfile>().FirstOrDefault(x=>snapshot!=null&&snapshot.Profile!=null&&x.Id==snapshot.Profile.Id);loading=false;return;}try{loading=true;snapshot=store.Load(profile);ApplySnapshot();}catch(Exception ex){ShowError(ex);}finally{loading=false;}}
 void ApplySnapshot(){name.Text=snapshot.IsNew?"全局 + 应用分流":snapshot.Profile.Name;direct.Set(snapshot.Direct);split.Set(snapshot.Split);dirty=false;status.Text=snapshot.IsNew?"尚无应用分流规则。加入应用后会自动创建新的规则集。":"已读取现有规则；修改会自动保存，你在 v2rayN 中的手动修改会保留。";}
 void NewProfile(){if(busy)return;if(store==null){ChooseFolder();return;}if(!Discard())return;loading=true;try{profiles.SelectedIndex=-1;snapshot=store.Load(null);ApplySnapshot();var names=store.Profiles().Select(x=>x.Name).ToList();string baseName=name.Text;for(int i=2;names.Contains(name.Text);i++)name.Text=baseName+" "+i;}finally{loading=false;}MarkDirty(true);}
 void Reload(){if(busy)return;if(store==null){ChooseFolder();return;}if(!Discard())return;try{LoadProfiles(snapshot!=null&&snapshot.Profile!=null?snapshot.Profile.Id:null);}catch(Exception ex){ShowError(ex);}}
 void AddApps(AppPane target,bool running){if(store==null){ChooseFolder();return;}bool byPath=scope.SelectedIndex==1;var entries=new List<AppEntry>();if(running){autoSave.Stop();pickerOpen=true;try{using(var p=new ProcessPicker(this,target,target==direct?split:direct,byPath))p.ShowDialog(this);}finally{pickerOpen=false;}ScheduleSave();return;}else{using(var f=new OpenFileDialog{Title="选择要加入名单的应用",Filter="应用程序 (*.exe)|*.exe",Multiselect=true}){if(f.ShowDialog(this)!=DialogResult.OK)return;foreach(string path in f.FileNames){var entry=new AppEntry(byPath?path:Path.GetFileName(path));entry.Description=AppEntry.Describe(path,entry.Name);entries.Add(entry);}}}foreach(var entry in entries)Insert(target,entry);}
 internal void Insert(AppPane target,AppEntry entry){AppPane other=target==direct?split:direct;other.Entries.RemoveAll(x=>string.Equals(x.Match,entry.Match,StringComparison.OrdinalIgnoreCase));RoutingStore.AddUnique(target.Entries,entry);other.RefreshRows();target.Added();}
 void MoveApps(AppPane source){var chosen=source.Selected();if(chosen.Count==0)return;AppPane destination=source==direct?split:direct;foreach(var entry in chosen){source.Entries.Remove(entry);RoutingStore.AddUnique(destination.Entries,entry);}source.RefreshRows();destination.Added();}
 internal bool IsDirty{get{return dirty;}}
 internal async Task<bool> Save(bool notify=true){if(store==null||snapshot==null){ChooseFolder();return false;}string newName=name.Text;var d=direct.Entries.ToList();var s=split.Entries.ToList();SetBusy(true);status.Text="正在校验并备份配置…";try{var result=await Task.Run(()=>store.Save(snapshot,newName,d,s));snapshot=result;loading=true;LoadProfiles(result.Profile.Id);loading=false;dirty=false;status.Text="已保存。请从托盘退出 v2rayN 后重新打开，选择「"+result.Profile.Name+"」。";if(notify)Toast.Notify(this,"已自动保存「"+result.Profile.Name+"」","从托盘退出并重新打开 v2rayN，在路由中选择它即可生效",true);return true;}catch(Exception ex){ShowError(ex);return false;}finally{SetBusy(false);}}
 void OpenProxy(){if(busy)return;if(store==null){ChooseFolder();return;}try{Process.Start(new ProcessStartInfo(Path.Combine(store.Root,"v2rayN.exe")){WorkingDirectory=store.Root,UseShellExecute=true});}catch(Exception ex){ShowError(ex);}}
 void SetBusy(bool value){busy=value;direct.Enabled=!value;split.Enabled=!value;profiles.Enabled=!value;name.Enabled=!value;UseWaitCursor=value;}
 void ShowError(Exception ex){status.Text="没有完成："+ex.Message;MessageBox.Show(Form.ActiveForm??this,ex.Message,"应用分流助手",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
}
}

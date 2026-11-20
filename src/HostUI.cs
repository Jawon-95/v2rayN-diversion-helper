using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AppRoutingManager {
/// <summary>域名 / IP 名单：顶部输入框添加，双击条目直接修改，提交时逐条校验。</summary>
public sealed class HostPane : ListCard {
 public const string Help="粘贴网址会自动提取主域名，可一次粘贴多条。\r\n\r\n"+
  "example.com　　　　该域名及所有子域名\r\n"+
  "full:www.example.com　只匹配这一个域名\r\n"+
  "keyword:example　　域名包含关键字\r\n"+
  "regexp:^api\\d+\\.example\\.com$　正则表达式\r\n"+
  "1.2.3.4　10.0.0.0/8　IP 或 IP 段\r\n"+
  "geosite:google　geoip:telegram　规则库（需本机已有）\r\n\r\n"+
  "双击条目可修改，清空内容即删除。";
 public readonly List<HostEntry> Entries=new List<HostEntry>();
 public readonly InputBox Input=new InputBox("输入域名、IP 或粘贴网址，回车添加",Theme.IconAdd){Dock=DockStyle.Fill};
 public HostPane Other{get{return Partner as HostPane;}set{Partner=value;}}
 public event Action<HostPane> MoveRequested;
 readonly ToolTip error=new ToolTip(),helpTip=new ToolTip();bool editing;DataGridViewRow editRow;
 protected override string EmptyText{get{return "还没有条目";}}
 protected override string EmptyGlyph{get{return Theme.IconGlobe;}}

 public HostPane(string title,string tip,Color accent):base(title,tip,accent){
  var help=new FlatButton("",ButtonKind.Ghost,Theme.IconHelp){Accent=Theme.Muted,Cursor=Cursors.Help,TabStop=false};help.Height=Theme.S(30);help.Refit();Tools.Controls.Add(help);
  // 独立的提示框：悬停或点击显示，鼠标移开就隐藏（共用的 ToolTip 被手动 Show 后不会自动消失）。
  Action showHelp=()=>helpTip.Show(Help,help,0,help.Height+Theme.S(4),30000);
  help.MouseEnter+=(s,e)=>showHelp();help.Click+=(s,e)=>showHelp();help.MouseLeave+=(s,e)=>helpTip.Hide(help);help.VisibleChanged+=(s,e)=>helpTip.Hide(help);
  var row=new Panel{Dock=DockStyle.Top,Height=Theme.S(48),BackColor=Theme.Surface,Padding=new Padding(0,0,0,Theme.S(12))};
  var add=new FlatButton("添加",ButtonKind.Primary){Accent=accent,Dock=DockStyle.Right,AutoWidth=false,Width=Theme.S(72)};
  row.Controls.Add(Input);row.Controls.Add(new Panel{Dock=DockStyle.Right,Width=Theme.S(8),BackColor=Theme.Surface});row.Controls.Add(add);AddTop(row);
  add.Click+=(s,e)=>Submit();
  Input.Box.KeyDown+=(s,e)=>{
   if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Submit();}
   else if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;Input.Box.Clear();}
   else if(e.Control&&e.KeyCode==Keys.V&&Clipboard.ContainsText()&&Clipboard.GetText().IndexOf('\n')>=0){e.SuppressKeyPress=true;string problem=Commit(null,Clipboard.GetText());if(problem!=null)ShowInputError(problem);}
  };
  Input.Box.TextChanged+=(s,e)=>error.Hide(Input);
  Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="value",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=64});
  Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="kind",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,FillWeight=36,ReadOnly=true});
  Grid.ReadOnly=false;Grid.Columns[1].ReadOnly=true;Grid.Columns[0].DefaultCellStyle.Padding=new Padding(Theme.S(10),Theme.S(4),Theme.S(4),Theme.S(4));Grid.EditMode=DataGridViewEditMode.EditProgrammatically;
  foreach(DataGridViewColumn c in Grid.Columns)c.SortMode=DataGridViewColumnSortMode.NotSortable;
  Grid.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F2||e.KeyCode==Keys.Enter&&!Grid.IsCurrentCellInEditMode){EditCurrent();e.Handled=true;}else if(e.Control&&e.KeyCode==Keys.V&&!Grid.IsCurrentCellInEditMode){PasteClipboard();e.Handled=true;}};
  Grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)Edit(e.RowIndex);};
  Grid.EditingControlShowing+=(s,e)=>{e.Control.Font=Theme.Font(10);};
  // 结束编辑与校验事件的先后顺序随触发方式不同，统一在消息循环空闲时收尾。
  Grid.CellBeginEdit+=(s,e)=>{editing=true;editRow=Grid.Rows[e.RowIndex];};
  Grid.CellValidating+=Validate;
  Grid.CellEndEdit+=(s,e)=>{error.Hide(Grid);BeginInvoke(new Action(Finish));};
 }
 protected override void RequestMove(){if(Grid.IsCurrentCellInEditMode&&!Grid.EndEdit())return;if(MoveRequested!=null)MoveRequested(this);}
 protected override void BuildMenu(ToolStripItemCollection items){var edit=Theme.Item(items,"修改",EditCurrent);edit.ShortcutKeyDisplayString="F2";}
 protected override bool AcceptText{get{return true;}}
 protected override void DropText(string text){string problem=Commit(null,text);if(problem!=null)ShowInputError(problem);}
 protected override void PaintContent(Graphics g,int row,int column,Rectangle b,object value){
  if(column==0)TextRenderer.DrawText(g,Convert.ToString(value),Theme.Font(10),new Rectangle(b.X+Theme.S(14),b.Y,b.Width-Theme.S(18),b.Height),Theme.Ink,Theme.TextFlags);
  else TextRenderer.DrawText(g,Convert.ToString(value),Theme.Font(9),new Rectangle(b.X+Theme.S(4),b.Y,b.Width-Theme.S(8),b.Height),Theme.Muted,Theme.TextFlags);
 }
 /// <summary>提交输入框中的内容。</summary>
 public void Submit(){string problem=Commit(null,Input.Box.Text);if(problem!=null)ShowInputError(problem);else Input.Box.Clear();Input.Box.Focus();}
 void ShowInputError(string message){error.Show(message,Input,0,Input.Height+Theme.S(4),4000);}
 public void Edit(int index){if(index<0||index>=Grid.RowCount)return;Grid.ClearSelection();Grid.CurrentCell=Grid.Rows[index].Cells[0];Grid.Rows[index].Selected=true;Grid.Focus();Grid.BeginEdit(true);}
 void EditCurrent(){var row=Grid.SelectedRows.Cast<DataGridViewRow>().OrderBy(r=>r.Index).FirstOrDefault()??Grid.CurrentRow;if(row!=null)Edit(row.Index);else Input.Box.Focus();}
 void Validate(object sender,DataGridViewCellValidatingEventArgs e){
  if(e.ColumnIndex!=0||!editing)return;
  var old=Grid.Rows[e.RowIndex].Tag as HostEntry;string text=Convert.ToString(e.FormattedValue).Trim();
  if(old!=null&&text==old.Text)return;
  string problem;Check(text,old,out problem);
  if(problem!=null){e.Cancel=true;ShowError(problem+"（Esc 取消）",e.RowIndex);}
 }
 /// <summary>编辑结束后按单元格里已提交的内容更新名单；输入有误时恢复原样并提示。</summary>
 void Finish(){
  if(Grid.IsCurrentCellInEditMode||!editing)return;editing=false;var row=editRow;editRow=null;
  if(row==null||row.Index<0||row.DataGridView!=Grid){RefreshRows();return;}
  var old=row.Tag as HostEntry;string text=Convert.ToString(row.Cells[0].Value).Trim();
  if(old!=null&&text==old.Text)return;
  string problem;var result=Check(text,old,out problem);int index=row.Index;
  if(problem!=null){RefreshRows();ShowError(problem,Math.Min(index,Grid.RowCount-1));return;}
  Apply(old,result);
 }
 void ShowError(string message,int rowIndex){var r=rowIndex>=0?Grid.GetCellDisplayRectangle(0,rowIndex,true):new Rectangle(Theme.S(16),0,0,0);error.Show(message,Grid,r.Left,r.Bottom+2,4000);}
 void PasteClipboard(){if(!Clipboard.ContainsText())return;string problem=Commit(null,Clipboard.GetText());if(problem!=null)ShowInputError(problem);}
 /// <summary>校验一段输入（可多条）；返回去重后的结果，出错时给出原因。</summary>
 public List<HostEntry> Check(string text,HostEntry old,out string problem){
  problem=null;if(string.IsNullOrWhiteSpace(text))return new List<HostEntry>();
  var parsed=HostEntry.ParseMany(text,out problem);if(parsed==null)return null;
  var fresh=parsed.Where(x=>!Entries.Any(y=>y!=old&&HostEntry.Same(x,y))).ToList();
  if(fresh.Count==0){problem="「"+parsed[0].Text+"」已经在这个名单里了";return null;}
  return fresh;
 }
 /// <summary>用新条目替换 old（old 为 null 表示新增）；同一条目若在另一份名单中会被移过来。返回错误信息或 null。</summary>
 public string Commit(HostEntry old,string text){if(old==null&&string.IsNullOrWhiteSpace(text))return null;string problem;var result=Check(text,old,out problem);if(problem!=null)return problem;Apply(old,result);return null;}
 void Apply(HostEntry old,List<HostEntry> result){
  int index=old!=null?Entries.IndexOf(old):-1;if(old!=null)Entries.Remove(old);if(index<0||index>Entries.Count)index=Entries.Count;int first=index;
  bool moved=false;foreach(var x in result){if(Other!=null&&Other.Entries.RemoveAll(y=>HostEntry.Same(x,y))>0)moved=true;if(Entries.Any(y=>HostEntry.Same(x,y)))continue;Entries.Insert(index++,x);}
  if(moved)Other.RefreshRows();
  bool changed=old==null?result.Count>0:!(result.Count==1&&result[0].Value==old.Value);
  RefreshRows();for(int i=first;i<index&&i<Grid.RowCount;i++)Grid.Rows[i].Selected=true;
  if(index>first&&first<Grid.RowCount&&Grid.Visible&&!Grid.Rows[first].Displayed)try{Grid.FirstDisplayedScrollingRowIndex=first;}catch(InvalidOperationException){}
  if(changed)RaiseChanged();
 }
 public void Set(IEnumerable<HostEntry> values){Entries.Clear();Entries.AddRange(values);RefreshRows();}
 public void RefreshRows(){Grid.Rows.Clear();foreach(var x in Entries){int i=Grid.Rows.Add(x.Text,x.Kind);Grid.Rows[i].Tag=x;}SetCount(Entries.Count);Grid.ClearSelection();Grid.Invalidate();}
 public List<HostEntry> Selected(){return Grid.SelectedRows.Cast<DataGridViewRow>().Where(r=>r.Tag!=null).OrderBy(r=>r.Index).Select(r=>(HostEntry)r.Tag).ToList();}
 public override void RemoveSelected(){if(Grid.IsCurrentCellInEditMode)return;var values=Selected();if(values.Count==0)return;foreach(var x in values)Entries.Remove(x);RefreshRows();RaiseChanged();}
 public void Added(){RefreshRows();RaiseChanged();}
}
}

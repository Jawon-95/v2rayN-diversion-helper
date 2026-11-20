using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace AppRoutingManager {
/// <summary>规则集列表：点击切换，「+」新建，双击或 F2 重命名，悬停 / 右键 / Delete 删除。</summary>
public sealed class ProfilePane : ListCard {
 public sealed class Item{public string Id,Name;public bool Active,Unsaved;}
 public readonly List<Item> Items=new List<Item>();
 /// <summary>当前正在编辑的规则集（尚未保存的新规则集为 null）。</summary>
 public string CurrentId;
 public event Action<Item> SwitchRequested,DeleteRequested;
 public event Action AddRequested;
 /// <summary>重命名：返回错误信息，成功返回 null。</summary>
 public Func<Item,string,string> Renamer;
 readonly ToolTip error=new ToolTip();Item editing;string signature;
 protected override string EmptyText{get{return "还没有规则集";}}
 protected override string EmptyGlyph{get{return Theme.IconMore;}}
 protected override string PrimaryGlyph{get{return Theme.IconEdit;}}
 protected override string PrimaryText{get{return "重命名";}}

 public ProfilePane():base("规则集","每个规则集是一套独立的分流名单\r\n在 v2rayN 主界面底部「路由」中选择使用哪一个",Theme.Blue){
  var add=new FlatButton("",ButtonKind.Ghost,Theme.IconAdd){Accent=Theme.Blue,TabStop=false};add.Height=Theme.S(30);add.Refit();Theme.Tips.SetToolTip(add,"新建规则集");add.Click+=(s,e)=>{if(AddRequested!=null)AddRequested();};Tools.Controls.Add(add);
  Grid.Columns.Add(new DataGridViewTextBoxColumn{Name="name",AutoSizeMode=DataGridViewAutoSizeColumnMode.Fill,SortMode=DataGridViewColumnSortMode.NotSortable});
  Grid.MultiSelect=false;Grid.ReadOnly=false;Grid.EditMode=DataGridViewEditMode.EditProgrammatically;Grid.Columns[0].DefaultCellStyle.Padding=new Padding(Theme.S(10),Theme.S(4),Theme.S(4),Theme.S(4));
  Grid.EditingControlShowing+=(s,e)=>{e.Control.Font=Theme.Font(10);};
  Grid.MouseUp+=(s,e)=>{int row;if(e.Button==MouseButtons.Left&&!Grid.IsCurrentCellInEditMode&&ActionAt(e.Location,out row)<0&&row>=0)Switch(row);};
  Grid.CellDoubleClick+=(s,e)=>{if(e.RowIndex>=0)Edit(e.RowIndex);};
  Grid.KeyDown+=(s,e)=>{if(Grid.IsCurrentCellInEditMode)return;if(e.KeyCode==Keys.F2){RequestMove();e.Handled=true;}else if(e.KeyCode==Keys.Enter&&Grid.CurrentRow!=null){Switch(Grid.CurrentRow.Index);e.Handled=true;}};
  Grid.CellEndEdit+=(s,e)=>BeginInvoke(new Action(Finish));
 }
 Item At(int row){return row>=0&&row<Grid.RowCount?Grid.Rows[row].Tag as Item:null;}
 Item SelectedItem(){var r=Grid.SelectedRows.Cast<DataGridViewRow>().FirstOrDefault()??Grid.CurrentRow;return r!=null?r.Tag as Item:null;}
 void Switch(int row){var item=At(row);if(item==null)return;if(item.Id!=CurrentId&&!item.Unsaved){if(SwitchRequested!=null)SwitchRequested(item);}else MarkCurrent();}
 protected override void RequestMove(){var item=SelectedItem();if(item!=null)Edit(Items.IndexOf(item));}
 public override void RemoveSelected(){var item=SelectedItem();MarkCurrent();if(item!=null&&DeleteRequested!=null)DeleteRequested(item);}
 protected override void MenuClosed(){if(!Grid.IsCurrentCellInEditMode)MarkCurrent();}
 /// <summary>开始就地重命名第 index 行。</summary>
 public void Edit(int index){if(index<0||index>=Grid.RowCount)return;editing=At(index);Grid.ClearSelection();Grid.CurrentCell=Grid.Rows[index].Cells[0];Grid.Rows[index].Selected=true;Grid.Focus();Grid.BeginEdit(true);}
 void Finish(){
  if(Grid.IsCurrentCellInEditMode||editing==null)return;var item=editing;editing=null;
  int index=Items.IndexOf(item);string text=index>=0&&index<Grid.RowCount?Convert.ToString(Grid.Rows[index].Cells[0].Value).Trim():item.Name;
  if(text==item.Name||text.Length==0){Rebuild();return;}
  string problem=Renamer!=null?Renamer(item,text):null;
  if(problem!=null){Rebuild();var r=Grid.GetCellDisplayRectangle(0,Math.Max(0,Math.Min(index,Grid.RowCount-1)),true);error.Show(problem,Grid,r.Left,r.Bottom+2,4000);}
 }
 /// <summary>更新列表；内容没变化时不重建，避免打断悬停和编辑。</summary>
 public void Set(IEnumerable<Item> items,string currentId){
  var list=items.ToList();string sig=currentId+"|"+string.Join("|",list.Select(x=>x.Id+"/"+x.Name+"/"+x.Active+"/"+x.Unsaved));
  if(sig==signature||Grid.IsCurrentCellInEditMode)return;signature=sig;
  Items.Clear();Items.AddRange(list);CurrentId=currentId;Rebuild();
 }
 void Rebuild(){Grid.Rows.Clear();foreach(var x in Items){int i=Grid.Rows.Add(x.Name);Grid.Rows[i].Tag=x;}SetCount(Items.Count);MarkCurrent();Grid.Invalidate();}
 public void ShowCurrent(){if(!Grid.IsCurrentCellInEditMode)MarkCurrent();}
 void MarkCurrent(){Grid.ClearSelection();var x=Items.FirstOrDefault(i=>CurrentId==null?i.Unsaved:i.Id==CurrentId);if(x==null)return;int r=Items.IndexOf(x);if(r<Grid.RowCount){Grid.Rows[r].Selected=true;try{Grid.CurrentCell=Grid.Rows[r].Cells[0];}catch(InvalidOperationException){}}}
 protected override void PaintContent(Graphics g,int row,int column,Rectangle b,object value){
  var x=At(row);if(x==null)return;bool current=x.Unsaved?CurrentId==null:x.Id==CurrentId;
  string badge=x.Active?"使用中":x.Unsaved?"未保存":null;int right=b.Right-Theme.S(6);
  if(badge!=null){var f=Theme.Font(8.5f);var size=TextRenderer.MeasureText(badge,f);var r=new Rectangle(right-size.Width-Theme.S(4),b.Y+(b.Height-Theme.S(20))/2,size.Width+Theme.S(4),Theme.S(20));
   Color c=x.Active?Theme.Green:Theme.Faint;Theme.FillRound(g,Theme.Tint(c,0.14),r,Theme.S(10));TextRenderer.DrawText(g,badge,f,r,c,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);right=r.Left-Theme.S(6);}
  TextRenderer.DrawText(g,x.Name,Theme.Font(10,current),new Rectangle(b.X+Theme.S(14),b.Y,Math.Max(0,right-b.X-Theme.S(14)),b.Height),Theme.Ink,Theme.TextFlags);
 }
}
}

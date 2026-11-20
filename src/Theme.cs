using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;

namespace AppRoutingManager {
/// <summary>统一的配色、字体、图标与基础控件。</summary>
public static class Theme {
 public static readonly Color Ink=Color.FromArgb(17,24,39),Muted=Color.FromArgb(107,114,128),Faint=Color.FromArgb(156,163,175),
  Background=Color.FromArgb(243,245,249),Surface=Color.White,Line=Color.FromArgb(229,231,235),LineStrong=Color.FromArgb(209,213,219),Track=Color.FromArgb(234,237,242),
  Blue=Color.FromArgb(37,99,235),Green=Color.FromArgb(5,150,105),Indigo=Color.FromArgb(79,70,229),Amber=Color.FromArgb(180,83,9),Red=Color.FromArgb(220,38,38),
  SelectedBack=Color.FromArgb(232,240,254),HoverBack=Color.FromArgb(244,246,250);
 // Segoe MDL2 Assets 图标
 public const string IconAdd="",IconDelete="",IconSwitch="",IconMore="",IconSearch="",IconRefresh="",IconHelp="",
  IconChevron="",IconEdit="",IconWarning="",IconCheck="",IconFolder="",IconApps="",IconGlobe="",IconOpen="";
 static float scale;
 public static float Scale{get{if(scale==0){using(var g=Graphics.FromHwnd(IntPtr.Zero))scale=g.DpiX/96f;}return scale;}}
 public static int S(double v){return (int)Math.Round(v*Scale);}
 static readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
 public static Font Font(float size,bool bold=false){string k=size+(bold?"b":"");Font f;if(!fonts.TryGetValue(k,out f))fonts[k]=f=new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular);return f;}
 public static Font Icons(float size){string k="i"+size;Font f;if(!fonts.TryGetValue(k,out f))fonts[k]=f=new Font("Segoe MDL2 Assets",size);return f;}
 /// <summary>与白色混合，amount 为原色占比（0~1）。</summary>
 public static Color Tint(Color c,double amount){Func<int,int> m=v=>(int)Math.Round(255-(255-v)*amount);return Color.FromArgb(m(c.R),m(c.G),m(c.B));}
 public static Color Darker(Color c,double k){return Color.FromArgb((int)(c.R*k),(int)(c.G*k),(int)(c.B*k));}
 public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<=0){p.AddRectangle(r);return p;}
  p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 public static void FillRound(Graphics g,Color color,RectangleF r,float radius,Color? border=null,float width=1){var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;using(var p=Round(r,radius)){using(var b=new SolidBrush(color))g.FillPath(b,p);if(border!=null)using(var pen=new Pen(border.Value,width))g.DrawPath(pen,p);}g.SmoothingMode=old;}
 public const TextFormatFlags TextFlags=TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
 public static void Glyph(Graphics g,string glyph,float size,Color color,Rectangle r){TextRenderer.DrawText(g,glyph,Icons(size),r,color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix);}
 public static readonly ToolTip Tips=new ToolTip{AutoPopDelay=30000,InitialDelay=250,ReshowDelay=100,ShowAlways=true};
 public static Label Label(string text,float size=10,bool bold=false){return new Label{Text=text,AutoSize=true,ForeColor=Ink,BackColor=Color.Transparent,Font=Font(size,bold),Margin=Padding.Empty};}
 public static DataGridView Grid(){
  var g=new DataGridView{Dock=DockStyle.Fill,BackgroundColor=Surface,BorderStyle=BorderStyle.None,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AllowUserToResizeColumns=false,AutoGenerateColumns=false,MultiSelect=true,SelectionMode=DataGridViewSelectionMode.FullRowSelect,RowHeadersVisible=false,ColumnHeadersVisible=false,EnableHeadersVisualStyles=false,GridColor=Surface,CellBorderStyle=DataGridViewCellBorderStyle.None,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,ColumnHeadersHeight=S(34),StandardTab=true};
  g.DefaultCellStyle.Font=Font(10);g.DefaultCellStyle.BackColor=Surface;g.DefaultCellStyle.ForeColor=Ink;g.DefaultCellStyle.SelectionBackColor=SelectedBack;g.DefaultCellStyle.SelectionForeColor=Ink;
  g.ColumnHeadersDefaultCellStyle.BackColor=Surface;g.ColumnHeadersDefaultCellStyle.ForeColor=Muted;g.ColumnHeadersDefaultCellStyle.SelectionBackColor=Surface;g.ColumnHeadersDefaultCellStyle.Font=Font(9);g.ColumnHeadersDefaultCellStyle.Padding=new Padding(S(4),0,0,0);
  g.RowTemplate.Height=S(40);
  typeof(DataGridView).GetProperty("DoubleBuffered",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(g,true,null);
  return g;
 }
 /// <summary>表格行背景：整行圆角高亮，按单元格分段绘制。</summary>
 public static void PaintRow(DataGridView grid,DataGridViewCellPaintingEventArgs e,Color back){
  var g=e.Graphics;var cell=e.CellBounds;using(var b=new SolidBrush(Surface))g.FillRectangle(b,cell);
  if(back==Surface)return;var row=grid.GetRowDisplayRectangle(e.RowIndex,false);
  var state=g.Save();g.SetClip(cell);FillRound(g,back,new RectangleF(row.X+S(4),cell.Y+S(2),row.Width-S(8)-1,cell.Height-S(4)),S(6));g.Restore(state);
 }
 public static ContextMenuStrip Menu(){var m=new ContextMenuStrip{Font=Font(10),ShowImageMargin=false,ShowCheckMargin=true,Renderer=new MenuRenderer(),Padding=new Padding(0,S(4),0,S(4))};return m;}
 public static ToolStripMenuItem Item(ToolStripItemCollection items,string text,Action click,bool check=false,bool enabled=true){var i=new ToolStripMenuItem(text){Checked=check,Enabled=enabled,Padding=new Padding(S(2),S(5),S(12),S(5))};if(click!=null)i.Click+=(s,e)=>click();items.Add(i);return i;}
 public static void Section(ToolStripItemCollection items,string text){var l=new ToolStripLabel(text){ForeColor=Faint,Font=Font(8.5f),Padding=new Padding(S(26),S(6),0,S(2))};items.Add(l);}
 sealed class Colors:ProfessionalColorTable{
  public override Color ToolStripDropDownBackground{get{return Surface;}}public override Color MenuBorder{get{return LineStrong;}}
  public override Color MenuItemSelected{get{return SelectedBack;}}public override Color MenuItemBorder{get{return SelectedBack;}}
  public override Color ImageMarginGradientBegin{get{return Surface;}}public override Color ImageMarginGradientMiddle{get{return Surface;}}public override Color ImageMarginGradientEnd{get{return Surface;}}
  public override Color SeparatorDark{get{return Line;}}public override Color SeparatorLight{get{return Surface;}}
 }
 sealed class MenuRenderer:ToolStripProfessionalRenderer{
  public MenuRenderer():base(new Colors()){RoundedEdges=false;}
  protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){Glyph(e.Graphics,IconCheck,9,Blue,e.ImageRectangle);}
  protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){if(!e.Item.Enabled)e.TextColor=Faint;else if(!(e.Item is ToolStripLabel))e.TextColor=Ink;base.OnRenderItemText(e);}
 }
}

public enum ButtonKind{Primary,Secondary,Ghost}
/// <summary>圆角扁平按钮：可带图标与下拉箭头，宽度随文字自动调整。</summary>
public sealed class FlatButton : Control {
 public ButtonKind Kind;public string Glyph;public Color Accent=Theme.Blue;public bool Dropdown,AutoWidth=true;bool hover,down;
 public FlatButton(string text,ButtonKind kind=ButtonKind.Secondary,string glyph=null){
  SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
  Kind=kind;Glyph=glyph;Font=Theme.Font(9.5f);Cursor=Cursors.Hand;Height=Theme.S(34);Text=text;TabStop=true;BackColor=Color.Transparent;Fit();
 }
 public void PerformClick(){if(Enabled)OnClick(EventArgs.Empty);}
 void Fit(){if(!AutoWidth)return;int w=Theme.S(Kind==ButtonKind.Ghost&&string.IsNullOrEmpty(Text)?0:24);if(!string.IsNullOrEmpty(Text))w+=TextRenderer.MeasureText(Text,Font).Width;if(Glyph!=null)w+=Theme.S(string.IsNullOrEmpty(Text)?34:22);if(Dropdown)w+=Theme.S(18);Width=Math.Max(w,Height);}
 protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Fit();Invalidate();}
 public void Refit(){Fit();Invalidate();}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=down=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){down=true;Invalidate();}base.OnMouseDown(e);}
 protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}
 protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
 protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
 protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
 protected override void OnKeyUp(KeyEventArgs e){if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter)PerformClick();base.OnKeyUp(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.Clear(Parent!=null?Parent.BackColor:Theme.Background);
  if(Parent is Card)g.Clear(Theme.Surface);
  Color back,fore,border=Color.Empty;
  if(Kind==ButtonKind.Primary){back=!Enabled?Theme.LineStrong:down?Theme.Darker(Accent,0.8):hover?Theme.Darker(Accent,0.9):Accent;fore=Color.White;}
  else if(Kind==ButtonKind.Secondary){back=down?Theme.Track:hover?Theme.HoverBack:Theme.Surface;fore=Enabled?Theme.Ink:Theme.Faint;border=hover?Theme.Faint:Theme.LineStrong;}
  else{back=down?Theme.Track:hover?Color.FromArgb(236,239,244):Color.Empty;fore=Enabled?Theme.Ink:Theme.Faint;}
  var r=new RectangleF(0.5f,0.5f,Width-1.5f,Height-1.5f);
  if(back!=Color.Empty||border!=Color.Empty)Theme.FillRound(g,back==Color.Empty?Theme.Surface:back,r,Theme.S(7),border==Color.Empty?(Color?)null:border);
  if(Focused&&ShowFocusCues)Theme.FillRound(g,Color.Transparent,new RectangleF(1.5f,1.5f,Width-3.5f,Height-3.5f),Theme.S(6),Theme.Blue,1.5f);
  int x=Theme.S(12);bool textOnly=Glyph==null;int tw=string.IsNullOrEmpty(Text)?0:TextRenderer.MeasureText(Text,Font).Width;
  int content=(Glyph!=null?Theme.S(string.IsNullOrEmpty(Text)?16:22):0)+tw+(Dropdown?Theme.S(18):0);x=(Width-content)/2;
  if(Glyph!=null){Theme.Glyph(g,Glyph,10,Kind==ButtonKind.Primary?fore:Enabled?Accent:Theme.Faint,new Rectangle(x,0,Theme.S(16),Height));x+=Theme.S(string.IsNullOrEmpty(Text)?16:22);}
  if(tw>0){TextRenderer.DrawText(g,Text,Font,new Rectangle(x,0,tw+Theme.S(2),Height),fore,TextFormatFlags.VerticalCenter|TextFormatFlags.Left|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);x+=tw;}
  if(Dropdown)Theme.Glyph(g,Theme.IconChevron,7,Kind==ButtonKind.Primary?fore:Theme.Muted,new Rectangle(x+Theme.S(4),0,Theme.S(14),Height));
 }
}

/// <summary>白色圆角卡片。</summary>
public class Card : Panel {
 bool highlight;public Color HighlightColor=Theme.Blue;
 public bool Highlight{get{return highlight;}set{if(highlight!=value){highlight=value;Invalidate();}}}
 public Card(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);BackColor=Theme.Background;Padding=new Padding(Theme.S(18),Theme.S(14),Theme.S(18),Theme.S(14));}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(BackColor);Theme.FillRound(g,Theme.Surface,new RectangleF(0.5f,0.5f,Width-1.5f,Height-1.5f),Theme.S(10),highlight?HighlightColor:Theme.Line,highlight?Theme.S(2):1);}
}

/// <summary>分段切换（页签）。</summary>
public sealed class Segmented : Control {
 readonly string[] items;int selected,hover=-1;public event Action Changed;
 public Segmented(params string[] items){this.items=items;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Font=Theme.Font(10);Cursor=Cursors.Hand;Height=Theme.S(36);Width=Widths().Sum()+Theme.S(6);}
 public int Count{get{return items.Length;}}
 public int SelectedIndex{get{return selected;}set{if(value==selected||value<0||value>=items.Length)return;selected=value;Invalidate();if(Changed!=null)Changed();}}
 int[] Widths(){var w=new int[items.Length];for(int i=0;i<items.Length;i++)w[i]=TextRenderer.MeasureText(items[i],Theme.Font(10,true)).Width+Theme.S(36);return w;}
 Rectangle ItemRect(int index){var w=Widths();int pad=Theme.S(3),x=pad;for(int i=0;i<index;i++)x+=w[i];return new Rectangle(x,pad,w[index]-(index==items.Length-1?0:0),Height-pad*2);}
 int Hit(Point p){for(int i=0;i<items.Length;i++)if(ItemRect(i).Contains(p))return i;return -1;}
 protected override void OnMouseMove(MouseEventArgs e){int h=Hit(e.Location);if(h!=hover){hover=h;Invalidate();}base.OnMouseMove(e);}
 protected override void OnMouseLeave(EventArgs e){hover=-1;Invalidate();base.OnMouseLeave(e);}
 protected override void OnMouseDown(MouseEventArgs e){int h=Hit(e.Location);if(h>=0)SelectedIndex=h;base.OnMouseDown(e);}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.Clear(Parent!=null?Parent.BackColor:Theme.Surface);
  Theme.FillRound(g,Theme.Track,new RectangleF(0,0,Width-1,Height-1),Theme.S(8));
  for(int i=0;i<items.Length;i++){var r=ItemRect(i);bool on=i==selected;
   if(on)Theme.FillRound(g,Theme.Surface,new RectangleF(r.X,r.Y,r.Width,r.Height-0.5f),Theme.S(6),Theme.Line);
   TextRenderer.DrawText(g,items[i],on?Theme.Font(10,true):Theme.Font(10),r,on?Theme.Ink:i==hover?Theme.Ink:Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}
 }
}
static class Sums{public static int Sum(this int[] a){int s=0;foreach(int x in a)s+=x;return s;}}

/// <summary>带圆点的状态胶囊。</summary>
public sealed class Pill : Control {
 Color color=Theme.Faint;
 public Pill(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Font=Theme.Font(9);Height=Theme.S(28);}
 public void Set(string text,Color c){color=c;Text=text;Width=TextRenderer.MeasureText(text,Font).Width+Theme.S(34);Invalidate();}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent!=null?Parent.BackColor:Theme.Surface);
  Theme.FillRound(g,Theme.Tint(color,0.12),new RectangleF(0,0,Width-1,Height-1),Height/2f);
  int d=Theme.S(8);var old=g.SmoothingMode;g.SmoothingMode=SmoothingMode.AntiAlias;using(var b=new SolidBrush(color))g.FillEllipse(b,Theme.S(12),(Height-d)/2f,d,d);g.SmoothingMode=old;
  TextRenderer.DrawText(g,Text,Font,new Rectangle(Theme.S(26),0,Width-Theme.S(28),Height),Theme.Darker(color,0.85),Theme.TextFlags);}
}

/// <summary>圆角输入框，可带前置图标与占位提示。</summary>
public sealed class InputBox : Panel {
 public readonly TextBox Box=new TextBox{BorderStyle=BorderStyle.None};readonly string glyph,cue;
 public InputBox(string cue,string glyph=null){this.cue=cue;this.glyph=glyph;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  Height=Theme.S(36);Box.Font=Theme.Font(10);Box.BackColor=Theme.Surface;Box.ForeColor=Theme.Ink;Controls.Add(Box);Cursor=Cursors.IBeam;
  Box.HandleCreated+=(s,e)=>Native.SendMessage(Box.Handle,0x1501,(IntPtr)1,this.cue);Box.GotFocus+=(s,e)=>Invalidate();Box.LostFocus+=(s,e)=>Invalidate();Click+=(s,e)=>Box.Focus();Arrange();}
 void Arrange(){int left=Theme.S(glyph!=null?34:12);Box.SetBounds(left,(Height-Box.PreferredHeight)/2,Math.Max(10,Width-left-Theme.S(10)),Box.PreferredHeight);}
 protected override void OnResize(EventArgs e){base.OnResize(e);Arrange();}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent!=null&&!(Parent is Card)?Parent.BackColor:Theme.Surface);
  bool focus=Box.Focused;Theme.FillRound(g,Theme.Surface,new RectangleF(0.5f,0.5f,Width-1.5f,Height-1.5f),Theme.S(7),focus?Theme.Blue:Theme.LineStrong,focus?1.6f:1);
  if(glyph!=null)Theme.Glyph(g,glyph,10,Theme.Faint,new Rectangle(Theme.S(8),0,Theme.S(22),Height));}
}

/// <summary>顶部提示条：只用于必须处理的问题。</summary>
public sealed class Banner : Control {
 public string Title,Detail;
 public Banner(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Dock=DockStyle.Top;Height=Theme.S(42);Visible=false;}
 public void Set(string title,string detail){if(title==null){Visible=false;return;}if(title!=Title||detail!=Detail){Title=title;Detail=detail;Invalidate();}Visible=true;Theme.Tips.SetToolTip(this,title+"\r\n"+detail);}
 protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Color.FromArgb(255,247,237));using(var p=new Pen(Color.FromArgb(253,215,170)))g.DrawLine(p,0,Height-1,Width,Height-1);
  int x=Theme.S(22);Theme.Glyph(g,Theme.IconWarning,11,Color.FromArgb(234,88,12),new Rectangle(x,0,Theme.S(20),Height));x+=Theme.S(30);
  var bold=Theme.Font(9.5f,true);int tw=TextRenderer.MeasureText(g,Title,bold,Size.Empty,TextFormatFlags.NoPadding).Width;
  TextRenderer.DrawText(g,Title,bold,new Rectangle(x,0,tw+Theme.S(4),Height),Color.FromArgb(154,52,18),Theme.TextFlags|TextFormatFlags.NoPadding);x+=tw+Theme.S(16);
  if(!string.IsNullOrEmpty(Detail))TextRenderer.DrawText(g,Detail,Theme.Font(9.5f),new Rectangle(x,0,Width-x-Theme.S(16),Height),Color.FromArgb(124,45,18),Theme.TextFlags|TextFormatFlags.NoPadding);}
}

/// <summary>简单的单行输入对话框。</summary>
public static class Prompt {
 public static string Ask(IWin32Window owner,string title,string value){
  using(var f=new Form{Text=title,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Surface,Font=Theme.Font(10),ClientSize=new Size(Theme.S(420),Theme.S(130))}){
   var input=new InputBox("");input.Box.Text=value;input.SetBounds(Theme.S(20),Theme.S(22),Theme.S(380),Theme.S(36));f.Controls.Add(input);
   var ok=new FlatButton("确定",ButtonKind.Primary){AutoWidth=false,Width=Theme.S(90)};ok.Location=new Point(Theme.S(310),Theme.S(78));ok.Click+=(s,e)=>{f.DialogResult=DialogResult.OK;};f.Controls.Add(ok);
   var cancel=new FlatButton("取消"){AutoWidth=false,Width=Theme.S(90)};cancel.Location=new Point(Theme.S(210),Theme.S(78));cancel.Click+=(s,e)=>{f.DialogResult=DialogResult.Cancel;};f.Controls.Add(cancel);
   input.Box.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;f.DialogResult=DialogResult.OK;}else if(e.KeyCode==Keys.Escape){e.SuppressKeyPress=true;f.DialogResult=DialogResult.Cancel;}};
   f.Shown+=(s,e)=>{input.Box.Focus();input.Box.SelectAll();};
   return f.ShowDialog(owner)==DialogResult.OK?input.Box.Text.Trim():null;
  }
 }
}
}

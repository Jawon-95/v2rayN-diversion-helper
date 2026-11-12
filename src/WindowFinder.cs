using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppRoutingManager {
/// <summary>类似 Spy++ / Visual Studio 的“查找窗口”准星：按住拖到任意窗口上，松开即返回该窗口所属进程。</summary>
public sealed class WindowFinder : Control {
 public event Action<int> Picked;
 public event Action<string> Hovering;
 public event Action<bool> DragChanged;
 public bool Dragging{get;private set;}
 IntPtr current;FrameOverlay overlay;readonly int self;

 public WindowFinder(){
  SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.UserPaint|ControlStyles.ResizeRedraw,true);
  Cursor=Cursors.Hand;BackColor=Color.White;using(var me=Process.GetCurrentProcess())self=me.Id;
 }
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;var r=ClientRectangle;r.Width--;r.Height--;
  using(var border=new Pen(Dragging?Theme.Blue:Theme.Line))g.DrawRectangle(border,r);
  float cx=Width/2f,cy=Height/2f,rad=Math.Min(Width,Height)*0.27f;
  using(var pen=new Pen(Theme.Blue,Math.Max(1.5f,Width/22f))){
   g.DrawEllipse(pen,cx-rad,cy-rad,rad*2,rad*2);
   if(!Dragging){float l=rad*1.55f,gap=rad*0.35f;g.DrawLine(pen,cx-l,cy,cx-gap,cy);g.DrawLine(pen,cx+gap,cy,cx+l,cy);g.DrawLine(pen,cx,cy-l,cx,cy-gap);g.DrawLine(pen,cx,cy+gap,cx,cy+l);}
  }
 }
 protected override void OnMouseDown(MouseEventArgs e){
  base.OnMouseDown(e);if(e.Button!=MouseButtons.Left||Dragging)return;
  Dragging=true;current=IntPtr.Zero;Capture=true;Cursor=Cursors.Cross;Invalidate();
  if(DragChanged!=null)DragChanged(true);Say("移动到想要分流的应用窗口上，然后松开鼠标…");
 }
 protected override void OnMouseMove(MouseEventArgs e){
  base.OnMouseMove(e);if(!Dragging)return;
  IntPtr h=Hit(Cursor.Position);if(h==current)return;current=h;
  if(h==IntPtr.Zero){if(overlay!=null)overlay.Hide();Say("移动到想要分流的应用窗口上，然后松开鼠标…");return;}
  if(overlay==null)overlay=new FrameOverlay();overlay.Track(Native.Bounds(h));
  int pid=Native.PidOf(h);string title=Native.Text(h);
  Say("松开以选择：  "+ProcessScanner.NameOf(pid)+"  ·  PID "+pid+(string.IsNullOrEmpty(title)?"":"  ·  "+title));
 }
 protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);Finish(true);}
 protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(Dragging&&!Capture)Finish(Control.MouseButtons==MouseButtons.None);}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Escape){current=IntPtr.Zero;Finish(false);}}
 void Finish(bool pick){
  if(!Dragging)return;IntPtr h=current;Dragging=false;current=IntPtr.Zero;
  if(Capture)Capture=false;Cursor=Cursors.Hand;if(overlay!=null){overlay.Close();overlay.Dispose();overlay=null;}Invalidate();
  if(DragChanged!=null)DragChanged(false);
  if(pick&&h!=IntPtr.Zero&&Picked!=null)Picked(Native.PidOf(h));
 }
 void Say(string text){if(Hovering!=null)Hovering(text);}
 bool Usable(IntPtr h){return h!=IntPtr.Zero&&Native.RawPid(h)!=self&&!Native.IsShell(h);}
 IntPtr Hit(Point pt){
  IntPtr h=Native.WindowFromPoint(new Native.POINT(pt.X,pt.Y));if(h!=IntPtr.Zero)h=Native.GetAncestor(h,2);
  if(h!=IntPtr.Zero&&Native.RawPid(h)!=self)return Usable(h)?h:IntPtr.Zero;
  // 光标位于本工具自己的窗口（或高亮框）上：按 Z 序找它下面的窗口
  IntPtr found=IntPtr.Zero;
  Native.EnumWindows((w,l)=>{
   if(!Native.IsWindowVisible(w)||Native.RawPid(w)==self||Native.Cloaked(w))return true;
   if((Native.GetWindowLong(w,-20)&0x20)!=0)return true;
   if(!Native.Bounds(w).Contains(pt))return true;
   found=w;return false;
  },IntPtr.Zero);
  return Usable(found)?found:IntPtr.Zero;
 }
}

/// <summary>鼠标拖动时围绕目标窗口的红色高亮框（只有边框，可点击穿透）。</summary>
sealed class FrameOverlay : Form {
 public FrameOverlay(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(232,62,80);Opacity=0.9;}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ExStyle|=0x80|0x20|0x08000000;return cp;}}
 public void Track(Rectangle r){
  int t=Math.Max(3,(int)Math.Round(DeviceDpiScale()*3));
  if(r.Width<t*2+2||r.Height<t*2+2){Hide();return;}
  Bounds=r;var region=new Region(new Rectangle(0,0,r.Width,r.Height));region.Exclude(new Rectangle(t,t,r.Width-t*2,r.Height-t*2));
  var old=Region;Region=region;if(old!=null)old.Dispose();
  if(!Visible)Show();
 }
 float DeviceDpiScale(){using(var g=CreateGraphics())return g.DpiX/96f;}
}
}

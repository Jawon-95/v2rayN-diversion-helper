using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace AppRoutingManager {
/// <summary>窗口底部的轻提示：滑入、停留约 2.5 秒后自动滑出消失，无需点击确定。</summary>
public sealed class Toast : Form {
 const int InMs=180,HoldMs=2500,OutMs=260;
 readonly string title,detail;readonly bool ok;readonly Timer timer=new Timer{Interval=15};readonly Stopwatch clock=new Stopwatch();
 readonly Font titleFont=new Font("Microsoft YaHei UI",10.5f,FontStyle.Bold),detailFont=new Font("Microsoft YaHei UI",9f);
 readonly float scale;readonly int baseTop,lift;long shift;

 public static void Notify(Form owner,string title,string detail,bool ok){
  if(owner==null||owner.IsDisposed)return;
  if(owner.InvokeRequired){owner.BeginInvoke(new Action(()=>Notify(owner,title,detail,ok)));return;}
  if(!owner.Visible)return;
  foreach(var old in owner.OwnedForms.OfType<Toast>().ToList())old.Close();
  var toast=new Toast(owner,title,detail,ok);
  // 所属窗口关闭时立即结束提示，避免计时器继续访问已释放的窗口
  FormClosingEventHandler closing=null;closing=(s,e)=>{if(!e.Cancel)toast.Finish();};owner.FormClosing+=closing;
  toast.FormClosed+=(s,e)=>owner.FormClosing-=closing;
  toast.Show(owner);toast.clock.Start();toast.timer.Start();
 }
 void Finish(){timer.Stop();if(!IsDisposed)Close();}
 Toast(Form owner,string title,string detail,bool ok){
  this.title=title;this.detail=detail;this.ok=ok;
  FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;BackColor=Color.FromArgb(30,41,59);Opacity=0;DoubleBuffered=true;Cursor=Cursors.Hand;
  using(var g=owner.CreateGraphics())scale=g.DpiX/96f;
  Size ts=TextRenderer.MeasureText(title,titleFont),ds=string.IsNullOrEmpty(detail)?Size.Empty:TextRenderer.MeasureText(detail,detailFont);
  int w=Math.Max(ts.Width,ds.Width)+S(18+26+12+22),h=S(14)+ts.Height+(ds.Height>0?S(2)+ds.Height:0)+S(14);
  var area=owner.RectangleToScreen(owner.ClientRectangle);w=Math.Min(w,Math.Max(S(200),area.Width-S(40)));
  Size=new Size(w,h);lift=S(18);baseTop=area.Bottom-h-S(76);Left=area.Left+(area.Width-w)/2;Top=baseTop+lift;
  using(var path=Rounded(new Rectangle(0,0,w,h),S(10)))Region=new Region(path);
  timer.Tick+=Step;Click+=(s,e)=>Close();
  FormClosed+=(s,e)=>{timer.Dispose();titleFont.Dispose();detailFont.Dispose();};
 }
 int S(double v){return (int)Math.Round(v*scale);}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ExStyle|=0x80|0x08000000;return cp;}}
 static double Ease(double t){t=Math.Max(0,Math.Min(1,t));return 1-Math.Pow(1-t,3);}
 void Step(object sender,EventArgs e){
  if(IsDisposed||Owner==null||Owner.IsDisposed||Owner.Disposing){timer.Stop();if(!IsDisposed)Dispose();return;}
  long t=clock.ElapsedMilliseconds-shift;double o;int offset;
  if(t<InMs){double k=Ease(t/(double)InMs);o=k;offset=(int)((1-k)*lift);}
  else if(t<InMs+HoldMs){o=1;offset=0;if(Bounds.Contains(Cursor.Position))shift=clock.ElapsedMilliseconds-InMs;} // 鼠标停在提示上时暂不消失

  else if(t<InMs+HoldMs+OutMs){double k=Ease((t-InMs-HoldMs)/(double)OutMs);o=1-k;offset=(int)(k*lift);}
  else{Finish();return;}
  Opacity=o*0.97;Top=baseTop+offset;
 }
 static GraphicsPath Rounded(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
 protected override void OnPaint(PaintEventArgs e){
  var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
  int d=S(22),x=S(18),y=(Height-d)/2;var circle=new Rectangle(x,y,d,d);
  using(var b=new SolidBrush(ok?Color.FromArgb(34,197,94):Color.FromArgb(245,158,11)))g.FillEllipse(b,circle);
  using(var pen=new Pen(Color.White,Math.Max(2f,scale*2f))){pen.StartCap=pen.EndCap=LineCap.Round;
   if(ok)g.DrawLines(pen,new[]{new PointF(x+d*0.28f,y+d*0.52f),new PointF(x+d*0.44f,y+d*0.68f),new PointF(x+d*0.72f,y+d*0.36f)});
   else{g.DrawLine(pen,x+d/2f,y+d*0.27f,x+d/2f,y+d*0.56f);g.DrawLine(pen,x+d/2f,y+d*0.73f,x+d/2f,y+d*0.74f);}}
  int tx=x+d+S(12);var ts=TextRenderer.MeasureText(title,titleFont);int ty=S(14);
  var flags=TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis;
  TextRenderer.DrawText(g,title,titleFont,new Rectangle(tx,ty,Width-tx-S(16),ts.Height),Color.White,flags);
  if(!string.IsNullOrEmpty(detail))TextRenderer.DrawText(g,detail,detailFont,new Rectangle(tx,ty+ts.Height+S(2),Width-tx-S(16),Height),Color.FromArgb(203,213,225),flags);
 }
}
}

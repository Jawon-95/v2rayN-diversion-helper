using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppRoutingManager;
// 用法：UiTests.exe <v2rayN 测试夹具目录> <截图输出目录>
public static class UiTests {
 static int passed;
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
 static void Assert(bool ok,string text){if(!ok)throw new Exception("FAILED: "+text);passed++;Console.WriteLine("PASS "+text);}
 static void Wait(Func<bool> done,int ms){var c=Stopwatch.StartNew();while(!done()&&c.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}Application.DoEvents();}
 static void Await(Task t,int ms){Wait(()=>t.IsCompleted,ms);t.GetAwaiter().GetResult();}
 static void Shot(Control c,string path){using(var image=new Bitmap(c.Width,c.Height)){c.DrawToBitmap(image,new Rectangle(Point.Empty,c.Size));image.Save(path,ImageFormat.Png);}}
 [STAThread]public static int Main(string[] args){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  WindowsFormsSynchronizationContext.AutoInstall=false;SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
  string root=args[0],shots=args[1];Directory.CreateDirectory(shots);
  using(var form=new MainForm(root)){
   form.Show();Wait(()=>Field(form,"store")!=null&&!(bool)Field(form,"busy"),15000);
   var a=(AppPane)Field(form,"direct");var b=(AppPane)Field(form,"split");
   Assert(a.Grid.Rows.Count==a.Entries.Count&&b.Grid.Rows.Count==b.Entries.Count,"main lists populate both grids");
   Shot(form,Path.Combine(shots,"main.png"));
   var fake=new AppEntry("ui-autosave-test.exe");Call(form,"Insert",a,fake);Assert(form.IsDirty,"main form change marks dirty");
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);
   Func<RouteSnapshot> live=()=>{var st=new RoutingStore(root);return st.Load(st.Profiles().First(p=>p.Managed&&p.Active));};
   Assert(live().Direct.Any(x=>x.Match==fake.Match),"main form change is auto-saved to v2rayN");
   Assert(form.OwnedForms.OfType<Toast>().Any(),"auto-save shows a toast");
   int row=a.Entries.FindIndex(x=>x.Match==fake.Match);a.Grid.ClearSelection();a.Grid.Rows[row].Selected=true;a.RemoveSelected();
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(!live().Direct.Any(x=>x.Match==fake.Match),"deleting is auto-saved too");
   Assert(form.Controls.Find("save",true).Length==0&&form.GetType().GetField("save",BindingFlags.NonPublic|BindingFlags.Instance)==null,"main save button is removed");
   using(var quick=new ProcessPicker(form,b,a,false)){quick.Show();Application.DoEvents();Toast.Notify(quick,"测试","窗口关闭时提示仍在显示",true);Thread.Sleep(50);Application.DoEvents();quick.Close();}
   Wait(()=>false,1200);Assert(true,"closing a window while its toast is animating does not crash");
   foreach(bool byPath in new[]{false,true}){
    using(var picker=new ProcessPicker(form,b,a,byPath)){
     picker.Show();Await((Task)Call(picker,"Reload"),15000);
     var tops=(System.Collections.Generic.List<ProcGroup>)Field(picker,"tops");var grid=(DataGridView)Field(picker,"grid");var save=(Button)Field(picker,"save");
     Assert(tops.Count>0&&grid.RowCount>0,"picker lists processes ("+(byPath?"path":"name")+")");
     Assert(tops.Any(g=>g.Section==0)&&tops.Any(g=>g.Section!=0),"apps and background processes are separated");
     Assert(tops.Any(g=>g.Image!=null),"process icons are loaded");
     var multi=tops.FirstOrDefault(g=>g.Items.Count>1||g.Children.Count>0);
     if(multi!=null){int before=grid.RowCount;Call(picker,"ToggleExpand",multi);Assert(grid.RowCount>before,"expanding a group shows its child processes");}
     Assert(!save.Enabled,"save button is disabled without changes");
     var candidate=tops.First(g=>g.Selectable&&!b.Entries.Any(x=>string.Equals(x.Match,g.Match,StringComparison.OrdinalIgnoreCase)));
     var located=tops.Last(g=>g.Items.Count>0);Await((Task)Call(picker,"Pick",located.Items[0].Pid),15000);
     var cur=grid.CurrentCell;Assert(cur!=null&&grid.Rows[cur.RowIndex].Selected,"crosshair pick selects a row");
     Assert(((System.Collections.IDictionary)Field(picker,"pending")).Count==0&&!save.Enabled,"crosshair pick does not change check state");
     Shot(picker,Path.Combine(shots,"picker-located-"+(byPath?"path":"name")+".png"));
     Call(picker,"Toggle",candidate);Assert(save.Enabled,"checking a process enables save");
     Shot(picker,Path.Combine(shots,"picker-"+(byPath?"path":"name")+".png"));
     Call(picker,"Toggle",candidate);Assert(!save.Enabled,"unchecking it again disables save");
     if(!byPath){
      Call(picker,"Toggle",candidate);Assert(save.Enabled,"re-check enables save");
      var task=(Task<bool>)Call(picker,"SaveChanges",picker);Await(task,60000);
      Assert(task.Result,"saving from the picker succeeds");
      Assert(new RoutingStore(root).Load(new RoutingStore(root).Profiles().First(p=>p.Managed&&p.Active)).Split.Any(x=>string.Equals(x.Match,candidate.Match,StringComparison.OrdinalIgnoreCase)),"saved entry is persisted to v2rayN");
      Assert(!save.Enabled&&!form.IsDirty,"after saving the button dims again");
      Assert(picker.OwnedForms.OfType<Toast>().Any(),"a toast notification is shown");
      Application.DoEvents();Thread.Sleep(400);Application.DoEvents();Shot(picker,Path.Combine(shots,"picker-saved.png"));
      using(var t=new Bitmap(picker.OwnedForms.OfType<Toast>().First().Width,picker.OwnedForms.OfType<Toast>().First().Height)){var toast=picker.OwnedForms.OfType<Toast>().First();toast.DrawToBitmap(t,new Rectangle(Point.Empty,toast.Size));t.Save(Path.Combine(shots,"toast.png"),ImageFormat.Png);}
      Wait(()=>!picker.OwnedForms.OfType<Toast>().Any(),5000);foreach(var x in picker.OwnedForms.OfType<Toast>())Console.WriteLine("toast left: opacity="+x.Opacity+" visible="+x.Visible+" disposed="+x.IsDisposed+" bounds="+x.Bounds+" cursor="+Cursor.Position);Assert(!picker.OwnedForms.OfType<Toast>().Any(),"toast disappears by itself");
      Call(picker,"Toggle",candidate);var undo=(Task<bool>)Call(picker,"SaveChanges",picker);Await(undo,60000);Assert(undo.Result&&!b.Entries.Any(x=>string.Equals(x.Match,candidate.Match,StringComparison.OrdinalIgnoreCase)),"unchecking and saving removes the entry");
     }
     picker.Close();
    }
   }
  }
  Console.WriteLine("ALL "+passed+" UI CHECKS PASSED");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

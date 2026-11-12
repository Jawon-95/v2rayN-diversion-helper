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
public static class UiTests {
 static int passed;
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
 static void Assert(bool ok,string text){if(!ok)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
 [STAThread]public static int Main(string[] args){try{
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  using(var form=new MainForm(args[0])){
   var store=new RoutingStore(args[0]);typeof(MainForm).GetField("store",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,store);((TextBox)Field(form,"folder")).Text=args[0];
   Call(form,"LoadProfiles",new object[]{null});form.Show();var ready=Stopwatch.StartNew();while((bool)Field(form,"busy")&&ready.ElapsedMilliseconds<10000){Application.DoEvents();Thread.Sleep(10);}Application.DoEvents();form.PerformLayout();
   var a=(AppPane)Field(form,"direct");var b=(AppPane)Field(form,"split");Assert(a.Grid.Rows.Count==a.Entries.Count&&b.Grid.Rows.Count==b.Entries.Count,"native lists populate both grids");Assert(((TextBox)Field(form,"name")).Text==store.Profiles().First(p=>p.Managed&&p.Active).Name,"rule-set name is loaded into editable field");var save=(Button)Field(form,"save");Assert(save.Right<=save.Parent.ClientSize.Width&&save.Left>=0,"save button stays inside the visible window");form.Size=form.MinimumSize;form.PerformLayout();Assert(save.Right<=save.Parent.ClientSize.Width&&save.Left>=0,"save button remains visible at minimum window size");form.Size=new Size(1160,780);form.PerformLayout();
   using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(args[1],ImageFormat.Png);}
   var fake=new AppEntry("ui-test-only.exe");Call(form,"Insert",a,fake);Assert(a.Entries.Any(x=>x.Match==fake.Match),"add application to direct list");Call(form,"Insert",b,fake);Assert(!a.Entries.Any(x=>x.Match==fake.Match)&&b.Entries.Any(x=>x.Match==fake.Match),"moving an entry prevents exact duplicate across lists");int row=b.Entries.FindIndex(x=>x.Match==fake.Match);b.Grid.ClearSelection();b.Grid.Rows[row].Selected=true;b.RemoveSelected();Assert(!b.Entries.Any(x=>x.Match==fake.Match),"delete selected application updates the list");
  }
  foreach(bool fullPath in new[]{false,true}){
   using(var picker=new ProcessPicker(fullPath)){
    picker.Show();var task=(Task)Call(picker,"RefreshProcesses");var clock=Stopwatch.StartNew();while(!task.IsCompleted&&clock.ElapsedMilliseconds<12000){Application.DoEvents();Thread.Sleep(10);}task.GetAwaiter().GetResult();Application.DoEvents();
    var search=(TextBox)Field(picker,"search");search.Text=Process.GetCurrentProcess().ProcessName;var grid=(DataGridView)Field(picker,"grid");Assert(grid.Rows.Count>0,"process picker lists and filters running processes");grid.ClearSelection();grid.Rows[0].Selected=true;
    using(var image=new Bitmap(picker.Width,picker.Height)){picker.CreateControl();picker.DrawToBitmap(image,new Rectangle(Point.Empty,picker.Size));if(!fullPath)image.Save(Path.Combine(Path.GetDirectoryName(args[1]),"process-picker-preview.png"),ImageFormat.Png);}
    Call(picker,"AcceptRows");Assert(picker.SelectedEntries.Count==1&&picker.SelectedEntries[0].ByPath==fullPath,"double-click selection can add "+(fullPath?"full path":"process name"));
   }
  }
  Console.WriteLine("ALL "+passed+" UI CHECKS PASSED");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}

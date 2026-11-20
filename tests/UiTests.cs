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
using System.Runtime.InteropServices;
using AppRoutingManager;
// 用法：UiTests.exe <v2rayN 测试夹具目录> <截图输出目录>
public static class UiTests {
 static int passed;
 static object Field(object o,string name){return o.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);}
 static object Call(object o,string name,params object[] args){return o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);}
 [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,int msg,IntPtr w,IntPtr l);
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
   var a=(AppPane)Field(form,"direct");var b=(AppPane)Field(form,"split");var split=b;
   Assert(a.Grid.Rows.Count==a.Entries.Count&&b.Grid.Rows.Count==b.Entries.Count,"main lists populate both grids");
   Shot(form,Path.Combine(shots,"main.png"));
   var fake=new AppEntry("ui-autosave-test.exe");Call(form,"Insert",a,fake);Assert(form.IsDirty,"main form change marks dirty");
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);
   Func<RouteSnapshot> live=()=>{var st=new RoutingStore(root);return st.Load(st.Profiles().First(p=>p.Managed&&p.Active));};
   Assert(live().Direct.Any(x=>x.Match==fake.Match),"main form change is auto-saved to v2rayN");
   Assert(form.OwnedForms.OfType<Toast>().Any(),"auto-save shows a toast");
   int row=a.Entries.FindIndex(x=>x.Match==fake.Match);a.Grid.ClearSelection();a.Grid.Rows[row].Selected=true;a.RemoveSelected();
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(!live().Direct.Any(x=>x.Match==fake.Match),"deleting is auto-saved too");
   var dh=(HostPane)Field(form,"directHosts");var ph=(HostPane)Field(form,"proxyHosts");var nav=(Segmented)Field(form,"nav");
   Assert(nav.Count==2&&dh.Grid.Rows.Count==dh.Entries.Count,"domain / ip page is present");
   Assert(dh.Commit(null,"https://www.ui-host-test.example/path?x=1")==null&&form.IsDirty,"pasted url is added and marks dirty");
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(live().DirectHosts.Any(x=>x.Value=="domain:ui-host-test.example"),"host change is auto-saved");
   dh.Grid.ClearSelection();dh.Grid.Rows[dh.Entries.FindIndex(x=>x.Value=="domain:ui-host-test.example")].Selected=true;Call(form,"MoveHosts",dh);
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(live().ProxyHosts.Any(x=>x.Value=="domain:ui-host-test.example")&&!live().DirectHosts.Any(x=>x.Value=="domain:ui-host-test.example"),"moving a host to the proxy list is saved");
   Assert(ph.Commit(null,"ui-host-test.example")!=null,"duplicate in the same list is rejected");
   nav.SelectedIndex=1;form.Activate();Application.DoEvents();Assert(dh.Visible&&!a.Visible,"switching to the domain page");
   string tag="inline-"+Guid.NewGuid().ToString("N").Substring(0,8);int count=dh.Entries.Count;
   dh.Input.Box.Text="https://www."+tag+".example/a/b?c";dh.Submit();
   Assert(dh.Entries.Any(x=>x.Value=="domain:"+tag+".example")&&dh.Grid.RowCount==dh.Entries.Count&&dh.Input.Box.Text=="","input box adds entry reduced to main domain");
   dh.Input.Box.Text="bad..domain";dh.Submit();Shot(form,Path.Combine(shots,"main-hosts-error.png"));
   Assert(dh.Entries.Count==count+1&&dh.Input.Box.Text=="bad..domain","invalid input is rejected and kept for fixing");dh.Input.Box.Text="";
   int pos=dh.Entries.FindIndex(x=>x.Value=="domain:"+tag+".example");dh.Edit(pos);Application.DoEvents();Assert(dh.Grid.IsCurrentCellInEditMode,"double-click style edit starts inline editing");
   ((TextBox)dh.Grid.EditingControl).Text="full:api."+tag+".example";dh.Grid.NotifyCurrentCellDirty(true);Shot(form,Path.Combine(shots,"main-hosts-editing.png"));Assert(dh.Grid.EndEdit(),"valid inline edit commits");Wait(()=>dh.Entries[pos].Value!="domain:"+tag+".example",3000);
   Assert(dh.Entries[pos].Value=="full:api."+tag+".example"&&dh.Grid.RowCount==dh.Entries.Count,"inline edit replaces the row in place");
   dh.Edit(pos);Application.DoEvents();((TextBox)dh.Grid.EditingControl).Text="bad..domain";dh.Grid.NotifyCurrentCellDirty(true);dh.Grid.EndEdit();Wait(()=>false,300);Assert(dh.Entries[pos].Value=="full:api."+tag+".example"&&dh.Grid.RowCount==dh.Entries.Count,"invalid inline edit is refused");
   dh.Edit(pos);Application.DoEvents();dh.Grid.CancelEdit();dh.Grid.EndEdit();Wait(()=>false,300);Assert(dh.Entries[pos].Value=="full:api."+tag+".example"&&dh.Grid.RowCount==dh.Entries.Count,"cancelled edit keeps the old value");
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(live().DirectHosts.Any(x=>x.Value=="full:api."+tag+".example"),"inline edits are auto-saved");
   Assert(dh.Commit(dh.Entries[pos],"")==null&&!dh.Entries.Any(x=>x.Value.Contains(tag)),"clearing a row deletes it");
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(!live().DirectHosts.Any(x=>x.Value.Contains(tag)),"deletion is auto-saved");
   Shot(form,Path.Combine(shots,"main-hosts.png"));
   ph.Grid.ClearSelection();ph.Grid.Rows[ph.Entries.FindIndex(x=>x.Value=="domain:ui-host-test.example")].Selected=true;ph.RemoveSelected();
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(!live().ProxyHosts.Any(x=>x.Value=="domain:ui-host-test.example"),"deleting a host is saved");
   nav.SelectedIndex=0;Application.DoEvents();
   string notepad=Path.Combine(Environment.SystemDirectory,"notepad.exe");Call(form,"AddFiles",split,new[]{notepad});Assert(split.Entries.Any(x=>x.Match=="notepad.exe"),"dropped exe is added");
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);split.Grid.ClearSelection();split.Grid.Rows[split.Entries.FindIndex(x=>x.Match=="notepad.exe")].Selected=true;split.RemoveSelected();
   Wait(()=>!form.IsDirty&&!(bool)Field(form,"busy"),60000);Assert(!live().Split.Any(x=>x.Match=="notepad.exe"),"removing the dropped exe is saved");
   // 规则集：新建 → 重命名 → 删除
   var pp=(ProfilePane)Field(form,"profilePane");Func<System.Collections.Generic.List<RouteProfile>> managed=()=>new RoutingStore(root).Profiles().Where(p=>p.Managed).ToList();
   int profileCount=managed().Count;Assert(pp.Grid.RowCount==profileCount&&pp.Grid.SelectedRows.Count==1,"profile list shows every rule set with the current one selected");
   Call(form,"NewProfile");Wait(()=>managed().Count==profileCount+1&&!(bool)Field(form,"busy"),60000);Application.DoEvents();
   Assert(pp.Grid.RowCount==profileCount+1&&pp.Grid.IsCurrentCellInEditMode,"new rule set is saved and enters rename");
   string pname="UI 规则集 "+Guid.NewGuid().ToString("N").Substring(0,6);((TextBox)pp.Grid.EditingControl).Text=pname;pp.Grid.NotifyCurrentCellDirty(true);Shot(form,Path.Combine(shots,"main-profiles.png"));pp.Grid.EndEdit();
   Wait(()=>managed().Any(p=>p.Name==pname),5000);Assert(managed().Any(p=>p.Name==pname)&&pp.Items.Any(x=>x.Name==pname),"inline rename is saved");
   pname=pname+" b";pp.Edit(pp.Items.FindIndex(x=>x.Name!=null&&x.Name.StartsWith("UI ")));Application.DoEvents();((TextBox)pp.Grid.EditingControl).Text=pname;pp.Grid.NotifyCurrentCellDirty(true);PostMessage(pp.Handle,0x201,(IntPtr)1,IntPtr.Zero);PostMessage(pp.Handle,0x202,IntPtr.Zero,IntPtr.Zero);Wait(()=>managed().Any(p=>p.Name==pname),5000);Application.DoEvents();Assert(!pp.Grid.IsCurrentCellInEditMode&&managed().Any(p=>p.Name==pname),"clicking the card background finishes the rename");
   string dup=managed().First(p=>p.Name!=pname).Name;var newItem=pp.Items.First(x=>x.Name==pname);
   Assert(((Func<ProfilePane.Item,string,string>)pp.Renamer)(newItem,dup)!=null,"rename to an existing name is refused");
   var activeItem=pp.Items.FirstOrDefault(x=>x.Active);string confirmed=null;((MainForm)form).ConfirmDelete=n=>{confirmed=n;return true;};
   if(activeItem!=null){Call(form,"DeleteProfile",activeItem);Wait(()=>false,300);Assert(confirmed==null&&managed().Any(p=>p.Id==activeItem.Id),"active rule set cannot be deleted");}
   Call(form,"DeleteProfile",newItem);Wait(()=>!managed().Any(p=>p.Name==pname)&&!(bool)Field(form,"busy"),30000);Application.DoEvents();
   Assert(confirmed==pname&&!managed().Any(p=>p.Name==pname)&&pp.Grid.RowCount==profileCount&&pp.Items.All(x=>x.Name!=pname),"deleting a rule set asks first and removes it");
   var nowSnap=(RouteSnapshot)Field(form,"snapshot");Assert(nowSnap!=null&&nowSnap.Profile!=null&&nowSnap.Profile.Active,"after deleting the current rule set the active one is loaded");
   Assert(form.Controls.Find("save",true).Length==0&&form.GetType().GetField("save",BindingFlags.NonPublic|BindingFlags.Instance)==null,"main save button is removed");
   using(var quick=new ProcessPicker(form,b,a,false)){quick.Show();Application.DoEvents();Toast.Notify(quick,"测试","窗口关闭时提示仍在显示",true);Thread.Sleep(50);Application.DoEvents();quick.Close();}
   Wait(()=>false,1200);Assert(true,"closing a window while its toast is animating does not crash");
   foreach(bool byPath in new[]{false,true}){
    using(var picker=new ProcessPicker(form,b,a,byPath)){
     picker.Show();Await((Task)Call(picker,"Reload"),15000);
     var tops=(System.Collections.Generic.List<ProcGroup>)Field(picker,"tops");var grid=(DataGridView)Field(picker,"grid");var save=(Control)Field(picker,"save");
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

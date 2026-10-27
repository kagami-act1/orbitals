using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    static class AppIconTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        static void Set(Controller owner,string name,object value) { typeof(Controller).GetField(name,Private).SetValue(owner,value); }
        static AppIconState State(Controller owner) { return (AppIconState)typeof(Controller).GetProperty("TrayState",Private).GetValue(owner,null); }
        static void Refresh(Controller owner) { typeof(Controller).GetMethod("RefreshTrayState",Private).Invoke(owner,null); }
        public static List<string> Run() {
            var result=new List<string>();
            foreach(AppIconState state in new[]{AppIconState.Running,AppIconState.Stopped,AppIconState.Error}) {
                string resource="OrbitRing.AppIcon"+(state==AppIconState.Running?"":state==AppIconState.Stopped?".Stopped":".Error");
                using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)) {
                    BitmapDecoder decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                    Check(decoder.Frames.Select(f=>f.PixelWidth).OrderBy(x=>x).SequenceEqual(new[]{16,20,24,32,48,64,128,256}),"supplied "+state+" artwork embeds all native Windows sizes including optimized 16 and 20 pixel frames",result);
                }
                foreach(int size in new[]{16,20,24,32}) using(System.Drawing.Icon icon=AppIcon.Tray(state,size)) Check(icon.Width==size && icon.Height==size,"tray "+state+" loads its supplied native "+size+" pixel artwork",result);
                using(System.Drawing.Icon icon=AppIcon.Tray(state)) using(System.Drawing.Bitmap bitmap=icon.ToBitmap()) {
                    var c=bitmap.GetPixel(16,16); Check(state==AppIconState.Running?c.G>c.R+30:state==AppIconState.Error?c.R>c.G+50:c.B>c.R && c.R<150,"the "+state+" planet retains its supplied state color",result);
                }
            }
            Check(AppIcon.State(false,true,true,false)==AppIconState.Running && AppIcon.State(true,true,true,true)==AppIconState.Stopped && AppIcon.State(false,false,false,true)==AppIconState.Stopped && AppIcon.State(false,true,false,true)==AppIconState.Error,"intentional pause and disabled features take precedence over operational errors",result);
            using(Controller owner=new Controller(true)) {
                string saved=Json.Write(owner.Config); Set(owner,"settingsLoadFailed",true); Check(State(owner)==AppIconState.Error,"a settings load failure selects the supplied error icon",result);
                owner.Apply(owner.Config.Clone()); Check(State(owner)==AppIconState.Running,"applying valid settings clears the settings load error state",result);
                Set(owner,"planetRegistrationFailed",true); Check(State(owner)==AppIconState.Error,"a persistent invocation-key registration failure selects the error state",result);
                Set(owner,"planetRegistrationFailed",false); Check(State(owner)==AppIconState.Running && Json.Write(owner.Config)==saved,"health recovery restores the running icon without changing saved preferences",result);
                Forms.NotifyIcon tray=new Forms.NotifyIcon { Visible=false }; Set(owner,"tray",tray); Refresh(owner); var normal=tray.Icon; Refresh(owner);
                Check(Object.ReferenceEquals(normal,tray.Icon) && tray.Icon.Width==AppIcon.TraySize,"tray refresh reuses the cached running artwork at the Windows small-icon size",result);
                Set(owner,"paused",true); Refresh(owner); var stopped=tray.Icon; Check(!Object.ReferenceEquals(normal,stopped) && tray.Text.Contains("一時停止"),"pausing switches to the supplied stopped artwork and accessible state text",result);
                Set(owner,"paused",false); Set(owner,"settingsLoadFailed",true); Refresh(owner); var error=tray.Icon;
                Check(!Object.ReferenceEquals(error,normal) && !Object.ReferenceEquals(error,stopped) && tray.Text.Contains("読み込みエラー") && tray.Text.Length<=63,"error status uses its own artwork and a bounded descriptive tooltip",result);
                Refresh(owner); Check(Object.ReferenceEquals(error,tray.Icon),"an unchanged error state does not recreate or reassign tray artwork",result);
                owner.Config.PlanetEnabled=false; Set(owner,"settingsLoadFailed",false); Set(owner,"planetRegistrationFailed",true); Refresh(owner);
                Check(State(owner)==AppIconState.Running,"disabled planet shortcuts cannot cause an error state while orbit remains enabled",result);
                owner.Config.Orbit.Enabled=false; Refresh(owner); Check(Object.ReferenceEquals(stopped,tray.Icon) && tray.Text.Contains("OFF"),"disabling both input features uses the stopped artwork without changing the temporary pause flag",result);
            }
            return result;
        }
    }
}

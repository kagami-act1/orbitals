using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    static class ReliabilityTests {
        static void Check(bool value,string name,List<string> results) { if(!value) throw new Exception(name); results.Add("PASS "+name); }
        public static List<string> Run() {
            List<string> results=new List<string>();
            ActionItem mute=ActionCatalog.Create().Single(p=>p.Item.Label=="ミュート切替").Create();
            Preferences config=Preferences.Defaults(); config.Items[0]=mute; config.Validate();
            Check(mute.Kind=="toggle" && mute.Toggle.IsSystemMute,"mute preset is an explicit two-state toggle",results);
            ToggleState state=new ToggleState(); Action complete;
            state.SynchronizeMute(mute,false);
            Check(state.Display(mute).Icon=="ms:volume_up" && state.Prepare(mute,out complete).Value=="on","unmuted endpoint selects mute ON and unmuted icon",results);
            state.SynchronizeMute(mute,true);
            Check(state.Display(mute).Icon=="ms:volume_off" && state.Prepare(mute,out complete).Value=="off","external mute changes synchronize next operation and icon",results);
            mute.Toggle.Stages[0].Icon="ms:star";
            state.SynchronizeMute(mute,true);
            Check(state.Display(mute).Icon=="ms:star","mute synchronization preserves user state icons",results);
            mute.Toggle.Stages.Reverse(); state.SynchronizeMute(mute,false);
            Check(state.Prepare(mute,out complete).Value=="on","reordered mute states still use actual endpoint state",results);
            mute.Toggle.Stages[0].Value="invalid"; bool rejected=false;
            try { config.Validate(); } catch { rejected=true; }
            Check(rejected,"invalid explicit mute state rejected",results);
            config=Preferences.Defaults(); config.Items[0]=ActionCatalog.Create().Single(p=>p.Item.Label=="ミュート切替").Create();
            config.Items[0].Toggle.Stages[0].Icon="ms:star";
            MaterialIcons.ReleaseCatalog(config);
            HashSet<string> retained=new HashSet<string> { "ms:volume_up","ms:volume_off" }; foreach(ActionItem action in config.Items) { retained.Add(ActionIcons.EffectiveKey(action)); if(action.Toggle!=null) foreach(ToggleStage stage in action.Toggle.Stages) retained.Add(ActionIcons.EffectiveKey(stage.Action())); }
            foreach(ConversionChoice choice in config.Conversion.Images.Concat(config.Conversion.Pdf)) if(choice.Enabled) retained.Add(ActionIcons.EffectiveKey(choice.Action()));
            Check(!MaterialIcons.CatalogLoaded && MaterialIcons.CachedCount<=retained.Count,"idle icon cache retains only configured states and automatic ring icons",results);
            Check(MaterialIcons.Find("ms:star")!=null && MaterialIcons.Find(null)==null && !MaterialIcons.CatalogLoaded,"runtime icon lookup does not reload full catalog",results);
            Check(MaterialIcons.All.Count==4001 && MaterialIcons.Categories.Sum(c=>c.Count)==4001,"picker catalog reloads completely after idle release",results);
            using(Controller controller=new Controller(true)) { controller.Apply(config); Preferences view=controller.Presentation(); string label=controller.Config.Items[0].Label; view.Items[0].Label="display only"; Check(controller.Config.Items[0].Label==label && view.Items.Count==config.Items.Count && view.Appearance.RingAccent==config.Appearance.RingAccent,"lightweight ring presentation keeps saved actions independent",results); }
            Check(AppIcon.Image.IsFrozen,"application icon is embedded and immutable",results);
            using(System.Drawing.Icon icon=AppIcon.Tray()) Check(icon.Width==32 && icon.Height==32,"tray icon uses embedded 32 pixel artwork",results);
            Native.RECT monitor=new Native.RECT { Left=0,Top=0,Right=1920,Bottom=1080 };
            RingPlacement first=new RingPlacement(new Native.POINT { X=1500,Y=900 },monitor,1,.75);
            RingPlacement second=new RingPlacement(new Native.POINT { X=100,Y=80 },monitor,1,.75);
            Check(first.Offset==new System.Windows.Point(1346.25,746.25) && second.Offset==new System.Windows.Point(-53.75,-73.75),"new cursor snapshot determines ring position without previous-position reuse",results);
            Check(second.WindowSize==new System.Windows.Size(1920,1080),"full monitor including taskbar remains interactive when ring clips",results);
            monitor=new Native.RECT { Left=-2560,Top=-1440,Right=0,Bottom=0 };
            RingPlacement scaled=new RingPlacement(new Native.POINT { X=-1280,Y=-720 },monitor,1.5,1);
            Check(Math.Abs(scaled.Offset.X-648.3333333333)<.001 && scaled.Offset.Y==275,"negative monitor coordinates and 150 percent DPI preserve invocation center",results);
            using(Controller controller=new Controller(true)) {
                RingWindow ring=new RingWindow(controller);
                IntPtr handle=new System.Windows.Interop.WindowInteropHelper(ring).EnsureHandle();
                Check(Native.IsWindow(handle) && (Native.GetWindowLong(handle,-20)&0x80)!=0,"runtime HWND uses tool-window style to stay out of taskbar",results);
                ring.Retire(); ring.Retire();
                Check(!Native.IsWindow(handle) && ring.Content==null,"retiring ring destroys cached native surface and releases visual tree",results);
            }
            return results;
        }
    }
}

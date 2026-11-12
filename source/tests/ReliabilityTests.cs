using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    static class ReliabilityTests {
        static void Check(bool value,string name,List<string> results) { if(!value) throw new Exception(name); results.Add("PASS "+name); }
        static void CheckIconCache(List<string> results) {
            Preferences empty=Preferences.Defaults(); empty.Items.Clear(); empty.Orbit.Bindings.Clear();
            foreach(DropKind source in ConversionCatalog.Sources) empty.Conversion.List(source).Clear();
            MaterialIcons.ReleaseCatalog(empty);
            Check(MaterialIcons.CachedCount==0 && !MaterialIcons.CatalogLoaded,"unused icon cache and search catalog are released",results);

            Preferences config=Preferences.Defaults(); config.Items[0]=ActionCatalog.Create().Single(p=>p.Item.Label=="ミュート切替").Create(); config.Items[0].Toggle.Stages[0].Icon="ms:star";
            PlanetProfile additional=UserContent.NewPlanet(config); additional.Items[0]=PlanetStack.Create(new ActionItem("Home","shortcut","Ctrl+Home") { Icon="ms:home" }); config.Planets.Add(additional);
            ApplicationSettings app=new ApplicationSettings { Executable="sample.exe" };
            ApplicationPlanet appPlanet=new ApplicationPlanet { PlanetId="main",Items=Enumerable.Range(0,8).Select(i=>PlanetStack.Empty()).ToList() };
            appPlanet.Items[0]=new ActionItem("Microphone","microphone","") { MuteIcons=new MuteIcons { Muted="ms:lock",Unmuted="ms:favorite" } }; app.Planets.Add(appPlanet); config.Applications.Add(app);
            string[] conversionIcons={"ms:camera","ms:picture_as_pdf","ms:description","ms:table_view","ms:slideshow","ms:folder_zip","ms:unarchive"};
            for(int i=0;i<ConversionCatalog.Sources.Length;i++) config.Conversion.List(ConversionCatalog.Sources[i])[0].Icon=conversionIcons[i];
            MaterialIcons.ReleaseCatalog(config);
            Check(MaterialIcons.CachedCount==0,"idle cleanup does not load configured icons into a cold cache",results);
            string[] selected=new[]{"ms:star","ms:home","ms:lock","ms:favorite"}.Concat(conversionIcons).ToArray();
            MaterialIcons.Prepare(selected); Dictionary<string,MaterialIcon> loaded=selected.ToDictionary(key=>key,key=>MaterialIcons.Find(key));
            Check(loaded.Values.All(icon=>icon!=null),"configured icons can be prepared without opening the search catalog",results);
            MaterialIcons.Find("ms:delete");
            MaterialIcons.ReleaseCatalog(config);
            Check(!MaterialIcons.CatalogLoaded && MaterialIcons.CachedCount==selected.Length && loaded.All(pair=>Object.ReferenceEquals(pair.Value,MaterialIcons.Find(pair.Key))),"cleanup reuses additional planet stack app mute-state toggle and all conversion-source icons",results);
            MaterialIcons.ReleaseCatalog(config);
            Check(MaterialIcons.CachedCount==selected.Length && loaded.All(pair=>Object.ReferenceEquals(pair.Value,MaterialIcons.Find(pair.Key))),"repeated idle cleanup does not reload or expand the icon cache",results);
            config.Planets.Clear(); config.Applications.Clear(); foreach(DropKind source in ConversionCatalog.Sources) config.Conversion.List(source).Clear();
            MaterialIcons.ReleaseCatalog(config);
            Check(MaterialIcons.CachedCount==1 && Object.ReferenceEquals(loaded["ms:star"],MaterialIcons.Find("ms:star")),"removing configured actions releases their cached vectors",results);
            Check(MaterialIcons.All.Count==4001 && MaterialIcons.Categories.Sum(c=>c.Count)==4001,"picker catalog reloads completely after idle release",results);
            MaterialIcons.ReleaseCatalog(config);
            Check(!MaterialIcons.CatalogLoaded && MaterialIcons.CachedCount<64,"closing the picker discards thousands of unconfigured search records",results);
        }
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
            CheckIconCache(results);
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

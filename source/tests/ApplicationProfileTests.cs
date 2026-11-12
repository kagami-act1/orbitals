using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class ApplicationProfileTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static object Field(SettingsWindow editor,string name) { return typeof(SettingsWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor); }
        static void Call(SettingsWindow editor,string name,params object[] args) { typeof(SettingsWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,args); }
        public static List<string> Run() {
            Preferences policy=Preferences.Defaults();
            policy.Orbit.Exclude("disabled.exe"); policy.Orbit.SetEnabled("DISABLED.EXE",true,true);
            List<string> policyResults=new List<string>();
            Check(!policy.Orbit.IsPlanetExcluded("disabled.exe") && policy.Orbit.IsOrbitExcluded("disabled.exe") && policy.Orbit.Resolve("disabled.exe","L","")==null,"enabling planet in a legacy blacklist entry leaves orbit disabled",policyResults);
            policy.Orbit.SetEnabled("disabled.exe",false,true); policy.Orbit.SetEnabled("disabled.exe",true,false);
            Check(policy.Orbit.IsPlanetExcluded("disabled.exe") && !policy.Orbit.IsOrbitExcluded("disabled.exe") && policy.Orbit.Resolve("disabled.exe","L","")!=null,"a planet-only exclusion keeps gesture recognition available",policyResults);
            Preferences restoredPolicy=policy.Clone(); restoredPolicy.Validate();
            Check(restoredPolicy.Orbit.IsPlanetExcluded("DISABLED.EXE") && !restoredPolicy.Orbit.IsOrbitExcluded("disabled.exe") && !restoredPolicy.Orbit.IsPlanetExcluded("other.exe"),"independent application feature permissions survive save and reload",policyResults);
            using(Controller owner=new Controller(true)) {
                owner.Apply(restoredPolicy); SettingsWindow editor=new SettingsWindow(owner); editor.Navigate(2);
                Check((int)Field(editor,"page")==4 && ((System.Collections.IList)Field(editor,"navigation")).Count==4,"old blacklist navigation opens the applications page with four top-level sections",policyResults);
                Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close();
            }
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults();
            ApplicationSettings browser=new ApplicationSettings { Executable="browser.exe" },paint=new ApplicationSettings { Executable="paint.exe" }; config.Applications.Add(browser); config.Applications.Add(paint); config.Validate();
            Check(ApplicationProfiles.Items(config,"BROWSER.EXE",0)==config.Items && ApplicationProfiles.Orbit(config,"unknown.exe")==config.Orbit,"unconfigured application groups inherit the current common settings",r);
            config.Items[0]=ActionCatalog.Create().Single(a=>a.Item.Value=="system-mute").Create();
            ApplicationProfiles.EnablePlanet(config,browser); ApplicationProfiles.EnableOrbit(config,browser); config.Validate();
            List<ActionItem> own=ApplicationProfiles.Items(config,"BROWSER.EXE",0); OrbitPreferences gestures=ApplicationProfiles.Orbit(config,"browser.exe");
            own[1].Label="専用の貼り付け"; gestures.Bindings[0].Action.Value="Ctrl+C";
            Check(own!=config.Items && own[1].Label!=config.Items[1].Label && gestures.Bindings!=config.Orbit.Bindings && config.Orbit.Bindings[0].Action.Value=="Alt+Left","editing an application ring or gesture cannot change common content",r);
            Check(own[0].Toggle.Id!=config.Items[0].Toggle.Id,"application copies retain independent toggle states",r);
            Check(ApplicationProfiles.Items(config,"paint.exe",0)==config.Items && ApplicationProfiles.Orbit(config,"paint.exe")==config.Orbit,"switching to another active application selects its own inheritance rules",r);
            Check(gestures.Button==config.Orbit.Button && gestures.Profiles==config.Orbit.Profiles && gestures.Distance==config.Orbit.Distance,"application gestures share common input settings and blacklist",r);
            config.Orbit.Exclude("browser.exe"); Check(gestures.Resolve("BROWSER.EXE","L","")==null,"blacklist takes priority over application gesture overrides",r); config.Orbit.Profiles.Clear();
            browser.OverridePlanet=false; browser.OverrideOrbit=false;
            Check(ApplicationProfiles.Items(config,"browser.exe",0)==config.Items && ApplicationProfiles.Orbit(config,"browser.exe")==config.Orbit,"turning off overrides immediately restores common groups",r);
            ApplicationProfiles.EnablePlanet(config,browser); ApplicationProfiles.EnableOrbit(config,browser);
            Check(ApplicationProfiles.Items(config,"browser.exe",0)[1].Label=="専用の貼り付け" && ApplicationProfiles.Orbit(config,"browser.exe").Bindings[0].Action.Value=="Ctrl+C","turning overrides back on preserves dedicated content",r);
            browser.Bindings.Clear(); browser.OverrideOrbit=false; ApplicationProfiles.EnableOrbit(config,browser);
            Check(ApplicationProfiles.Orbit(config,"browser.exe").Bindings.Count==0,"an intentionally empty application gesture list remains empty when reenabled",r);
            PlanetProfile extra=UserContent.NewPlanet(config); config.Planets.Add(extra); ApplicationProfiles.Synchronize(config); config.Validate();
            ApplicationProfiles.Items(config,"browser.exe",1)[0]=new ActionItem("専用","shortcut","F8");
            Check(browser.Planets.Count==2 && extra.Items[0].Kind=="none","adding another planet creates a separate application layout without editing common content",r);
            string id=extra.Id; config.Planets.RemoveAt(0); ApplicationProfiles.Synchronize(config); extra=UserContent.NewPlanet(config); config.Planets.Add(extra); ApplicationProfiles.Synchronize(config); config.Validate();
            Check(extra.Id!=id && browser.Planets.Count==2 && ApplicationProfiles.Items(config,"browser.exe",1)[0].Kind=="none","a deleted planet cannot transfer dedicated actions to a later planet at the same index",r);
            browser.PlanetId=extra.Id; Check(ApplicationProfiles.RuntimePlanet(config,"BROWSER.EXE",0)==1 && ApplicationProfiles.RuntimePlanet(config,"unknown.exe",0)==0,"main trigger selects the planet assigned to the active application",r);
            config.MainPlanetName="共通リング"; extra.Name="仕事用"; Preferences named=config.Clone(); named.Validate();
            Check(UserContent.Name(named,0)=="共通リング" && UserContent.Name(named,1)=="仕事用" && ApplicationProfiles.RuntimePlanet(named,"browser.exe",0)==1,"main and extra planet names survive backup without breaking application assignments",r);
            PlanetProfile third=UserContent.NewPlanet(named); named.Planets.Add(third); ApplicationProfiles.Synchronize(named);
            Check(ApplicationProfiles.RuntimePlanet(named,"browser.exe",2)==1,"the active application's assigned planet takes precedence over a different planet trigger",r);
            named.Planets.RemoveAt(0); ApplicationProfiles.Synchronize(named); named.Validate();
            Check(named.Applications[0].PlanetId==null && ApplicationProfiles.RuntimePlanet(named,"browser.exe",0)==0,"deleting an assigned planet clears the mapping instead of selecting another at the same index",r);
            named=config.Clone(); named.MainPlanetName=named.Planets[0].Name; Check(Reject(named.Validate),"planet names cannot duplicate the renamed main planet",r);
            named=config.Clone(); named.Applications[0].PlanetId=Guid.NewGuid().ToString("N"); Check(Reject(named.Validate),"application mappings cannot reference an unknown planet",r);
            browser.PlanetId=null;
            Preferences copy=config.Clone(); copy.Validate(); Check(copy.Applications.Count==2 && ApplicationProfiles.Items(copy,"browser.exe",0)[1].Label=="専用の貼り付け" && ApplicationProfiles.Orbit(copy,"browser.exe").Bindings.Count==0,"JSON backups preserve application overrides and deliberate empty gesture sets",r);
            string legacy="{\"Version\":1,\"Hotkey\":\"F13\",\"Items\":"+Json.Write(config.Items)+"}"; Preferences old=Json.Read<Preferences>(legacy); old.Validate(); Check(old.Applications.Count==0,"older settings load with common behavior and no implicit application overrides",r);
            copy=config.Clone(); copy.Applications.Add(new ApplicationSettings { Executable="BROWSER.EXE" }); Check(Reject(copy.Validate),"duplicate application executable names are rejected case insensitively",r);
            copy=config.Clone(); copy.Applications[0].Executable="../browser.exe"; Check(Reject(copy.Validate),"application matching accepts executable names rather than paths",r);
            copy=config.Clone(); copy.Applications[0].Planets[0].PlanetId="unknown"; Check(Reject(copy.Validate),"application layouts cannot reference missing planets",r);
            copy=config.Clone(); copy.Applications[0].Planets[0].Items.RemoveAt(0); Check(Reject(copy.Validate),"malformed application ring layouts are rejected",r);
            copy=config.Clone(); copy.Applications[0].Bindings.Add(new OrbitBinding { Pattern="L",Tolerance=36,Action=new ActionItem("無効","none","") }); Check(Reject(copy.Validate),"application gesture thresholds use the same validated bounds as common gestures",r);
            copy=config.Clone(); copy.Planets.Add(UserContent.NewPlanet(copy)); copy.Planets[1].Id=copy.Planets[0].Id; Check(Reject(copy.Validate),"duplicate planet identities cannot alias application settings",r);
            copy=config.Clone(); for(int i=2;i<=32;i++) copy.Applications.Add(new ApplicationSettings { Executable="app"+i+".exe" }); Check(Reject(copy.Validate),"application profile count remains bounded",r);
            string path=Path.Combine(Path.GetTempPath(),"orbitals-app-draft-"+Guid.NewGuid().ToString("N")+".json");
            try {
                copy=config.Clone(); copy.Applications[0].Bindings.Add(new OrbitBinding { Pattern="",Tolerance=9,Action=new ActionItem("作成中","none","") });
                WorkingCopy.Write(path,new WorkingCopyRecord { Version=1,Config=copy,Application="browser.exe",Page=4,StackPath=new int[0] }); WorkingCopyRecord read=WorkingCopy.Read(path);
                Check(read.Application=="browser.exe" && read.Page==4 && read.Config.Applications[0].Bindings.Last().Pattern=="" && read.Config.Applications[0].Bindings.Last().Tolerance==9,"draft recovery preserves the selected application and unfinished dedicated gestures",r);
                read.Config.Applications[0].Bindings.Last().Tolerance=1; Check(Reject(delegate { WorkingCopy.Write(path,read); }),"invalid application draft thresholds are rejected without overwriting recovery data",r);
            } finally { WorkingCopy.Delete(path); if(File.Exists(path+".tmp")) File.Delete(path+".tmp"); }
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); SettingsWindow editor=new SettingsWindow(owner); Preferences draft=(Preferences)Field(editor,"draft");
                Call(editor,"OpenApplication",draft.Applications[0],0); Call(editor,"Assign",1,new ActionItem("アプリ編集","shortcut","F8")); draft=(Preferences)Field(editor,"draft");
                Check(ApplicationProfiles.Items(draft,"browser.exe",0)[1].Label=="アプリ編集" && draft.Items[1].Label!="アプリ編集","application editor assignments address dedicated planet content",r);
                Call(editor,"Undo"); draft=(Preferences)Field(editor,"draft"); Check((string)Field(editor,"editorApplication")=="browser.exe" && ApplicationProfiles.Items(draft,"browser.exe",0)[1].Label=="専用の貼り付け","undo restores application content and editor scope together",r);
                Call(editor,"OpenApplication",draft.Applications[1],1); Call(editor,"Assign",0,new ActionItem("専用動作","shortcut","F8")); draft=(Preferences)Field(editor,"draft");
                Check(draft.Applications[1].Bindings[0].Action.Label=="専用動作" && draft.Orbit.Bindings[0].Action.Label=="戻る","application gesture assignments leave the common binding unchanged",r);
                editor.Navigate(4); Check(((TextBlock)Field(editor,"pageHeading")).Text=="アプリ別設定","the new application tab is reachable after editing dedicated gestures",r);
                Call(editor,"RestoreSaved"); Check(Field(editor,"editorApplication")==null,"restoring saved content returns the editor to common settings",r);
                editor.ReleaseResources(); editor.Close();
            }
            r.AddRange(policyResults); return r;
        }
    }
}

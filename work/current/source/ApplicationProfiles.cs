using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    public sealed class ApplicationPlanet {
        public string PlanetId { get; set; }
        public List<ActionItem> Items { get; set; }
    }
    public sealed class ApplicationSettings {
        public string Executable { get; set; }
        public string PlanetId { get; set; }
        public bool OverridePlanet { get; set; }
        public bool OverrideOrbit { get; set; }
        public bool OrbitInitialized { get; set; }
        public List<ApplicationPlanet> Planets { get; set; }
        public List<OrbitBinding> Bindings { get; set; }
        public ApplicationSettings() { Planets=new List<ApplicationPlanet>(); Bindings=new List<OrbitBinding>(); }
    }
    static class ApplicationProfiles {
        public const int Maximum=32;
        public static ApplicationSettings Find(Preferences config,string executable) { return config.Applications.FirstOrDefault(a=>String.Equals(a.Executable,executable,StringComparison.OrdinalIgnoreCase)); }
        public static string PlanetId(Preferences config,int index) { return index==0?"main":config.Planets[index-1].Id; }
        public static int RuntimePlanet(Preferences config,string executable,int requested) {
            ApplicationSettings app=Find(config,executable); if(requested!=0 || app==null || String.IsNullOrEmpty(app.PlanetId)) return requested;
            int found=Enumerable.Range(0,config.Planets.Count+1).FirstOrDefault(i=>PlanetId(config,i)==app.PlanetId); return found;
        }
        public static List<ActionItem> Items(Preferences config,string executable,int index) {
            ApplicationSettings app=Find(config,executable); ApplicationPlanet planet=app==null || !app.OverridePlanet?null:app.Planets.FirstOrDefault(p=>p.PlanetId==PlanetId(config,index));
            return planet==null?UserContent.Items(config,index):planet.Items;
        }
        public static OrbitPreferences Orbit(Preferences config,string executable) { ApplicationSettings app=Find(config,executable); return app!=null && app.OverrideOrbit?config.Orbit.WithBindings(app.Bindings):config.Orbit; }
        public static void EnablePlanet(Preferences config,ApplicationSettings app) { app.OverridePlanet=true; Synchronize(config); }
        public static void EnableOrbit(Preferences config,ApplicationSettings app) {
            if(!app.OrbitInitialized) { app.Bindings=Json.Read<List<OrbitBinding>>(Json.Write(config.Orbit.Bindings)); foreach(OrbitBinding binding in app.Bindings) PlanetStack.NewIdentities(new[]{binding.Action}); app.OrbitInitialized=true; }
            app.OverrideOrbit=true;
        }
        public static void Synchronize(Preferences config) {
            HashSet<string> ids=new HashSet<string>(Enumerable.Range(0,config.Planets.Count+1).Select(i=>PlanetId(config,i)));
            foreach(ApplicationSettings app in config.Applications) {
                if(!String.IsNullOrEmpty(app.PlanetId) && !ids.Contains(app.PlanetId)) app.PlanetId=null;
                app.Planets.RemoveAll(p=>!ids.Contains(p.PlanetId));
                if(!app.OverridePlanet) continue;
                for(int i=0;i<=config.Planets.Count;i++) { string id=PlanetId(config,i); if(app.Planets.Any(p=>p.PlanetId==id)) continue; app.Planets.Add(new ApplicationPlanet { PlanetId=id,Items=UserContent.Items(config,i).Select(UserContent.CopyAction).ToList() }); }
            }
        }
        public static void Validate(Preferences config) {
            if(config.Applications==null || config.Applications.Count>Maximum) throw new Exception("アプリ別設定は32件までです。");
            HashSet<string> executables=new HashSet<string>(StringComparer.OrdinalIgnoreCase),planetIds=new HashSet<string>(Enumerable.Range(0,config.Planets.Count+1).Select(i=>PlanetId(config,i)));
            foreach(ApplicationSettings app in config.Applications) {
                if(app!=null && !String.IsNullOrEmpty(app.PlanetId) && !planetIds.Contains(app.PlanetId)) throw new Exception("アプリで表示するplanetが見つかりません。");
                if(app==null || !OrbitPreferences.ValidExecutable(app.Executable) || !executables.Add(app.Executable) || app.Planets==null || app.Planets.Count>8 || app.Bindings==null) throw new Exception("アプリ別設定の実行ファイル名や内容を確認してください。");
                HashSet<string> used=new HashSet<string>();
                foreach(ApplicationPlanet planet in app.Planets) { if(planet==null || !planetIds.Contains(planet.PlanetId) || !used.Add(planet.PlanetId) || planet.Items==null || planet.Items.Count!=8) throw new Exception("アプリ別のplanet設定が不正です。"); }
                if(app.OverridePlanet && used.Count!=planetIds.Count) throw new Exception("アプリ別のplanet設定が不足しています。");
                foreach(string key in UserContent.Hotkeys(config)) { Chord trigger=Chord.Parse(key); OrbitPreferences.ValidateBindings(app.Bindings,trigger); foreach(ApplicationPlanet planet in app.Planets) foreach(ActionItem item in planet.Items) Preferences.ValidateAction(item,trigger); }
            }
        }
    }
}

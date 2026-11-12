using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace OrbitRing {
    public sealed class WorkingCopyRecord {
        public int Version { get; set; }
        public string SavedFingerprint { get; set; }
        public Preferences Config { get; set; }
        public int Page { get; set; }
        public int Planet { get; set; }
        public int Gesture { get; set; }
        public int Selected { get; set; }
        public int[] StackPath { get; set; }
        public string Application { get; set; }
    }
    static class WorkingCopy {
        public static string Fingerprint(Preferences config) { using(SHA256 sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Json.Write(config)))).Replace("-",""); }
        public static WorkingCopyRecord Read(string path) {
            if(!File.Exists(path)) return null;
            WorkingCopyRecord record=Json.ReadFile<WorkingCopyRecord>(path); Check(record); return record;
        }
        static void Check(WorkingCopyRecord record) {
            if(record==null || record.Version!=1 || record.Config==null || record.Config.Version!=1 || record.Config.Items==null || record.Config.Items.Count!=8 || record.Config.Planets==null || record.Config.Planets.Count>=UserContent.MaxPlanets || record.Config.Favorites==null || record.Config.Favorites.Count>UserContent.MaxFavorites || record.Config.GestureLibrary==null || record.Config.GestureLibrary.Count>UserContent.MaxGestures || record.Config.Orbit==null || record.Config.Orbit.Bindings==null || record.Config.Orbit.Bindings.Count>128 || record.Config.Orbit.Profiles==null || record.Config.Appearance==null || record.StackPath==null || record.StackPath.Length>PlanetStack.MaxDepth) throw new Exception("下書きの形式が不正です。");
            CheckActions(record.Config.Items,0); CheckActions(record.Config.Favorites,0);
            if((record.Config.MainPlanetName??"").Length>20) throw new Exception("下書きのplanet名が長すぎます。");
            foreach(PlanetProfile profile in record.Config.Planets) { if(profile==null || (profile.Name??"").Length>20 || (profile.Hotkey??"").Length>48 || profile.Items==null || profile.Items.Count!=8) throw new Exception("下書きのplanetが不正です。"); CheckActions(profile.Items,0); }
            if(record.Config.Orbit.Profiles.Count>64 || record.Config.Orbit.Profiles.Any(p=>p==null || !OrbitPreferences.ValidExecutable(p.Executable)) || !new[]{"Right","Middle","X1","X2"}.Contains(record.Config.Orbit.Button) || record.Config.Orbit.Distance<12 || record.Config.Orbit.Distance>120) throw new Exception("下書きのorbit設定が不正です。");
            foreach(SavedGesture saved in record.Config.GestureLibrary) {
                Guid id;
                if(saved==null || !Guid.TryParseExact(saved.Id,"N",out id) || String.IsNullOrWhiteSpace(saved.Name) || saved.Name.Length>20 || !new[]{"","Ctrl","Alt","Shift"}.Contains(saved.Modifier) || saved.Tolerance<5 || saved.Tolerance>35 || (saved.Shape==null?!OrbitPreferences.ValidPattern(saved.Pattern):saved.Pattern!="Shape" || !GestureShape.Valid(saved.Shape))) throw new Exception("下書きのプリセットが不正です。");
            }
            CheckBindings(record.Config.Orbit.Bindings);
            if(record.Config.Applications==null || record.Config.Applications.Count>ApplicationProfiles.Maximum) throw new Exception("下書きのアプリ別設定が不正です。");
            HashSet<string> executables=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(ApplicationSettings app in record.Config.Applications) {
                if(app==null || !OrbitPreferences.ValidExecutable(app.Executable) || !executables.Add(app.Executable) || app.Planets==null || app.Planets.Count>UserContent.MaxPlanets) throw new Exception("下書きのアプリ別設定が不正です。");
                if(!String.IsNullOrEmpty(app.PlanetId) && !Enumerable.Range(0,record.Config.Planets.Count+1).Any(i=>ApplicationProfiles.PlanetId(record.Config,i)==app.PlanetId)) throw new Exception("下書きのアプリが参照するplanetが見つかりません。");
                foreach(ApplicationPlanet planet in app.Planets) { if(planet==null || String.IsNullOrWhiteSpace(planet.PlanetId) || planet.PlanetId.Length>32 || planet.Items==null || planet.Items.Count!=8) throw new Exception("下書きのアプリ別planetが不正です。"); CheckActions(planet.Items,0); }
                CheckBindings(app.Bindings);
            }
            if(record.Application!=null && !executables.Contains(record.Application)) throw new Exception("下書きの編集中アプリが見つかりません。");
            record.Config.Appearance.Validate();
            ActionPlugins.ValidateAll(record.Config.Plugins,new Chord());
            if(record.Config.Conversion==null) throw new Exception("下書きの変換設定が空です。"); record.Config.Conversion.Validate(true);
            if(record.Config.Updates==null || (record.Config.Updates.Repository??"").Length>240) throw new Exception("下書きの更新設定が不正です。");
        }
        static void CheckBindings(List<OrbitBinding> bindings) {
            if(bindings==null || bindings.Count>128) throw new Exception("下書きのジェスチャーが不正です。");
            foreach(OrbitBinding binding in bindings) {
                if(binding==null || binding.Action==null || (binding.Pattern??"").Length>12 || !new[]{"","Ctrl","Alt","Shift"}.Contains(binding.Modifier) || binding.Tolerance<5 || binding.Tolerance>35 || (binding.Shape!=null && !GestureShape.Valid(binding.Shape)) || (binding.OriginalShape!=null && !GestureShape.Valid(binding.OriginalShape))) throw new Exception("下書きのジェスチャーが不正です。");
                CheckActions(new[]{binding.Action},0);
            }
        }
        static void CheckActions(IEnumerable<ActionItem> actions,int depth) {
            foreach(ActionItem action in actions) {
                if(action==null || (!ExtendedActions.Kinds.Contains(action.Kind) && action.Kind!="mute") || (action.Label??"").Length>20 || (action.Value??"").Length>8192) throw new Exception("下書きのアクションが不正です。");
                ActionIcons.Validate(action.Icon);
                if(action.MuteIcons!=null) action.MuteIcons.Validate();
                if(action.Kind=="stack") { if(depth>=PlanetStack.MaxDepth || !PlanetStack.ValidChildren(action)) throw new Exception("下書きのスタックが不正です。"); CheckActions(action.Children,depth+1); }
                if(action.Kind=="toggle" && (action.Toggle==null || action.Toggle.Stages==null || action.Toggle.Stages.Count<2 || action.Toggle.Stages.Count>16 || action.Toggle.Stages.Any(s=>s==null))) throw new Exception("下書きのトグルが不正です。");
                if(action.Kind=="toggle") foreach(ToggleStage stage in action.Toggle.Stages) { if(stage.Kind=="toggle" || stage.Kind=="stack") throw new Exception("下書きのトグル操作が不正です。"); CheckActions(new[]{stage.Action()},depth); }
            }
        }
        public static void Write(string path,WorkingCopyRecord record) {
            Check(record); string text=Json.Write(record),temporary=path+".tmp";
            try { File.WriteAllText(temporary,text,new UTF8Encoding(false)); if(File.Exists(path)) File.Replace(temporary,path,null); else File.Move(temporary,path); }
            finally { if(File.Exists(temporary)) File.Delete(temporary); }
        }
        public static void Delete(string path) { if(File.Exists(path)) File.Delete(path); }
    }
}

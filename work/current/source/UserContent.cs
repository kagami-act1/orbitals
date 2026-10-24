using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    public sealed class PlanetProfile {
        public string Id { get; set; }
        public PlanetProfile() { Id=Guid.NewGuid().ToString("N"); }
        public string Name { get; set; }
        public string Hotkey { get; set; }
        public List<ActionItem> Items { get; set; }
    }
    public sealed class SavedGesture {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Pattern { get; set; }
        public string Modifier { get; set; }
        public List<GesturePoint> Shape { get; set; }
        public int Tolerance { get; set; }
        public SavedGesture() { Tolerance=16; }
        internal GesturePreset Preset() { return new GesturePreset("user-"+Id,GesturePresets.Category(new OrbitBinding { Pattern=Pattern,Modifier=Modifier,Shape=Shape }),Name,Pattern,Modifier,Shape) { Tolerance=Tolerance }; }
        public static SavedGesture From(OrbitBinding binding,string name) {
            SavedGesture saved=new SavedGesture { Id=Guid.NewGuid().ToString("N"),Name=name,Pattern=binding.Pattern,Modifier=binding.Modifier,Shape=binding.Shape,Tolerance=binding.Tolerance };
            return Json.Read<SavedGesture>(Json.Write(saved));
        }
    }
    static class UserContent {
        public const int MaxPlanets=8,MaxFavorites=64,MaxGestures=64;
        public static List<ActionItem> Items(Preferences config,int index) { return index==0?config.Items:config.Planets[index-1].Items; }
        public static string Name(Preferences config,int index) { return index==0?config.MainPlanetName:config.Planets[index-1].Name; }
        public static IEnumerable<string> Hotkeys(Preferences config) { yield return config.Hotkey; foreach(PlanetProfile profile in config.Planets) yield return profile.Hotkey; }
        public static ActionItem CopyAction(ActionItem item) { ActionItem copy=item.Clone(); PlanetStack.NewIdentities(new[]{copy}); return copy; }
        public static PlanetProfile NewPlanet(Preferences config,List<ActionItem> source=null) {
            if(config.Planets.Count>=MaxPlanets-1) throw new Exception("planetはメインを含めて8個までです。");
            HashSet<string> taken=new HashSet<string>(Hotkeys(config).Select(Key));
            string hotkey=Enumerable.Range(10,15).Select(i=>"Ctrl+Alt+F"+i).FirstOrDefault(k=>!taken.Contains(Key(k)));
            if(hotkey==null) throw new Exception("planetの呼び出しキーに空きがありません。");
            int number=2; while(Enumerable.Range(0,config.Planets.Count+1).Any(i=>Name(config,i)=="planet "+number)) number++;
            List<ActionItem> items=source==null?Enumerable.Range(0,8).Select(i=>PlanetStack.Empty()).ToList():source.Select(a=>a.Clone()).ToList();
            PlanetStack.NewIdentities(items);
            return new PlanetProfile { Name="planet "+number,Hotkey=hotkey,Items=items };
        }
        static string Key(string text) { Chord chord=Chord.Parse(text); return chord.Modifiers+":"+chord.Key; }
        public static void Validate(Preferences config) {
            if(config.Planets==null || config.Planets.Count>=MaxPlanets || config.Favorites==null || config.Favorites.Count>MaxFavorites || config.GestureLibrary==null || config.GestureLibrary.Count>MaxGestures) throw new Exception("保存件数が上限を超えています。");
            if(String.IsNullOrWhiteSpace(config.MainPlanetName) || config.MainPlanetName.Length>20 || config.MainPlanetName.Any(Char.IsControl)) throw new Exception("planetの名前は1〜20文字で入力してください。");
            HashSet<string> names=new HashSet<string>(StringComparer.OrdinalIgnoreCase) { config.MainPlanetName },keys=new HashSet<string> { Key(config.Hotkey) },planetIds=new HashSet<string>();
            foreach(PlanetProfile profile in config.Planets) {
                Guid planetId; if(profile==null || !Guid.TryParseExact(profile.Id,"N",out planetId) || !planetIds.Add(profile.Id)) throw new Exception("planetの識別子が不正、または重複しています。");
                if(profile==null || String.IsNullOrWhiteSpace(profile.Name) || profile.Name.Length>20 || !names.Add(profile.Name) || profile.Items==null || profile.Items.Count!=8) throw new Exception("planetには重複しない名前と8つの枠が必要です。");
                Chord trigger=Chord.Parse(profile.Hotkey);
                if(trigger.Modifiers==0 && (trigger.Key<0x70 || trigger.Key>0x87)) throw new Exception("planetの呼び出しには修飾キーかF1〜F24を使ってください。");
                if(!keys.Add(Key(profile.Hotkey))) throw new Exception("planetの呼び出しキーが重複しています。");
            }
            foreach(string hotkey in Hotkeys(config)) {
                Chord trigger=Chord.Parse(hotkey);
                foreach(ActionItem action in config.AllActions()) Preferences.ValidateAction(action,trigger);
                foreach(ActionItem action in config.Favorites) Preferences.ValidateAction(action,trigger);
            }
            HashSet<string> ids=new HashSet<string>(),gestureNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(SavedGesture saved in config.GestureLibrary) {
                Guid id;
                if(saved==null || !Guid.TryParseExact(saved.Id,"N",out id) || !ids.Add(saved.Id) || String.IsNullOrWhiteSpace(saved.Name) || saved.Name.Length>20 || !gestureNames.Add(saved.Name) || !new[]{"","Ctrl","Alt","Shift"}.Contains(saved.Modifier) || saved.Tolerance<5 || saved.Tolerance>35) throw new Exception("自分のプリセットの名前・識別子・認識閾値を確認してください。");
                OrbitBinding binding=saved.Preset().Create();
                if(binding.Shape==null?!OrbitPreferences.ValidPattern(binding.Pattern):binding.Pattern!="Shape" || !GestureShape.Valid(binding.Shape)) throw new Exception("自分のプリセットに有効な動きを登録してください。");
            }
        }
    }
}

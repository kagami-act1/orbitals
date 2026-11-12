using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    public sealed class OrbitBinding {
        public string Pattern { get; set; }
        public string Modifier { get; set; }
        public ActionItem Action { get; set; }
        public List<GesturePoint> Shape { get; set; }
        public List<GesturePoint> OriginalShape { get; set; }
        public int Tolerance { get; set; }
        public OrbitBinding() { Modifier=""; Tolerance=16; }
        internal double MatchLimit { get { return Tolerance/100.0; } }
    }
    public sealed class AppProfile {
        public string Executable { get; set; }
        public bool Excluded { get; set; }
        public bool DisablePlanet { get; set; }
        public bool DisableOrbit { get; set; }
    }
    public sealed class OrbitPreferences {
        public bool Enabled { get; set; }
        public string Button { get; set; }
        public int Distance { get; set; }
        public bool Trail { get; set; }
        public bool CleanDrawing { get; set; }
        public List<OrbitBinding> Bindings { get; set; }
        public List<AppProfile> Profiles { get; set; }
        public OrbitPreferences() {
            Enabled=true; Button="Right"; Distance=30; Trail=true; CleanDrawing=true; Profiles=new List<AppProfile>();
            Bindings=new List<OrbitBinding> {
                new OrbitBinding { Pattern="L",Action=new ActionItem("戻る","shortcut","Alt+Left") },
                new OrbitBinding { Pattern="R",Action=new ActionItem("進む","shortcut","Alt+Right") },
                new OrbitBinding { Pattern="UD",Action=new ActionItem("再読み込み","shortcut","Ctrl+R") },
                new OrbitBinding { Pattern="DR",Action=new ActionItem("タブを閉じる","shortcut","Ctrl+W") },
                new OrbitBinding { Pattern="DU",Action=new ActionItem("閉じたタブを戻す","shortcut","Ctrl+Shift+T") },
                new OrbitBinding { Pattern="U",Action=new ActionItem("最大化 / 元に戻す","window","maximize") },
                new OrbitBinding { Pattern="D",Action=new ActionItem("最小化","window","minimize") },
                new OrbitBinding { Pattern="WheelUp",Action=new ActionItem("その場所で拡大","wheel","Ctrl+Up") },
                new OrbitBinding { Pattern="WheelDown",Action=new ActionItem("その場所で縮小","wheel","Ctrl+Down") },
                new OrbitBinding { Pattern="Rocker",Action=new ActionItem("次のタブ","shortcut","Ctrl+Tab") }
            };
        }
        public AppProfile Profile(string executable) { return Profiles.Find(p=>String.Equals(p.Executable,executable,StringComparison.OrdinalIgnoreCase)); }
        internal OrbitPreferences WithBindings(List<OrbitBinding> bindings) { OrbitPreferences view=(OrbitPreferences)MemberwiseClone(); view.Bindings=bindings; return view; }
        public bool IsExcluded(string executable) { return IsPlanetExcluded(executable) && IsOrbitExcluded(executable); }
        public bool IsPlanetExcluded(string executable) { AppProfile profile=Profile(executable); return profile!=null && (profile.Excluded || profile.DisablePlanet); }
        public bool IsOrbitExcluded(string executable) { AppProfile profile=Profile(executable); return profile!=null && (profile.Excluded || profile.DisableOrbit); }
        public void SetEnabled(string executable,bool planet,bool enabled) {
            if(!ValidExecutable(executable)) throw new Exception("実行ファイル名を指定してください。");
            AppProfile profile=Profile(executable);
            if(profile==null) { if(enabled) return; if(Profiles.Count>=64) throw new Exception("ブラックリストは64件までです。"); profile=new AppProfile { Executable=executable }; Profiles.Add(profile); }
            if(profile.Excluded) { profile.Excluded=false; profile.DisablePlanet=true; profile.DisableOrbit=true; }
            if(planet) profile.DisablePlanet=!enabled; else profile.DisableOrbit=!enabled;
        }
        public static bool ValidExecutable(string executable) {
            return !String.IsNullOrWhiteSpace(executable) && executable.Length>4 && executable.Length<=260 && executable==executable.Trim() && executable.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) && executable.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())<0;
        }
        public void Exclude(string executable) {
            if(!ValidExecutable(executable)) throw new Exception("実行ファイル名を指定してください。例: msedge.exe");
            AppProfile profile=Profile(executable);
            if(profile!=null) { profile.Excluded=true; return; }
            if(Profiles.Count>=64) throw new Exception("ブラックリストは64件までです。");
            Profiles.Add(new AppProfile { Executable=executable,Excluded=true });
        }
        public OrbitBinding Resolve(string executable,string pattern,string modifier) {
            return IsOrbitExcluded(executable)?null:Bindings.Find(x=>x.Pattern==pattern && x.Modifier==modifier);
        }
        public OrbitBinding ResolveDrawing(string executable,string pattern,IList<Point> points,string modifier) {
            if(IsOrbitExcluded(executable)) return null;
            bool ambiguous; OrbitBinding match=GestureShape.Match(Bindings,points,modifier,out ambiguous);
            if(match!=null || ambiguous) return match;
            OrbitBinding legacy=Resolve(executable,pattern,modifier);
            return legacy!=null && legacy.Shape==null && GestureShape.Distance(GestureShape.Normalize(points),GestureShape.Template(legacy))<legacy.MatchLimit?legacy:null;
        }
        public static bool ValidPattern(string p) { return p=="WheelUp" || p=="WheelDown" || p=="Rocker" || (!String.IsNullOrEmpty(p) && p.Length<=12 && p.All(c=>"UDLR".Contains(c)) && !p.Where((c,i)=>i>0 && c==p[i-1]).Any()); }
        internal static void ValidateBindings(List<OrbitBinding> bindings,Chord trigger) {
            if(bindings==null || bindings.Count>128) throw new Exception("orbitの割り当ては128件までです。");
            HashSet<string> keys=new HashSet<string>();
            foreach(OrbitBinding b in bindings) {
                if(b!=null && (b.Tolerance<5 || b.Tolerance>35)) throw new Exception("ジェスチャーの許容するずれは5〜35％で指定してください。");
                if(b==null || (b.Shape==null?!ValidPattern(b.Pattern):b.Pattern!="Shape" || !GestureShape.Valid(b.Shape)) || !new[]{"","Ctrl","Alt","Shift"}.Contains(b.Modifier)) throw new Exception("ジェスチャーを描いて登録してください。同時に押すキーも確認してください。");
                if(b.Shape==null && !keys.Add(b.Pattern+":"+b.Modifier)) throw new Exception("同じジェスチャーと同時に押すキーが重複しています。");
                if(b.OriginalShape!=null && (b.Shape==null || !GestureShape.Valid(b.OriginalShape))) throw new Exception("元の描画データが不正です。ジェスチャーを描き直してください。");
                Preferences.ValidateAction(b.Action,trigger); if(b.Action.Kind=="stack") throw new Exception("スタックはplanet専用です。");
            }
            for(int i=0;i<bindings.Count;i++) for(int j=0;j<i;j++) if(bindings[i].Modifier==bindings[j].Modifier && (bindings[i].Shape!=null || bindings[j].Shape!=null) && GestureShape.Distance(GestureShape.Template(bindings[i]),GestureShape.Template(bindings[j]))<.045) throw new Exception("同じ形のジェスチャーが重複しています。形か、同時に押すキーを変更してください。");
        }
        public void Validate(Chord trigger) {
            if(!new[]{"Right","Middle","X1","X2"}.Contains(Button) || Distance<12 || Distance>120) throw new Exception("orbitの開始ボタンまたは移動距離が不正です。");
            ValidateBindings(Bindings,trigger);
            if(Profiles==null || Profiles.Count>64) throw new Exception("ブラックリストは64件までです。");
            HashSet<string> names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(AppProfile p in Profiles) {
                if(p==null || !ValidExecutable(p.Executable) || !names.Add(p.Executable)) throw new Exception("重複のない実行ファイル名（例: msedge.exe）を指定してください。");
            }
        }
    }
    // Incremental direction recognition with bounded state and no idle polling.
    sealed class DirectionStroke {
        Point anchor;
        readonly double threshold;
        public string Pattern { get; private set; }
        public bool Overflow { get; private set; }
        public DirectionStroke(Point start,double distance) { anchor=start; threshold=distance; Pattern=""; }
        public bool Add(Point p) {
            Vector d=p-anchor; if(d.Length<threshold) return false;
            char direction=Math.Abs(d.X)>=Math.Abs(d.Y)?(d.X>0?'R':'L'):(d.Y>0?'D':'U');
            anchor=p;
            if(Pattern.Length>0 && Pattern[Pattern.Length-1]==direction) return false;
            if(Pattern.Length>=12) { Overflow=true; return false; }
            Pattern+=direction; return true;
        }
    }
    enum OrbitRelease { Cancel, Click, Action }
    static class OrbitPolicy {
        public static OrbitRelease Release(bool cancelled,bool holding,bool chord,bool overflow,string pattern,bool physicalRelease) {
            if(!physicalRelease || cancelled || holding || chord || overflow) return OrbitRelease.Cancel;
            return String.IsNullOrEmpty(pattern)?OrbitRelease.Click:OrbitRelease.Action;
        }
        public static bool MayDispatch(int captured,int current,bool cancelled,bool holding,bool holdLive) { return captured==current && !cancelled && (!holding || holdLive); }
    }
}

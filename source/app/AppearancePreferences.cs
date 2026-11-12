using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace OrbitRing {
    public sealed class AppearancePreferences {
        public string RingAccent { get; set; }
        public double RingOpacity { get; set; }
        public double StackGap { get; set; }
        public bool RingAnimation { get; set; }
        public bool PlanetSwitchAnimation { get; set; }
        public bool TextOnly { get; set; }
        public bool EvenSpacing { get; set; }
        [System.Web.Script.Serialization.ScriptIgnore] public double StackSpacing { get { return StackGap*2.5; } set { StackGap=value/2.5; } }
        public string TrailColor { get; set; }
        public double TrailWidth { get; set; }
        public double TrailLength { get; set; }
        public AppearancePreferences() { RingAccent="#93E9C1"; RingOpacity=1; StackGap=PlanetStack.DefaultGap; RingAnimation=true; PlanetSwitchAnimation=true; TrailColor="#93E9C1"; TrailWidth=3; TrailLength=0; }
        public static bool ValidColor(string value) { if(value==null || value.Length!=7 || value[0]!='#') return false; for(int i=1;i<7;i++) if(!Uri.IsHexDigit(value[i])) return false; return true; }
        static bool Range(double value,double min,double max) { return !Double.IsNaN(value) && !Double.IsInfinity(value) && value>=min && value<=max; }
        public void Validate() {
            if(!ValidColor(RingAccent) || !ValidColor(TrailColor)) throw new Exception("色は #RRGGBB で指定してください。");
            if(!Range(RingOpacity,.35,1) || !Range(TrailWidth,1,8) || !Range(TrailLength,0,800)) throw new Exception("外観の透明度・軌跡の太さ・長さが範囲外です。");
            if(!Range(StackGap,PlanetStack.MinGap,PlanetStack.MaxGap)) throw new Exception("弧状スタックの間隔は20〜400で指定してください。");
        }
        [System.Web.Script.Serialization.ScriptIgnore] public Brush Accent { get { return Ui.Brush(RingAccent); } }
        [System.Web.Script.Serialization.ScriptIgnore] public Brush Selected { get { Color c=(Color)ColorConverter.ConvertFromString(RingAccent); return new SolidColorBrush(Color.FromRgb((byte)(24*.7+c.R*.3),(byte)(30*.7+c.G*.3),(byte)(38*.7+c.B*.3))); } }
        [System.Web.Script.Serialization.ScriptIgnore] public Brush Trail { get { return Ui.Brush(TrailColor); } }
        // Retain only the requested arc length, without changing recognition data.
        public static Point[] VisibleTrail(IList<Point> source,double limit) {
            if(source==null || source.Count==0) return new Point[0];
            if(limit<=0) { Point[] all=new Point[source.Count]; source.CopyTo(all,0); return all; }
            List<Point> reverse=new List<Point> { source[source.Count-1] }; double walked=0;
            for(int i=source.Count-1;i>0;i--) { Vector delta=source[i-1]-source[i]; double length=delta.Length; if(walked+length>limit) { if(length>.000001) reverse.Add(source[i]+delta*((limit-walked)/length)); break; } reverse.Add(source[i-1]); walked+=length; }
            reverse.Reverse(); return reverse.ToArray();
        }
    }
    static class SettingsBackup {
        public static Preferences Read(string path) { Preferences value=Json.ReadFile<Preferences>(path); if(value==null) throw new Exception("設定が空です。"); value.Validate(); PlanetStack.UpgradeLegacy(value); return value; }
        public static void Write(string path,Preferences value) {
            value.Validate(); string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try { System.IO.File.WriteAllText(temporary,Json.Write(value),new System.Text.UTF8Encoding(false)); if(System.IO.File.Exists(path)) System.IO.File.Replace(temporary,path,null); else System.IO.File.Move(temporary,path); }
            finally { if(System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary); }
        }
    }
}

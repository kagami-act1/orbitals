using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class AppearanceTests {
        static void Check(bool condition,string label,List<string> results) { if(!condition) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults(); config.Validate();
            Check(config.Appearance.RingAccent=="#93E9C1" && config.Appearance.TrailWidth==3 && config.Appearance.TrailLength==0 && config.Appearance.RingOpacity==1,"appearance defaults preserve original colors and visibility",r);
            Check(config.Appearance.StackGap==20 && config.Appearance.StackSpacing==50 && config.Appearance.RingAnimation,"shared arc spacing uses twenty physical units at the displayed standard of fifty",r);
            config.Appearance.RingAnimation=false; Check(!config.Clone().Appearance.RingAnimation,"the ring reveal setting persists when disabled",r); config.Appearance.RingAnimation=true;
            string older=Json.Write(config).Replace("\"StackGap\":20,","").Replace("\"RingAnimation\":true,",""); Preferences compatible=Json.Read<Preferences>(older); compatible.Validate(); Check(compatible.Appearance.StackGap==20 && compatible.Appearance.RingAnimation,"missing appearance fields use the current shared spacing and reveal defaults",r);
            System.Windows.Controls.Canvas canvas=new System.Windows.Controls.Canvas(); PlanetReveal reveal=PlanetReveal.Play(canvas,new Point(100,80),.75,config.Appearance.Accent);
            Check(!reveal.IsHitTestVisible && !reveal.Focusable && System.Windows.Controls.Canvas.GetLeft(reveal)==100 && System.Windows.Controls.Canvas.GetTop(reveal)==80,"the reveal is an input-transparent decoration anchored at the invocation position",r);
            reveal.Advance(TimeSpan.FromSeconds(1000)); Check(reveal.Progress==0 && reveal.Running,"ring reveal starts from its first rendering frame rather than application uptime",r);
            reveal.Advance(TimeSpan.FromSeconds(1000)+TimeSpan.FromMilliseconds(70));
            reveal.LayoutTransform=System.Windows.Media.Transform.Identity;
            System.Windows.Media.Imaging.RenderTargetBitmap frame=new System.Windows.Media.Imaging.RenderTargetBitmap(410,410,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            reveal.Measure(new Size(410,410)); reveal.Arrange(new Rect(0,0,410,410)); frame.Render(reveal); byte[] pixels=new byte[410*410*4]; frame.CopyPixels(pixels,410*4,0);
            int visible=0,outside=0; for(int y=0;y<410;y++) for(int x=0;x<410;x++) if(pixels[(y*410+x)*4+3]>90) { visible++; if((new Point(x,y)-new Point(205,205)).Length>170) outside++; }
            Check(visible>150 && outside>100,"the reveal remains visible on the inner and outer circular paths",r);
            int[] radii={92,113,134,155,176,197},hits=new int[6]; int displaced=0;
            for(int y=0;y<410;y++) for(int x=0;x<410;x++) if(pixels[(y*410+x)*4+3]>45) {
                double radius=(new Point(x+.5,y+.5)-new Point(205,205)).Length; int nearest=Enumerable.Range(0,6).OrderBy(i=>Math.Abs(radii[i]-radius)).First();
                if(Math.Abs(radii[nearest]-radius)>2.2) displaced++; else hits[nearest]++;
            }
            Check(hits.All(count=>count>35) && displaced==0,"all six trails share one fixed center with circular radii and no expansion",r);
            reveal.Advance(TimeSpan.FromSeconds(1000)+TimeSpan.FromMilliseconds(180)); frame.Render(reveal); frame.CopyPixels(pixels,410*4,0); displaced=0;
            for(int y=0;y<410;y++) for(int x=0;x<410;x++) if(pixels[(y*410+x)*4+3]>45 && radii.Min(radius=>Math.Abs(radius-(new Point(x+.5,y+.5)-new Point(205,205)).Length))>2.2) displaced++;
            Check(displaced==0 && reveal.Progress>0.4 && reveal.Progress<0.5,"rotation keeps every trail on the same concentric path throughout the reveal",r);
            reveal.Advance(TimeSpan.FromSeconds(1000)+TimeSpan.FromMilliseconds(PlanetReveal.DurationMilliseconds+10));
            Check(canvas.Children.Count==0 && !reveal.Running,"the short reveal detaches its rendering callback and releases its visual after completion",r);
            PlanetReveal previous=PlanetReveal.Play(canvas,new Point(),1,config.Appearance.Accent),replacement=PlanetReveal.Play(canvas,new Point(),1,config.Appearance.Accent);
            Check(!previous.Running && replacement.Running && canvas.Children.Count==1,"replaying the preview replaces an unfinished reveal without accumulating frame handlers",r); replacement.Cancel();
            string legacy=Json.Write(config); legacy=legacy.Replace("\"Appearance\":"+Json.Write(config.Appearance)+",",""); Preferences old=Json.Read<Preferences>(legacy); old.Validate(); Check(old.Appearance.RingAccent==config.Appearance.RingAccent,"settings without appearance load default appearance",r);
            config.Appearance.RingAccent="#8DBEFF"; config.Appearance.TrailColor="#FFA8C2"; config.Appearance.TrailWidth=6; config.Appearance.TrailLength=160; config.Appearance.RingOpacity=.6;
            config.Items[0]=PlanetStack.Create(config.Items[0]); config.Orbit.Exclude("test.exe"); config.Validate();
            Preferences cloned=config.Clone(); Check(cloned.Appearance.TrailColor=="#FFA8C2" && cloned.Appearance.RingOpacity==.6 && !Json.Write(config).Contains("Selected"),"appearance round trips without serializing drawing resources",r);
            foreach(string color in new[]{"red","#fff","#FFFFFFFF","#ZZ9999","#12\n345"}) { cloned=config.Clone(); cloned.Appearance.TrailColor=color; Check(Reject(cloned.Validate),"invalid color rejected: "+color.Replace("\n","newline"),r); }
            cloned=config.Clone(); cloned.Appearance.RingOpacity=0; Check(Reject(cloned.Validate),"fully invisible ring cannot be saved",r);
            cloned=config.Clone(); cloned.Appearance.TrailWidth=Double.NaN; Check(Reject(cloned.Validate),"nonfinite trail width rejected",r);
            Point[] curve={new Point(0,0),new Point(100,0),new Point(100,100)}; string original=Json.Write(curve); Point[] shown=AppearancePreferences.VisibleTrail(curve,140);
            Check(shown.Length==3 && shown[0]==new Point(60,0) && shown[1]==curve[1] && shown.Last()==curve.Last() && Json.Write(curve)==original,"trail length clips history at exact arc length without changing recognition points",r);
            Check(AppearancePreferences.VisibleTrail(curve,0).Length==3 && AppearancePreferences.VisibleTrail(new Point[0],40).Length==0,"full and empty trail modes remain safe",r);
            string folder=Path.Combine(Path.GetTempPath(),"OrbitalsBackupTest-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); string file=Path.Combine(folder,"settings.json");
            try {
                SettingsBackup.Write(file,config); Preferences restored=SettingsBackup.Read(file);
                Check(Json.Write(restored)==Json.Write(config) && restored.Items[0].Children[0].Value==config.Items[0].Children[0].Value && restored.Orbit.IsExcluded("test.exe"),"standard JSON backup restores actions stacks gestures blacklist and appearance",r);
                string before=File.ReadAllText(file); cloned=config.Clone(); cloned.Appearance.TrailLength=900;
                Check(Reject(delegate { SettingsBackup.Write(file,cloned); }) && File.ReadAllText(file)==before,"invalid backup cannot overwrite an existing file",r);
                config.Appearance.TrailWidth=4; SettingsBackup.Write(file,config); Check(SettingsBackup.Read(file).Appearance.TrailWidth==4 && Directory.GetFiles(folder).Length==1,"replacing a JSON backup is atomic and leaves no temporary files",r);
                File.WriteAllText(file,"{invalid"); Check(Reject(delegate { SettingsBackup.Read(file); }),"malformed JSON is rejected before touching editor settings",r);
            } finally { if(File.Exists(file)) File.Delete(file); Directory.Delete(folder); }
            Point[] jitter=Enumerable.Range(0,64).Select(i=>new Point(i*2,Math.Sin(i*1.9)*2.2)).ToArray(); List<GesturePoint> raw=GestureShape.Normalize(jitter),clean=GestureCleanup.Clean(raw);
            Check(clean.Max(point=>point.Y)-clean.Min(point=>point.Y)<.00001 && GestureShape.Distance(raw,clean)<.065,"stronger cleanup removes moderate hand jitter within bounded shape error",r);
            Point[] lateral={new Point(0,100),new Point(0,0),new Point(30,0),new Point(30,100)}; clean=GestureCleanup.Clean(GestureShape.Normalize(lateral));
            Check(clean.Last().X-clean[0].X>.2,"stronger cleanup preserves intentional lateral movement",r);
            return r;
        }
    }
}

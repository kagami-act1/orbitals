using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class GestureToleranceTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> r=new List<string>();
            Point[] registered={new Point(0,0),new Point(0,100),new Point(100,100)},attempt={new Point(0,0),new Point(20,80),new Point(100,100)};
            OrbitBinding binding=new OrbitBinding { Pattern="Shape",Shape=GestureShape.Normalize(registered),Action=new ActionItem("角","shortcut","F8"),Tolerance=5 }; bool ambiguous;
            double distance=GestureShape.Distance(binding.Shape,GestureShape.Normalize(attempt));
            Check(distance>.05 && distance<.35 && GestureShape.Match(new[]{binding},attempt,"",out ambiguous)==null,"strict per-gesture tolerance rejects a partially similar stroke",r);
            binding.Tolerance=35; Check(GestureShape.Match(new[]{binding},attempt,"",out ambiguous)==binding,"relaxing the same gesture threshold accepts the same input",r);
            OrbitBinding other=new OrbitBinding { Pattern="Shape",Shape=GestureShape.Normalize(registered.Select(p=>new Point(p.Y,p.X)).ToArray()),Tolerance=5,Action=new ActionItem("別","shortcut","F8") };
            GestureShape.Match(new[]{binding,other},attempt,"",out ambiguous); Check(other.Tolerance==5,"changing one gesture threshold leaves other gesture thresholds unchanged",r);
            OrbitPreferences directions=new OrbitPreferences(); OrbitBinding back=directions.Bindings[0]; Point[] slanted={new Point(200,0),new Point(100,35),new Point(0,70)};
            back.Tolerance=5; Check(directions.ResolveDrawing("test.exe","L",slanted,"")==null,"built-in direction gestures also enforce their own threshold",r);
            back.Tolerance=35; Check(directions.ResolveDrawing("test.exe","L",slanted,"")==back,"built-in direction thresholds can be adjusted independently",r);
            SavedGesture saved=SavedGesture.From(binding,"角の動き"); Check(saved.Preset().Create().Tolerance==35 && saved.Preset().Create().Action.Kind=="none","saved movement presets retain tolerance without binding an action",r);
            Preferences config=Preferences.Defaults(); config.Orbit.Bindings[0].Tolerance=4; Check(Reject(config.Validate),"thresholds below five percent cannot enter saved settings",r);
            config=Preferences.Defaults(); config.Orbit.Bindings[0].Tolerance=36; Check(Reject(config.Validate),"thresholds above thirty-five percent cannot enter saved settings",r);
            OrbitBinding legacy=Json.Read<OrbitBinding>("{\"Pattern\":\"L\",\"Action\":{\"Label\":\"戻る\",\"Kind\":\"none\"}}"); Check(legacy.Tolerance==16,"older gesture settings receive the standard recognition tolerance",r);
            DrawingVisual visual=new DrawingVisual(); using(DrawingContext dc=visual.RenderOpen()) GestureTrail.Draw(dc,new[]{new Point(4,15),new Point(260,15)},12,Brushes.White);
            RenderTargetBitmap bitmap=new RenderTargetBitmap(268,32,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); byte[] pixels=new byte[268*32*4]; bitmap.CopyPixels(pixels,268*4,0);
            List<int> alpha=Enumerable.Range(8,248).Select(x=>(int)pixels[(15*268+x)*4+3]).ToList(); bool monotone=true; for(int i=1;i<alpha.Count;i++) monotone &= alpha[i]>=alpha[i-1] && alpha[i]-alpha[i-1]<=3;
            Check(monotone && alpha.Distinct().Count()>150 && alpha.Last()>240,"straight animated trails use a continuous gradient without visible opacity steps",r);
            DrawingVisual empty=new DrawingVisual(); using(DrawingContext dc=empty.RenderOpen()) { GestureTrail.Draw(dc,new Point[0],2,Brushes.White); GestureTrail.Draw(dc,new[]{new Point(1,1),new Point(1,1)},2,Brushes.White); }
            Check(empty.Drawing==null || empty.Drawing.Children.Count==0,"empty and stationary trails produce no invalid gradient geometry",r);
            return r;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class GestureShapeTests {
        static void Check(bool condition,string label,List<string> results) { if(!condition) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static OrbitBinding Custom(string name,Point[] path) { return new OrbitBinding { Pattern="Shape",Shape=GestureShape.Normalize(path),Action=new ActionItem(name,"shortcut","F6") }; }
        public static List<string> Run() {
            List<string> r=new List<string>();
            Point[] w={new Point(0,0),new Point(30,100),new Point(60,25),new Point(90,100),new Point(120,0)};
            Point[] circle=Enumerable.Range(0,65).Select(i=>new Point(100*Math.Cos(i*Math.PI/32),100*Math.Sin(i*Math.PI/32))).ToArray();
            OrbitBinding custom=Custom("W",w),round=Custom("丸",circle); bool ambiguous;
            Point[] moved=w.Select(p=>new Point(p.X*2.4-1900,p.Y*2.4+500)).ToArray();
            Check(GestureShape.Match(new[]{custom,round},moved,"",out ambiguous)==custom,"custom shapes ignore position and uniform size",r);
            Point[] noisy=circle.Select((p,i)=>new Point(p.X*1.7+300+Math.Sin(i)*2,p.Y*1.7-200+Math.Cos(i)*2)).ToArray();
            Check(GestureShape.Match(new[]{custom,round},noisy,"",out ambiguous)==round,"curved custom stroke tolerates drawing noise",r);
            Check(GestureShape.Match(new[]{custom},w.Reverse().ToArray(),"",out ambiguous)==null,"reversed drawing direction remains distinct",r);
            Check(GestureShape.Match(new[]{custom},w.Select(p=>new Point(p.Y,-p.X)).ToArray(),"",out ambiguous)==null,"rotated shapes are not mistaken for original orientation",r);
            Check(GestureShape.Match(new[]{custom},circle,"",out ambiguous)==null,"unregistered shape is rejected",r);
            Check(GestureShape.Match(new[]{custom,Custom("近いW",moved)},w,"",out ambiguous)==null && ambiguous,"ambiguous templates do not execute an arbitrary action",r);
            Check(GestureShape.Match(new[]{custom},w,"Ctrl",out ambiguous)==null,"shape modifiers are isolated",r);
            Preferences prefs=Preferences.Defaults(); prefs.Orbit.Bindings.Add(custom); prefs.Orbit.Bindings.Add(round); prefs.Validate();
            Check(prefs.Orbit.ResolveDrawing("test.exe","DRUR",moved,"").Action.Label=="W","custom runtime resolution uses actual path",r);
            Check(prefs.Orbit.ResolveDrawing("test.exe","L",new[]{new Point(100,30),new Point(10,30)},"").Action.Value=="Alt+Left","existing direction gestures remain compatible",r);
            prefs.Orbit.Exclude("test.exe");
            Check(prefs.Orbit.ResolveDrawing("TEST.EXE","DRUR",w,"")==null,"blacklist blocks custom shapes case insensitively",r);
            Check(prefs.Orbit.Resolve("test.exe","WheelUp","")==null && prefs.Orbit.Resolve("test.exe","Rocker","")==null,"blacklist also blocks wheel and rocker actions",r);
            prefs.Orbit.Profiles.Clear(); Check(prefs.Orbit.ResolveDrawing("test.exe","DRUR",w,"")==custom,"removing blacklist restores custom shape recognition",r);
            Preferences clone=prefs.Clone(); clone.Validate(); Check(clone.Orbit.Bindings.Last().Shape.Count==64 && clone.Orbit.Bindings.Last().Shape[0].X==round.Shape[0].X,"custom shapes survive settings serialization",r);
            prefs.Orbit.Bindings.Add(Custom("重複W",w)); Check(Reject(prefs.Validate),"duplicate shape rejected before save",r); prefs.Orbit.Bindings.RemoveAt(prefs.Orbit.Bindings.Count-1);
            clone=prefs.Clone(); clone.Orbit.Bindings.Last().Shape[0].X=Double.PositiveInfinity; Check(Reject(clone.Validate),"non-finite stored coordinates rejected",r);
            clone=prefs.Clone(); clone.Orbit.Bindings.Last().Shape.RemoveAt(0); Check(Reject(clone.Validate),"malformed template size rejected",r);
            Check(GestureShape.Normalize(new[]{new Point(1,1),new Point(1,1)})==null,"stationary clicks cannot become shape templates",r);
            List<Point> longPath=new List<Point>(); for(int i=0;i<3000;i++) GestureShape.Append(longPath,new Point(i,Math.Sin(i/20.0)*100));
            Check(longPath.Count<=512 && longPath[0].X==0 && longPath.Last().X==2999,"long gesture paths remain bounded and preserve endpoints",r);
            List<Point> samples=new List<Point>(); for(int segment=1;segment<w.Length;segment++) for(int i=0;i<50;i++) samples.Add(w[segment-1]+(w[segment]-w[segment-1])*i/50.0); samples.Add(w.Last());
            Check(GestureShape.Match(new[]{custom},samples,"",out ambiguous)==custom,"sampling speed does not change recorded shape",r);
            return r;
        }
    }
}

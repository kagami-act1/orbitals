using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class GestureCleanupTests {
        static void Check(bool condition,string label,List<string> results) { if(!condition) throw new Exception(label); results.Add("PASS "+label); }
        static double MaxTurn(List<GesturePoint> path,int trim=2) {
            double largest=0;
            for(int i=trim;i<path.Count-trim;i++) { Vector a=new Vector(path[i].X-path[i-1].X,path[i].Y-path[i-1].Y),b=new Vector(path[i+1].X-path[i].X,path[i+1].Y-path[i].Y); if(a.Length*b.Length>.00000001) largest=Math.Max(largest,Math.Abs(Vector.AngleBetween(a,b))); }
            return largest;
        }
        static Point[] Curve(double angle,double sweep,double noise) {
            return Enumerable.Range(0,129).Select(i=> { double a=angle+sweep*i/128,r=100+noise*Math.Sin(i*1.9); return new Point(r*Math.Cos(a),r*Math.Sin(a)); }).ToArray();
        }
        public static List<string> Run() {
            List<string> r=new List<string>();
            Point[] jitter=Enumerable.Range(0,64).Select(i=>new Point(i*2,Math.Sin(i*1.9)*.65)).ToArray();
            List<GesturePoint> original=GestureShape.Normalize(jitter); string before=Json.Write(original); List<GesturePoint> clean=GestureCleanup.Clean(original);
            Check(clean.Max(p=>p.Y)-clean.Min(p=>p.Y)<.00001,"cleanup straightens small hand jitter",r);
            Check(Json.Write(original)==before,"cleanup preserves original drawing for restoration",r);
            foreach(string directions in new[]{"UD","DU","LR","RL","DR","URD"}) {
                original=GestureShape.Template(new OrbitBinding { Pattern=directions }); clean=GestureCleanup.Clean(original);
                Check(GestureShape.Valid(clean) && GestureShape.Distance(original,clean)<.035,"cleanup retains corners and drawing order: "+directions,r);
            }
            Point[] circle=Enumerable.Range(0,65).Select(i=>new Point(100*Math.Cos(i*Math.PI/32),100*Math.Sin(i*Math.PI/32))).ToArray();
            original=GestureShape.Normalize(circle); clean=GestureCleanup.Clean(original);
            Check(GestureShape.Distance(original,clean)<.035 && clean.Max(p=>p.X)-clean.Min(p=>p.X)>.9 && clean.Max(p=>p.Y)-clean.Min(p=>p.Y)>.9,"cleanup preserves closed curves",r);
            Point[] w={new Point(0,0),new Point(30,100),new Point(60,25),new Point(90,100),new Point(120,0)};
            original=GestureShape.Normalize(w); clean=GestureCleanup.Clean(original);
            OrbitBinding binding=new OrbitBinding { Pattern="Shape",Shape=clean,OriginalShape=original,Action=new ActionItem("W","shortcut","F6") }; bool ambiguous;
            Check(GestureShape.Match(new[]{binding},w,"",out ambiguous)==binding,"original custom drawing still matches cleaned template",r);
            Check(GestureShape.Match(new[]{binding},w.Reverse().ToArray(),"",out ambiguous)==null,"cleanup never reverses drawing order",r);
            Preferences prefs=Preferences.Defaults(); prefs.Orbit.Bindings.Add(binding); prefs.Validate(); Preferences clone=prefs.Clone();
            Check(Json.Write(clone.Orbit.Bindings.Last().OriginalShape)==Json.Write(original),"original drawing survives save and reload",r);
            clone.Orbit.Bindings.Last().OriginalShape[0].X=Double.NaN; bool invalid=false; try { clone.Validate(); } catch { invalid=true; }
            Check(invalid,"malformed original drawing rejected before save",r);
            Point[] wavering=Enumerable.Range(0,64).Select(i=>new Point(i*2,Math.Sin(i*Math.PI/63)*Math.Cos(i*1.7)*4.5)).ToArray();
            original=GestureShape.Normalize(wavering); clean=GestureCleanup.Clean(original);
            Check(clean.Max(point=>point.Y)-clean.Min(point=>point.Y)<.00001 && GestureShape.Distance(original,clean)<.11,"strong cleanup straightens a visibly wavering line",r);
            Point[] roughCircle=Enumerable.Range(0,65).Select(i=> { double a=i*Math.PI/32,radius=100+Math.Sin(i*1.7)*7; return new Point(radius*Math.Cos(a),radius*Math.Sin(a)); }).ToArray();
            original=GestureShape.Normalize(roughCircle); string rawCircle=Json.Write(original); clean=GestureCleanup.Clean(original);
            double cx=(clean.Min(point=>point.X)+clean.Max(point=>point.X))/2,cy=(clean.Min(point=>point.Y)+clean.Max(point=>point.Y))/2;
            double[] radii=clean.Select(point=>Math.Sqrt((point.X-cx)*(point.X-cx)+(point.Y-cy)*(point.Y-cy))).ToArray();
            Check(radii.Max()-radii.Min()<.01 && GestureShape.Distance(original,clean)<.11,"rough closed circular stroke is regularized to a circle",r);
            Check(Json.Write(original)==rawCircle,"regularizing a circle retains exact original template",r);
            Check(GestureShape.Distance(clean,GestureShape.Normalize(roughCircle.Reverse().ToArray()))>.2,"regularized circle still distinguishes clockwise and counterclockwise",r);
            Point[] ellipse=circle.Select(point=>new Point(point.X*1.5,point.Y)).ToArray(); clean=GestureCleanup.Clean(GestureShape.Normalize(ellipse));
            Check((clean.Max(point=>point.X)-clean.Min(point=>point.X))/(clean.Max(point=>point.Y)-clean.Min(point=>point.Y))>1.25,"intentional oval is not forced into a circle",r);
            foreach(double sweep in new[]{Math.PI/2,Math.PI,Math.PI*1.5,-Math.PI}) {
                Point[] arc=Curve(.27,sweep,1.2); original=GestureShape.Normalize(arc); clean=GestureCleanup.Clean(original);
                Check(MaxTurn(clean)<12 && GestureShape.Distance(original,clean)<.025,"curved arc remains smooth without diagonal snapping: "+sweep,r);
                binding=new OrbitBinding { Pattern="Shape",Shape=clean,OriginalShape=original,Action=new ActionItem("arc","shortcut","F6") };
                Check(GestureShape.Match(new[]{binding},arc,"",out ambiguous)==binding,"hand-drawn arc still matches corrected template: "+sweep,r);
            }
            Point[] shallow=Enumerable.Range(0,65).Select(i=>new Point(i*2,4*Math.Sin(i*Math.PI/64))).ToArray();
            original=GestureShape.Normalize(shallow); clean=GestureCleanup.Clean(original);
            Check(clean.Max(p=>p.Y)-clean.Min(p=>p.Y)>.025 && MaxTurn(clean)<5,"shallow intentional bow is not flattened into a line",r);
            Point[] sBend=Enumerable.Range(0,129).Select(i=>new Point(i*2,30*Math.Sin(i*Math.PI/64)+.8*Math.Sin(i*1.7))).ToArray();
            original=GestureShape.Normalize(sBend); clean=GestureCleanup.Clean(original);
            Check(MaxTurn(clean)<15 && GestureShape.Distance(original,clean)<.03 && clean.Max(p=>p.Y)-clean.Min(p=>p.Y)>.2,"S bend is smoothed without invented corners",r);
            Point[] eight=Enumerable.Range(0,193).Select(i=> { double a=.2+2*Math.PI*i/192; return new Point(90*Math.Sin(a)+.4*Math.Sin(i*1.7),60*Math.Sin(a*2)); }).ToArray();
            original=GestureShape.Normalize(eight); clean=GestureCleanup.Clean(original);
            Check(MaxTurn(clean)<40 && GestureShape.Distance(original,clean)<.035,"crossing loop retains its shape and smooth tangents",r);
            Check(GestureShape.Distance(clean,GestureShape.Normalize(eight.Reverse().ToArray()))>.2,"loop correction retains drawing direction",r);
            return r;
        }
    }
}

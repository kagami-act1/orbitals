using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    public sealed class GesturePoint {
        public double X { get; set; }
        public double Y { get; set; }
        public GesturePoint() { }
        public GesturePoint(Point p) { X=p.X; Y=p.Y; }
    }
    // Ordered single-stroke templates: equal arc-length samples, translation and
    // uniform-scale normalization. Orientation and drawing direction are retained.
    static class GestureShape {
        public const int Samples=64;
        public static double Extent(IList<Point> points) { return points.Count==0?0:Math.Max(points.Max(p=>p.X)-points.Min(p=>p.X),points.Max(p=>p.Y)-points.Min(p=>p.Y)); }
        public static void Append(List<Point> points,Point point) {
            if(points.Count>0 && (points[points.Count-1]-point).Length<2) return;
            if(points.Count>=512) { for(int i=points.Count-2;i>0;i-=2) points.RemoveAt(i); }
            points.Add(point);
        }
        public static List<GesturePoint> Normalize(IList<Point> raw) {
            if(raw==null || raw.Count<2 || raw.Count>512 || raw.Any(p=>!Finite(p.X) || !Finite(p.Y))) return null;
            double length=0; for(int i=1;i<raw.Count;i++) length+=(raw[i]-raw[i-1]).Length;
            if(length<.0001) return null;
            List<Point> sampled=new List<Point>(Samples); int segment=1; double walked=0;
            for(int i=0;i<Samples;i++) {
                double target=length*i/(Samples-1);
                while(segment<raw.Count-1 && walked+(raw[segment]-raw[segment-1]).Length<target) { walked+=(raw[segment]-raw[segment-1]).Length; segment++; }
                Vector delta=raw[segment]-raw[segment-1]; double fraction=delta.Length<.000001?0:Math.Max(0,Math.Min(1,(target-walked)/delta.Length));
                sampled.Add(raw[segment-1]+delta*fraction);
            }
            double size=Extent(sampled); if(size<.0001) return null;
            double cx=sampled.Average(p=>p.X),cy=sampled.Average(p=>p.Y);
            return sampled.Select(p=>new GesturePoint(new Point(Math.Round((p.X-cx)/size,6),Math.Round((p.Y-cy)/size,6)))).ToList();
        }
        static bool Finite(double v) { return !Double.IsNaN(v) && !Double.IsInfinity(v); }
        public static bool Valid(List<GesturePoint> points) { return points!=null && points.Count==Samples && points.All(p=>p!=null && Finite(p.X) && Finite(p.Y) && Math.Abs(p.X)<=2 && Math.Abs(p.Y)<=2) && Extent(points.Select(p=>new Point(p.X,p.Y)).ToArray())>.5; }
        public static List<GesturePoint> Template(OrbitBinding binding) {
            if(binding.Shape!=null) return binding.Shape;
            if(!OrbitPreferences.ValidPattern(binding.Pattern) || binding.Pattern=="WheelUp" || binding.Pattern=="WheelDown" || binding.Pattern=="Rocker") return null;
            List<Point> points=new List<Point> { new Point(0,0) }; Point at=new Point();
            foreach(char c in binding.Pattern) { at+=c=='U'?new Vector(0,-1):c=='D'?new Vector(0,1):c=='L'?new Vector(-1,0):new Vector(1,0); points.Add(at); }
            return Normalize(points);
        }
        public static double Distance(IList<GesturePoint> a,IList<GesturePoint> b) {
            if(a==null || b==null || a.Count!=Samples || b.Count!=Samples) return Double.MaxValue;
            double sum=0; for(int i=0;i<Samples;i++) { double x=a[i].X-b[i].X,y=a[i].Y-b[i].Y; sum+=Math.Sqrt(x*x+y*y); } return sum/Samples;
        }
        public static OrbitBinding Match(IEnumerable<OrbitBinding> candidates,IList<Point> path,string modifier,out bool ambiguous) {
            ambiguous=false; List<GesturePoint> sample=Normalize(path); if(sample==null) return null;
            OrbitBinding best=null; double nearest=Double.MaxValue,second=Double.MaxValue;
            foreach(OrbitBinding candidate in candidates) {
                if(candidate.Modifier!=modifier || candidate.Shape==null) continue;
                double distance=Distance(sample,candidate.Shape);
                if(distance>candidate.MatchLimit) continue;
                if(distance<nearest) { second=nearest; nearest=distance; best=candidate; }
                else if(distance<second) second=distance;
            }
            if(best==null) return null;
            ambiguous=best!=null && second-nearest<.025;
            return ambiguous?null:best;
        }
    }
}

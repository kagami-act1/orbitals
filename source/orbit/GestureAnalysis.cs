using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    sealed class GestureCandidate { public OrbitBinding Binding; public double Distance; }
    sealed class GestureAnalysis {
        public OrbitBinding Match;
        public List<GestureCandidate> Candidates;
        public bool Ambiguous;
        public static GestureAnalysis Evaluate(OrbitPreferences config,IList<Point> path,string modifier) {
            List<GesturePoint> sample=GestureShape.Normalize(path);
            GestureAnalysis result=new GestureAnalysis { Candidates=new List<GestureCandidate>() };
            if(sample==null || GestureShape.Extent(path)<config.Distance) return result;
            DirectionStroke stroke=new DirectionStroke(path[0],config.Distance); foreach(Point point in path.Skip(1)) stroke.Add(point);
            result.Match=config.ResolveDrawing("orbitals-test.exe",stroke.Pattern,path,modifier);
            result.Candidates=config.Bindings.Where(b=>b.Modifier==modifier && GestureShape.Template(b)!=null).Select(b=>new GestureCandidate { Binding=b,Distance=GestureShape.Distance(sample,GestureShape.Template(b)) }).Where(c=>c.Distance<.3).OrderBy(c=>c.Distance).Take(3).ToList();
            bool ambiguous; GestureShape.Match(config.Bindings,path,modifier,out ambiguous); result.Ambiguous=ambiguous;
            return result;
        }
    }
}

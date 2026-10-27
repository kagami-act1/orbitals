using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    sealed class GesturePreset {
        public readonly string Id,Category,Name;
        readonly string pattern,modifier;
        readonly List<GesturePoint> shape;
        public int Tolerance=16;
        public GesturePreset(string id,string category,string name,string pattern,string modifier="",List<GesturePoint> shape=null) {
            Id=id; Category=category; Name=name; this.pattern=pattern; this.modifier=modifier; this.shape=shape;
        }
        public OrbitBinding Create() {
            return new OrbitBinding { Pattern=shape==null?pattern:"Shape",Modifier=modifier,Action=new ActionItem(Name,"none",""),Shape=shape==null?null:shape.Select(p=>new GesturePoint(new Point(p.X,p.Y))).ToList(),Tolerance=Tolerance };
        }
        public bool UsesSameGesture(OrbitBinding current) {
            if(current.Modifier!=modifier) return false;
            if(current.Shape==null && shape==null) return current.Pattern==pattern;
            return GestureShape.Distance(GestureShape.Template(current),GestureShape.Template(Create()))<.045;
        }
        public bool Matches(string query) { return (Name+" "+Category).IndexOf((query??"").Trim(),StringComparison.OrdinalIgnoreCase)>=0; }
    }
    static class GesturePresets {
        public static readonly string[] Categories={"直線","往復","折れ線","曲線","ホイール","クリック","複合"};
        static readonly List<GesturePreset> catalog=Build();
        public static List<GesturePreset> Create() { return new List<GesturePreset>(catalog); }
        static List<GesturePreset> Build() {
            List<GesturePreset> presets=new List<GesturePreset> {
                new GesturePreset("left","直線","左へ","L"),
                new GesturePreset("right","直線","右へ","R"),
                new GesturePreset("up","直線","上へ","U"),
                new GesturePreset("down","直線","下へ","D"),
                new GesturePreset("up-down","往復","上 → 下","UD"),
                new GesturePreset("down-up","往復","下 → 上","DU"),
                new GesturePreset("down-right","折れ線","下 → 右","DR"),
                new GesturePreset("right-down","折れ線","右 → 下","RD"),
                new GesturePreset("down-left","折れ線","下 → 左","DL"),
                new GesturePreset("left-up","折れ線","左 → 上","LU"),
                new GesturePreset("right-up","折れ線","右 → 上","RU"),
                new GesturePreset("up-left","折れ線","上 → 左","UL"),
                new GesturePreset("up-right","折れ線","上 → 右","UR"),
                new GesturePreset("wheel-up","ホイール","ホイール上","WheelUp"),
                new GesturePreset("wheel-down","ホイール","ホイール下","WheelDown"),
                new GesturePreset("left-click","クリック","左クリック","Rocker"),
                new GesturePreset("shift-left-click","クリック","左クリック","Rocker","Shift")
            };
            List<GesturePoint> c=GestureShape.Normalize(Enumerable.Range(0,97).Select(i=> { double a=-Math.PI/3-4*Math.PI/3*i/96; return new Point(Math.Cos(a),Math.Sin(a)); }).ToArray());
            List<GesturePoint> v=GestureShape.Normalize(new[]{new Point(0,0),new Point(.5,1),new Point(1,0)});
            List<GesturePoint> s=GestureShape.Normalize(Enumerable.Range(0,129).Select(i=> { double t=i/128.0; return new Point(Math.Cos(3*Math.PI*t),t*3); }).ToArray());
            List<GesturePoint> m=GestureShape.Normalize(new[]{new Point(0,1),new Point(.25,0),new Point(.5,.6),new Point(.75,0),new Point(1,1)});
            presets.Add(new GesturePreset("c","曲線","C字","","",c));
            presets.Add(new GesturePreset("v","折れ線","V字","","",v));
            presets.Add(new GesturePreset("s","曲線","S字","","",s));
            presets.Add(new GesturePreset("m","折れ線","M字","","",m));
            presets.Add(new GesturePreset("w","折れ線","W字","","",GestureShape.Normalize(new[]{new Point(0,0),new Point(.25,1),new Point(.5,.4),new Point(.75,1),new Point(1,0)})));
            presets.Add(new GesturePreset("z","折れ線","Z字","","",GestureShape.Normalize(new[]{new Point(0,0),new Point(1,0),new Point(0,1),new Point(1,1)})));
            presets.Add(new GesturePreset("n","折れ線","N字","","",GestureShape.Normalize(new[]{new Point(0,1),new Point(0,0),new Point(1,1),new Point(1,0)})));
            presets.Add(new GesturePreset("inverted-v","折れ線","逆V字（∧）","","",GestureShape.Normalize(new[]{new Point(0,1),new Point(.5,0),new Point(1,1)})));
            foreach(bool clockwise in new[]{true,false}) {
                List<GesturePoint> circle=GestureShape.Normalize(Enumerable.Range(0,129).Select(i=> { double angle=-Math.PI/2+(clockwise?1:-1)*Math.PI*2*i/128; return new Point(Math.Cos(angle),Math.Sin(angle)); }).ToArray());
                presets.Add(new GesturePreset(clockwise?"circle-clockwise":"circle-counterclockwise","曲線",clockwise?"○（右回り）":"○（左回り）","","",circle));
            }
            return presets.OrderBy(p=>Array.IndexOf(Categories,p.Category)).ToList();
        }
        public static string Category(OrbitBinding binding) {
            if(binding.Shape!=null && !GestureShape.Valid(binding.Shape)) return "複合";
            if(binding.Pattern=="WheelUp" || binding.Pattern=="WheelDown") return "ホイール";
            if(binding.Pattern=="Rocker") return "クリック";
            List<GesturePoint> shape=GestureShape.Template(binding); if(shape==null) return "複合";
            GesturePreset nearest=catalog.Where(p=>GestureShape.Template(p.Create())!=null).OrderBy(p=>GestureShape.Distance(shape,GestureShape.Template(p.Create()))).First();
            if(GestureShape.Distance(shape,GestureShape.Template(nearest.Create()))<.1) return nearest.Category;
            Point first=new Point(shape[0].X,shape[0].Y); Vector axis=shape.Select(p=>new Point(p.X,p.Y)-first).OrderByDescending(v=>v.LengthSquared).First(); axis.Normalize();
            if(shape.All(p=>Math.Abs(Vector.CrossProduct(new Point(p.X,p.Y)-first,axis))<.06)) { double length=0; for(int i=1;i<shape.Count;i++) length+=(new Point(shape[i].X,shape[i].Y)-new Point(shape[i-1].X,shape[i-1].Y)).Length; return length>1.35?"往復":"直線"; }
            double sharp=0,smooth=0; for(int i=4;i<shape.Count-4;i+=4) { Vector a=new Vector(shape[i].X-shape[i-4].X,shape[i].Y-shape[i-4].Y),b=new Vector(shape[i+4].X-shape[i].X,shape[i+4].Y-shape[i].Y); if(a.Length<.001 || b.Length<.001) continue; double turn=Math.Abs(Vector.AngleBetween(a,b)); if(turn>55) sharp++; else smooth+=turn; }
            return sharp==0?"曲線":smooth>100?"複合":"折れ線";
        }
        public static int Existing(IList<OrbitBinding> bindings,GesturePreset preset) {
            for(int i=0;i<bindings.Count;i++) if(preset.UsesSameGesture(bindings[i])) return i;
            return -1;
        }
        public static int Add(IList<OrbitBinding> bindings,GesturePreset preset) {
            int existing=Existing(bindings,preset); if(existing>=0) return existing;
            if(bindings.Count>=128) throw new InvalidOperationException("ジェスチャーは128件までです。");
            bindings.Add(preset.Create()); return bindings.Count-1;
        }
    }
}

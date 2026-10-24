using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    sealed class PlanetTarget {
        public int Id,Slot,Depth;
        public ActionItem Item;
        public Point Position;
        public double Angle,Radius,Size;
    }
    sealed class PlanetLayer {
        public int Depth;
        public bool Arc;
        public double Radius,Angle,Spacing,Size,Gap;
        public string Title;
        public readonly List<PlanetTarget> Targets=new List<PlanetTarget>();
    }
    // Navigation keeps a path through the actual action tree. Rebuilding visible
    // layers restores mixed arc/level parents without copying action identities.
    sealed class PlanetMenu {
        sealed class Step { public ActionItem Stack; public int Slot; }
        readonly IList<ActionItem> root;
        readonly double size;
        readonly double gap;
        readonly List<Step> path=new List<Step>();
        public readonly List<PlanetLayer> Layers=new List<PlanetLayer>();
        public readonly List<PlanetTarget> Targets=new List<PlanetTarget>();
        public bool CanGoBack { get { return path.Count>0; } }
        public string Title { get { return path.Count==0?null:path[path.Count-1].Stack.Label; } }
        public PlanetMenu(IList<ActionItem> items,double buttonSize,double stackGap=PlanetStack.DefaultGap) { root=items; size=buttonSize; gap=stackGap; Build(); }
        public static Point Polar(double radius,double angle) { return new Point(205+Math.Cos(angle)*radius,205+Math.Sin(angle)*radius); }
        public static double ArcSpacing(double radius,double buttonSize) { return 2*Math.Asin(Math.Min(1,(buttonSize+12)/(2*radius))); }
        public static double OuterEdge(PlanetLayer layer) { return layer.Arc?layer.Radius+layer.Size/2+10:189; }
        public static double InnerEdge(PlanetLayer layer) { return layer.Arc?layer.Radius-layer.Size/2-10:RingSurface.InnerRadius; }
        void AddLayer(IList<ActionItem> items,int depth,bool arc,double radius,double angle,string title,double gap=PlanetStack.DefaultGap) {
            int[] occupied=Enumerable.Range(0,items.Count).Where(i=>items[i].Kind!="none").ToArray();
            int rows=arc?Math.Max(1,(occupied.Length+7)/8):1;
            for(int row=0;row<rows;row++) {
            double buttonSize=arc?size*.9:size,rowRadius=radius+row*(buttonSize+20+gap);
            PlanetLayer layer=new PlanetLayer { Depth=depth,Arc=arc,Radius=rowRadius,Angle=angle,Size=buttonSize,Gap=gap,Title=title };
            layer.Spacing=arc?ArcSpacing(rowRadius,buttonSize):Math.PI/4;
            int[] slots=arc?occupied.Skip(row*8).Take(8).ToArray():Enumerable.Range(0,8).ToArray(); int count=slots.Length,shown=0;
            foreach(int slot in slots) {
                double at=arc?angle+(shown-(count-1)/2.0)*layer.Spacing:(slot*45-90)*Math.PI/180;
                PlanetTarget target=new PlanetTarget { Id=Targets.Count,Slot=slot,Depth=depth,Item=items[slot],Radius=rowRadius,Angle=at,Size=layer.Size,Position=Polar(rowRadius,at) };
                Targets.Add(target); layer.Targets.Add(target); shown++;
            }
            Layers.Add(layer);
            }
        }
        void Build() {
            Layers.Clear(); Targets.Clear(); AddLayer(root,0,false,131,0,null);
            for(int depth=0;depth<path.Count;depth++) {
                Step step=path[depth]; PlanetTarget parent=Targets.First(t=>t.Depth==depth && t.Slot==step.Slot);
                if(PlanetStack.Mode(step.Stack)=="arc") AddLayer(step.Stack.Children,depth+1,true,Layers.Max(layer=>OuterEdge(layer))+gap+size*.9/2+10,parent.Angle,step.Stack.Label,gap);
                else { Layers.Clear(); Targets.Clear(); AddLayer(step.Stack.Children,depth+1,false,131,0,step.Stack.Label); }
            }
        }
        public PlanetTarget Target(int id) { return id>=0 && id<Targets.Count?Targets[id]:null; }
        public bool IsExpanded(int id) { PlanetTarget target=Target(id); return target!=null && target.Depth<path.Count && Object.ReferenceEquals(path[target.Depth].Stack,target.Item); }
        public bool Open(int id) {
            PlanetTarget target=Target(id);
            if(target==null || target.Item.Kind!="stack" || target.Depth>=PlanetStack.MaxDepth) return false;
            bool collapse=IsExpanded(id);
            if(path.Count>target.Depth) path.RemoveRange(target.Depth,path.Count-target.Depth);
            if(!collapse) path.Add(new Step { Stack=target.Item,Slot=target.Slot });
            Build(); return true;
        }
        public bool Open(ActionItem item) { PlanetTarget target=Targets.FirstOrDefault(t=>Object.ReferenceEquals(t.Item,item)); return target!=null && Open(target.Id); }
        public bool Back() { if(path.Count==0) return false; path.RemoveAt(path.Count-1); Build(); return true; }
        public int KeyboardTarget(int slot) { PlanetTarget target=Targets.FirstOrDefault(t=>t.Depth==Layers[Layers.Count-1].Depth && t.Slot==slot && t.Item.Kind!="none"); return target==null?-1:target.Id; }
        public int IconHit(Point point) {
            for(int i=Targets.Count-1;i>=0;i--) if(Targets[i].Item.Kind!="none" && (Targets[i].Position-point).Length<=Targets[i].Size/2) return i;
            return -1;
        }
        static double AngleDifference(double a,double b) { return Math.Atan2(Math.Sin(a-b),Math.Cos(a-b)); }
        public int Hit(Point point) {
            int icon=IconHit(point); if(icon>=0) return icon;
            Vector delta=point-new Point(205,205); double radius=delta.Length,angle=Math.Atan2(delta.Y,delta.X);
            for(int i=Layers.Count-1;i>=0;i--) {
                PlanetLayer layer=Layers[i];
                double inner=layer.Arc?layer.Radius-55:RingSurface.InnerRadius;
                double outer=i+1<Layers.Count?(layer.Radius+Layers[i+1].Radius)/2:layer.Arc?layer.Radius+90:RingSurface.OuterRadius;
                if(radius<inner || radius>outer) continue;
                if(!layer.Arc) { int slot=RingSurface.Slot(delta.X,delta.Y); PlanetTarget rootTarget=layer.Targets[slot]; return rootTarget.Item.Kind=="none"?-1:rootTarget.Id; }
                PlanetTarget nearest=null; double distance=Double.MaxValue;
                foreach(PlanetTarget target in layer.Targets) { double current=Math.Abs(AngleDifference(angle,target.Angle)); if(current<distance) { distance=current; nearest=target; } }
                if(nearest!=null && distance<=layer.Spacing/2) return nearest.Id;
            }
            return -1;
        }
    }
}

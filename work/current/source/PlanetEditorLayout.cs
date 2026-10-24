using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    sealed class EditorTarget {
        public int Slot,Depth;
        public int[] Path;
        public ActionItem Item;
        public Point Position;
        public double Angle,Size,Radius;
    }
    sealed class PlanetEditorLayout {
        public readonly PlanetMenu Menu;
        public readonly List<EditorTarget> Targets=new List<EditorTarget>();
        public readonly int[] Context,ExpandedPath;
        public readonly int Selected;
        public PlanetEditorLayout(IList<ActionItem> root,int[] path,int selected,double stackGap=PlanetStack.DefaultGap) {
            Menu=new PlanetMenu(root,76,stackGap); List<int> opened=new List<int>();
            foreach(int slot in path.Take(PlanetStack.MaxDepth)) {
                PlanetTarget target=Menu.Targets.FirstOrDefault(t=>t.Depth==opened.Count && t.Slot==slot && t.Item.Kind=="stack");
                if(target==null || !Menu.Open(target.Id)) break; opened.Add(slot);
            }
            Context=opened.ToArray(); Selected=selected;
            PlanetTarget chosen=Menu.Targets.FirstOrDefault(t=>t.Depth==Context.Length && t.Slot==selected);
            if(chosen!=null && chosen.Item.Kind=="stack" && PlanetStack.Mode(chosen.Item)=="arc" && Menu.Open(chosen.Id)) opened.Add(selected);
            ExpandedPath=opened.ToArray();
            foreach(PlanetTarget target in Menu.Targets) Targets.Add(new EditorTarget { Slot=target.Slot,Depth=target.Depth,Path=ExpandedPath.Take(target.Depth).ToArray(),Item=target.Item,Position=target.Position,Angle=target.Angle,Size=target.Size,Radius=target.Radius });
            PlanetLayer layer=Menu.Layers.Last();
            if(!layer.Arc) return;
            IList<ActionItem> items=Resolve(root,ExpandedPath); int empty=-1;
            if(Context.SequenceEqual(ExpandedPath) && selected>=0 && selected<items.Count && items[selected].Kind=="none") empty=selected;
            else for(int i=0;i<items.Count;i++) if(items[i].Kind=="none") { empty=i; break; }
            if(empty<0) { if(items.Count>=PlanetStack.MaxArcItems) return; empty=items.Count; }
            if(layer.Targets.Count==8) { double radius=layer.Radius+layer.Size+20+layer.Gap; layer=new PlanetLayer { Depth=layer.Depth,Arc=true,Radius=radius,Angle=layer.Angle,Spacing=PlanetMenu.ArcSpacing(radius,layer.Size),Size=layer.Size,Gap=layer.Gap,Title=layer.Title }; Menu.Layers.Add(layer); }
            double angle=layer.Targets.Count==0?layer.Angle:layer.Targets.Last().Angle+layer.Spacing;
            Targets.Add(new EditorTarget { Slot=empty,Depth=layer.Depth,Path=ExpandedPath.ToArray(),Item=empty<items.Count?items[empty]:PlanetStack.Empty(),Position=PlanetMenu.Polar(layer.Radius,angle),Angle=angle,Size=layer.Size,Radius=layer.Radius });
        }
        public static IList<ActionItem> Resolve(IList<ActionItem> root,IEnumerable<int> path) { IList<ActionItem> items=root; foreach(int slot in path) items=items[slot].Children; return items; }
        public bool IsSelected(EditorTarget target) { return target.Slot==Selected && target.Path.SequenceEqual(Context); }
        public bool IsExpanded(EditorTarget target) { return target.Item.Kind=="stack" && target.Depth<ExpandedPath.Length && target.Slot==ExpandedPath[target.Depth]; }
    }
}

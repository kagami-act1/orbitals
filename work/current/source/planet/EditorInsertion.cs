using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    static class EditorInsertion {
        static int Height(ActionItem item) { return item.Kind!="stack"?0:1+item.Children.Select(Height).DefaultIfEmpty(0).Max(); }
        public static List<ActionItem> Prepare(IList<ActionItem> items,int before,ActionItem item,int from,int depth,out int selected) {
            selected=-1;
            if(item==null || item.Kind=="none" || before<0 || before>items.Count || from>=items.Count || depth+Height(item)>PlanetStack.MaxDepth) return null;
            if(from<0 && items.All(a=>a.Kind!="none")) return null;
            var ordered=new List<ActionItem>();
            for(int i=0;i<=items.Count;i++) {
                if(i==before) { selected=ordered.Count; ordered.Add(item); }
                if(i<items.Count && i!=from && items[i].Kind!="none") ordered.Add(items[i]);
            }
            if(ordered.Count>items.Count) return null;
            while(ordered.Count<items.Count) ordered.Add(PlanetStack.Empty());
            return ordered;
        }
    }
}

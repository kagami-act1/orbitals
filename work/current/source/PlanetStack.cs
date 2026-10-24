using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    static class PlanetStack {
        public const int MaxDepth=3;
        public const int MaxArcItems=32;
        public const double DefaultGap=20,MinGap=8,MaxGap=160;
        public static bool ValidChildren(ActionItem stack) { return stack.Children!=null && (Mode(stack)=="arc"?stack.Children.Count>=1 && stack.Children.Count<=MaxArcItems:stack.Children.Count==8); }
        public static ActionItem Empty() { return new ActionItem("未割り当て","none",""); }
        public static ActionItem Create(ActionItem first=null) {
            ActionItem folder=new ActionItem("スタック","stack","") { StackMode="level",Children=Enumerable.Range(0,8).Select(i=>Empty()).ToList() };
            if(first!=null && first.Kind!="none") folder.Children[0]=first.Clone();
            return folder;
        }
        public static string Mode(ActionItem stack) { return String.IsNullOrEmpty(stack.StackMode)?"level":stack.StackMode; }
        public static bool Erase(IList<ActionItem> items,int slot,bool arc) {
            if(slot<0 || slot>=items.Count || items[slot].Kind=="none") return false;
            if(arc) { items.RemoveAt(slot); if(items.Count==0) items.Add(Empty()); }
            else items[slot]=Empty();
            return true;
        }
        public static void UpgradeLegacy(Preferences config) {
            foreach(ActionItem item in config.AllActions()) if(item.Kind=="stack" && item.StackMode==null) {
                if(item.Icon=="ms:folder" || item.Icon=="folder") item.Icon=null;
                item.StackMode="level";
            }
        }
        public static IEnumerable<ActionItem> Walk(IEnumerable<ActionItem> items) {
            foreach(ActionItem item in items) { yield return item; if(item.Kind=="stack" && item.Children!=null) foreach(ActionItem child in Walk(item.Children)) yield return child; }
        }
        public static void NewIdentities(IEnumerable<ActionItem> items) { foreach(ActionItem item in Walk(items)) if(item.Kind=="toggle" && item.Toggle!=null) item.Toggle.Id=Guid.NewGuid().ToString("N"); }
        public static bool Add(ActionItem stack,ActionItem action) {
            if(stack.Kind!="stack" || stack.Children==null) return false;
            int empty=stack.Children.FindIndex(a=>a.Kind=="none"); if(empty>=0) { stack.Children[empty]=action; return true; }
            if(Mode(stack)!="arc" || stack.Children.Count>=MaxArcItems) return false; stack.Children.Add(action); return true;
        }
    }
}

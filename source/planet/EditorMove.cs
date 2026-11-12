using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    sealed class EditorMove {
        IList<ActionItem> source,destination;
        int from,to;
        ActionItem item,replacement;
        bool append;
        public string Error { get; private set; }
        public string Hint { get; private set; }
        public int[] Path { get; private set; }
        public int Slot { get { return to; } }
        public bool Allowed { get { return Error==null; } }
        static int Height(ActionItem action) { return action.Kind!="stack"?0:1+action.Children.Select(Height).DefaultIfEmpty(0).Max(); }
        static bool Prefix(int[] first,int[] second) { return first.Length<=second.Length && first.SequenceEqual(second.Take(first.Length)); }
        static IList<ActionItem> Resolve(IList<ActionItem> root,int[] path,out ActionItem parent) {
            parent=null; IList<ActionItem> items=root;
            foreach(int slot in path) { if(items==null || slot<0 || slot>=items.Count || items[slot].Kind!="stack") return null; parent=items[slot]; items=parent.Children; }
            return items;
        }
        static EditorMove Invalid(string text) { return new EditorMove { Error=text }; }
        public static EditorMove Plan(IList<ActionItem> root,int[] fromPath,int sourceSlot,int[] toPath,int targetSlot) {
            fromPath=fromPath??new int[0]; toPath=toPath??new int[0]; ActionItem sourceParent,targetParent;
            IList<ActionItem> source=Resolve(root,fromPath,out sourceParent),destination=Resolve(root,toPath,out targetParent);
            if(source==null || destination==null || sourceSlot<0 || sourceSlot>=source.Count || targetSlot<0 || targetSlot>destination.Count || source[sourceSlot].Kind=="none") return Invalid("移動する項目と移動先を選んでください。");
            if(fromPath.SequenceEqual(toPath) && sourceSlot==targetSlot) return Invalid("同じ場所です。");
            ActionItem item=source[sourceSlot],target=targetSlot<destination.Count?destination[targetSlot]:PlanetStack.Empty();
            if(item.Kind=="stack" && (Object.ReferenceEquals(item,target) || Prefix(fromPath.Concat(new[]{sourceSlot}).ToArray(),toPath))) return Invalid("スタックを自分自身やその中へ移動することはできません。");
            bool append=target.Kind=="stack"; int[] path=(int[])toPath.Clone(); int slot=targetSlot;
            if(append) {
                if(target.Children==null) return Invalid("スタックの内容を確認してください。");
                destination=target.Children; path=toPath.Concat(new[]{targetSlot}).ToArray();
                if(Object.ReferenceEquals(source,destination)) return Invalid("同じスタック内です。移動先の枠へドラッグしてください。");
                slot=target.Children.FindIndex(a=>a.Kind=="none"); if(slot<0) slot=target.Children.Count;
                if(slot==destination.Count && (PlanetStack.Mode(target)!="arc" || destination.Count>=PlanetStack.MaxArcItems)) return Invalid("スタックがいっぱいです。階層型は8枠、弧状は32件までです。");
            } else if(slot==destination.Count && (targetParent==null || PlanetStack.Mode(targetParent)!="arc" || destination.Count>=PlanetStack.MaxArcItems)) return Invalid("この場所へ項目を追加できません。");
            if(path.Length+Height(item)>PlanetStack.MaxDepth) return Invalid("スタックは3階層までです。より浅い場所へ移動してください。");
            if(!append && target.Kind!="none" && fromPath.Length+Height(target)>PlanetStack.MaxDepth) return Invalid("入れ替えるとスタックが3階層を超えます。");
            return new EditorMove { source=source,destination=destination,from=sourceSlot,to=slot,item=item,replacement=target,append=append,Path=path,Hint=append?"スタックへ移動":target.Kind=="none"?"ここに移動":"ここで入れ替え" };
        }
        public void Apply() {
            if(!Allowed) throw new InvalidOperationException(Error);
            if(to==destination.Count) destination.Add(item); else destination[to]=item;
            source[from]=append || replacement.Kind=="none"?PlanetStack.Empty():replacement;
        }
    }
}

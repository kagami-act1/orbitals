using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class EvenSpacingTests {
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        public static List<string> Run() {
            var result=new List<string>(); Preferences config=Preferences.Defaults();
            Check(!config.Appearance.EvenSpacing && !Json.Read<AppearancePreferences>("{}").EvenSpacing,"even spacing is optional and remains off for existing settings",result);
            for(int count=0;count<=8;count++) {
                var items=Enumerable.Range(0,8).Select(i=>i<count?new ActionItem("操作 "+i,"shortcut","F8"):PlanetStack.Empty()).ToList(); string before=Json.Write(items);
                PlanetMenu menu=new PlanetMenu(items,76,PlanetStack.DefaultGap,true); bool correct=menu.Targets.Count==count && menu.Hit(new Point(205,205))==-1;
                for(int i=0;i<count;i++) { PlanetTarget target=menu.Targets[i]; correct &= (target.Position-PlanetMenu.Polar(131,-Math.PI/2+i*2*Math.PI/count)).Length<.001 && menu.Hit(PlanetMenu.Polar(250,target.Angle))==target.Id && menu.IconHit(target.Position)==target.Id && menu.KeyboardTarget(i)==target.Id && menu.Number(target.Id)==i+1; }
                PlanetEditorLayout edit=new PlanetEditorLayout(items,new int[0],0,PlanetStack.DefaultGap,true);
                correct &= edit.Targets.Count==count+(count<8?1:0) && edit.Targets.Where(t=>t.Item.Kind!="none").All(t=>menu.Targets.Any(m=>m.Slot==t.Slot && (m.Position-t.Position).Length<.001)) && Json.Write(items)==before;
                Check(correct,"even layout keeps drawing selection keys and editor positions consistent with "+count+" actions",result);
            }
            var sparse=Enumerable.Range(0,8).Select(i=>PlanetStack.Empty()).ToList(); sparse[1]=new ActionItem("A","shortcut","F8"); sparse[4]=new ActionItem("B","shortcut","F9"); sparse[7]=PlanetStack.Create();
            PlanetMenu even=new PlanetMenu(sparse,76,20,true); Check(even.Targets.Select(t=>t.Slot).SequenceEqual(new[]{1,4,7}) && even.KeyboardTarget(1)==1 && even.Targets[1].Item==sparse[4],"uniform display preserves sparse source slots while keyboard selection follows displayed order",result);
            even.Open(2); Check(even.Layers.Last().Uniform && even.Targets.Count==0 && even.Back(),"empty circular stacks remain navigable with uniform layout enabled",result);
            PlanetMenu fixedMenu=new PlanetMenu(sparse,76); Check(fixedMenu.Targets.Count==8 && (fixedMenu.Targets[4].Position-RingSurface.Position(4)).Length<.001 && fixedMenu.KeyboardTarget(1)==1,"turning even spacing off restores original eight-slot geometry and keyboard mapping",result);
            config.Appearance.EvenSpacing=true; Check(config.Clone().Appearance.EvenSpacing,"uniform layout choice persists in backups and saved preferences",result);
            sparse[7].StackMode="arc"; sparse[7].Children=new List<ActionItem> { new ActionItem("Copy","shortcut","Ctrl+C"),new ActionItem("Paste","shortcut","Ctrl+V") };
            even=new PlanetMenu(sparse,76,20,true); even.Open(2); PlanetLayer arc=even.Layers.Last();
            Check(arc.Arc && !arc.Uniform && Math.Abs(PlanetMenu.InnerEdge(arc)-PlanetMenu.OuterEdge(even.Layers[0])-20)<.001 && arc.Targets.All(t=>even.Hit(t.Position)==t.Id),"uniform circular rings preserve arc stack spacing and hit selection",result);
            return result;
        }
    }
}

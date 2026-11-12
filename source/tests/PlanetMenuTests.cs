using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class PlanetMenuTests {
        static void Check(bool value,string label,List<string> result) { if(!value) throw new Exception(label); result.Add("PASS "+label); }
        static ActionItem Key(string label) { return new ActionItem(label,"shortcut","F6"); }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences prefs=Preferences.Defaults();
            ActionItem first=PlanetStack.Create(Key("先頭")); first.StackMode="arc"; first.Children[3]=new ActionItem("保持","hold","Ctrl"); first.Children[7]=new ActionItem("音量","volume","");
            ActionItem nested=PlanetStack.Create(Key("子の操作")); first.Children[4]=nested; prefs.Items[2]=first; prefs.Validate();
            PlanetMenu menu=new PlanetMenu(prefs.Items,76,50); Check(menu.Targets.Count==8 && !menu.CanGoBack,"planet starts with the original eight root slots",result);
            menu.Open(2); Check(menu.Layers.Count==2 && menu.Layers[1].Arc && Object.ReferenceEquals(menu.Targets[0].Item,prefs.Items[0]),"arc stack leaves the original ring visible",result);
            PlanetLayer arc=menu.Layers[1];
            Check(arc.Targets.Count==4 && arc.Targets.Select(t=>t.Slot).SequenceEqual(new[]{0,3,4,7}) && arc.Targets.All(t=>Math.Abs((t.Position-new Point(205,205)).Length-283.2)<.00001),"occupied child slots form a concentric arc next to the parent",result);
            Check(arc.Targets.All(t=>menu.Hit(t.Position)==t.Id && menu.IconHit(t.Position)==t.Id),"arc actions are selected by their current pointer position",result);
            RingSurface surface=new RingSurface(); Preferences shown=prefs.Clone(); shown.Items=menu.Targets.Select(t=>t.Item.Clone()).ToList(); surface.Bind(shown,menu); surface.StackTitle=menu.Title;
            foreach(PlanetTarget target in arc.Targets) surface.UpdatePointer(target.Position);
            Check(surface.Children.OfType<System.Windows.Controls.Button>().Count()==menu.Targets.Count && arc.Targets.All(t=>surface.IconSlot(t.Position)==t.Id && surface.IconNumber(t.Id)==t.Slot+1),"runtime surface renders and labels every arc action without root-index assumptions",result);
            Check(menu.Target(menu.KeyboardTarget(3)).Item==first.Children[3] && menu.KeyboardTarget(1)==-1,"arc keyboard selection preserves original slots and skips empty ones",result);
            Check(menu.Targets.All(a=>menu.Targets.All(b=>a==b || (a.Position-b.Position).Length>(a.Size+b.Size)/2+10)),"root and arc buttons do not overlap",result);
            Check(menu.Hit(new Point(205,205))==-1 && menu.Hit(PlanetMenu.Polar(600,0))==-1 && menu.Hit(PlanetMenu.Polar(241,Math.PI))==-1,"center outside and unused arc angles remain cancellation areas",result);
            PlanetTarget second=arc.Targets.Single(t=>t.Item==nested); menu.Open(second.Id);
            Check(menu.Layers.Count==1 && !menu.Layers[0].Arc && menu.Layers[0].Depth==2 && menu.Targets[0].Item==nested.Children[0],"level mode replaces the ring even when entered from an arc",result);
            menu.Back(); Check(menu.Layers.Count==2 && menu.Layers[1].Targets.Any(t=>t.Item==nested),"back restores the previous arc with its original parent ring",result);
            menu.Open(2); Check(!menu.CanGoBack && menu.Layers.Count==1,"clicking the expanded stack again collapses it",result);
            ActionItem sibling=PlanetStack.Create(Key("別のスタック")); sibling.StackMode="arc"; prefs.Items[4]=sibling; menu=new PlanetMenu(prefs.Items,76); menu.Open(2); menu.Open(4);
            Check(menu.Layers.Count==2 && menu.Layers[1].Targets[0].Item==sibling.Children[0],"opening another root stack replaces the previous arc",result);
            menu.Back(); Check(!menu.CanGoBack && menu.Targets.Count==8,"back from a replacement arc returns to the root once",result);
            foreach(double size in new[]{60.0,76.0,90.0}) {
                ActionItem full=PlanetStack.Create(); full.StackMode="arc"; for(int i=0;i<8;i++) full.Children[i]=Key("操作"+i); prefs.Items[0]=full;
                menu=new PlanetMenu(prefs.Items,size); menu.Open(0); arc=menu.Layers.Last();
                Check(arc.Targets.Count==8 && arc.Targets.All(a=>arc.Targets.All(b=>a==b || (a.Position-b.Position).Length>(a.Size+b.Size)/2+10)),"eight arc actions remain separate at button size "+size,result);
                Check(Enumerable.Range(0,8).All(i=>menu.Target(menu.KeyboardTarget(i)).Item==full.Children[i]),"eight arc actions keep their keyboard targets at size "+size,result);
            }
            prefs.Items[0]=PlanetStack.Create(); prefs.Items[0].StackMode="arc"; menu=new PlanetMenu(prefs.Items,76); menu.Open(0);
            Check(menu.CanGoBack && menu.Layers.Last().Targets.Count==0 && menu.KeyboardTarget(0)==-1 && menu.Back(),"empty arcs open and close without invoking an empty slot",result);
            ActionItem aStack=PlanetStack.Create(),bStack=PlanetStack.Create(),cStack=PlanetStack.Create(); aStack.StackMode=bStack.StackMode=cStack.StackMode="arc"; aStack.Children[0]=bStack; bStack.Children[0]=cStack; cStack.Children[7]=new ActionItem("拡大","wheel","Ctrl+Up"); prefs.Items[0]=aStack;
            menu=new PlanetMenu(prefs.Items,90,50); menu.Open(0); menu.Open(menu.KeyboardTarget(0)); menu.Open(menu.KeyboardTarget(0));
            int final=menu.KeyboardTarget(7); Check(menu.Layers.Count==4 && Math.Abs(menu.Target(final).Radius-591.5)<.00001 && menu.Hit(menu.Target(final).Position)==final && menu.Target(final).Item==cStack.Children[7],"three nested arcs retain actual actions and work beyond the old cancel radius",result);
            menu.Back(); menu.Back(); menu.Back(); Check(!menu.CanGoBack && menu.Targets.Count==8,"nested arcs return one level at a time",result);
            Preferences clone=prefs.Clone(); clone.Validate(); Check(clone.Items[0].StackMode=="arc" && clone.Items[0].Children[0].StackMode=="arc","per-stack display modes survive settings serialization",result);
            clone.Items[0].StackMode="unknown"; bool rejected=false; try { clone.Validate(); } catch { rejected=true; } Check(rejected,"unknown stack modes are rejected before saving",result);
            clone=prefs.Clone(); clone.Items[0].StackMode=null; clone.Items[0].Icon="ms:folder"; clone.Items[0].Children[0].StackMode=null; clone.Items[0].Children[0].Icon="ms:home"; PlanetStack.UpgradeLegacy(clone);
            Check(clone.Items[0].StackMode=="level" && clone.Items[0].Icon==null && clone.Items[0].Children[0].Icon=="ms:home","legacy default stack icons migrate while custom icons remain selected",result);
            return result;
        }
    }
}

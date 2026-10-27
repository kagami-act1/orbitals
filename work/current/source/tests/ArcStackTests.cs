using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class ArcStackTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults(); ActionItem stack=PlanetStack.Create(); stack.StackMode="arc"; config.Items[2]=stack;
            for(int count=1;count<=32;count++) {
                Check(PlanetStack.Add(stack,new ActionItem("操作"+count,"shortcut","F8")),"arc stack accepts action "+count,r);
                PlanetMenu menu=new PlanetMenu(config.Items,76); menu.Open(2); PlanetEditorLayout editor=new PlanetEditorLayout(config.Items,new int[0],2);
                Check(menu.Targets.Count(t=>t.Depth==1)==count && menu.Layers.Count==1+(count+7)/8 && editor.Targets.Count(t=>t.Depth==1 && t.Item.Kind=="none")== (count<32?1:0),"arc preview and runtime grow to "+count+" actions with one available add button",r);
                Check(menu.Targets.Where(t=>t.Depth==1).All(t=>menu.IconHit(t.Position)==t.Id && menu.Hit(PlanetMenu.Polar(t.Radius,t.Angle))==t.Id),"all arc rows select their own action at count "+count,r);
            }
            string full=Json.Write(stack); Check(!PlanetStack.Add(stack,new ActionItem("超過","shortcut","F8")) && Json.Write(stack)==full,"full arc stack rejects overflow without losing existing actions",r);
            config.Validate(); Preferences restored=config.Clone(); restored.Validate(); Check(restored.Items[2].Children.Count==32 && restored.Items[2].Children[31].Label=="操作32","all 32 arc actions survive settings round trips",r);
            PlanetMenu visible=new PlanetMenu(config.Items,90); visible.Open(2); Preferences shown=config.Clone(); shown.Items=visible.Targets.Select(t=>t.Item.Clone()).ToList(); RingSurface surface=new RingSurface(); surface.Bind(shown,visible); surface.UpdatePointer(visible.Targets.Last().Position);
            Check(surface.IconNumber(visible.Targets.Last().Id)==32 && surface.IconSlot(visible.Targets.Last().Position)==visible.Targets.Last().Id,"runtime rendering and pointer selection support the thirty-second arc action",r);
            stack.Children[9]=PlanetStack.Empty(); Check(PlanetStack.Add(stack,new ActionItem("空き枠","shortcut","F8")) && stack.Children.Count==32 && stack.Children[9].Label=="空き枠","arc additions reuse empty positions before appending",r);
            ActionItem nested=PlanetStack.Create(new ActionItem("孫","shortcut","F8")); nested.StackMode="arc"; stack.Children[31]=nested; PlanetMenu nestedMenu=new PlanetMenu(config.Items,76); nestedMenu.Open(2); nestedMenu.Open(nested);
            Check(nestedMenu.Layers.Last().Radius>nestedMenu.Layers.Where(l=>l.Depth==1).Max(l=>l.Radius) && nestedMenu.Targets.Any(t=>t.Depth==2 && t.Item.Label=="孫"),"nested arcs open beyond all visible parent rows",r);
            ActionItem level=PlanetStack.Create(); for(int i=0;i<8;i++) PlanetStack.Add(level,new ActionItem("項目"+i,"shortcut","F8")); Check(!PlanetStack.Add(level,new ActionItem("超過","shortcut","F8")) && level.Children.Count==8,"level stacks retain their eight-position layout",r);
            string path=Path.Combine(Path.GetTempPath(),"orbitals-arc-draft-"+Guid.NewGuid().ToString("N")+".json");
            try { WorkingCopy.Write(path,new WorkingCopyRecord { Version=1,Config=config,Page=0,Selected=31,StackPath=new[]{2} }); WorkingCopyRecord read=WorkingCopy.Read(path); Check(read.Selected==31 && read.Config.Items[2].Children.Count==32,"drafts preserve later arc rows and the selected action",r); }
            finally { WorkingCopy.Delete(path); if(File.Exists(path+".tmp")) File.Delete(path+".tmp"); }
            using(Controller owner=new Controller(true)) {
                Preferences sample=Preferences.Defaults(); ActionItem fullStack=PlanetStack.Create(); fullStack.StackMode="arc"; for(int i=0;i<8;i++) fullStack.Children[i]=new ActionItem("操作"+i,"shortcut","F8"); sample.Items[2]=fullStack; owner.Apply(sample);
                SettingsWindow editor=new SettingsWindow(owner); var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(SettingsWindow).GetMethod("SetEditorContext",flags).Invoke(editor,new object[]{new[]{2}});
                typeof(SettingsWindow).GetMethod("Assign",flags).Invoke(editor,new object[]{8,new ActionItem("追加9","shortcut","F8")});
                Preferences edited=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor);
                Check(edited.Items[2].Children.Count==9 && edited.Items[2].Children[8].Label=="追加9","the editor assigns an action to the virtual add slot beyond the initial eight",r);
                typeof(SettingsWindow).GetMethod("Undo",flags).Invoke(editor,null); edited=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor);
                Check(edited.Items[2].Children.Count==8,"undo removes an appended arc slot together with its action",r);
                typeof(SettingsWindow).GetMethod("Assign",flags).Invoke(editor,new object[]{8,new ActionItem("追加9","shortcut","F8")}); editor.Navigate(1); editor.Navigate(4);
                Check((int)typeof(SettingsWindow).GetField("selected",flags).GetValue(editor)==0,"leaving a later arc slot for gestures or applications resets the hidden ring selection",r);
                typeof(SettingsWindow).GetMethod("RestoreSaved",flags).Invoke(editor,null);
                editor.ReleaseResources(); editor.Close();
            }
            return r;
        }
    }
}

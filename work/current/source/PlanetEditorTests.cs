using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class PlanetEditorTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static ActionItem Key(string name) { return new ActionItem(name,"shortcut","F6"); }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults();
            ActionItem stack=PlanetStack.Create(Key("子の先頭")); stack.StackMode="arc"; stack.Children[3]=Key("子の中央"); stack.Children[7]=Key("子の末尾"); config.Items[2]=stack;
            string before=Json.Write(config); PlanetEditorLayout editor=new PlanetEditorLayout(config.Items,new int[0],2); PlanetMenu runtime=new PlanetMenu(config.Items,76); runtime.Open(2);
            Check(editor.Menu.Layers.Count==2 && editor.Menu.Layers[1].Arc && editor.Targets.Count(t=>t.Depth==0)==8,"selecting an arc stack previews its children beside the parent ring",r);
            Check(runtime.Targets.All(t=>editor.Targets.Any(e=>e.Depth==t.Depth && e.Slot==t.Slot && e.Position==t.Position && Object.ReferenceEquals(e.Item,t.Item))),"editor arc geometry and occupied action identities match the runtime ring",r);
            Check(editor.Targets.Count(t=>t.Depth==1 && t.Item.Kind=="none")==1 && editor.Targets.Single(t=>t.Depth==1 && t.Item.Kind=="none").Slot==1,"arc editor provides one available slot without showing every empty runtime slot",r);
            Check(editor.Targets.Where(t=>t.Depth==1).All(t=>t.Path.SequenceEqual(new[]{2})) && editor.Targets.Where(t=>t.Depth==0).All(t=>t.Path.Length==0),"every visible action addresses its actual parent stack",r);
            Check(editor.Targets.Count(editor.IsSelected)==1 && editor.Targets.Single(editor.IsSelected).Item==stack,"parent selection is independent of equal-numbered child slots",r);
            editor=new PlanetEditorLayout(config.Items,new[]{2},5);
            Check(editor.Targets.Single(t=>t.Depth==1 && t.Item.Kind=="none").Slot==5 && editor.Targets.Single(editor.IsSelected).Slot==5,"selected empty arc slot stays visible while awaiting an action",r);
            stack.StackMode="level"; editor=new PlanetEditorLayout(config.Items,new int[0],2);
            Check(editor.Menu.Layers.Count==1 && editor.Targets.Count==8,"level stack selection retains the original ring",r);
            editor=new PlanetEditorLayout(config.Items,new[]{2},0);
            Check(editor.Menu.Layers.Count==1 && !editor.Menu.Layers[0].Arc && editor.Targets.All(t=>t.Depth==1 && t.Path.SequenceEqual(new[]{2})),"entering a level stack replaces the displayed ring",r);
            stack.StackMode="arc"; ActionItem nested=PlanetStack.Create(Key("孫の操作")); nested.StackMode="arc"; stack.Children[3]=nested;
            editor=new PlanetEditorLayout(config.Items,new[]{2},3);
            Check(editor.Menu.Layers.Count==3 && editor.Targets.Where(t=>t.Depth==2).All(t=>t.Path.SequenceEqual(new[]{2,3})) && editor.Targets.Any(t=>t.Item==nested.Children[0]),"nested arc preview retains both parent layers and correct child addresses",r);
            nested.StackMode="level"; editor=new PlanetEditorLayout(config.Items,new[]{2,3},0);
            Check(editor.Menu.Layers.Count==1 && editor.Targets.All(t=>t.Depth==2 && t.Path.SequenceEqual(new[]{2,3})),"mixed arc and level stacks follow the same replacement rules in the editor",r);
            config.Items[2]=stack=PlanetStack.Create(Key("子の先頭")); stack.StackMode="arc"; stack.Children[3]=Key("子の中央");
            LayoutEditor surface=new LayoutEditor(); surface.Bind(config,2); int[] chosenPath=null; int chosenSlot=-1;
            surface.SelectionChanged=delegate(int[] path,int slot) { chosenPath=path; chosenSlot=slot; };
            Button child=surface.Children.OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="4 子の中央 · 配置を編集"); child.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(chosenSlot==3 && chosenPath.SequenceEqual(new[]{2}),"clicking an arc child selects that child instead of the root slot",r);
            Check(surface.Children.OfType<Button>().All(b=>Canvas.GetLeft(b)>=0 && Canvas.GetTop(b)>=0 && Canvas.GetLeft(b)+b.Width<=surface.Width && Canvas.GetTop(b)+b.Height<=surface.Height || Double.IsNaN(b.Width)),"expanded editor buttons remain inside the measured canvas",r);
            List<Button> actions=surface.Children.OfType<Button>().Where(b=>!Double.IsNaN(b.Width)).ToList(); Button firstChild=actions[8],nextChild=actions[9];
            Point firstCenter=new Point(Canvas.GetLeft(firstChild)+firstChild.Width/2,Canvas.GetTop(firstChild)+firstChild.Height/2),nextCenter=new Point(Canvas.GetLeft(nextChild)+nextChild.Width/2,Canvas.GetTop(nextChild)+nextChild.Height/2); Vector toward=nextCenter-firstCenter; toward.Normalize();
            Check(surface.DropSlot(firstCenter+toward*(firstChild.Width/2-1))==8,"drop selection follows the nearest visible button at adjacent arc edges",r);
            bool fits=true;
            for(int slot=0;slot<8;slot++) {
                Preferences sample=Preferences.Defaults(); ActionItem full=PlanetStack.Create(); full.StackMode="arc"; for(int i=0;i<8;i++) full.Children[i]=Key("操作"+i); sample.Items[slot]=full;
                PlanetEditorLayout fullLayout=new PlanetEditorLayout(sample.Items,new int[0],slot); surface.Bind(sample,slot);
                fits &= fullLayout.Targets.Count==17 && fullLayout.Targets.Count(t=>t.Depth==1 && t.Item.Kind=="none")==1;
                fits &= surface.Children.OfType<Button>().Where(b=>!Double.IsNaN(b.Width)).All(b=>Canvas.GetLeft(b)>=0 && Canvas.GetTop(b)>=0 && Canvas.GetLeft(b)+b.Width<=surface.Width && Canvas.GetTop(b)+b.Height<=surface.Height);
            }
            Check(fits,"full arc stacks in all eight directions retain their contents and fit the editor",r);
            Preferences untouched=Preferences.Defaults(); untouched.Items[2]=PlanetStack.Create(Key("先頭")); untouched.Items[2].StackMode="arc"; before=Json.Write(untouched); surface.Bind(untouched,2);
            Check(Json.Write(untouched)==before,"opening and rendering stack previews never changes saved actions",r);
            PlanetEditorViewport viewport=new PlanetEditorViewport(surface); StackPanel tools=viewport.Children.OfType<StackPanel>().Single();
            Button enlarge=tools.Children.OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="編集画面を拡大"),fit=tools.Children.OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="編集画面の全体を表示");
            enlarge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(viewport.Zoom>1 && Json.Write(untouched)==before,"editor magnification does not change runtime ring dimensions or actions",r);
            fit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(viewport.Zoom==1,"fit command restores the complete editor view",r);
            surface.Bind(Preferences.Defaults(),0);
            Button[] labels=surface.Children.OfType<Button>().Where(button=>button.Content is System.Windows.Controls.TextBlock).ToArray();
            Check(labels.Length==8 && labels.Any(button=>((System.Windows.Controls.TextBlock)button.Content).Text=="貼り付け") && labels.Any(button=>((System.Windows.Controls.TextBlock)button.Content).Text=="保存"),"all eight root labels remain visible including upper and lower right actions",r);
            Check(labels.All(button=>new Rect(0,0,surface.Width,surface.Height).Contains(new Rect(System.Windows.Controls.Canvas.GetLeft(button),System.Windows.Controls.Canvas.GetTop(button),button.DesiredSize.Width,button.DesiredSize.Height))),"labels moved to avoid collisions remain inside the editor canvas",r);
            PlanetEditorViewport testedViewport=viewport; testedViewport.Measure(new Size(520,380)); testedViewport.Arrange(new Rect(0,0,520,380)); testedViewport.UpdateLayout();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(PlanetEditorViewport).GetMethod("SetZoom",flags).Invoke(testedViewport,new object[]{1.05}); testedViewport.UpdateLayout();
            System.Windows.Controls.ScrollViewer scroll=(System.Windows.Controls.ScrollViewer)typeof(PlanetEditorViewport).GetField("scroll",flags).GetValue(testedViewport);
            Check(System.Windows.Controls.Grid.GetRow(scroll)==1 && testedViewport.RowDefinitions[0].ActualHeight>0 && scroll.TranslatePoint(new Point(),testedViewport).Y>=testedViewport.RowDefinitions[0].ActualHeight,"zoom controls occupy a separate row and cannot overlap the action editor",r);
            return r;
        }
    }
}

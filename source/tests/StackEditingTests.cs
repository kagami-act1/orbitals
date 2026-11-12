using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class StackEditingTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(SettingsWindow editor,string name) { return typeof(SettingsWindow).GetField(name,Private).GetValue(editor); }
        static void Call(SettingsWindow editor,string name,params object[] args) { typeof(SettingsWindow).GetMethod(name,Private).Invoke(editor,args); }
        static ActionItem Key(string name) { return new ActionItem(name,"shortcut","F8"); }
        static ActionItem Arc(int count) { ActionItem stack=PlanetStack.Create(); stack.StackMode="arc"; stack.Children=Enumerable.Range(0,count).Select(i=>Key("操作"+i)).ToList(); return stack; }
        static bool Near(double a,double b) { return Math.Abs(a-b)<.00001; }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        public static List<string> Run() {
            List<string> r=new List<string>();
            foreach(double size in new[]{60.0,76.0,90.0}) foreach(double gap in new[]{8.0,38.0,92.0,160.0}) {
                bool correct=true;
                for(int slot=0;slot<8;slot++) {
                    Preferences config=Preferences.Defaults(); ActionItem parent=Arc(16),child=Arc(8),grandchild=Arc(8); parent.Children[9]=child; child.Children[3]=grandchild; config.Items[slot]=parent;
                    PlanetMenu menu=new PlanetMenu(config.Items,size,gap); menu.Open(slot); menu.Open(child); menu.Open(grandchild);
                    for(int i=1;i<menu.Layers.Count;i++) {
                        PlanetLayer layer=menu.Layers[i],previous=menu.Layers[i-1];
                        correct &= Near(PlanetMenu.InnerEdge(layer)-PlanetMenu.OuterEdge(previous),gap);
                        for(int j=1;j<layer.Targets.Count;j++) correct &= Near((layer.Targets[j].Position-layer.Targets[j-1].Position).Length,layer.Size+12);
                    }
                    correct &= menu.Targets.Where(t=>t.Depth>0).All(t=>menu.Hit(t.Position)==t.Id);
                }
                Check(correct,"nested arc gaps and adjacent button distances remain consistent at size "+size+" and gap "+gap,r);
            }
            Preferences sample=Preferences.Defaults(); sample.Items[2]=Arc(8); sample.Appearance.StackGap=94;
            PlanetEditorLayout preview=new PlanetEditorLayout(sample.Items,new int[0],2,sample.Appearance.StackGap); PlanetMenu runtime=new PlanetMenu(sample.Items,76,sample.Appearance.StackGap); runtime.Open(2);
            Check(runtime.Targets.All(t=>preview.Targets.Any(e=>e.Item==t.Item && e.Position==t.Position)),"custom stack distances match between editor and runtime",r);
            EditorTarget add=preview.Targets.Single(t=>t.Depth==1 && t.Item.Kind=="none"); PlanetLayer last=runtime.Layers.Last();
            Check(Near(add.Radius-last.Radius-last.Size-20,94),"the next editor add row uses the selected stack gap",r);
            Preferences copy=sample.Clone(); copy.Validate();
            Check(copy.Appearance.StackGap==94 && new AppearancePreferences().StackGap==20,"shared stack gaps survive serialization with the current physical default",r);
            foreach(double invalid in new[]{7.0,161.0,Double.NaN,Double.PositiveInfinity,Double.NegativeInfinity}) {
                copy=sample.Clone(); copy.Appearance.StackGap=invalid;
                Check(Reject(copy.Validate),"invalid stack distance is rejected: "+invalid,r);
            }
            string draftPath=Path.Combine(Path.GetTempPath(),"orbitals-stack-edit-"+Guid.NewGuid().ToString("N")+".json");
            try {
                WorkingCopyRecord record=new WorkingCopyRecord { Version=1,Config=sample,StackPath=new int[0] }; WorkingCopy.Write(draftPath,record);
                Check(WorkingCopy.Read(draftPath).Config.Appearance.StackGap==94,"draft recovery retains the shared stack distance",r);
                string before=File.ReadAllText(draftPath); record.Config.Appearance.StackGap=1000;
                Check(Reject(delegate { WorkingCopy.Write(draftPath,record); }) && File.ReadAllText(draftPath)==before,"invalid distances cannot overwrite a valid recovery file",r);
                sample.Appearance.StackGap=94;
            } finally { WorkingCopy.Delete(draftPath); if(File.Exists(draftPath+".tmp")) File.Delete(draftPath+".tmp"); }
            List<ActionItem> circular=Preferences.Defaults().Items; ActionItem next=circular[3];
            Check(PlanetStack.Erase(circular,2,false) && circular.Count==8 && circular[2].Kind=="none" && circular[3]==next,"erasing a circular action preserves every other slot",r);
            ActionItem level=PlanetStack.Create(Key("先頭")); level.Children[1]=Key("次"); next=level.Children[1]; PlanetStack.Erase(level.Children,0,false);
            Check(level.Children.Count==8 && level.Children[0].Kind=="none" && level.Children[1]==next,"erasing a level stack child leaves its circular position empty",r);
            ActionItem arc=Arc(3); next=arc.Children[2]; PlanetStack.Erase(arc.Children,1,true);
            Check(arc.Children.Count==2 && arc.Children[1]==next,"erasing an arc child closes that position without losing the next action",r);
            PlanetStack.Erase(arc.Children,1,true); PlanetStack.Erase(arc.Children,0,true);
            Check(PlanetStack.ValidChildren(arc) && arc.Children.Count==1 && arc.Children[0].Kind=="none" && PlanetStack.Add(arc,Key("再追加")),"erasing the final arc child leaves a valid reusable stack",r);
            string unchanged=Json.Write(arc); Check(!PlanetStack.Erase(arc.Children,-1,true) && !PlanetStack.Erase(arc.Children,3,true) && Json.Write(arc)==unchanged,"invalid erase requests leave existing actions intact",r);
            using(Controller owner=new Controller(true)) {
                sample=Preferences.Defaults(); sample.Items[2]=Arc(3); sample.Items[2].Children[1]=Arc(2); owner.Apply(sample);
                SettingsWindow editor=new SettingsWindow(owner);
                Call(editor,"SetEditorContext",new[]{2}); typeof(SettingsWindow).GetField("selected",Private).SetValue(editor,1); Call(editor,"RefreshAll");
                editor.Navigate(3); Call(editor,"SelectSettingsCategory",1); Slider gap=Descendants<Slider>((DependencyObject)editor.Content).Single(s=>System.Windows.Automation.AutomationProperties.GetName(s)=="弧状スタックの間隔"); gap.Value=260;
                Preferences edited=(Preferences)Field(editor,"draft");
                Check(edited.Appearance.StackGap==104 && !Json.Write(edited.Items).Contains("StackGap"),"the global distance slider stores one value without per-action settings",r);
                Call(editor,"Undo"); edited=(Preferences)Field(editor,"draft"); Check(edited.Appearance.StackGap==20,"undo restores the previous shared stack distance",r);
                Call(editor,"Redo"); edited=(Preferences)Field(editor,"draft"); Check(edited.Appearance.StackGap==104,"redo reapplies the shared stack distance",r);
                editor.Navigate(0);
                Call(editor,"SetEditorContext",new[]{2,1}); typeof(SettingsWindow).GetField("selected",Private).SetValue(editor,0); Call(editor,"RefreshAll");
                string before=Json.Write(edited); LayoutEditor surface=(LayoutEditor)Field(editor,"surface");
                Button rootStack=surface.Children.OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="3 スタック · 配置を編集");
                Check(surface.Children.OfType<Button>().Where(b=>b.ToolTip as string!="未割り当て").All(b=>b.ContextMenu!=null && ((MenuItem)b.ContextMenu.Items[0]).Header as string=="消去"),"all placed editor icons and visible labels expose the erase menu",r);
                ((MenuItem)rootStack.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); edited=(Preferences)Field(editor,"draft");
                Check(edited.Items[2].Kind=="none" && edited.Items.Count==8 && ((List<int>)Field(editor,"stackPath")).Count==0,"erasing an expanded ancestor clears its slot and exits the removed subtree",r);
                Call(editor,"Undo"); edited=(Preferences)Field(editor,"draft");
                Check(Json.Write(edited)==before && ((List<int>)Field(editor,"stackPath")).SequenceEqual(new[]{2,1}),"undo restores an erased stack with all descendants and the previous editor context",r);
                Call(editor,"Redo"); edited=(Preferences)Field(editor,"draft"); Check(edited.Items[2].Kind=="none","redo removes the restored subtree in one edit",r);
                Call(editor,"Undo"); Call(editor,"Erase",new[]{2,1},0); edited=(Preferences)Field(editor,"draft");
                Check(edited.Items[2].Children[1].Children.Count==1 && edited.Items[2].Children[1].Children[0].Label=="操作1","the editor compacts the correct nested arc after erasing a child",r);
                Call(editor,"Undo"); edited=(Preferences)Field(editor,"draft"); Check(Json.Write(edited)==before,"undo restores a compacted nested arc without changing siblings",r);
                Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close();
            }
            return r;
        }
        static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject {
            if(parent is T) yield return (T)parent;
            foreach(object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject element=child as DependencyObject; if(element!=null) foreach(T descendant in Descendants<T>(element)) yield return descendant; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class StackMoveTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string text,List<string> result) { if(!value) throw new Exception(text); result.Add("PASS "+text); }
        static ActionItem Action(int number) { return new ActionItem("操作"+number,"shortcut","F"+(number%10+1)); }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences config=Preferences.Defaults();
            config.Items[1]=PlanetStack.Create(); config.Items[1].Children=Enumerable.Range(0,8).Select(Action).ToList(); string saved=Json.Write(config);
            Check(!EditorMove.Plan(config.Items,new int[0],0,new int[0],1).Allowed && Json.Write(config)==saved,"a full level stack rejects incoming moves without losing the source or existing children",result);
            config.Items[1].StackMode="arc"; config.Items[1].Children=Enumerable.Range(0,32).Select(Action).ToList(); saved=Json.Write(config);
            Check(!EditorMove.Plan(config.Items,new int[0],0,new int[0],1).Allowed && Json.Write(config)==saved,"a full 32-item arc rejects incoming moves without changing either side",result);
            config=Preferences.Defaults(); config.Items[1]=PlanetStack.Create(); config.Items[2]=PlanetStack.Create(PlanetStack.Create(Action(1))); ActionItem nested=config.Items[2]; EditorMove plan=EditorMove.Plan(config.Items,new int[0],2,new int[0],1); Check(plan.Allowed,"a nested stack can move into another stack when the resulting depth fits",result); plan.Apply(); config.Validate();
            Check(config.Items[2].Kind=="none" && Object.ReferenceEquals(config.Items[1].Children[0],nested) && config.Items[1].Children[0].Children[0].Kind=="stack","moving a stack preserves its complete nested contents and the ring's empty position",result);
            config=Preferences.Defaults(); config.Items[1]=PlanetStack.Create(); config.Items[2]=PlanetStack.Create(PlanetStack.Create(PlanetStack.Create(Action(1)))); config.Validate(); saved=Json.Write(config);
            Check(!EditorMove.Plan(config.Items,new int[0],2,new int[0],1).Allowed && Json.Write(config)==saved,"moving an existing subtree cannot exceed the three-level stack limit",result);
            config=Preferences.Defaults(); config.Items[1]=PlanetStack.Create(); config.Items[1].StackMode="arc"; config.Items[1].Children=Enumerable.Range(0,5).Select(Action).ToList(); plan=EditorMove.Plan(config.Items,new int[0],2,new[]{1},5); plan.Apply(); config.Validate();
            Check(config.Items[1].Children.Count==6 && config.Items[1].Children[5].Label=="元に戻す" && config.Items[2].Kind=="none","the arc's virtual add slot accepts a moved action from the ring",result);
            config=Preferences.Defaults(); config.Items[1]=PlanetStack.Create(Action(2)); config.Items[0]=new ActionItem("マイク","toggle","") { Toggle=ToggleDefinition.CreateMicrophoneMute() }; string toggle=Json.Write(config.Items[0]); plan=EditorMove.Plan(config.Items,new int[0],0,new[]{1},0); plan.Apply(); config.Validate();
            Check(config.Items[0].Label=="操作2" && Json.Write(config.Items[1].Children[0])==toggle,"same-numbered slots in different parents can exchange a toggle without changing its identity",result);
            Check(!EditorMove.Plan(config.Items,new[]{1},0,new int[0],1).Allowed,"dropping a child back onto its own immediate parent is a harmless no-op",result);
            Check(!EditorMove.Plan(config.Items,new[]{9},0,new int[0],0).Allowed && !EditorMove.Plan(config.Items,new int[0],0,new[]{1},50).Allowed,"stale or invalid source and target paths are rejected",result);
            using(Controller owner=new Controller(true)) {
                SettingsWindow editor=new SettingsWindow(owner); editor.Navigate(3); TextBox key=(TextBox)typeof(SettingsWindow).GetField("hotkey",Private).GetValue(editor); string original=owner.Config.Hotkey;
                typeof(SettingsWindow).GetMethod("SelectInvocationKey",Private).Invoke(editor,new object[]{key,"Pause"}); Preferences draft=(Preferences)typeof(SettingsWindow).GetField("draft",Private).GetValue(editor);
                Check(key.IsReadOnly && key.Text=="Pause" && draft.Hotkey=="Pause" && owner.Config.Hotkey==original,"selecting a missing keyboard key updates only the draft and keeps manual text entry disabled",result);
                typeof(SettingsWindow).GetMethod("Undo",Private).Invoke(editor,null); draft=(Preferences)typeof(SettingsWindow).GetField("draft",Private).GetValue(editor); Check(draft.Hotkey==original,"choosing Pause from the key list can be undone",result);
                typeof(SettingsWindow).GetMethod("RestoreSaved",Private).Invoke(editor,null); editor.ReleaseResources(); editor.Close();
            }
            return result;
        }
    }
}

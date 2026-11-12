using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class EditorMoveTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(object target,string name) { return target.GetType().GetField(name,Private).GetValue(target); }
        static object Call(object target,string name,params object[] args) { return target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        static Point Position(LayoutEditor surface,EditorTarget item) { return (Point)Call(surface,"Project",item.Position); }
        static EditorTarget Target(LayoutEditor surface,int[] path,int slot) { return ((PlanetEditorLayout)Field(surface,"layout")).Targets.Single(t=>t.Slot==slot && t.Path.SequenceEqual(path)); }
        static void Begin(LayoutEditor surface,EditorTarget item) {
            PlanetEditorLayout layout=(PlanetEditorLayout)Field(surface,"layout");
            Button button=((List<Button>)Field(surface,"buttons"))[layout.Targets.IndexOf(item)];
            Call(surface,"BeginMove",button,item,Position(surface,item));
        }
        static void Drag(LayoutEditor surface,int[] path,int from,int to) {
            EditorTarget source=Target(surface,path,from),destination=Target(surface,path,to); Begin(surface,source);
            Call(surface,"UpdateMove",Position(surface,destination),true); Call(surface,"FinishMove",Position(surface,destination));
        }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences config=Preferences.Defaults();
            config.Items[2]=new ActionItem("マイク","toggle","") { Toggle=ToggleDefinition.CreateMicrophoneMute() }; config.Items[5]=PlanetStack.Empty();
            ActionItem stack=PlanetStack.Create(new ActionItem("子A","shortcut","F6")); stack.StackMode="arc";
            ActionItem nested=PlanetStack.Create(new ActionItem("孫A","shortcut","F7")); nested.StackMode="arc"; nested.Children[1]=new ActionItem("孫B","shortcut","F8"); stack.Children[1]=nested; config.Items[3]=stack;
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    LayoutEditor surface=(LayoutEditor)Field(editor,"surface"); string original=Json.Write(owner.Config),microphone=Json.Write(config.Items[2]);
                    Drag(surface,new int[0],2,5); Preferences draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items[2].Kind=="none" && Json.Write(draft.Items[5])==microphone && draft.Items.Count==8,"dragging a microphone toggle to an empty root slot preserves every toggle state and leaves the source empty",result);
                    Check(Json.Write(owner.Config)==original && ((List<EditSnapshot>)Field(editor,"history")).Count==1,"a drag changes only the draft and creates exactly one undo entry",result);
                    string moved=Json.Write(draft); Call(surface,"FinishMove",surface.Position(5));
                    Check(Json.Write(draft)==moved && ((List<EditSnapshot>)Field(editor,"history")).Count==1,"a repeated mouse release cannot apply a second swap",result);
                    Call(editor,"Undo"); Check(Json.Write((Preferences)Field(editor,"draft"))==original,"undo restores a drag without losing microphone state icons",result);
                    Call(editor,"Redo"); Check(Json.Write((Preferences)Field(editor,"draft"))==moved,"redo reapplies a drag in one step",result);
                    EditorTarget source=Target(surface,new int[0],5); Begin(surface,source); Call(surface,"UpdateMove",new Point(-50,-50),true); Call(surface,"FinishMove",new Point(-50,-50));
                    Check(Json.Write((Preferences)Field(editor,"draft"))==moved,"releasing outside the editor cancels the move",result);
                    Begin(surface,source); Call(surface,"UpdateMove",surface.Position(2),true); Call(surface,"CancelMove",true); Call(surface,"FinishMove",surface.Position(2));
                    Check(Json.Write((Preferences)Field(editor,"draft"))==moved,"escape or lost capture cancels a pending move",result);
                    Begin(surface,source); Call(surface,"FinishMove",Position(surface,source)+new Vector(2,2));
                    Check(Json.Write((Preferences)Field(editor,"draft"))==moved,"a click or small pointer motion never rearranges an action",result);
                    Call(surface,"UpdateMove",surface.Position(2),true); Call(surface,"FinishMove",surface.Position(2));
                    Check(Json.Write((Preferences)Field(editor,"draft"))==moved,"entering a button with the mouse held cannot start a move without a press on that item",result);
                    Begin(surface,source); Call(surface,"FinishMove",surface.Position(2));
                    Check(Json.Write(((Preferences)Field(editor,"draft")).Items[2])==microphone,"a fast drag commits using the release position even without an intermediate mouse move",result);
                    Button label=surface.Children.OfType<Button>().Single(b=>b.Tag is EditorTarget && ((EditorTarget)b.Tag).Depth==0 && ((EditorTarget)b.Tag).Slot==5);
                    Point labelCenter=new Point(Canvas.GetLeft(label)+label.DesiredSize.Width/2,Canvas.GetTop(label)+label.DesiredSize.Height/2);
                    Check(((PlanetEditorLayout)Field(surface,"layout")).Targets[surface.DropSlot(labelCenter)].Slot==5,"visible item names are valid drop targets",result);
                    Call(editor,"RestoreSaved"); Call(editor,"SetEditorContext",new[]{3}); Call(editor,"RefreshAll");
                    Drag(surface,new[]{3},0,2); draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items[3].Children[0].Kind=="none" && draft.Items[3].Children[2].Label=="子A" && draft.Items[3].Children[1].Kind=="stack","moving inside an arc changes the addressed child while preserving its nested stack",result);
                    Call(editor,"SetEditorContext",new[]{3,1}); Call(editor,"RefreshAll"); Drag(surface,new[]{3,1},0,1); draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items[3].Children[1].Children[0].Label=="孫B" && draft.Items[3].Children[1].Children[1].Label=="孫A","nested arc actions swap within their own parent",result);
                    string before=Json.Write(draft); Begin(surface,Target(surface,new int[0],2)); Call(surface,"UpdateMove",Position(surface,Target(surface,new[]{3,1},0)),true); Call(surface,"FinishMove",Position(surface,Target(surface,new[]{3,1},0)));
                    draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items[2].Label=="孫B" && Json.Write(draft.Items[3].Children[1].Children[0])==microphone,"a drag exchanges occupied slots across visible stack levels without losing either action",result);
                    Call(editor,"Undo"); Check(Json.Write((Preferences)Field(editor,"draft"))==before,"a cross-level exchange is restored by a single undo",result);
                    Call(editor,"RestoreSaved"); Call(editor,"SetEditorContext",new[]{3}); Call(editor,"RefreshAll");
                    source=Target(surface,new int[0],0); EditorTarget empty=Target(surface,new[]{3},2); Begin(surface,source); Call(surface,"FinishMove",Position(surface,empty)); draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items.Count==8 && draft.Items[0].Kind=="none" && draft.Items[3].Children[2].Label=="コピー" && draft.Items[3].Children[0].Label=="子A","root actions move directly into an expanded stack's empty slot while the root retains its empty position",result);
                    Call(editor,"Undo"); Check(Json.Write((Preferences)Field(editor,"draft"))==original,"undo restores a root-to-stack move and the stack contents",result);
                    Call(editor,"RestoreSaved"); Drag(surface,new int[0],0,3); draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items[0].Kind=="none" && draft.Items[3].Kind=="stack" && draft.Items[3].Children[2].Label=="コピー","dropping onto the stack itself appends the action rather than replacing the stack",result);
                    Call(editor,"RestoreSaved"); Call(editor,"SetEditorContext",new[]{3}); Call(editor,"RefreshAll"); source=Target(surface,new[]{3},0); empty=Target(surface,new int[0],5); Begin(surface,source); Call(surface,"FinishMove",Position(surface,empty)); draft=(Preferences)Field(editor,"draft");
                    Check(draft.Items[5].Label=="子A" && draft.Items[3].Children[0].Kind=="none","actions can also move out of an expanded stack into the ring",result);
                    Call(editor,"RestoreSaved"); Call(editor,"SetEditorContext",new[]{3}); Call(editor,"RefreshAll"); string unchanged=Json.Write(Field(editor,"draft")); int checkpoints=((List<EditSnapshot>)Field(editor,"history")).Count;
                    source=Target(surface,new int[0],3); EditorTarget descendant=Target(surface,new[]{3},1); Begin(surface,source); Call(surface,"UpdateMove",Position(surface,descendant),true); Call(surface,"FinishMove",Position(surface,descendant));
                    Check(Json.Write(Field(editor,"draft"))==unchanged && ((List<EditSnapshot>)Field(editor,"history")).Count==checkpoints,"a stack cannot move into its own descendant or create an undo record for a rejected drop",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

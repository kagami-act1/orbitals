using System;
using System.Collections.Generic;

namespace OrbitRing {
    static class ToggleTests {
        sealed class Input : IActionInput {
            public bool Fail;
            public bool IsDown(ushort key) { return false; }
            public void Send(ushort key,bool up) { if(Fail && !up) throw new Exception("failed"); }
        }
        static void Check(bool value,string name,List<string> results) { if(!value) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static ActionItem Example() {
            ActionItem item=new ActionItem("切替","toggle","") { Icon="ms:toggle_off", Toggle=ToggleDefinition.Create() };
            item.Toggle.Stages[0].Value="Ctrl+1"; item.Toggle.Stages[1].Value="Ctrl+2"; return item;
        }
        public static List<string> Run() {
            List<string> results=new List<string>(); ActionItem item=Example(); Preferences config=Preferences.Defaults(); config.Items[0]=item; config.Validate();
            Preferences restored=config.Clone(); restored.Validate();
            Check(Json.Write(restored)==Json.Write(config),"toggle complete definition JSON round trip",results);
            ActionItem cloned=item.Clone(); cloned.Toggle.Stages[0].Label="changed";
            Check(item.Toggle.Stages[0].Label=="ON","toggle clone isolates nested stage edits",results);
            ToggleState state=new ToggleState(); Action done; ActionItem action=state.Prepare(item,out done);
            Check(state.Display(item).Label.EndsWith("OFF") && state.Display(item).Icon=="ms:toggle_off" && action.Value=="Ctrl+1","toggle initial OFF display and first operation",results);
            done(); done();
            Check(state.Display(item).Label.EndsWith("ON") && state.Display(item).Icon=="ms:toggle_on" && state.Prepare(item,out done).Value=="Ctrl+2","toggle advances once and shows resulting ON icon",results);
            done(); Check(state.Prepare(item,out done).Value=="Ctrl+1" && state.Display(item).Label.EndsWith("OFF"),"two stage toggle wraps to first operation",results);
            item.Toggle.Stages.Add(new ToggleStage { Label="第三",Kind="macro",Value="A\nwait:20\nB",Icon="ms:star" }); config.Validate();
            Check(state.Display(item).Label.EndsWith("OFF") && state.Prepare(item,out done).Value=="Ctrl+1","editing a definition resets sequence",results);
            done(); state.Prepare(item,out done); done(); action=state.Prepare(item,out done);
            Check(action.Kind=="macro" && action.Value=="A\nwait:20\nB","third stage resolves its own macro",results);
            done(); Check(state.Display(item).Icon=="ms:star" && state.Prepare(item,out done).Value=="Ctrl+1","three stage toggle wraps and displays its own icon",results);
            done(); config.Swap(0,4); state.Retain(config);
            Check(state.Display(config.Items[4]).Label.EndsWith("ON"),"moving action preserves toggle state",results);
            ActionItem second=Example(); config.Items[0]=second; state.Retain(config);
            Check(state.Display(second).Label.EndsWith("OFF") && state.Display(item).Label.EndsWith("ON"),"different toggle actions have independent counters",results);
            state.Prepare(item,out done); state.Reset(); done();
            Check(state.Display(item).Label.EndsWith("OFF"),"reset invalidates pending completion",results);
            state.Prepare(item,out done); config.Items[4]=Preferences.Defaults().Items[4]; state.Retain(config); done();
            Check(state.Display(item).Label.EndsWith("OFF"),"removing action invalidates pending completion",results);
            state.Prepare(item,out done); item.Toggle.Stages[0].Label="new"; state.Retain(new Preferences { Items=new List<ActionItem>{item} }); done();
            Check(state.Display(item).Label.EndsWith("OFF"),"stale completion cannot advance edited stages",results);
            Input input=new Input(); ActionPlayback player=new ActionPlayback(input); DateTime now=DateTime.UtcNow;
            state.Prepare(item,out done); input.Fail=true; Check(Reject(delegate { player.Press("A"); done(); }) && state.Display(item).Label.EndsWith("OFF"),"failed key send does not advance toggle",results); input.Fail=false;
            player.StartMacro("A\nwait:100\nB",now); player.Tick(now,true,false); player.Tick(now.AddMilliseconds(30),true,false); player.Tick(now.AddMilliseconds(130),false,false);
            Check(!player.Completed && !player.Active,"focus cancellation is not macro completion",results);
            player.StartMacro("wait:100\nB",now); player.Tick(now,true,true);
            Check(!player.Completed,"escape cancellation is not macro completion",results);
            player.StartMacro("A",now); input.Fail=true; Check(Reject(delegate { player.Tick(now,true,false); }) && !player.Completed,"failed macro is not marked successful",results); input.Fail=false;
            player.StartMacro("A\nwait:100\nB",now); player.Tick(now,true,false); player.Tick(now.AddMilliseconds(30),true,false); player.Tick(now.AddMilliseconds(130),true,false); player.Tick(now.AddMilliseconds(160),true,false);
            if(player.Completed) done();
            Check(player.Completed && state.Display(item).Label.EndsWith("new"),"fully completed macro commits next toggle state",results);
            player.Stop(); Check(!player.Completed,"cleanup clears macro success before future execution",results);
            config=Preferences.Defaults(); config.Items[0]=Example(); config.Items[1]=config.Items[0].Clone();
            Check(Reject(delegate { config.Validate(); }),"duplicate toggle IDs rejected",results);
            config.Items[1]=Example(); config.Items[0].Toggle.Stages[0].Value=config.Hotkey;
            Check(Reject(delegate { config.Validate(); }),"toggle shortcut cannot recursively invoke ring",results);
            config.Items[0].Toggle.Stages[0].Kind="macro";
            Check(Reject(delegate { config.Validate(); }),"toggle macro cannot recursively invoke ring",results);
            config.Items[0]=Example(); config.Items[0].Toggle.Stages[0].Kind="toggle";
            Check(Reject(delegate { config.Validate(); }),"nested toggles are rejected",results);
            config.Items[0]=Example(); config.Items[0].Toggle.Stages[0].Icon="ms:nonexistent_icon";
            Check(Reject(delegate { config.Validate(); }),"invalid stage icons rejected",results);
            config.Items[0]=Example(); config.Items[0].Toggle.Stages.RemoveAt(0);
            Check(Reject(delegate { config.Validate(); }),"single stage toggle rejected",results);
            config.Items[0]=Example(); for(int i=2;i<16;i++) config.Items[0].Toggle.Stages.Add(new ToggleStage { Label="状態",Kind="shortcut",Value="A" }); config.Validate();
            config.Items[0].Toggle.Stages.Add(new ToggleStage { Label="状態",Kind="shortcut",Value="A" });
            Check(Reject(delegate { config.Validate(); }),"stage limit allows sixteen and rejects seventeen",results);
            config.Items[0]=Example(); config.Items[0].Toggle.Stages[0].Kind="open"; config.Items[0].Toggle.Stages[0].Value="javascript:alert(1)";
            Check(Reject(delegate { config.Validate(); }),"toggle file stages use safe target validation",results);
            Check(new ToggleState().Display(item).Label.EndsWith("OFF"),"process restart begins at initial state",results);
            item=Example(); item.Toggle.Stages[0].Icon=null; item.Toggle.Stages[0].Value="Ctrl+C";
            state.Prepare(item,out done); done(); ActionItem display=state.Display(item);
            Check(Ui.Glyph(display)==Ui.Glyph(item.Toggle.Stages[0].Action()) && display.Kind=="toggle","automatic stage icon follows operation without changing toggle behavior",results);
            Check(ActionIcons.EffectiveKey(display)=="ms:content_copy","automatic toggle state uses its current operation's material icon",results);
            Check(!Json.Write(display).Contains("AutomaticGlyph") && !Json.Write(display).Contains("AutomaticIcon"),"runtime display glyph is excluded from persisted configuration",results);
            return results;
        }
    }
}

using System;
using System.Collections.Generic;

namespace OrbitRing {
    public sealed class ToggleStage {
        public string Label { get; set; }
        public string Kind { get; set; }
        public string Value { get; set; }
        string icon;
        public string Icon { get { return icon; } set { icon=CustomIcons.Share(value); } }
        public ActionItem Action() { return new ActionItem(Label,Kind,Value) { Icon=Icon }; }
    }
    public sealed class ToggleDefinition {
        public string Id { get; set; }
        public string InitialLabel { get; set; }
        public List<ToggleStage> Stages { get; set; }
        public static ToggleDefinition Create() {
            return new ToggleDefinition { Id=Guid.NewGuid().ToString("N"), InitialLabel="OFF", Stages=new List<ToggleStage> {
                new ToggleStage { Label="ON", Kind="shortcut", Value="", Icon="ms:toggle_on" },
                new ToggleStage { Label="OFF", Kind="shortcut", Value="", Icon="ms:toggle_off" }
            }};
        }
        public static ToggleDefinition CreateMute() {
            ToggleDefinition toggle=Create(); toggle.InitialLabel="ミュートOFF";
            toggle.Stages[0]=new ToggleStage { Label="ミュートON",Kind="mute",Value="on",Icon="ms:volume_off" };
            toggle.Stages[1]=new ToggleStage { Label="ミュートOFF",Kind="mute",Value="off",Icon="ms:volume_up" };
            return toggle;
        }
        public static ToggleDefinition CreateMicrophoneMute() {
            ToggleDefinition toggle=CreateMute();
            foreach(ToggleStage stage in toggle.Stages) { stage.Kind="microphone"; stage.Icon=stage.Value=="on"?"ms:mic_off":"ms:mic"; }
            return toggle;
        }
        [System.Web.Script.Serialization.ScriptIgnore] public bool IsSystemMute {
            get { return MuteKind=="mute"; }
        }
        [System.Web.Script.Serialization.ScriptIgnore] public string MuteKind {
            get { return Stages!=null && Stages.Count==2 && Stages[0]!=null && Stages[1]!=null && (Stages[0].Kind=="mute" || Stages[0].Kind=="microphone") && Stages[1].Kind==Stages[0].Kind &&
                ((Stages[0].Value=="on" && Stages[1].Value=="off") || (Stages[0].Value=="off" && Stages[1].Value=="on"))?Stages[0].Kind:null; }
        }
        public void Validate(Chord trigger) {
            Guid id;
            if(!Guid.TryParseExact(Id,"N",out id)) throw new Exception("トグルの識別子が不正です。");
            if(String.IsNullOrWhiteSpace(InitialLabel) || InitialLabel.Length>20) throw new Exception("初期状態名は1〜20文字で指定してください。");
            if(Stages==null || Stages.Count<2 || Stages.Count>16) throw new Exception("トグルは2〜16段階で指定してください。");
            for(int i=0;i<Stages.Count;i++) {
                ToggleStage stage=Stages[i];
                if(stage==null || stage.Kind=="toggle" || stage.Kind=="hold" || stage.Kind=="stack" || stage.Kind=="none")
                    throw new Exception((i+1)+"回目の操作の種類が不正です。");
                try { Preferences.ValidateAction(stage.Action(),trigger); }
                catch(Exception ex) { throw new Exception((i+1)+"回目: "+ex.Message); }
            }
        }
    }
    // State belongs to an action identity, so moving its slot does not reset it.
    // Definition changes and process restarts return it to the initial display.
    sealed class ToggleState {
        sealed class State { public string Signature; public int Last=-1; public int Revision; }
        readonly Dictionary<string,State> states=new Dictionary<string,State>();
        State Get(ActionItem item) {
            string signature=Json.Write(item.Toggle); State state;
            if(!states.TryGetValue(item.Toggle.Id,out state) || state.Signature!=signature) {
                state=new State { Signature=signature }; states[item.Toggle.Id]=state;
            }
            return state;
        }
        public ActionItem Display(ActionItem item) {
            if(item.Kind!="toggle") return item.Clone();
            State state=Get(item); ActionItem display=item.Clone();
            ToggleStage current=state.Last<0?null:item.Toggle.Stages[state.Last];
            display.Icon=current==null?item.Icon:current.Icon;
            if(current!=null && String.IsNullOrEmpty(current.Icon)) { display.AutomaticGlyph=Ui.Glyph(current.Action()); display.AutomaticIcon=ActionIcons.DefaultKey(current.Action()); }
            display.Label=item.Label+" · "+(current==null?item.Toggle.InitialLabel:current.Label);
            int next=(state.Last+1)%item.Toggle.Stages.Count;
            display.Value="次: "+(next+1)+"回目 · "+item.Toggle.Stages[next].Label;
            return display;
        }
        public ActionItem Prepare(ActionItem item,out Action complete) {
            if(item.Kind!="toggle") { complete=null; return item; }
            State state=Get(item); int revision=state.Revision;
            int next=(state.Last+1)%item.Toggle.Stages.Count;
            complete=delegate {
                State current;
                if(states.TryGetValue(item.Toggle.Id,out current) && Object.ReferenceEquals(current,state) && state.Revision==revision) {
                    state.Last=next; state.Revision++;
                }
            };
            return item.Toggle.Stages[next].Action();
        }
        public void Retain(Preferences config) {
            HashSet<string> ids=new HashSet<string>();
            foreach(ActionItem item in config.AllActions()) if(item.Kind=="toggle") { ids.Add(item.Toggle.Id); Get(item); }
            foreach(string id in new List<string>(states.Keys)) if(!ids.Contains(id)) states.Remove(id);
        }
        public void SynchronizeMute(ActionItem item,bool muted) {
            if(!AudioMute.IsToggle(item)) return;
            State state=Get(item); int last=item.Toggle.Stages[0].Value==(muted?"on":"off")?0:1;
            if(state.Last!=last) { state.Last=last; state.Revision++; }
        }
        public void Reset() { states.Clear(); }
    }
}

using System;
using System.Linq;

namespace OrbitRing {
    public sealed class MuteIcons {
        string muted,unmuted;
        public string Muted { get { return muted; } set { muted=CustomIcons.Share(value); } }
        public string Unmuted { get { return unmuted; } set { unmuted=CustomIcons.Share(value); } }
        public void Validate() { ActionIcons.Validate(Muted); ActionIcons.Validate(Unmuted); }
    }
    static class AudioMute {
        public static bool IsToggle(ActionItem item) { return item.Kind=="toggle" && item.Toggle!=null && item.Toggle.MuteKind!=null; }
        public static bool Supports(ActionItem item) { return IsToggle(item) || item.Kind=="volume" || item.Kind=="microphone" || item.Kind=="mute" || (item.Kind=="shortcut" && String.Equals(item.Value,"VolumeMute",StringComparison.OrdinalIgnoreCase)); }
        public static bool IsMicrophone(ActionItem item) { return item.Kind=="microphone" || (IsToggle(item) && item.Toggle.MuteKind=="microphone"); }
        static ToggleStage Stage(ActionItem item,bool muted) { return item.Toggle.Stages.Single(s=>s.Value==(muted?"on":"off")); }
        public static ActionItem StateAction(ActionItem item,bool muted) {
            if(IsToggle(item)) return Stage(item,muted).Action();
            return new ActionItem(muted?"ミュートON":"ミュートOFF",IsMicrophone(item)?"microphone":"mute",muted?"on":"off") {
                Icon=item.MuteIcons==null?item.Icon:muted?item.MuteIcons.Muted:item.MuteIcons.Unmuted
            };
        }
        public static void SetIcon(ActionItem item,bool muted,string key) {
            if(IsToggle(item)) Stage(item,muted).Icon=key;
            else {
                if(item.MuteIcons==null) item.MuteIcons=new MuteIcons { Muted=item.Icon,Unmuted=item.Icon };
                if(muted) item.MuteIcons.Muted=key; else item.MuteIcons.Unmuted=key;
            }
        }
        public static ActionItem Display(ActionItem item,bool muted) {
            ActionItem display=item.Clone(),state=StateAction(item,muted);
            display.Icon=state.Icon; display.AutomaticIcon=ActionIcons.DefaultKey(state); display.AutomaticGlyph=Ui.Glyph(state);
            display.Label=item.Label+" · "+(muted?"ミュートON":"ミュートOFF"); return display;
        }
    }
}

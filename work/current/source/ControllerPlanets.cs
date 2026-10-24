using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    sealed partial class Controller {
        readonly Dictionary<int,int> planetKeys=new Dictionary<int,int>();
        void ClearPlanetKeys() { if(receiver==null) return; foreach(int id in planetKeys.Keys) Native.UnregisterHotKey(receiver.Handle,id); planetKeys.Clear(); }
        bool RegisterPlanetKeys(Preferences config) {
            ClearPlanetKeys(); int index=0; bool complete=true;
            if(!config.PlanetEnabled || ShortcutRecording || paused) return true;
            foreach(string key in UserContent.Hotkeys(config)) { Chord chord=Chord.Parse(key); int id=400+index; if(Native.RegisterHotKey(receiver.Handle,id,chord.Modifiers|0x4000,chord.Key)) planetKeys.Add(id,index); else complete=false; index++; }
            return complete;
        }
        void ReplacePlanetKeys(Preferences next) {
            if(RegisterPlanetKeys(next)) return;
            RegisterPlanetKeys(Config); throw new Exception("planetの呼び出しキーが他のアプリと競合しています。別のキーを指定してください。");
        }
    }
}

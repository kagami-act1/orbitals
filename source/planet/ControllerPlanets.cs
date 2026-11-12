using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    sealed partial class Controller {
        readonly Dictionary<int,int> planetKeys=new Dictionary<int,int>();
        int activePlanet;
        string planetApplication;
        bool directPlanet;
        int[] PlanetChoices { get { return ApplicationProfiles.WheelPlanets(Config,directPlanet?null:planetApplication,activePlanet); } }
        public bool CanSwitchPlanet { get { return Config.PlanetWheelSwitch && !InsideStack && PlanetChoices.Length>1; } }
        public string PlanetName { get { return UserContent.Name(Config,activePlanet); } }
        public int PlanetPage { get { return Array.IndexOf(PlanetChoices,activePlanet); } }
        public int PlanetPageCount { get { return PlanetChoices.Length; } }
        public string PlanetPosition { get {
            if(!directPlanet && ApplicationProfiles.AssignedPlanet(Config,planetApplication).HasValue) return "アプリ専用";
            int[] choices=PlanetChoices; if(choices.Length<2) return activePlanet>0?"専用planet":"";
            return (Array.IndexOf(choices,activePlanet)+1)+" / "+choices.Length;
        } }
        public void SwitchPlanet(int steps) {
            if(steps==0 || ring==null || !CanSwitchPlanet || busy || paused || MacroRecording || ShortcutRecording) return;
            int[] choices=PlanetChoices;
            int next=choices[PlanetWheel.Next(Array.IndexOf(choices,activePlanet),choices.Length,steps)]; if(next==activePlanet) return;
            activePlanet=next; ActiveItems=ApplicationProfiles.Items(Config,planetApplication,next); ring.ChangePlanet(steps);
        }
        void ClearPlanetKeys() { if(receiver==null) return; foreach(int id in planetKeys.Keys) Native.UnregisterHotKey(receiver.Handle,id); planetKeys.Clear(); }
        bool RegisterPlanetKeys(Preferences config) {
            ClearPlanetKeys(); bool complete=true; planetRegistrationFailed=false;
            if(!config.PlanetEnabled || ShortcutRecording || paused) return true;
            foreach(KeyValuePair<int,string> binding in UserContent.HotkeyBindings(config)) { Chord chord=Chord.Parse(binding.Value); int id=400+binding.Key; if(Native.RegisterHotKey(receiver.Handle,id,chord.Modifiers|0x4000,chord.Key)) planetKeys.Add(id,binding.Key); else complete=false; }
            planetRegistrationFailed=!complete; RefreshTrayState(); return complete;
        }
        void ReplacePlanetKeys(Preferences next) {
            if(RegisterPlanetKeys(next)) return;
            RegisterPlanetKeys(Config); throw new Exception("planetの呼び出しキーが他のアプリと競合しています。別のキーを指定してください。");
        }
    }
}

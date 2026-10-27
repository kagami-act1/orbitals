using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    sealed partial class Controller {
        public readonly uint ProcessId=(uint)Process.GetCurrentProcess().Id;
        public bool Diagnostic { get; private set; }
        OrbitEngine orbit;
        OrbitOverlay orbitOverlay;
        volatile bool paused;
        bool gestureHolding;
        Action cancelSearch;
        Native.POINT actionOrigin;
        public List<ActionItem> ActiveItems;
        Forms.ToolStripMenuItem pauseMenu;
        public bool MacroRecording;
        public bool OrbitAllowed { get { return Config.Orbit.Enabled && !paused && !MacroRecording && !ShortcutRecording && ring==null && conversionPlanets==null; } }
        void InitializeOrbit(Forms.ContextMenuStrip menu) {
            pauseMenu=new Forms.ToolStripMenuItem("一時停止"); pauseMenu.Click+=delegate { TogglePause(); }; menu.Items.Insert(0,pauseMenu);
            UpdateOrbitEngine();
        }
        void TogglePause() {
            paused=!paused; HideRing(); CloseOrbitFrame();
            try {
                UpdateOrbitEngine();
                if(!Diagnostic && !RegisterPlanetKeys(Config)) Notify("呼び出しキーを再登録できませんでした。ほかのアプリとの競合を確認してください。");
            } catch(Exception ex) { paused=true; ClearPlanetKeys(); CloseOrbitFrame(); Notify(ex.Message); }
            RefreshTrayState();
        }
        bool ResolvePlanet(int index) {
            string executable=ExtendedActions.Executable(target); if(!directPlanet) index=ApplicationProfiles.RuntimePlanet(Config,executable,index); activePlanet=index; planetApplication=executable; ActiveItems=ApplicationProfiles.Items(Config,executable,index);
            if(paused) { Notify("一時停止中です。通知領域から再開できます。"); return false; }
            if(Config.Orbit.IsPlanetExcluded(ExtendedActions.Executable(target))) return false;
            return true;
        }
        public void ExecuteOrbit(ActionItem item,IntPtr destination,Native.POINT point,bool hold) {
            if(item.Kind=="none" || !Config.Orbit.Enabled || paused || busy || ShortcutRecording || !Native.IsWindow(destination) || Native.GetForegroundWindow()!=destination) return;
            target=destination; actionOrigin=point;
            if(item.Kind=="planet") { QueueLinkedPlanet(item.Value,destination,orbit); return; }
            if(item.Kind=="wheel") { try { ExtendedActions.Wheel(item.Value,point,playback); } catch(Exception ex) { HideRing(); Notify(ex.Message); } return; }
            if(hold) {
                if(orbit==null || !orbit.HoldLive) return;
                try { playback.StartHold(item.Value); playbackTarget=destination; holding=true; gestureHolding=true; busy=true; Native.SetCursorPos(point.X,point.Y); playbackTimer.Start(); }
                catch(Exception ex) { HideRing(); Notify(ex.Message); }
                return;
            }
            Execute(item);
        }
        public void ShowOrbitFrame(OrbitFrame frame) {
            if(frame==null || !frame.Visible || !Config.Orbit.Enabled || paused || ShortcutRecording) { CloseOrbitFrame(); return; }
            if(orbitOverlay==null || orbitOverlay.Session!=frame.Session) { CloseOrbitFrame(); orbitOverlay=new OrbitOverlay(frame,Config.Appearance); }
            else orbitOverlay.Update(frame);
        }
        void CloseOrbitFrame() { if(orbitOverlay!=null) { orbitOverlay.Retire(); orbitOverlay=null; } }
        bool ExecuteExtended(ActionItem item,IntPtr destination,Action complete) {
            switch(item.Kind) {
                case "window": ExtendedActions.WindowAction(destination,item.Value); break;
                case "window-size": ExtendedActions.WindowAction(destination,"size:"+item.Value); break;
                case "audiosession": AudioActions.ChangeApplication(destination,item.Value); break;
                case "audiodevice": AudioActions.SelectDevice(item.Value); break;
                case "stamp": ExtendedActions.Text(DateTime.Now.ToString(item.Value=="date"?"yyyy/MM/dd":item.Value=="time"?"HH:mm":"yyyy/MM/dd HH:mm",System.Globalization.CultureInfo.InvariantCulture),destination); break;
                case "settings": Process.Start(new ProcessStartInfo(ExtendedActions.SettingsUri(item.Value)) { UseShellExecute=true }); break;
                case "convert": OpenConversion(destination,item.Value); break;
                case "planet": QueueLinkedPlanet(item.Value,destination); break;
                case "launch": ApplicationLaunch.Run(item.Value); break;
                case "text": ExtendedActions.Text(item.Value,destination); break;
                case "pasteplain": ExtendedActions.Text(Clipboard.ContainsText()?Clipboard.GetText():"",destination); break;
                case "microphone": if(String.IsNullOrEmpty(item.Value)) SystemMute.ToggleMicrophone(); else SystemMute.Set(item.Value=="on",true); break;
                case "output": playback.Press("Win+Ctrl+V"); break;
                case "wheel": ExtendedActions.Wheel(item.Value,actionOrigin,playback); break;
                case "orbitals":
                    if(item.Value=="pause") TogglePause();
                    else {
                        string exe=ExtendedActions.Executable(destination); if(String.IsNullOrEmpty(exe)) throw new Exception("アプリを取得できません。");
                        Preferences next=Config.Clone(); next.Orbit.Exclude(exe); Apply(next); Notify(exe+"をブラックリストに追加しました。「アプリ」内でON / OFFを変更できます。");
                    } break;
                case "search":
                    busy=true;
                    cancelSearch=ExtendedActions.Search(item.Value,destination,delegate { playback.Press("Ctrl+C"); },delegate(bool success,string message) { cancelSearch=null; busy=false; if(success && complete!=null) complete(); if(message!=null) Notify(message); }); return true;
                default: return false;
            }
            if(complete!=null) complete(); return true;
        }
    }
}

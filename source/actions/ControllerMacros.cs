using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace OrbitRing {
    sealed partial class Controller {
        Action<string> macroTestReport;
        int macroReportedStep;
        DateTime macroTime;
        string macroClipboard;
        sealed class MacroActions : IMacroActions {
            readonly Controller owner;
            [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd,StringBuilder text,int count);
            public MacroActions(Controller value) { owner=value; }
            public bool SelectWindow(MacroStep step) {
                IntPtr hwnd=Native.GetForegroundWindow(); if(hwnd==IntPtr.Zero || !Native.IsWindow(hwnd)) return false;
                uint pid; ExtendedActions.GetWindowThreadProcessId(hwnd,out pid); if(pid==owner.ProcessId) return false;
                StringBuilder title=new StringBuilder(2048); GetWindowText(hwnd,title,title.Capacity);
                if(!MacroDefinition.Matches(step,ExtendedActions.Executable(hwnd),title.ToString())) return false;
                owner.playbackTarget=hwnd; return true;
            }
            public void Execute(MacroStep step) {
                IntPtr destination=owner.playbackTarget;
                string value=step.Kind=="text"?MacroDefinition.ExpandText(step.Value,owner.macroTime,owner.macroClipboard):step.Value;
                ActionItem action=new ActionItem("ステップ",step.Kind,value);
                if(step.Kind=="open") { using(Process process=Process.Start(new ProcessStartInfo(OpenTarget.Resolve(value)) { UseShellExecute=true })) { } }
                else if(step.Kind=="mute") SystemMute.Set(value=="on");
                else if(step.Kind=="volume") SystemMute.Toggle();
                else if(!owner.ExecuteExtended(action,destination,null)) throw new Exception("この操作はマクロから実行できません。");
            }
        }
        void StartMacroRun(string text,IntPtr destination,Action complete) {
            macroTime=DateTime.Now; macroClipboard=null;
            if(MacroStep.Parse(text).Any(s=>s.Kind=="text" && (s.Value??"").Contains("{clipboard}"))) { macroClipboard=Clipboard.ContainsText()?Clipboard.GetText():""; if(macroClipboard.Length>4096) throw new Exception("差し込むクリップボードの文字は4096文字までです。"); }
            playback.StartMacro(text,DateTime.UtcNow); playbackCompleted=complete; playbackTarget=destination; holding=false; busy=true; macroReportedStep=0;
            if(!Diagnostic && !escapeRegistered) escapeRegistered=Native.RegisterHotKey(receiver.Handle,100,0x4000,27);
            if(playbackTimer!=null) playbackTimer.Start();
        }
        void ReportMacro(string message,bool finished) { Action<string> report=macroTestReport; if(finished) macroTestReport=null; if(report!=null) report(message); }
        public void TestMacro(string text,Action<string> report) {
            if(Diagnostic) { if(report!=null) report("動作確認モードでは入力を送りません。"); return; }
            try {
                HideRing();
                foreach(string hotkey in UserContent.Hotkeys(Config)) Preferences.ValidateAction(new ActionItem("テスト","macro",text),Chord.Parse(hotkey));
                macroTestReport=report; ReportMacro("3秒後に開始します。操作したいアプリを前面にしてください。Escで中止します。",false);
                if(!escapeRegistered) escapeRegistered=Native.RegisterHotKey(receiver.Handle,100,0x4000,27);
                busy=true; pending=new DispatcherTimer { Interval=TimeSpan.FromSeconds(3) };
                pending.Tick+=delegate {
                    StopPending(); IntPtr destination=Native.GetForegroundWindow(); uint pid; ExtendedActions.GetWindowThreadProcessId(destination,out pid);
                    if(destination==IntPtr.Zero || !Native.IsWindow(destination) || pid==ProcessId) { ReportMacro("テストを開始できませんでした。3秒以内に操作先のアプリへ切り替えてください。",true); HideRing(); return; }
                    try { StartMacroRun(text,destination,null); } catch(Exception ex) { ReportMacro(ex.Message,true); HideRing(); }
                }; pending.Start();
            } catch(Exception ex) { if(report!=null) report(ex.Message); }
        }
        public void CancelMacroTest() { if(macroTestReport!=null) HideRing(); }
    }
}

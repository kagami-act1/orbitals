using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OrbitRing {
    static class ApplicationLaunch {
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
        public static string Resolve(string value) {
            if(value!=null && value.Length>=2 && value[0]=='"' && value[value.Length-1]=='"') value=value.Substring(1,value.Length-2);
            string path=OpenTarget.Resolve(value),extension=Path.GetExtension(path);
            if(!File.Exists(path) || (!extension.Equals(".exe",StringComparison.OrdinalIgnoreCase) && !extension.Equals(".lnk",StringComparison.OrdinalIgnoreCase))) throw new Exception("アプリのexeファイル、またはショートカット（.lnk）を選んでください。");
            return path;
        }
        internal static ProcessStartInfo StartInfo(string value) { return new ProcessStartInfo(Resolve(value)) { UseShellExecute=true }; }
        public static void Run(string value) {
            ProcessStartInfo start=StartInfo(value); string path=start.FileName;
            // A shortcut may specify arguments or a different launch mode. Let Windows open it intact.
            if(Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)) {
                foreach(Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path))) {
                    using(process) { try { IntPtr hwnd=process.MainWindowHandle; if(hwnd!=IntPtr.Zero && String.Equals(ExtendedActions.ProcessPath(hwnd),path,StringComparison.OrdinalIgnoreCase)) { if(IsIconic(hwnd)) ShowWindow(hwnd,9); if(!Native.SetForegroundWindow(hwnd)) throw new Exception("アプリを前面へ移動できません。"); return; } } catch(InvalidOperationException) { } }
                }
            }
            using(Process process=Process.Start(start)) { }
        }
    }
}

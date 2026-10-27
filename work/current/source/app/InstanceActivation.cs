using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace OrbitRing {
    static class InstanceActivation {
        internal const string ReceiverName="OrbitRing.Hotkey";
        internal static readonly int SettingsMessage=(int)RegisterWindowMessage("Orbitals.OpenSettings.v1");
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string type,string title);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(uint process);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,int message,IntPtr w,IntPtr l);
        public static bool RequestSettings() { return RequestSettings(ReceiverName,2000); }
        internal static bool RequestSettings(string receiver,int timeout) {
            if(SettingsMessage==0) return false;
            Stopwatch watch=Stopwatch.StartNew();
            do {
                IntPtr window=FindWindowEx(new IntPtr(-3),IntPtr.Zero,null,receiver);
                if(window!=IntPtr.Zero) {
                    uint process; GetWindowThreadProcessId(window,out process);
                    if(process!=0) AllowSetForegroundWindow(process);
                    if(PostMessage(window,SettingsMessage,IntPtr.Zero,IntPtr.Zero)) return true;
                }
                if(watch.ElapsedMilliseconds>=timeout) return false;
                Thread.Sleep(40);
            } while(true);
        }
    }
}

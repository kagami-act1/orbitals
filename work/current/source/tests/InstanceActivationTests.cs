using System;
using System.Collections.Generic;
using System.Windows.Interop;
using System.Windows.Threading;

namespace OrbitRing {
    static class InstanceActivationTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static void Pump() { DispatcherFrame frame=new DispatcherFrame(); Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate { frame.Continue=false; })); Dispatcher.PushFrame(frame); }
        public static List<string> Run() {
            List<string> results=new List<string>(); string name="Orbitals.ActivationTest."+Guid.NewGuid().ToString("N"); int requests=0;
            Check(InstanceActivation.SettingsMessage>=0xC000 && InstanceActivation.SettingsMessage<=0xFFFF,"settings activation uses a registered application message",results);
            Check(!InstanceActivation.RequestSettings(name,0),"activation does not report success without a matching running instance",results);
            using(HwndSource receiver=new HwndSource(new HwndSourceParameters(name) { ParentWindow=new IntPtr(-3),Width=0,Height=0,WindowStyle=0 })) {
                receiver.AddHook(delegate(IntPtr hwnd,int message,IntPtr w,IntPtr l,ref bool handled) { if(message==InstanceActivation.SettingsMessage) { requests++; handled=true; } return IntPtr.Zero; });
                Check(InstanceActivation.RequestSettings(name,0),"a second launch can find a resident message-only window",results); Pump();
                Check(requests==1,"a second launch delivers exactly one request to open settings",results);
                Check(InstanceActivation.RequestSettings(name,0),"settings can be reopened after an earlier activation",results); Pump();
                Check(requests==2,"reopening settings does not require another resident instance",results);
            }
            Check(!InstanceActivation.RequestSettings(name,0),"activation cannot target a disposed receiver",results);
            return results;
        }
    }
}

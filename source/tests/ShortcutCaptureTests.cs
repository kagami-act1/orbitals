using System;
using System.Collections.Generic;

namespace OrbitRing {
    static class ShortcutCaptureTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static ShortcutCapture Capture(bool invocation,params ushort[] keys) {
            ShortcutCapture capture=new ShortcutCapture(invocation,new ushort[0]);
            foreach(ushort key in keys) capture.Feed(key,false);
            for(int i=keys.Length-1;i>=0;i--) capture.Feed(keys[i],true);
            return capture;
        }
        public static List<string> Run() {
            List<string> r=new List<string>();
            ShortcutCapture capture=Capture(true,162,164,120);
            Check(capture.Finished && !capture.Cancelled && capture.Value=="Ctrl+Alt+F9","the invocation recorder captures its currently registered shortcut",r);
            Check(Capture(true,124).Value=="F13" && Capture(false,65).Value=="A","function keys and unmodified action keys are recorded",r);
            Check(Capture(true,19).Value=="Pause" && Capture(true,162,3).Value=="Ctrl+Break" && Chord.Parse("Pause/Break").Key==19,"Pause and Control-Break can be recorded and keep distinct Windows key codes",r);
            capture=new ShortcutCapture(true,new ushort[0]); capture.Feed(19,false); Check(capture.Finished && capture.Value=="Pause","Pause recording completes even when the keyboard provides no separate release event",r);
            Preferences pause=Preferences.Defaults(); pause.Hotkey="Pause"; pause.Planets.Add(UserContent.NewPlanet(pause)); pause.Planets[0].Hotkey="Ctrl+Break"; pause.Planets[0].HotkeyEnabled=true; pause.Validate(); Check(pause.Clone().Hotkey=="Pause","Pause invocation validates and survives settings round trips for main and additional planets",r);
            Check(Capture(false,162).Value=="Ctrl" && Capture(false,163).Value=="Ctrl","either control key can be assigned to a hold action",r);
            Check(Capture(false,165,9).Value=="Alt+Tab" && Capture(false,91,160,83).Value=="Shift+Win+S","system combinations are named without relying on text input or the IME",r);
            Check(Capture(false,164,163,161,50).Value=="Ctrl+Alt+Shift+2","modifier order and digit names match the action parser",r);
            capture=new ShortcutCapture(false,new ushort[0]); capture.Feed(162,false); capture.Feed(67,false); capture.Feed(67,false); capture.Feed(67,true);
            Check(!capture.Finished && capture.Value=="Ctrl+C","key repeat does not complete a capture before modifiers are released",r);
            capture.Feed(162,true); Check(capture.Finished,"a shortcut completes after its final held key is released",r);
            capture=new ShortcutCapture(true,new ushort[0]); capture.Feed(65,false);
            Check(capture.Error!=null && !capture.Finished && capture.Value==null,"ordinary typing cannot become a global invocation key",r);
            capture.Feed(65,true); capture.Feed(125,false); capture.Feed(125,true);
            Check(capture.Finished && capture.Value=="F14","a rejected invocation key can be corrected in the same recording",r);
            capture=new ShortcutCapture(true,new ushort[0]); capture.Feed(162,false); capture.Feed(162,true);
            Check(!capture.Finished && capture.Value==null,"a modifier alone is reserved for actions rather than global invocation",r);
            capture=Capture(false,162,27); Check(capture.Cancelled && capture.Finished && capture.Value==null,"escape cancels recording after all keys are released",r);
            capture=new ShortcutCapture(true,new ushort[]{163}); capture.Feed(126,false); capture.Feed(126,true);
            Check(capture.Value=="Ctrl+F15" && !capture.Finished,"a modifier held before recording is included in the combination",r);
            capture.Feed(163,true); Check(capture.Finished,"releasing a preexisting modifier completes the recording",r);
            foreach(ushort key in new ushort[]{8,13,33,34,44,173,179,186,111,96}) {
                capture=Capture(false,key); Check(capture.Finished && Chord.Parse(capture.Value).Key==key,"recorded virtual key round trips: "+key,r);
            }
            return r;
        }
    }
}

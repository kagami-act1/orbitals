using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class ExecutionTests {
        sealed class FakeInput : IActionInput {
            public readonly HashSet<ushort> Down=new HashSet<ushort>();
            public readonly List<string> Events=new List<string>();
            public ushort FailKey;
            public ushort FailRelease;
            public bool IsDown(ushort key) { return Down.Contains(key); }
            public void Send(ushort key,bool up) {
                if(!up && key==FailKey) throw new Exception("simulated input failure");
                if(up && key==FailRelease) throw new Exception("simulated release failure");
                Events.Add((up?"up":"down")+key);
                if(up) Down.Remove(key); else Down.Add(key);
            }
        }
        static void Check(bool condition,string name,List<string> results) { if(!condition) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> results=new List<string>(); DateTime now=new DateTime(2026,10,17);
            Check(Chord.Parse("Ctrl").Key==17 && Chord.Parse("Shift").Key==16 && Chord.Parse("NumPad3").Key==0x63 && Chord.Parse("OemPlus").Key==0xBB,"standalone modifiers numpad and punctuation keys",results);
            FakeInput input=new FakeInput(); ActionPlayback player=new ActionPlayback(input);
            player.StartHold("Ctrl+Shift+A"); player.Tick(now,true,false);
            Check(input.Down.SetEquals(new ushort[]{17,16,65}) && player.Active,"hold stays down until release",results);
            player.Tick(now,true,true);
            Check(input.Down.Count==0 && !player.Active && String.Join(",",input.Events)=="down17,down16,down65,up65,up16,up17","mouse release reverses all held keys",results);
            player.StartHold("Space"); player.Tick(now,false,false);
            Check(input.Down.Count==0 && !player.Active,"foreground change cancels hold",results);
            player.StartHold("Alt"); player.Stop(); player.Stop();
            Check(input.Down.Count==0,"escape close and repeated cleanup release hold",results);
            input.Down.Add(17);
            Check(Reject(delegate { player.StartHold("Ctrl+C"); }) && input.Down.SetEquals(new ushort[]{17}),"preexisting user keys are not released",results); input.Down.Clear();
            input.FailKey=65;
            Check(Reject(delegate { player.StartHold("Ctrl+A"); }) && input.Down.Count==0 && !player.Active,"partial input failure releases introduced modifiers",results); input.FailKey=0;
            input.Down.Add(65);
            Check(Reject(delegate { player.Press("Ctrl+A"); }) && input.Down.SetEquals(new ushort[]{65}),"ordinary shortcuts do not release a physically held target key",results); input.Down.Clear();
            player.StartHold("Ctrl"); input.FailRelease=17;
            Check(Reject(delegate { player.Stop(); }) && player.NeedsRelease && input.Down.Contains(17),"failed key-up remains tracked for cleanup",results);
            input.FailRelease=0; player.Tick(now,true,false);
            Check(!player.NeedsRelease && input.Down.Count==0 && !player.Active,"cleanup retries key-up after playback has stopped",results);
            input.Events.Clear(); player.StartMacro("Ctrl+C\nwait:200\nCtrl+V",now);
            player.Tick(now,true,false); int count=input.Events.Count;
            player.Tick(now.AddMilliseconds(30),true,false); player.Tick(now.AddMilliseconds(229),true,false);
            Check(count==4 && input.Events.Count==count && input.Down.Count==0,"macro wait sends no keys and retains no modifiers",results);
            player.Tick(now.AddMilliseconds(230),true,false); player.Tick(now.AddMilliseconds(260),true,false);
            Check(!player.Active && String.Join(",",input.Events)=="down17,down67,up67,up17,down17,down86,up86,up17","macro executes ordered chords once",results);
            input.Events.Clear(); player.StartMacro("wait:200\nA",now); player.Tick(now,true,false); player.Tick(now.AddMilliseconds(200),false,false);
            Check(!player.Active && input.Events.Count==0,"macro cancels before typing into another app",results);
            player.StartMacro("wait:200\nA",now); player.Tick(now,true,true);
            Check(!player.Active && input.Events.Count==0,"macro escape prevents remaining steps",results);
            Check(Reject(delegate { MacroStep.Parse("wait:0"); }) && Reject(delegate { MacroStep.Parse("wait:30001"); }) && Reject(delegate { MacroStep.Parse("wait:30000\nwait:30000\nwait:1"); }) && Reject(delegate { MacroStep.Parse(String.Join("\n",Enumerable.Repeat("A",65))); }),"macro rejects invalid delays and oversized sequences",results);
            Preferences p=Preferences.Defaults(); p.Items[0]=new ActionItem("長押し","hold","Shift"); p.Items[1]=new ActionItem("連続","macro","A\nwait:50\nB"); p.Validate(); Preferences restored=p.Clone(); restored.Validate();
            Check(restored.Items[0].Kind=="hold" && restored.Items[1].Value==p.Items[1].Value,"new action types persist with existing configuration format",results);
            p.Items[0].Icon="star"; restored=p.Clone(); restored.Swap(0,3);
            Check(restored.Items[3].Clone().Icon=="star" && Ui.Glyph(restored.Items[3])==ActionIcons.Find("star"),"custom icons survive persistence clone and reorder",results);
            restored.Items[3].Icon=null; Check(Ui.Glyph(restored.Items[3])=="\uE765","automatic icon follows action type after reset",results);
            p.Items[1].Value=p.Hotkey; Check(Reject(delegate { p.Validate(); }),"macro cannot invoke its own ring shortcut",results);
            LayoutEditor editor=new LayoutEditor(); editor.Bind(Preferences.Defaults(),0); bool radial=true;
            for(int i=0;i<8;i++) { Vector a=editor.Position(i)-new Point(430,320),b=editor.LabelCenter(i,146,32)-new Point(430,320); radial &= Math.Abs(Vector.CrossProduct(a,b))<.001 && b.Length>a.Length; }
            Check(radial,"all eight label centers align radially outside their icons",results);
            return results;
        }
    }
}

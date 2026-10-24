using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class OrbitTests {
        static void Check(bool condition,string label,List<string> results) { if(!condition) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> r=new List<string>();
            Check(OrbitPolicy.Release(false,false,false,false,"",true)==OrbitRelease.Click,"short physical release replays ordinary click",r);
            Check(OrbitPolicy.Release(false,false,false,false,"DR",true)==OrbitRelease.Action,"completed stroke executes on release",r);
            Check(OrbitPolicy.Release(true,false,false,false,"DR",true)==OrbitRelease.Cancel,"escape or focus loss cancels action and replay",r);
            Check(OrbitPolicy.Release(false,true,false,false,"L",true)==OrbitRelease.Cancel,"hold release cannot execute a second action",r);
            Check(OrbitPolicy.Release(false,false,true,false,"",true)==OrbitRelease.Cancel,"wheel and rocker cannot leak a context click",r);
            Check(OrbitPolicy.Release(false,false,false,true,"LRLR",true)==OrbitRelease.Cancel,"overlong stroke cannot execute a matching prefix",r);
            Check(OrbitPolicy.Release(false,false,false,false,"L",false)==OrbitRelease.Cancel,"missed physical release cancels safely",r);
            Check(!OrbitPolicy.MayDispatch(1,2,false,false,false) && !OrbitPolicy.MayDispatch(1,1,true,false,false),"cancelled or superseded queued actions are discarded",r);
            Check(!OrbitPolicy.MayDispatch(1,1,false,true,false) && OrbitPolicy.MayDispatch(1,1,false,true,true),"hold cannot start after button release",r);
            DirectionStroke s=new DirectionStroke(new Point(500,500),30); s.Add(new Point(512,508)); Check(s.Pattern=="","pointer jitter retains normal click",r);
            s.Add(new Point(470,500)); s.Add(new Point(420,502)); Check(s.Pattern=="L","long straight stroke has one direction",r);
            s.Add(new Point(420,550)); Check(s.Pattern=="LD","corners form ordered direction sequences",r);
            DirectionStroke negative=new DirectionStroke(new Point(-1000,-800),30); negative.Add(new Point(-1000,-850)); negative.Add(new Point(-960,-850)); Check(negative.Pattern=="UR","negative monitor coordinates recognized",r);
            DirectionStroke over=new DirectionStroke(new Point(0,0),12); for(int i=0;i<30;i++) over.Add(new Point(i%2==0?40:0,0)); Check(over.Overflow && over.Pattern.Length==12,"unbounded input cannot grow gesture state",r);
            Check(!OrbitPreferences.ValidPattern("LL") && !OrbitPreferences.ValidPattern("RUQ") && !OrbitPreferences.ValidPattern(""),"invalid and redundant gesture directions rejected",r);
            Preferences p=Preferences.Defaults(); p.Validate();
            Check(p.Orbit.Resolve("msedge.exe","L","").Action.Value=="Alt+Left","common gestures apply by default",r);
            p.Orbit.Exclude("msedge.exe"); AppProfile profile=p.Orbit.Profile("msedge.exe");
            Check(p.Orbit.IsExcluded("MSEDGE.EXE") && p.Orbit.Resolve("MSEDGE.EXE","L","")==null,"blacklist matches executable names case insensitively",r);
            profile.Excluded=true; Check(p.Orbit.Resolve("msedge.exe","R","")==null,"excluded app cannot fall back to common bindings",r); profile.Excluded=false;
            p.Orbit.Bindings.Add(new OrbitBinding { Pattern="L",Modifier="Ctrl",Action=new ActionItem("保存","shortcut","Ctrl+S") }); p.Validate(); Check(p.Orbit.Resolve("msedge.exe","L","Ctrl").Action.Value=="Ctrl+S","modifier bindings resolve independently",r);
            p.Orbit.Bindings.Add(new OrbitBinding { Pattern="L",Action=new ActionItem("コピー","shortcut","Ctrl+C") }); Check(Reject(p.Validate),"duplicate gesture rejected before save",r); p.Orbit.Bindings.RemoveAt(p.Orbit.Bindings.Count-1);
            p.Orbit.Exclude("MSEDGE.EXE"); Preferences clone=p.Clone(); clone.Validate(); Check(clone.Orbit.Profiles.Count==1 && clone.Orbit.IsExcluded("msedge.exe"),"blacklist survives serialization without duplicate entries",r);
            clone.Orbit.Profiles.Clear(); Check(clone.Orbit.Resolve("msedge.exe","L","").Action.Value=="Alt+Left","removing blacklist entry immediately restores common actions",r);
            clone=p.Clone();
            foreach(string name in new[]{"a/b.exe","a\\b.exe","*.exe",".exe","a.exe ","a:exe.exe","a\n.exe"}) Check(Reject(delegate { clone.Orbit.Exclude(name); }),"invalid blacklist name rejected: "+name.Replace("\n","newline"),r);
            clone=p.Clone(); for(int i=1;i<64;i++) clone.Orbit.Exclude("test"+i+".exe"); Check(Reject(delegate { clone.Orbit.Exclude("overflow.exe"); }) && clone.Orbit.Profiles.Count==64,"blacklist size bound is enforced before mutation",r);
            clone=p.Clone();
            clone.Orbit.Profiles.Add(new AppProfile { Executable="MSEDGE.EXE" }); Check(Reject(clone.Validate),"duplicate executable profiles rejected",r);
            clone=p.Clone(); clone.Orbit.Profiles[0].Executable="../test.exe"; Check(Reject(clone.Validate),"profile names cannot contain paths",r);
            clone=p.Clone(); clone.Orbit.Distance=0; Check(Reject(clone.Validate),"zero recognition distance rejected",r);
            clone=p.Clone(); clone.Orbit.Bindings[0].Action=new ActionItem("再帰","shortcut",p.Hotkey); Check(Reject(clone.Validate),"gesture cannot recursively invoke planet trigger",r);
            string legacy="{\"Version\":1,\"Hotkey\":\"Ctrl+Alt+F9\",\"Items\":"+Json.Write(p.Items)+"}"; Preferences migrated=Json.Read<Preferences>(legacy); migrated.Validate(); Check(migrated.Orbit.Enabled && migrated.Items[0].Value=="Ctrl+C","legacy settings gain orbit defaults without changing planet",r);
            Chord trigger=Chord.Parse(p.Hotkey); int checkedCount=0;
            foreach(ActionPreset preset in ActionCatalog.Create()) { ActionItem item=preset.Create(); if(item.Value=="" && new[]{"shortcut","open","launch","text","toggle","audiodevice"}.Contains(item.Kind)) continue; Preferences.ValidateAction(item,trigger); checkedCount++; }
            Check(checkedCount>=40,"all complete built-in action presets validate",r);
            Check(Reject(delegate { Preferences.ValidateAction(new ActionItem("検索","search","file:///x/{text}"),trigger); }),"search templates reject local and executable schemes",r);
            Check(Reject(delegate { Preferences.ValidateAction(new ActionItem("文字","text",new string('x',4097)),trigger); }),"text insertion has a bounded input length",r);
            Check(Reject(delegate { Preferences.ValidateAction(new ActionItem("移動","window","unrecognized"),trigger); }),"window commands use a fixed allowlist",r);
            Check(Reject(delegate { Preferences.ValidateAction(new ActionItem("起動","launch","https://example.com/a.exe"),trigger); }),"launch-or-focus cannot treat a URL as an executable",r);
            Check(p.Orbit.Resolve("test.exe","WheelUp","").Action.Kind=="wheel" && p.Orbit.Resolve("test.exe","Rocker","").Action.Value=="Ctrl+Tab","wheel and rocker defaults share action definitions",r);
            Preferences fresh=Preferences.Defaults();
            Check(GestureInput.ChordPattern(0x201,0)=="Rocker" && fresh.Orbit.Resolve("browser.exe",GestureInput.ChordPattern(0x201,0),"").Action.Value=="Ctrl+Tab","trigger followed by left button down resolves the next-tab action",r);
            Check(GestureInput.ChordPattern(0x202,0)==null && GestureInput.ChordPattern(0x200,0)==null,"left release and pointer movement cannot retrigger a chord",r);
            Check(GestureInput.ChordPattern(0x20A,120u<<16)=="WheelUp" && GestureInput.ChordPattern(0x20A,unchecked((uint)(-120<<16)))=="WheelDown","signed wheel input distinguishes up and down",r);
            Check(OrbitPolicy.MayDispatch(7,7,false,false,false) && OrbitPolicy.Release(false,false,true,false,"",true)==OrbitRelease.Cancel,"releasing trigger after a chord keeps its queued action and suppresses context click",r);
            Check(GestureInput.Caption("Rocker","Right").Contains("右ボタンを押したまま\n左クリック") && GestureInput.Caption("Rocker","X1").StartsWith("サイドボタン1"),"instructions name the selected trigger and explain button order",r);
            return r;
        }
    }
}

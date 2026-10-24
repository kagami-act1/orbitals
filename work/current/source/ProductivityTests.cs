using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class ProductivityTests {
        static void Check(bool value,string name,List<string> results) { if(!value) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults();
            PlanetProfile second=UserContent.NewPlanet(config); config.Planets.Add(second); config.Validate();
            Check(UserContent.Items(config,0)==config.Items && UserContent.Items(config,1)==second.Items && second.Items.All(a=>a.Kind=="none"),"additional planets have independent eight-slot layouts",r);
            Check(UserContent.Hotkeys(config).SequenceEqual(new[]{config.Hotkey,second.Hotkey}),"each planet exposes its own shortcut in layout order",r);
            Preferences invalid=config.Clone(); invalid.Planets[0].Hotkey="Alt+Ctrl+F9";
            Check(Reject(invalid.Validate),"equivalent shortcut spellings cannot open two planets",r);
            invalid=config.Clone(); invalid.Items[0]=new ActionItem("再帰","shortcut",second.Hotkey);
            Check(Reject(invalid.Validate),"an action cannot invoke another planet recursively",r);
            ActionItem toggle=ActionCatalog.Create().Single(p=>p.Item.Value=="system-mute").Create(); config.Items[0]=toggle;
            PlanetProfile duplicate=UserContent.NewPlanet(config,config.Items); config.Planets.Add(duplicate); config.Validate();
            Check(duplicate.Items[0].Toggle.Id!=toggle.Toggle.Id && duplicate.Items[0].Toggle.IsSystemMute,"duplicating a planet creates independent toggle identities",r);
            config.Favorites.Add(UserContent.CopyAction(toggle)); ActionItem placed=UserContent.CopyAction(config.Favorites[0]); placed.Label="変更";
            Check(config.Favorites[0].Label!=placed.Label && config.Favorites[0].Toggle.Id!=placed.Toggle.Id && config.Favorites[0].Toggle.Id!=toggle.Toggle.Id,"favorites and placements remain independent copies",r);
            OrbitBinding curve=GesturePresets.Create().Single(p=>p.Id=="c").Create(); curve.Action=new ActionItem("コピー","shortcut","Ctrl+C");
            SavedGesture saved=SavedGesture.From(curve,"自分の曲線"); config.GestureLibrary.Add(saved); config.Validate();
            OrbitBinding restored=saved.Preset().Create(); curve.Shape[0].X+=.1; restored.Shape[1].Y+=.1;
            Check(saved.Preset().Create().Action.Kind=="none" && saved.Shape[0].X!=curve.Shape[0].X && saved.Shape[1].Y!=restored.Shape[1].Y,"saved custom movements contain no action and own their geometry",r);
            Preferences roundtrip=config.Clone(); roundtrip.Validate();
            Check(roundtrip.Planets.Count==2 && roundtrip.Favorites.Count==1 && roundtrip.GestureLibrary.Count==1,"planets favorites and movement library survive JSON backup",r);
            List<ActionPreset> actions=ActionCatalog.Create();
            Check(actions.Count>=80 && actions.All(a=>ActionCatalog.Categories.Count(c=>c==a.Category)==1) && actions.Select(a=>a.Item.Kind+":"+a.Item.Value).Distinct().Count()==actions.Count,"standard actions have one category and no duplicate commands",r);
            Check(actions.All(a=>MaterialIcons.Find(ActionIcons.DefaultKey(a.Item))!=null),"every standard action has an available offline icon",r);
            Check(ActionCatalog.CategoryFor(toggle)=="音声・メディア" && ActionCatalog.CategoryFor(new ActionItem("自作","macro","F8"))=="カスタム操作","favorite categories follow the operation instead of its storage location",r);
            Native.INPUT left=ExtendedActions.WheelInput("Left"),right=ExtendedActions.WheelInput("Right"),up=ExtendedActions.WheelInput("Up");
            Check(left.data.mouse.flags==0x1000 && right.data.mouse.flags==0x1000 && unchecked((int)left.data.mouse.mouseData)==-120 && right.data.mouse.mouseData==120 && up.data.mouse.flags==0x800,"horizontal scrolling uses distinct signed horizontal wheel events",r);
            Check(ActionIcons.DefaultKey(new ActionItem("左","wheel","Left"))!=ActionIcons.DefaultKey(new ActionItem("右","wheel","Right")),"left and right scroll actions have distinct icons",r);
            Check(ExtendedActions.WindowSize("1280×720").SequenceEqual(new[]{1280,720}) && Reject(delegate { ExtendedActions.WindowSize("0x720"); }) && Reject(delegate { ExtendedActions.WindowSize("1280x99999"); }),"window sizes accept dimensions and reject invalid bounds",r);
            Check(ExtendedActions.SettingsUri("sound")=="ms-settings:sound" && Reject(delegate { ExtendedActions.SettingsUri("file:///x"); }),"Windows settings actions use a fixed URI allowlist",r);
            Check(AudioActions.AdjustedVolume(.99f,"up")==1 && AudioActions.AdjustedVolume(.01f,"down")==0 && Math.Abs(AudioActions.AdjustedVolume(.5f,"up")-.55f)<.0001,"per-application volume steps stay within zero and one",r);
            MacroCaptureBuffer capture=new MacroCaptureBuffer(); capture.Add("Ctrl+C",100); capture.Add("Ctrl+V",330); List<MacroStep> steps=MacroStep.Parse(capture.Text);
            Check(steps.Count==3 && steps[1].Delay==200 && steps[2].Key=="Ctrl+V","macro recording preserves chords and compensates playback spacing",r);
            capture=new MacroCaptureBuffer(); capture.Add("F8",0); capture.Add("F9",40030);
            Check(MacroStep.Parse(capture.Text).Where(s=>s.Key==null).Select(s=>s.Delay).SequenceEqual(new[]{30000,10000}),"long recorded waits split into valid bounded steps",r);
            capture=new MacroCaptureBuffer(); bool accepted=true; for(int i=0;i<64;i++) accepted &= capture.Add("F8",i*20); Check(accepted,"recorded macro accepts all 64 bounded steps",r);
            Check(KeyboardMacroRecorder.ModifierChord(new ushort[]{0xA2,0xA0},0xA0)=="Ctrl+Shift" && KeyboardMacroRecorder.ModifierChord(new ushort[]{0xA2},0xA2)=="Ctrl","recording preserves modifier-only chords without duplicate modifier keys",r);
            string full=capture.Text; Check(!capture.Add("F9",2000) && capture.Text==full,"recording limit never leaves an incomplete step",r);
            capture=new MacroCaptureBuffer(); capture.Add("F8",0); Check(!capture.Add("F9",61000),"recording cannot exceed total wait limit",r);
            OrbitPreferences gestures=new OrbitPreferences(); string before=Json.Write(gestures);
            Point[] back={new Point(300,100),new Point(200,100),new Point(100,100)};
            GestureAnalysis analysis=GestureAnalysis.Evaluate(gestures,back,"");
            Check(analysis.Match!=null && analysis.Match.Action.Label=="戻る" && analysis.Candidates[0].Binding==analysis.Match && Json.Write(gestures)==before,"recognition test uses live matching without editing bindings",r);
            Check(GestureAnalysis.Evaluate(gestures,back,"Ctrl").Match==null && GestureAnalysis.Evaluate(gestures,new[]{new Point(0,0),new Point(5,0)},"").Match==null,"recognition test respects modifier and minimum distance",r);
            string path=Path.Combine(Path.GetTempPath(),"orbitals-draft-test-"+Guid.NewGuid().ToString("N")+".json");
            try {
                Preferences unfinished=config.Clone(); unfinished.Orbit.Bindings.Add(new OrbitBinding { Pattern="",Action=new ActionItem("未完成","none","") });
                WorkingCopyRecord record=new WorkingCopyRecord { Version=1,Config=unfinished,SavedFingerprint=WorkingCopy.Fingerprint(config),Page=1,Planet=1,Gesture=unfinished.Orbit.Bindings.Count-1,StackPath=new int[0] };
                WorkingCopy.Write(path,record); WorkingCopyRecord read=WorkingCopy.Read(path);
                Check(read.Config.Orbit.Bindings.Last().Pattern=="" && Reject(read.Config.Validate) && read.Planet==1,"unfinished gestures restore as drafts without becoming valid runtime settings",r);
                read.Config.Orbit.Bindings.Last().Action.Label="変更"; WorkingCopy.Write(path,read);
                Check(WorkingCopy.Read(path).Config.Orbit.Bindings.Last().Action.Label=="変更" && !File.Exists(path+".tmp"),"draft updates atomically replace their previous content",r);
                read.Config.Items[0]=null; Check(Reject(delegate { WorkingCopy.Write(path,read); }),"malformed draft structure is rejected before replacement",r);
                read.Config=unfinished.Clone(); read.Config.GestureLibrary.Add(null); Check(Reject(delegate { WorkingCopy.Write(path,read); }),"malformed saved movement cannot enter a recovery draft",r);
                WorkingCopy.Delete(path); Check(WorkingCopy.Read(path)==null,"discarding a draft leaves no recovery data",r);
            } finally { WorkingCopy.Delete(path); if(File.Exists(path+".tmp")) File.Delete(path+".tmp"); }
            return r;
        }
    }
}

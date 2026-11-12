using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace OrbitRing {
    static class PlanetLinkTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(object target,string name) { return target.GetType().GetField(name,Private).GetValue(target); }
        static void Set(object target,string name,object value) { target.GetType().GetField(name,Private).SetValue(target,value); }
        static object Call(object target,string name,params object[] args) { return target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            var result=new List<string>(); Preferences config=Preferences.Defaults(); string original=Json.Write(config);
            ActionItem entry=ActionCatalog.Create().Single(p=>p.Item.Kind=="planet").Create();
            Check(entry.Value=="" && ExtendedActions.DefaultValue("planet")=="" && config.AllActions().All(a=>a.Kind!="planet") && Json.Write(Preferences.Defaults())==original,"planet opening is an unassigned action entry with no default gesture or target",result);
            Check(Reject(delegate { Preferences.ValidateAction(entry,Chord.Parse(config.Hotkey)); }) && PlanetLinks.Valid("main") && !PlanetLinks.Valid("planet 2") && !PlanetLinks.Valid("../main"),"planet action targets must be explicitly selected stable identifiers",result);
            config.Planets.Add(UserContent.NewPlanet(config)); config.Planets.Add(UserContent.NewPlanet(config));
            string reference=config.Planets[1].Id; entry.Value=reference; Preferences.ValidateAction(entry,Chord.Parse(config.Hotkey)); config.Orbit.Bindings[0].Action=entry;
            Check(PlanetLinks.Index(config,reference)==2 && !config.Planets[1].HotkeyEnabled && String.IsNullOrEmpty(config.Planets[1].Hotkey),"gesture targets can open an additional planet without enabling an individual shortcut",result);
            string[] codes,names; ActionOptions.Get("planet","",out codes,out names,config);
            Check(codes[0]=="" && names[0]=="開くplanetを選択" && codes.SequenceEqual(new[]{"","main",config.Planets[0].Id,reference}),"planet selector starts unassigned and lists every existing planet by its name",result);
            config.Planets[1].Name="変更後"; config.Planets.RemoveAt(0); config.Validate(); Preferences restored=config.Clone(); restored.Validate();
            Check(PlanetLinks.Index(restored,reference)==1 && restored.Orbit.Bindings[0].Action.Value==reference && restored.Planets[0].Name=="変更後","renaming and removing an earlier planet preserve gesture references across saving",result);
            restored.Planets.Clear(); restored.Validate(); ActionOptions.Get("planet",reference,out codes,out names,restored);
            Check(PlanetLinks.Index(restored,reference)==null && codes[0]==reference && names[0].Contains("削除された") && restored.Orbit.Bindings[0].Action.Value==reference,"deleted targets remain visibly unresolved without retargeting or resetting saved gestures",result);
            using(Controller owner=new Controller(true)) {
                config=Preferences.Defaults(); config.Planets.Add(UserContent.NewPlanet(config)); config.Planets.Add(UserContent.NewPlanet(config));
                Window target=new Window(); IntPtr hwnd=new WindowInteropHelper(target).EnsureHandle();
                try {
                    string executable=ExtendedActions.Executable(hwnd); var profile=new ApplicationSettings { Executable=executable,PlanetId=config.Planets[0].Id }; config.Applications.Add(profile); ApplicationProfiles.EnablePlanet(config,profile);
                    ApplicationProfiles.Items(config,executable,2)[0]=new ActionItem("選択したアプリ用の内容","shortcut","F8"); owner.Apply(config); string before=Json.Write(owner.Config); Set(owner,"target",hwnd);
                    Set(owner,"directPlanet",true); bool resolved=(bool)Call(owner,"ResolvePlanet",2);
                    Check(resolved && (int)Field(owner,"activePlanet")==2 && owner.ActiveItems[0].Label=="選択したアプリ用の内容" && owner.PlanetPage>=0 && !owner.PlanetPosition.Contains("アプリ専用"),"explicit gesture selection opens the requested planet while retaining that app's action overrides",result);
                    Set(owner,"directPlanet",false); resolved=(bool)Call(owner,"ResolvePlanet",2);
                    Check(resolved && (int)Field(owner,"activePlanet")==1,"ordinary hotkey invocation still follows the existing app assignment",result);
                    Check(Json.Write(owner.Config)==before,"planet resolution does not rewrite user settings or assign shortcuts",result);
                    owner.Config.Orbit.Exclude(executable); Set(owner,"directPlanet",true); Check(!(bool)Call(owner,"ResolvePlanet",2),"direct planet selection continues to honor planet exclusions",result);
                } finally { target.Close(); }
            }
            using(Controller owner=new Controller(true)) {
                config=Preferences.Defaults(); config.Planets.Add(UserContent.NewPlanet(config)); owner.Apply(config); string saved=Json.Write(owner.Config); int bindings=config.Orbit.Bindings.Count;
                SettingsWindow editor=new SettingsWindow(owner);
                try {
                    ComboBox kind=(ComboBox)Field(editor,"actionKind"); Check(!kind.Items.Cast<string>().Contains("planetを開く"),"the planet opening entry is scoped to the gesture editor",result);
                    editor.Navigate(1); kind=(ComboBox)Field(editor,"actionKind"); kind.SelectedItem="planetを開く";
                    ComboBox targets=(ComboBox)Field(editor,"extendedValues"); Preferences draft=(Preferences)Field(editor,"draft");
                    Check(draft.Orbit.Bindings[0].Action.Kind=="planet" && draft.Orbit.Bindings[0].Action.Value=="" && targets.SelectedIndex==0 && draft.Orbit.Bindings.Count==bindings,"choosing the new action does not preselect a planet or create another gesture",result);
                    targets.SelectedIndex=2; draft=(Preferences)Field(editor,"draft"); draft.Validate();
                    Check(draft.Orbit.Bindings[0].Action.Value==config.Planets[0].Id && Json.Write(owner.Config)==saved,"choosing a target edits only the selected gesture's draft until Save",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            OrbitEngine engine=(OrbitEngine)FormatterServices.GetUninitializedObject(typeof(OrbitEngine)); Set(engine,"session",7); Set(engine,"active",true); Func<bool> guard=engine.ActionGuard();
            Check(engine.TriggerDown && guard(),"queued gesture opening sees the hook's held trigger even when Windows key state was suppressed",result);
            Set(engine,"active",false); Check(!engine.TriggerDown && guard(),"releasing the trigger allows the same queued gesture to open its planet",result);
            Set(engine,"cancelled",true); Check(!guard(),"Escape cancellation invalidates a queued planet opening",result); Set(engine,"cancelled",false); Set(engine,"session",8); Check(!guard(),"a new gesture invalidates an earlier queued planet opening",result);
            return result;
        }
    }
}

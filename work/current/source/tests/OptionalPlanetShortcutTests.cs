using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class OptionalPlanetShortcutTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string text,List<string> result) { if(!value) throw new Exception(text); result.Add("PASS "+text); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static object Field(object owner,string name) { return owner.GetType().GetField(name,Private).GetValue(owner); }
        static void Call(object owner,string name,params object[] args) { owner.GetType().GetMethod(name,Private).Invoke(owner,args); }
        static IEnumerable<T> Find<T>(DependencyObject parent) where T:DependencyObject {
            if(parent is T) yield return (T)parent;
            foreach(object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject element=child as DependencyObject; if(element!=null) foreach(T item in Find<T>(element)) yield return item; }
        }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences config=Preferences.Defaults();
            for(int i=0;i<3;i++) config.Planets.Add(UserContent.NewPlanet(config)); config.Validate();
            Check(config.Planets.All(p=>!p.HotkeyEnabled && String.IsNullOrEmpty(p.Hotkey)) && UserContent.Hotkeys(config).SequenceEqual(new[]{config.Hotkey}),"new planets require no individual shortcut or reserved key combination",result);
            config.Planets[0].Hotkey="F13"; config.Planets[1].Hotkey="F14"; config.Planets[1].HotkeyEnabled=true; config.Planets[2].Hotkey="F15"; config.Planets[2].HotkeyEnabled=true; config.Validate();
            Check(UserContent.HotkeyBindings(config).Select(p=>p.Key).SequenceEqual(new[]{0,2,3}) && UserContent.Hotkeys(config).SequenceEqual(new[]{config.Hotkey,"F14","F15"}),"disabled shortcuts do not shift the runtime target of later enabled planets",result);
            Check(ApplicationProfiles.WheelPlanets(config,"unknown.exe",0).SequenceEqual(new[]{0,1,2,3}),"planets without individual shortcuts remain available through the wheel",result);
            config.Applications.Add(new ApplicationSettings { Executable="Claude.exe",PlanetId=config.Planets[0].Id });
            Check(ApplicationProfiles.RuntimePlanet(config,"claude.exe",0)==1,"application assignments can open a planet whose individual shortcut is disabled",result);
            Preferences copy=config.Clone(); copy.Validate(); Check(!copy.Planets[0].HotkeyEnabled && copy.Planets[0].Hotkey=="F13" && copy.Planets[1].HotkeyEnabled && copy.Planets[1].Hotkey=="F14","saving retains both the enable state and the chosen inactive key",result);
            copy.Planets[1].HotkeyEnabled=false; copy.Planets[2].Hotkey="F14"; copy.Validate(); copy.Planets[1].HotkeyEnabled=true;
            Check(Reject(copy.Validate),"only enabled individual shortcuts participate in collision checks",result);
            copy=config.Clone(); copy.Planets[0].Hotkey=""; copy.Planets[0].HotkeyEnabled=true; Check(Reject(copy.Validate),"enabling an individual shortcut requires a recorded or selected key",result);
            string legacy=Json.Write(config).Replace("\"HotkeyEnabled\":true,","").Replace("\"HotkeyEnabled\":false,","");
            Preferences imported=Json.Read<Preferences>(legacy); imported.Validate(); Check(imported.Planets.All(p=>p.HotkeyEnabled) && UserContent.Hotkeys(imported).Count()==4,"older saved shortcut assignments remain enabled without migration or rewriting settings",result);
            PlanetProfile duplicate=UserContent.NewPlanet(config,config.Items); Check(!duplicate.HotkeyEnabled && String.IsNullOrEmpty(duplicate.Hotkey),"duplicating a planet does not activate another shortcut",result);
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    typeof(SettingsWindow).GetField("editorPlanet",Private).SetValue(editor,1); Call(editor,"RefreshAll"); Call(editor,"ShowSide",6);
                    DockPanel panel=(DockPanel)Field(editor,"planetPanel"); CheckBox toggle=Find<CheckBox>(panel).Single(c=>c.Content as string=="個別ショートカットを使う");
                    TextBox key=Find<TextBox>(panel).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="planetの呼び出しショートカット"); StackPanel controls=(StackPanel)key.Parent;
                    Check(toggle.IsChecked==false && controls.Visibility==Visibility.Collapsed && key.Text=="F13","planet management makes disabled shortcuts explicit without showing inactive capture controls",result);
                    toggle.IsChecked=true; toggle.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent)); Preferences draft=(Preferences)Field(editor,"draft");
                    Check(draft.Planets[0].HotkeyEnabled && controls.Visibility==Visibility.Visible && key.IsReadOnly && !owner.Config.Planets[0].HotkeyEnabled,"enabling an individual key reveals recording controls and changes only the draft",result);
                    Call(editor,"Undo"); draft=(Preferences)Field(editor,"draft"); Check(!draft.Planets[0].HotkeyEnabled && draft.Planets[0].Hotkey=="F13","undo restores the disabled state without losing the stored key",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

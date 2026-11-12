using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace OrbitRing {
    static class FeatureSettingsTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static void Call(object target,string name,params object[] args) { target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static Preferences Draft(SettingsWindow editor) { return (Preferences)typeof(SettingsWindow).GetField("draft",Private).GetValue(editor); }
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject {
            if(root is T) yield return (T)root;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(T item in Children<T>(VisualTreeHelper.GetChild(root,i))) yield return item;
            if(root is ContentControl && ((ContentControl)root).Content is DependencyObject && VisualTreeHelper.GetChildrenCount(root)==0) foreach(T item in Children<T>((DependencyObject)((ContentControl)root).Content)) yield return item;
            if(root is Decorator && ((Decorator)root).Child!=null && VisualTreeHelper.GetChildrenCount(root)==0) foreach(T item in Children<T>(((Decorator)root).Child)) yield return item;
            if(root is Panel && VisualTreeHelper.GetChildrenCount(root)==0) foreach(UIElement child in ((Panel)root).Children) foreach(T item in Children<T>(child)) yield return item;
        }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults();
            Check(config.PlanetEnabled && config.Orbit.Enabled,"both input features are enabled in a new configuration",r);
            Preferences legacy=Json.Read<Preferences>(Json.Write(config).Replace("\"PlanetEnabled\":true,","").Replace("\"Enabled\":true,", "")); legacy.Validate();
            Check(legacy.PlanetEnabled && legacy.Orbit.Enabled,"legacy settings retain enabled features without requiring migration",r);
            foreach(bool planet in new[]{false,true}) foreach(bool orbit in new[]{false,true}) {
                config.PlanetEnabled=planet; config.Orbit.Enabled=orbit; Preferences restored=config.Clone(); restored.Validate();
                Check(restored.PlanetEnabled==planet && restored.Orbit.Enabled==orbit && restored.Items.Count==8 && restored.Orbit.Bindings.Count>0,"feature flags persist independently while retaining actions: "+planet+" / "+orbit,r);
            }
            using(Controller owner=new Controller(true)) {
                config=Preferences.Defaults(); config.PlanetEnabled=false; owner.Apply(config);
                Check(owner.OrbitAllowed,"disabling planet leaves orbit available",r);
                owner.SetShortcutRecording(true); Check(!owner.OrbitAllowed,"orbit dispatch is suspended during shortcut recording",r);
                owner.SetShortcutRecording(false); config.Orbit.Enabled=false; owner.Apply(config); Check(!owner.OrbitAllowed,"disabled orbit rejects pending gesture dispatch",r);
                SettingsWindow editor=new SettingsWindow(owner); editor.Navigate(3);
                CheckBox planet=Children<CheckBox>(editor.Content as DependencyObject).Single(c=>(string)c.Content=="planet（リング）を有効にする");
                planet.IsChecked=true; planet.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                Check(Draft(editor).PlanetEnabled && !Draft(editor).Orbit.Enabled,"the general settings checkbox changes only the selected feature",r);
                Call(editor,"Undo"); Check(!Draft(editor).PlanetEnabled && !Draft(editor).Orbit.Enabled,"undo restores the previous independent feature state",r);
                Call(editor,"Redo"); Check(Draft(editor).PlanetEnabled && !Draft(editor).Orbit.Enabled,"redo restores the independent feature change",r);
                new WindowInteropHelper(editor).EnsureHandle();
                TextBox first=(TextBox)typeof(SettingsWindow).GetField("hotkey",Private).GetValue(editor);
                FrameworkElement root=(FrameworkElement)editor.Content; Size bounds=new Size(1000,650); root.Measure(bounds); root.Arrange(new Rect(bounds)); root.UpdateLayout();
                Button capture=Children<Button>((DependencyObject)typeof(SettingsWindow).GetField("settingsPanel",Private).GetValue(editor)).First(b=>b.Content as string=="キーを記録");
                Point before=capture.TranslatePoint(new Point(),root); Size buttonSize=capture.RenderSize,fieldSize=first.RenderSize; Brush background=first.Background,foreground=first.Foreground; Thickness border=first.BorderThickness;
                Call(editor,"BeginKeyCapture",first,true); root.Measure(bounds); root.Arrange(new Rect(bounds)); root.UpdateLayout();
                Check(owner.ShortcutRecording && first.IsReadOnly,"starting key capture suspends invocation and protects the input text",r);
                Border recording=(Border)typeof(SettingsWindow).GetField("recordingBar",Private).GetValue(editor);
                Check(recording.Visibility==Visibility.Collapsed && !Object.ReferenceEquals(first.Background,background) && first.Foreground==EditorUi.Mint && first.BorderThickness==border,"key capture highlights only the exact input without adding a banner or changing its border size",r);
                Check((capture.TranslatePoint(new Point(),root)-before).Length<.01 && capture.RenderSize==buttonSize && first.RenderSize==fieldSize,"recording keeps the input and capture button at their original positions and sizes",r);
                editor.Navigate(0); Check(!owner.ShortcutRecording && first.IsReadOnly,"leaving settings cancels capture and keeps manual key entry disabled",r);
                Check(recording.Visibility==Visibility.Collapsed && first.BorderThickness==border && Object.ReferenceEquals(first.Background,background) && first.Foreground==foreground,"leaving the input restores its original appearance",r);
                editor.Navigate(3); TextBox second=(TextBox)typeof(SettingsWindow).GetField("hotkey",Private).GetValue(editor);
                Call(editor,"BeginKeyCapture",second,true);
                Check(!Object.ReferenceEquals(first,second) && owner.ShortcutRecording && second.IsReadOnly,"recording works on the rebuilt settings input after navigating back",r);
                Call(editor,"EndKeyCapture"); Check(!owner.ShortcutRecording && second.IsReadOnly,"ending capture restores normal shortcut handling while keeping the key display read-only",r);
                Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close();
            }
            using(Controller owner=new Controller(true)) {
                HwndSource source=new HwndSource(new HwndSourceParameters("Orbitals.FeatureTest") { ParentWindow=new IntPtr(-3),WindowStyle=0,Width=0,Height=0 });
                typeof(Controller).GetField("receiver",Private).SetValue(owner,source);
                ushort key=0;
                for(ushort candidate=0x87;candidate>=0x7C;candidate--) if(Native.RegisterHotKey(source.Handle,900,0x4007,candidate)) { key=candidate; Native.UnregisterHotKey(source.Handle,900); break; }
                Check(key!=0,"an unused native hotkey is available for feature lifecycle verification",r);
                config=Preferences.Defaults(); config.Hotkey="Ctrl+Alt+Shift+F"+(key-0x6F);
                bool registered=(bool)typeof(Controller).GetMethod("RegisterPlanetKeys",Private).Invoke(owner,new object[]{config});
                bool duplicate=Native.RegisterHotKey(source.Handle,900,0x4007,key); if(duplicate) Native.UnregisterHotKey(source.Handle,900);
                Check(registered && !duplicate,"enabled planet owns its native invocation hotkey",r);
                config.PlanetEnabled=false; Call(owner,"RegisterPlanetKeys",config);
                bool released=Native.RegisterHotKey(source.Handle,900,0x4007,key); if(released) Native.UnregisterHotKey(source.Handle,900);
                Check(released,"turning planet off releases its native hotkey for other applications",r);
                config.PlanetEnabled=true; owner.SetShortcutRecording(true); Call(owner,"RegisterPlanetKeys",config);
                released=Native.RegisterHotKey(source.Handle,900,0x4007,key); if(released) Native.UnregisterHotKey(source.Handle,900);
                Check(released,"invocation hotkeys remain unregistered throughout key recording",r);
                owner.SetShortcutRecording(false); Call(owner,"TogglePause"); Call(owner,"RegisterPlanetKeys",config);
                released=Native.RegisterHotKey(source.Handle,900,0x4007,key); if(released) Native.UnregisterHotKey(source.Handle,900);
                Check(released,"temporary pause releases planet hotkeys without changing the enabled preference",r);
                Call(owner,"TogglePause"); Call(owner,"RegisterPlanetKeys",config);
                bool acquired=Native.RegisterHotKey(source.Handle,900,0x4007,key); if(acquired) Native.UnregisterHotKey(source.Handle,900);
                Check(!acquired && config.PlanetEnabled,"resume restores the native invocation hotkey",r);
            }
            return r;
        }
    }
}

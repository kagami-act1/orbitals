using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class GestureEditorTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static T Field<T>(object target,string name) { return (T)target.GetType().GetField(name,Private).GetValue(target); }
        static object Call(object target,string name,params object[] args) { return target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        public static List<string> Run() {
            List<string> r=new List<string>();
            using(Controller owner=new Controller(true)) {
                SettingsWindow editor=new SettingsWindow(owner); editor.Navigate(1);
                ComboBox type=Field<ComboBox>(editor,"gestureType"),modifier=Field<ComboBox>(editor,"gestureModifier");
                GestureDrawing drawing=Field<GestureDrawing>(editor,"gesturePreview"); Preferences draft=Field<Preferences>(editor,"draft"); string action=Json.Write(draft.Orbit.Bindings[0].Action);
                Check(type.IsEnabled && modifier.IsEnabled && !drawing.DrawingEnabled && !Field<bool>(editor,"editingGesture"),"gesture conditions are editable while the drawing remains protected",r);
                Slider tolerance=Field<Slider>(editor,"gestureTolerance"); string shape=Json.Write(draft.Orbit.Bindings[0]);
                Check(tolerance.IsEnabled && tolerance.Visibility==Visibility.Visible,"recognition tolerance is adjustable without enabling shape editing",r);
                tolerance.Value=5;
                Check(draft.Orbit.Bindings[0].Tolerance==5 && !drawing.DrawingEnabled && Json.Write(draft.Orbit.Bindings[0].Action)==action,"adjusting tolerance preserves shape editing protection and the assigned action",r);
                bool ambiguous; System.Windows.Point[] stroke={new System.Windows.Point(200,0),new System.Windows.Point(100,35),new System.Windows.Point(0,70)};
                Check(GestureShape.Match(new[]{draft.Orbit.Bindings[0]},stroke,"",out ambiguous)==null,"the tolerance slider changes recognition strictness",r);
                Call(editor,"Undo"); draft=Field<Preferences>(editor,"draft"); type=Field<ComboBox>(editor,"gestureType"); modifier=Field<ComboBox>(editor,"gestureModifier"); drawing=Field<GestureDrawing>(editor,"gesturePreview"); tolerance=Field<Slider>(editor,"gestureTolerance");
                Check(Json.Write(draft.Orbit.Bindings[0])==shape && tolerance.IsEnabled,"undo restores the prior threshold without entering shape editing",r);
                Call(editor,"ToggleGestureTest"); tolerance.Value=35;
                Check(!tolerance.IsEnabled && draft.Orbit.Bindings[0].Tolerance==16,"recognition testing cannot change a saved movement threshold",r);
                Call(editor,"ToggleGestureTest"); tolerance.Value=23;
                Check(draft.Orbit.Bindings[0].Tolerance==23 && !drawing.DrawingEnabled,"leaving recognition testing makes the threshold immediately adjustable",r);
                modifier.SelectedIndex=2;
                Check(draft.Orbit.Bindings[0].Modifier=="Alt" && Field<int>(editor,"gestureIndex")==0 && !drawing.DrawingEnabled,"changing a modifier retains selection without enabling accidental drawing",r);
                foreach(int index in new[]{1,2,3}) {
                    type.SelectedIndex=index; string pattern=new[]{"","WheelUp","WheelDown","Rocker"}[index];
                    Check(!Field<Slider>(editor,"gestureTolerance").IsEnabled && Field<Slider>(editor,"gestureTolerance").Visibility==Visibility.Collapsed,"non-shape inputs do not expose an inapplicable recognition threshold: "+pattern,r);
                    Check(draft.Orbit.Bindings[0].Pattern==pattern && Json.Write(draft.Orbit.Bindings[0].Action)==action && type.SelectedIndex==index && modifier.SelectedIndex==2 && !drawing.DrawingEnabled,"changing gesture input preserves the assigned action and modifier: "+pattern,r);
                    Preferences saved=draft.Clone(); saved.Validate();
                    Check(saved.Orbit.Resolve("browser.exe",pattern,"Alt").Action.Value=="Alt+Left","changed button and wheel conditions survive saving and runtime resolution: "+pattern,r);
                }
                type.SelectedIndex=0;
                Check(drawing.DrawingEnabled && Field<bool>(editor,"editingGesture") && draft.Orbit.Bindings[0].Pattern=="","switching from button input to a shape opens drawing explicitly",r);
                drawing.Recorded(GestureShape.Template(new OrbitBinding { Pattern="L" }));
                Button done=Field<Button>(editor,"editGesture"); done.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(!drawing.DrawingEnabled && modifier.IsEnabled && type.IsEnabled && Json.Write(draft.Orbit.Bindings[0].Action)==action,"finishing a shape protects its geometry while keeping its conditions available",r);
                Check((bool)Call(editor,"Save") && owner.Config.Orbit.Bindings[0].Tolerance==23 && owner.Config.Orbit.Bindings[0].Pattern=="Shape" && owner.Config.Orbit.Bindings[0].Modifier=="Alt" && owner.Config.Orbit.Bindings[0].Action.Value=="Alt+Left","saving edited conditions applies the same custom action to the controller",r);
                Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close();
            }
            return r;
        }
    }
}

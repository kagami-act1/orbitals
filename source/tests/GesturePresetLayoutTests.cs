using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class GesturePresetLayoutTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(object owner,string name) { return owner.GetType().GetField(name,Private).GetValue(owner); }
        static void Call(object owner,string name,params object[] args) { owner.GetType().GetMethod(name,Private).Invoke(owner,args); }
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        static IEnumerable<Button> Cards(DependencyObject parent) {
            Button button=parent as Button; if(button!=null && button.Content is Grid) yield return button;
            foreach(object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject element=child as DependencyObject; if(element!=null) foreach(Button card in Cards(element)) yield return card; }
        }
        public static List<string> Run() {
            List<string> result=new List<string>(); using(Controller owner=new Controller(true)) {
                Preferences config=Preferences.Defaults(); config.Orbit.Bindings.Add(GesturePresets.Create().Single(p=>p.Id=="w").Create());
                config.GestureLibrary.Add(SavedGesture.From(GesturePresets.Create().Single(p=>p.Id=="c").Create(),"自分で登録した曲線")); owner.Apply(config);
                SettingsWindow editor=new SettingsWindow(owner);
                try {
                    editor.Navigate(1); Call(editor,"ShowSide",4); StackPanel rows=(StackPanel)Field(editor,"gesturePresetRows"); string saved=Json.Write((Preferences)Field(editor,"draft"));
                    foreach(Size size in new[]{new Size(1000,650),new Size(1320,800)}) {
                        FrameworkElement root=(FrameworkElement)editor.Content; root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout();
                        Button[] cards=Cards(rows).Where(b=>((Grid)b.Content).Children.OfType<GestureDrawing>().Any()).ToArray();
                        double[] icons=cards.Select(b=>((Grid)b.Content).Children.OfType<GestureDrawing>().Single().TranslatePoint(new Point(),rows).X).ToArray();
                        double[] labels=cards.Select(b=>((Grid)b.Content).Children.OfType<StackPanel>().Single().TranslatePoint(new Point(),rows).X).ToArray();
                        Check(cards.Length==28 && icons.Max()-icons.Min()<.1 && labels.Max()-labels.Min()<.1,"preset diagrams and text align in fixed columns across names registration states and custom rows: "+size,result);
                        Check(cards.All(b=>b.ActualHeight>=62 && ((Grid)b.Content).ActualWidth>=b.ActualWidth-20),"preset card content fills the available width and keeps a readable minimum height: "+size,result);
                    }
                    Check(Json.Write((Preferences)Field(editor,"draft"))==saved,"laying out presets does not rewrite gesture shapes or configured actions",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbitRing {
    sealed class EditorButtonRow : WrapPanel {
        protected override void OnVisualChildrenChanged(DependencyObject added,DependencyObject removed) {
            base.OnVisualChildrenChanged(added,removed);
            Button button=added as Button;
            if(button!=null) { Thickness margin=button.Margin; margin.Bottom=System.Math.Max(8,margin.Bottom); button.Margin=margin; }
        }
    }
    static class EditorUi {
        // The editor and the overlay share the same palette and controls.
        public static Brush Bg=Ui.Bg, Panel=Ui.Panel, Line=Ui.Line, Ink=Ui.Ink, Muted=Ui.Muted, Mint=Ui.Mint;
        public static Brush Brush(string color) { return Ui.Brush(color); }
        public static void Apply(Window window) {
            window.Resources["OrbitBg"]=Bg; window.Resources["OrbitPanel"]=Panel; window.Resources["OrbitLine"]=Line;
            window.Resources["OrbitInk"]=Ink; window.Resources["OrbitMuted"]=Muted; window.Resources["OrbitMint"]=Mint;
            window.Resources["OrbitSelected"]=Ui.Brush("#355747");
            using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitRing.EditorStyles"))
                window.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream));
        }
        public static TextBlock Text(string value,double size,Brush color) { return Ui.Text(value,size,color); }
        public static TextBox Input(string value) { return Ui.Input(value); }
        public static Button Button(string text,RoutedEventHandler action,bool primary) {
            return Ui.Button(text,action,primary);
        }
    }
}

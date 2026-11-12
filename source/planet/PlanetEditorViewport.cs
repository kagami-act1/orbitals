using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class PlanetEditorViewport : Grid {
        readonly LayoutEditor surface;
        readonly ScrollViewer scroll;
        readonly Viewbox view;
        readonly Button smaller,larger;
        readonly TextBlock amount;
        double zoom=1;
        bool panning;
        Point panStart;
        double panX,panY;
        int revision;
        public double Zoom { get { return zoom; } }
        public PlanetEditorViewport(LayoutEditor editor) {
            surface=editor; ClipToBounds=true;
            RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); RowDefinitions.Add(new RowDefinition());
            view=new Viewbox { Child=surface,Stretch=System.Windows.Media.Stretch.Uniform,StretchDirection=StretchDirection.Both,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center };
            scroll=new ScrollViewer { Content=view,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center }; SetRow(scroll,1); Children.Add(scroll);
            StackPanel tools=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(8),Background=EditorUi.Bg };
            smaller=Tool("−","編集画面を縮小",delegate { SetZoom(zoom/1.25); }); tools.Children.Add(smaller);
            Button fit=Tool("全体を表示","編集画面の全体を表示",delegate { SetZoom(1); }); tools.Children.Add(fit);
            larger=Tool("＋","編集画面を拡大",delegate { SetZoom(zoom*1.25); }); tools.Children.Add(larger);
            amount=EditorUi.Text("",11,EditorUi.Muted); amount.MinWidth=42; amount.VerticalAlignment=VerticalAlignment.Center; amount.Margin=new Thickness(6,0,6,0); tools.Children.Add(amount); Children.Add(tools);
            scroll.SizeChanged+=delegate { Resize(); }; surface.SizeChanged+=delegate { Resize(); }; Loaded+=delegate { Resize(); };
            surface.PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e) { if(Keyboard.Modifiers!=ModifierKeys.Control) return; SetZoom(zoom*(e.Delta>0?1.25:.8)); e.Handled=true; };
            surface.PreviewMouseDown+=delegate(object sender,MouseButtonEventArgs e) { if(e.ChangedButton!=MouseButton.Middle) return; panning=true; panStart=e.GetPosition(scroll); panX=scroll.HorizontalOffset; panY=scroll.VerticalOffset; surface.CaptureMouse(); e.Handled=true; };
            surface.PreviewMouseMove+=delegate(object sender,MouseEventArgs e) { if(!panning) return; Vector delta=e.GetPosition(scroll)-panStart; scroll.ScrollToHorizontalOffset(panX-delta.X); scroll.ScrollToVerticalOffset(panY-delta.Y); e.Handled=true; };
            surface.PreviewMouseUp+=delegate(object sender,MouseButtonEventArgs e) { if(!panning || e.ChangedButton!=MouseButton.Middle) return; panning=false; surface.ReleaseMouseCapture(); e.Handled=true; };
            surface.LostMouseCapture+=delegate { panning=false; };
            amount.ToolTip="Ctrl＋ホイールで拡大・縮小。中央ボタンを押してドラッグすると表示を移動できます。";
            SetZoom(1);
        }
        static Button Tool(string label,string name,RoutedEventHandler action) {
            Button button=EditorUi.Button(label,action,false); button.Padding=new Thickness(8,5,8,5); button.Margin=new Thickness(2); button.ToolTip=name; System.Windows.Automation.AutomationProperties.SetName(button,name); return button;
        }
        void SetZoom(double value) { zoom=Math.Max(1,Math.Min(4,value)); smaller.IsEnabled=zoom>1; larger.IsEnabled=zoom<4; amount.Text=Math.Round(zoom*100)+"％"; Resize(); }
        void Resize() {
            if(scroll.ActualWidth<30 || scroll.ActualHeight<30) return;
            double fit=Math.Min(1,Math.Min((scroll.ActualWidth-20)/surface.Width,(scroll.ActualHeight-20)/surface.Height));
            double width=surface.Width*fit*zoom,height=surface.Height*fit*zoom;
            double oldWidth=Double.IsNaN(view.Width)?width:view.Width,ratio=width/Math.Max(1,oldWidth),x=(scroll.HorizontalOffset+scroll.ActualWidth/2)*ratio-scroll.ActualWidth/2,y=(scroll.VerticalOffset+scroll.ActualHeight/2)*ratio-scroll.ActualHeight/2;
            view.Width=width; view.Height=height; int current=++revision;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate { if(current!=revision) return; scroll.ScrollToHorizontalOffset(zoom==1?0:x); scroll.ScrollToVerticalOffset(zoom==1?0:y); }));
        }
    }
}

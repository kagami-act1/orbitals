using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        Slider gestureTolerance;
        TextBlock gestureToleranceLabel;
        FrameworkElement BuildGestureTolerance() {
            StackPanel panel=new StackPanel { Margin=new Thickness(0,10,0,0) };
            gestureToleranceLabel=EditorUi.Text("",12,EditorUi.Muted); panel.Children.Add(gestureToleranceLabel);
            gestureTolerance=new Slider { Minimum=5,Maximum=35,TickFrequency=1,IsSnapToTickEnabled=true,IsMoveToPointEnabled=true,SmallChange=1,LargeChange=5,Margin=new Thickness(0,4,0,0),ToolTip="小さい値ほど、登録した形に近い場合だけ実行します。形全体のずれを比較します。" }; panel.Children.Add(gestureTolerance);
            System.Windows.Automation.AutomationProperties.SetName(gestureTolerance,"このジェスチャーの認識閾値");
            gestureTolerance.PreviewMouseLeftButtonDown+=delegate { sliderGesture=true; sliderHistory=false; };
            gestureTolerance.PreviewMouseLeftButtonUp+=delegate { sliderGesture=false; sliderHistory=false; };
            gestureTolerance.LostMouseCapture+=delegate { sliderGesture=false; sliderHistory=false; };
            gestureTolerance.ValueChanged+=delegate { if(updatingWorkspace || testingGesture || gestureIndex<0 || gestureIndex>=CurrentBindings.Count || !gestureTolerance.IsEnabled) return; int value=(int)gestureTolerance.Value; if(CurrentBindings[gestureIndex].Tolerance==value) return; ResizeCheckpoint(); CurrentBindings[gestureIndex].Tolerance=value; RefreshToleranceLabel(); Changed("このジェスチャーの認識閾値を変更しました。"); };
            return panel;
        }
        void RefreshToleranceLabel() { gestureToleranceLabel.Text="許容するずれ  "+(int)gestureTolerance.Value+"％   小さいほど厳密"; }
        void RefreshGestureTolerance(OrbitBinding binding,bool drawn) {
            gestureTolerance.Value=binding==null?16:binding.Tolerance; gestureTolerance.IsEnabled=binding!=null && drawn && !testingGesture;
            gestureTolerance.Visibility=drawn && !testingGesture?Visibility.Visible:Visibility.Collapsed; gestureToleranceLabel.Visibility=gestureTolerance.Visibility; RefreshToleranceLabel();
        }
    }
}

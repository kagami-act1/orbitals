using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        static bool IsBackgroundSource(DependencyObject source,DependencyObject boundary) {
            while(source!=null && source!=boundary) {
                if(source is ButtonBase || source is TextBoxBase || source is Selector || source is RangeBase || source is Thumb) return false;
                source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);
            }
            return source==boundary;
        }
        void EnableBackgroundReturn(UIElement area) {
            Point? start=null;
            area.PreviewMouseDown+=delegate(object sender,MouseButtonEventArgs e) { start=e.ChangedButton==MouseButton.Left && IsBackgroundSource(e.OriginalSource as DependencyObject,area)?(Point?)e.GetPosition(area):null; };
            area.PreviewMouseUp+=delegate(object sender,MouseButtonEventArgs e) {
                Point? pressed=start; start=null;
                if(page!=0 || e.ChangedButton!=MouseButton.Left || !pressed.HasValue || EditorDrag.Active || !IsBackgroundSource(e.OriginalSource as DependencyObject,area) || (e.GetPosition(area)-pressed.Value).LengthSquared>36) return;
                ShowSide(0);
            };
            area.LostMouseCapture+=delegate { start=null; };
        }
        void AddPanelClose(DockPanel heading,string name,int destination) {
            Button close=IconButton("\uE711",name,delegate { ShowSide(destination); });
            close.Margin=new Thickness(12,0,0,0); DockPanel.SetDock(close,Dock.Right); heading.Children.Add(close);
        }
        void CloseIconPicker() {
            EndKeyCapture();
            if(iconConversionChoice!=null) RefreshAll();
            else { iconMuteState=null; iconStage=-1; ShowSide(1); }
        }
    }
}

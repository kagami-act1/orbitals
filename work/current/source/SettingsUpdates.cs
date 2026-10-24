using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        TextBlock updateStatus;
        Button checkUpdate,openUpdate;
        Popup updatePopup;
        Button BuildUpdateButton() {
            checkUpdate=IconButton("\uE72C","更新を確認",async delegate { updatePopup.HorizontalOffset=checkUpdate.ActualWidth-318; updatePopup.IsOpen=true; await owner.CheckForUpdates(); RefreshUpdateSettings(); });
            checkUpdate.Margin=new Thickness(0,0,8,0);
            StackPanel panel=new StackPanel { Width=284 };
            panel.Children.Add(EditorUi.Text("現在のバージョン  "+UpdateService.Current.ToString(3),12,EditorUi.Muted));
            updateStatus=EditorUi.Text("",13,EditorUi.Ink); updateStatus.Margin=new Thickness(0,10,0,0); panel.Children.Add(updateStatus);
            openUpdate=EditorUi.Button("更新ページを開く",delegate { owner.OpenUpdatePage(owner.Config.Updates.Repository); updatePopup.IsOpen=false; },true); openUpdate.Margin=new Thickness(0,14,0,0); panel.Children.Add(openUpdate);
            updatePopup=new Popup { PlacementTarget=checkUpdate,Placement=PlacementMode.Bottom,VerticalOffset=8,StaysOpen=false,AllowsTransparency=true,
                Child=new Border { Width=318,Resources=Resources,Background=EditorUi.Panel,BorderBrush=EditorUi.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Child=panel } };
            RefreshUpdateSettings(); return checkUpdate;
        }
        void RefreshUpdateSettings() {
            if(updateStatus==null || checkUpdate==null || openUpdate==null) return;
            bool same=String.Equals(UpdateService.Repository(owner.Config.Updates.Repository),owner.UpdateRepository,StringComparison.OrdinalIgnoreCase);
            bool available=same && owner.LatestUpdate!=null && owner.LatestUpdate.Available;
            updateStatus.Text=same?owner.UpdateMessage??"更新を確認します。":"更新を確認します。";
            checkUpdate.IsEnabled=!owner.CheckingUpdate;
            checkUpdate.BorderBrush=available?EditorUi.Mint:EditorUi.Line;
            checkUpdate.ToolTip=owner.CheckingUpdate?"更新を確認しています…":available?"新しいバージョンがあります":"更新を確認";
            openUpdate.Visibility=available?Visibility.Visible:Visibility.Collapsed;
        }
    }
}

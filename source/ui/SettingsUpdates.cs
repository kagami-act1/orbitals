using System;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        TextBlock updateStatus;
        Button checkUpdate,openUpdate;
        void ClearUpdateSettings() { updateStatus=null; checkUpdate=null; openUpdate=null; }
        FrameworkElement BuildUpdateSettings() {
            StackPanel panel=new StackPanel();
            panel.Children.Add(EditorUi.Text("更新",19,EditorUi.Ink));
            TextBlock version=EditorUi.Text("現在のバージョン  "+UpdateService.Current.ToString(3),12,EditorUi.Muted); version.Margin=new Thickness(0,14,0,0); panel.Children.Add(version);
            EditorButtonRow buttons=new EditorButtonRow { Margin=new Thickness(0,14,0,0) }; panel.Children.Add(buttons);
            checkUpdate=EditorUi.Button("更新を確認",async delegate { await owner.CheckForUpdates(); RefreshUpdateSettings(); },true);
            System.Windows.Automation.AutomationProperties.SetName(checkUpdate,"更新を確認"); buttons.Children.Add(checkUpdate);
            openUpdate=EditorUi.Button("更新ページを開く",delegate { owner.OpenUpdatePage(owner.Config.Updates.Repository); },false); buttons.Children.Add(openUpdate);
            updateStatus=EditorUi.Text("",13,EditorUi.Ink); updateStatus.Margin=new Thickness(0,10,0,0); panel.Children.Add(updateStatus);
            RefreshUpdateSettings(); Border card=Card(panel); card.Margin=new Thickness(0,0,0,18); return card;
        }
        void RefreshUpdateSettings() {
            if(updateStatus==null || checkUpdate==null || openUpdate==null) return;
            bool same=String.Equals(UpdateService.Repository(owner.Config.Updates.Repository),owner.UpdateRepository,StringComparison.OrdinalIgnoreCase);
            bool available=same && owner.LatestUpdate!=null && owner.LatestUpdate.Available;
            updateStatus.Text=same?owner.UpdateMessage??"新しいバージョンがあるか確認します。":"新しいバージョンがあるか確認します。";
            checkUpdate.IsEnabled=!owner.CheckingUpdate;
            checkUpdate.Content=owner.CheckingUpdate?"確認中…":"更新を確認";
            checkUpdate.BorderBrush=available?EditorUi.Mint:EditorUi.Line;
            checkUpdate.ToolTip=owner.CheckingUpdate?"更新を確認しています…":available?"新しいバージョンがあります":"更新を確認";
            openUpdate.Visibility=available?Visibility.Visible:Visibility.Collapsed;
        }
    }
}

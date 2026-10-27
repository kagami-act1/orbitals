using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        StackPanel muteIconsPanel;
        bool? iconMuteState;
        void RefreshMuteIcons() {
            muteIconsPanel.Children.Clear();
            bool visible=page==0 && AudioMute.Supports(EditingAction); muteIconsPanel.Visibility=visible?Visibility.Visible:Visibility.Collapsed; if(!visible) return;
            muteIconsPanel.Children.Add(EditorUi.Text("状態ごとのアイコン・文字列",12,EditorUi.Muted));
            foreach(bool muted in new[]{true,false}) {
                bool state=muted; ActionItem action=AudioMute.StateAction(EditingAction,state);
                Button button=EditorUi.Button("",delegate { iconStage=-1; iconMuteState=state; ShowSide(3); },false); button.Margin=new Thickness(0,8,0,0); button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
                StackPanel content=new StackPanel { Orientation=Orientation.Horizontal }; FrameworkElement icon=ActionIcons.Visual(action,22); icon.Margin=new Thickness(0,0,10,0); content.Children.Add(icon);
                content.Children.Add(EditorUi.Text((state?"ミュートON":"ミュートOFF")+" の表示",13,EditorUi.Ink)); button.Content=content;
                System.Windows.Automation.AutomationProperties.SetName(button,(state?"ミュートON":"ミュートOFF")+"のアイコン・文字列を変更"); muteIconsPanel.Children.Add(button);
            }
        }
    }
}

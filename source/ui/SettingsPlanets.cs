using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        int editorPlanet;
        ComboBox planetSelector;
        Button planetAdd,planetManage;
        DockPanel planetPanel;
        void BuildPlanetSelector(DockPanel tools) {
            StackPanel controls=new StackPanel { Orientation=Orientation.Horizontal }; tools.Children.Add(controls);
            planetSelector=new ComboBox { Width=170,Padding=new Thickness(6),Margin=new Thickness(0,0,8,0) }; controls.Children.Add(planetSelector);
            System.Windows.Automation.AutomationProperties.SetName(planetSelector,"編集するplanet");
            planetSelector.SelectionChanged+=delegate { if(updatingWorkspace || planetSelector.SelectedIndex<0 || planetSelector.SelectedIndex==editorPlanet) return; editorPlanet=planetSelector.SelectedIndex; stackPath.Clear(); selected=0; RefreshAll(); ShowSide(0); };
            planetAdd=EditorUi.Button("＋ planet",delegate {
                try { PlanetProfile profile=UserContent.NewPlanet(draft); Remember(); draft.Planets.Add(profile); ApplicationProfiles.Synchronize(draft); editorPlanet=draft.Planets.Count; selected=0; stackPath.Clear(); RefreshAll(); ShowSide(6); Changed("planetを追加しました。個別ショートカットはOFFです。"); } catch(Exception ex) { Changed(ex.Message); }
            },false); planetAdd.Padding=new Thickness(10,6,10,6); controls.Children.Add(planetAdd);
            planetManage=EditorUi.Button("管理",delegate { ShowSide(6); },false); planetManage.Padding=new Thickness(10,6,10,6); controls.Children.Add(planetManage);
        }
        void UpdatePlanetSelector() {
            if(planetSelector==null) return; bool old=updatingWorkspace; updatingWorkspace=true;
            editorPlanet=Math.Max(0,Math.Min(draft.Planets.Count,editorPlanet));
            planetAdd.IsEnabled=planetManage.IsEnabled=CurrentApplication==null;
            planetSelector.ItemsSource=Enumerable.Range(0,draft.Planets.Count+1).Select(i=>UserContent.Name(draft,i)).ToArray(); planetSelector.SelectedIndex=editorPlanet; updatingWorkspace=old;
        }
        void BuildPlanetPanel() { planetPanel=new DockPanel(); side.Children.Add(planetPanel); }
        void RefreshPlanetPanel() {
            planetPanel.Children.Clear();
            StackPanel content=new StackPanel(); planetPanel.Children.Add(new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            DockPanel heading=new DockPanel { Margin=new Thickness(0,0,0,22) }; content.Children.Add(heading); AddPanelClose(heading,"planetの管理を閉じる",0);
            TextBlock title=EditorUi.Text("planetの管理",20,EditorUi.Ink); title.VerticalAlignment=VerticalAlignment.Center; heading.Children.Add(title);
            TextBox name=EditorUi.Input(UserContent.Name(draft,editorPlanet)); name.MaxLength=20;
            System.Windows.Automation.AutomationProperties.SetName(name,"planetの名前");
            TextBlock nameLabel=EditorUi.Text("名前",12,EditorUi.Muted); nameLabel.Margin=new Thickness(0,0,0,7); content.Children.Add(nameLabel); content.Children.Add(name);
            int index=editorPlanet;
            name.TextChanged+=delegate { if(UserContent.Name(draft,index)==name.Text) return; Remember(); if(index==0) draft.MainPlanetName=name.Text; else draft.Planets[index-1].Name=name.Text; UpdatePlanetSelector(); Changed("planetの名前を変更しました。"); };
            TextBlock keyLabel=EditorUi.Text("呼び出しショートカット",12,EditorUi.Muted); keyLabel.Margin=new Thickness(0,16,0,6); content.Children.Add(keyLabel);
            StackPanel keySettings=new StackPanel();
            if(index>0) {
                PlanetProfile profile=draft.Planets[index-1];
                CheckBox enabled=new CheckBox { Content="個別ショートカットを使う",IsChecked=profile.HotkeyEnabled,Foreground=EditorUi.Ink,Margin=new Thickness(0,6,0,12) }; content.Children.Add(enabled);
                keySettings.Visibility=profile.HotkeyEnabled?Visibility.Visible:Visibility.Collapsed;
                enabled.Click+=delegate { EndKeyCapture(); Remember(); profile.HotkeyEnabled=enabled.IsChecked==true; keySettings.Visibility=profile.HotkeyEnabled?Visibility.Visible:Visibility.Collapsed; Changed(profile.HotkeyEnabled?"個別ショートカットをONにしました。必要ならキーを記録して「保存」します。":"個別ショートカットをOFFにしました。ホイール切替やアプリ指定から開けます。"); };
                TextBlock hint=EditorUi.Text("個別キーを使わなくても、ホイール切替やアプリ指定から開けます。",12,EditorUi.Muted); hint.Margin=new Thickness(0,0,0,12); content.Children.Add(hint);
            }
            content.Children.Add(keySettings);
            TextBox key=RecordedKeyInput(index==0?draft.Hotkey:draft.Planets[index-1].Hotkey); System.Windows.Automation.AutomationProperties.SetName(key,"planetの呼び出しショートカット"); keySettings.Children.Add(key);
            key.TextChanged+=delegate { string old=index==0?draft.Hotkey:draft.Planets[index-1].Hotkey; if(old==key.Text) return; Remember(); if(index==0) draft.Hotkey=key.Text; else draft.Planets[index-1].Hotkey=key.Text; Changed("planetの呼び出しキーを変更しました。"); };
            FrameworkElement capture=InvocationKeyControls(key); capture.Margin=new Thickness(0,8,0,16); keySettings.Children.Add(capture);
            content.Children.Add(EditorUi.Text("リングの大きさと外観は共通です。変更は「保存」で反映します。",12,EditorUi.Muted));
            Button duplicate=EditorUi.Button("このplanetを複製",delegate {
                try { PlanetProfile profile=UserContent.NewPlanet(draft,UserContent.Items(draft,editorPlanet)); Remember(); draft.Planets.Add(profile); ApplicationProfiles.Synchronize(draft); editorPlanet=draft.Planets.Count; stackPath.Clear(); selected=0; RefreshAll(); ShowSide(6); Changed("planetを複製しました。"); } catch(Exception ex) { Changed(ex.Message); }
            },false); duplicate.Margin=new Thickness(0,20,0,8); content.Children.Add(duplicate);
            if(index>0) { Button remove=EditorUi.Button("このplanetを削除",delegate { Remember(); draft.Planets.RemoveAt(index-1); ApplicationProfiles.Synchronize(draft); editorPlanet=0; selected=0; stackPath.Clear(); RefreshAll(); ShowSide(0); Changed("planetを削除しました。「元に戻す」で戻せます。"); },false); remove.Margin=new Thickness(0); content.Children.Add(remove); }
        }
    }
}

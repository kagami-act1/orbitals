using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        FrameworkElement BuildAppsPage() {
            StackPanel panel=new StackPanel();
            StackPanel add=new StackPanel { Margin=new Thickness(0,0,0,18) }; panel.Children.Add(add);
            add.Children.Add(EditorUi.Text("ブラックリスト · "+draft.Orbit.Profiles.Count+"件",19,EditorUi.Ink));
            TextBlock help=EditorUi.Text("このアプリで使う機能にチェックを入れます。両方OFFにすると、どちらも反応しません。",13,EditorUi.Muted); help.Margin=new Thickness(0,8,0,14); add.Children.Add(help);
            DockPanel tools=new DockPanel(); add.Children.Add(tools);
            TextBox exe=EditorUi.Input(""); exe.MaxLength=260; exe.MinWidth=260;
            Button register=EditorUi.Button("＋ 追加",delegate {
                string value=exe.Text.Trim();
                if(!OrbitPreferences.ValidExecutable(value)) { Changed("実行ファイル名を入力してください。例: msedge.exe"); return; }
                if(draft.Orbit.Profile(value)!=null) { Changed("すでに登録されています。下の一覧でON / OFFを変更できます。"); return; }
                if(draft.Orbit.Profile(value)==null && draft.Orbit.Profiles.Count>=64) { Changed("ブラックリストは64件までです。"); return; }
                Remember(); draft.Orbit.Exclude(value); RefreshAll(); Changed(value+"を追加しました。「保存」で反映します。");
            },true); register.Margin=new Thickness(10,0,0,0); DockPanel.SetDock(register,Dock.Right); tools.Children.Add(register);
            Button browse=EditorUi.Button("exeを選ぶ",delegate { Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog { Filter="アプリ (*.exe)|*.exe" }; if(dialog.ShowDialog(this)==true) exe.Text=System.IO.Path.GetFileName(dialog.FileName); },false); browse.Margin=new Thickness(10,0,0,0); DockPanel.SetDock(browse,Dock.Right); tools.Children.Add(browse); tools.Children.Add(exe);
            StackPanel list=new StackPanel(); panel.Children.Add(list);
            var excluded=draft.Orbit.Profiles.OrderBy(p=>p.Executable,StringComparer.OrdinalIgnoreCase).ToList();
            if(excluded.Count==0) list.Children.Add(EditorUi.Text("アプリごとの制限はありません。",13,EditorUi.Muted));
            foreach(AppProfile app in excluded) {
                DockPanel row=new DockPanel();
                Button remove=EditorUi.Button("解除",delegate { Remember(); draft.Orbit.Profiles.Remove(app); RefreshAll(); Changed(app.Executable+"の除外を解除しました。「保存」で反映します。"); },false); remove.Margin=new Thickness(24,0,0,0); DockPanel.SetDock(remove,Dock.Right); row.Children.Add(remove);
                StackPanel label=new StackPanel { VerticalAlignment=VerticalAlignment.Center }; label.Children.Add(EditorUi.Text(app.Executable,17,EditorUi.Ink)); AddApplicationFeatureToggles(label,app.Executable); row.Children.Add(label);
                Border card=Card(row); card.Margin=new Thickness(0,0,0,10); list.Children.Add(card);
            }
            return panel;
        }
        void AddApplicationFeatureToggles(StackPanel parent,string executable) {
            WrapPanel row=new WrapPanel { Margin=new Thickness(0,10,0,0) }; parent.Children.Add(row);
            foreach(bool planet in new[]{true,false}) {
                bool ring=planet; string name=ring?"planet":"orbit";
                CheckBox toggle=new CheckBox { Content=name+"を使う",IsChecked=!(ring?draft.Orbit.IsPlanetExcluded(executable):draft.Orbit.IsOrbitExcluded(executable)),Foreground=EditorUi.Ink,Margin=new Thickness(0,0,22,0) };
                System.Windows.Automation.AutomationProperties.SetName(toggle,executable+"で"+name+"を使う"); row.Children.Add(toggle);
                toggle.Click+=delegate { try { Remember(); draft.Orbit.SetEnabled(executable,ring,toggle.IsChecked==true); RefreshAll(); Changed(executable+"の"+name+"を"+(toggle.IsChecked==true?"ON":"OFF")+"にしました。「保存」で反映します。"); } catch(Exception ex) { RefreshAll(); Changed(ex.Message); } };
            }
        }
    }
}

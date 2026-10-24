using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        Border BuildPluginSettings() {
            StackPanel panel=new StackPanel(); panel.Children.Add(EditorUi.Text("プラグイン",19,EditorUi.Ink));
            TextBlock help=EditorUi.Text("自作のアクション定義（JSON）を読み込みます。\n追加した操作はアクション一覧の「プラグイン」から配置できます。",13,EditorUi.Muted); help.Margin=new Thickness(0,12,0,12); panel.Children.Add(help);
            panel.Children.Add(EditorUi.Button("プラグインを読み込む",delegate { ImportPlugin(); },false));
            foreach(ActionPlugin plugin in draft.Plugins) {
                ActionPlugin entry=plugin; StackPanel item=new StackPanel { Margin=new Thickness(0,14,0,0) };
                CheckBox enabled=new CheckBox { Content=entry.Name,Foreground=EditorUi.Ink,IsChecked=entry.Enabled }; item.Children.Add(enabled);
                enabled.Click+=delegate { Remember(); entry.Enabled=enabled.IsChecked==true; Changed("プラグインの有効・無効を変更しました。「保存」で反映します。"); };
                TextBlock info=EditorUi.Text((String.IsNullOrEmpty(entry.Application)?"すべてのアプリ":entry.Application)+" · "+entry.Actions.Count+"アクション",12,EditorUi.Muted); info.Margin=new Thickness(0,6,0,6); item.Children.Add(info);
                item.Children.Add(EditorUi.Button("登録を外す",delegate { Remember(); draft.Plugins.Remove(entry); BuildGeneralSettings(); Changed("プラグインの登録を外しました。配置済みの枠は残ります。"); },false)); panel.Children.Add(item);
            }
            TextBlock note=EditorUi.Text("プログラムを含むプラグインは、アクション実行時だけ起動します。実行プログラムは通常のアプリと同じ権限で動作します。",12,EditorUi.Muted); note.Margin=new Thickness(0,12,0,0); panel.Children.Add(note);
            Border card=Card(panel); card.Margin=new Thickness(0,18,0,0); return card;
        }
        void ImportPlugin() {
            try {
                Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog { Title="プラグインの定義を読み込む",Filter="プラグイン定義 (*.json)|*.json",CheckFileExists=true };
                if(dialog.ShowDialog(this)!=true) return;
                AddPlugin(ActionPlugins.Import(dialog.FileName,Chord.Parse(draft.Hotkey)));
            } catch(Exception ex) { OrbitDialog.Info(this,"読み込めませんでした",ex.Message); }
        }
        void AddPlugin(ActionPlugin plugin) {
            ActionPlugins.Validate(plugin,Chord.Parse(draft.Hotkey)); int index=draft.Plugins.FindIndex(p=>p.Id==plugin.Id);
            if(index<0 && draft.Plugins.Count>=ActionPlugins.Maximum) throw new Exception("プラグインは16件まで登録できます。");
            Remember(); if(index<0) draft.Plugins.Add(plugin); else draft.Plugins[index]=plugin;
            BuildGeneralSettings(); Changed("「"+plugin.Name+"」を"+(index<0?"追加":"更新")+"しました。「保存」で反映します。");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        static readonly string[] StageKinds={"shortcut","open","macro","volume","mute","mute","window","launch","text","search","pasteplain","microphone","output","orbitals","wheel","audiosession","audiodevice","window-size","stamp","settings"};
        static readonly string[] StageNames={"キー / ショートカット","ファイル・フォルダー・URL","マクロ","ミュート切替","ミュートON","ミュートOFF","ウィンドウ操作","アプリを起動 / 前面へ","定型文を入力","選択した文字を検索","書式なし貼り付け","マイクのミュート切替","音声出力先の選択画面","orbitalsの操作","スクロール・ズーム","対象アプリの音量","音声出力先を直接切替","ウィンドウのサイズ指定","日付・時刻を入力","Windowsの設定を開く"};
        StackPanel togglePanel;
        int iconStage=-1, expandedStage=0;
        ActionItem IconTarget() {
            ActionItem item=EditingAction;
            if(iconMuteState.HasValue && AudioMute.Supports(item)) return AudioMute.StateAction(item,iconMuteState.Value);
            if(iconStage>=0 && item.Kind=="toggle" && iconStage<item.Toggle.Stages.Count) return item.Toggle.Stages[iconStage].Action();
            iconStage=-1; return item;
        }
        void ToggleChanged() { RefreshIcons(); RefreshSurface(); Changed("トグルを変更しました。「保存」で反映します。"); }
        void RenderToggle() {
            keyCapture=null; togglePanel.Children.Clear();
            ToggleDefinition toggle=EditingAction.Toggle;
            if(toggle==null) return;
            StackPanel initial=Section("初期状態の名前");
            TextBox initialName=EditorUi.Input(toggle.InitialLabel); initialName.MaxLength=20; initial.Children.Add(initialName);
            initialName.TextChanged+=delegate { if(toggle.InitialLabel==initialName.Text) return; Remember(); toggle.InitialLabel=initialName.Text; ToggleChanged(); };
            initial.Margin=new Thickness(0,0,0,12); togglePanel.Children.Add(initial);
            TextBlock hint=EditorUi.Text("実行するたびに上から順に進み、最後の次は1回目に戻ります。"+(page==0?"アイコンは実行後の状態を表します。":""),12,EditorUi.Muted);
            hint.Margin=new Thickness(0,0,0,12); togglePanel.Children.Add(hint);
            for(int i=0;i<toggle.Stages.Count;i++) {
                int index=i; ToggleStage stage=toggle.Stages[i];
                StackPanel body=new StackPanel { Margin=new Thickness(0,12,0,0) };
                Expander section=new Expander { IsExpanded=i==expandedStage, Content=body, Padding=new Thickness(10), Background=EditorUi.Panel, BorderBrush=EditorUi.Line, BorderThickness=new Thickness(1), Margin=new Thickness(0,0,0,10) };
                StackPanel heading=new StackPanel { Orientation=Orientation.Horizontal };
                if(page==0) { FrameworkElement stateIcon=ActionIcons.Visual(stage.Action(),24); stateIcon.Margin=new Thickness(0,0,10,0); heading.Children.Add(stateIcon); }
                TextBlock title=EditorUi.Text((i+1)+"回目 → "+stage.Label,14,EditorUi.Ink); title.MaxWidth=190; title.VerticalAlignment=VerticalAlignment.Center; heading.Children.Add(title); section.Header=heading;
                section.Expanded+=delegate { expandedStage=index; };
                StackPanel label=Section("実行後の状態名"); TextBox stateName=EditorUi.Input(stage.Label); stateName.MaxLength=20; label.Children.Add(stateName); body.Children.Add(label);
                stateName.TextChanged+=delegate { if(stage.Label==stateName.Text) return; Remember(); stage.Label=stateName.Text; title.Text=(index+1)+"回目 → "+stage.Label; ToggleChanged(); };
                if(page==0 && !AudioMute.IsToggle(EditingAction)) { Button icon=EditorUi.Button("この状態のアイコンを変更",delegate { iconMuteState=null; iconStage=index; ShowSide(3); },false); icon.Margin=new Thickness(0,8,0,12); body.Children.Add(icon); }
                ComboBox kind=new ComboBox { ItemsSource=StageNames, SelectedIndex=stage.Kind=="mute"?(stage.Value=="on"?4:5):Array.IndexOf(StageKinds,stage.Kind), Padding=new Thickness(8), FontSize=13, Margin=new Thickness(0,0,0,10) }; body.Children.Add(kind);
                kind.SelectionChanged+=delegate {
                    if(kind.SelectedIndex<0) return; Remember(); stage.Kind=StageKinds[kind.SelectedIndex];
                    stage.Value=stage.Kind=="mute"?(kind.SelectedIndex==4?"on":"off"):stage.Kind=="macro"?"Space\nwait:200\nSpace":ExtendedActions.DefaultValue(stage.Kind); expandedStage=index; RenderToggle(); ToggleChanged();
                };
                if(stage.Kind=="macro") BuildToggleMacro(body,stage,index);
                else if(stage.Kind!="volume" && stage.Kind!="mute") {
                    TextBox value=stage.Kind=="shortcut"?RecordedKeyInput(stage.Value):EditorUi.Input(stage.Value); value.MaxLength=stage.Kind=="shortcut"?48:4096; value.AcceptsReturn=stage.Kind=="text"; value.MinHeight=stage.Kind=="text"?80:0;
                    System.Windows.Automation.AutomationProperties.SetName(value,(i+1)+"回目の操作"); body.Children.Add(value);
                    value.TextChanged+=delegate { if(stage.Value==value.Text) return; Remember(); stage.Value=value.Text; ToggleChanged(); };
                    string[] codes,names; ActionOptions.Get(stage.Kind,stage.Value,out codes,out names,draft);
                    if(codes!=null) {
                        ComboBox command=new ComboBox { ItemsSource=names,SelectedIndex=Array.IndexOf(codes,stage.Value),Padding=new Thickness(8) }; body.Children.Add(command); value.Visibility=Visibility.Collapsed;
                        command.SelectionChanged+=delegate { if(command.SelectedIndex>=0) value.Text=codes[command.SelectedIndex]; };
                    }
                    if(stage.Kind=="pasteplain" || stage.Kind=="microphone" || stage.Kind=="output") value.Visibility=Visibility.Collapsed;
                    Button record=EditorUi.Button(stage.Kind=="open" || stage.Kind=="launch"?"ファイルを選ぶ…":"キーを記録",delegate {
                        if(stage.Kind=="open" || stage.Kind=="launch") { Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog(); if(dialog.ShowDialog(this)==true) value.Text=dialog.FileName; }
                        else BeginKeyCapture(value);
                    },false); record.Margin=new Thickness(0,8,0,0); if(stage.Kind=="shortcut" || stage.Kind=="open" || stage.Kind=="launch") body.Children.Add(record);
                }
                StackPanel controls=new StackPanel { Orientation=Orientation.Horizontal, Margin=new Thickness(0,14,0,0) };
                Button up=IconButton("\uE70E","この段階を上へ",delegate { Remember(); ToggleStage other=toggle.Stages[index-1]; toggle.Stages[index-1]=stage; toggle.Stages[index]=other; expandedStage=index-1; iconStage=-1; RenderToggle(); ToggleChanged(); }); up.IsEnabled=i>0; controls.Children.Add(up);
                Button down=IconButton("\uE70D","この段階を下へ",delegate { Remember(); ToggleStage other=toggle.Stages[index+1]; toggle.Stages[index+1]=stage; toggle.Stages[index]=other; expandedStage=index+1; iconStage=-1; RenderToggle(); ToggleChanged(); }); down.IsEnabled=i<toggle.Stages.Count-1; controls.Children.Add(down);
                Button remove=IconButton("\uE74D","この段階を削除",delegate { Remember(); toggle.Stages.RemoveAt(index); expandedStage=Math.Min(index,toggle.Stages.Count-1); iconStage=-1; RenderToggle(); ToggleChanged(); }); remove.IsEnabled=toggle.Stages.Count>2; controls.Children.Add(remove); body.Children.Add(controls);
                togglePanel.Children.Add(section);
            }
            Button add=EditorUi.Button("＋ 段階を追加",delegate {
                Remember(); toggle.Stages.Add(new ToggleStage { Label="状態 "+(toggle.Stages.Count+1), Kind="shortcut", Value="", Icon="ms:toggle_on" });
                expandedStage=toggle.Stages.Count-1; RenderToggle(); ToggleChanged();
            },false); add.IsEnabled=toggle.Stages.Count<16; add.Margin=new Thickness(0,0,0,12); togglePanel.Children.Add(add);
            togglePanel.Children.Add(EditorUi.Text("通常のトグルは起動時に初期状態へ戻ります。音量・マイクのミュートON／OFFの2段階はWindowsの状態と同期します。",11,EditorUi.Muted));
            Button reset=EditorUi.Button("すべてのトグルを初期状態に戻す",delegate { owner.ResetToggles(); Changed("トグルの順番を初期状態に戻しました。外部アプリの操作は実行していません。"); },false); reset.Margin=new Thickness(0,10,0,0); togglePanel.Children.Add(reset);
        }
        void BuildToggleMacro(StackPanel parent,ToggleStage stage,int stageIndex) {
            List<string> lines=new List<string>((stage.Value??"").Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));
            Action commit=delegate { string value=String.Join("\n",lines); if(value==stage.Value) return; Remember(); stage.Value=value; ToggleChanged(); };
            for(int i=0;i<lines.Count;i++) {
                int index=i; bool wait=lines[i].StartsWith("wait:",StringComparison.OrdinalIgnoreCase);
                StackPanel row=new StackPanel { Margin=new Thickness(0,0,0,10) };
                row.Children.Add(EditorUi.Text((i+1)+" · "+(wait?"待機 (ms)":"キー"),11,EditorUi.Muted));
                TextBox input=wait?EditorUi.Input(lines[i].Substring(5)):RecordedKeyInput(lines[i]); input.MaxLength=wait?5:48; System.Windows.Automation.AutomationProperties.SetName(input,(stageIndex+1)+"回目のステップ"+(i+1)+(wait?"の待機時間":"のキー")); row.Children.Add(input);
                input.TextChanged+=delegate { lines[index]=(wait?"wait:":"")+input.Text; commit(); };
                StackPanel tools=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,6,0,0) }; row.Children.Add(tools);
                if(!wait) { Button record=EditorUi.Button("記録",delegate { BeginKeyCapture(input); },false); record.Padding=new Thickness(8,5,8,5); tools.Children.Add(record); }
                Button up=IconButton("\uE70E","マクロのステップを上へ",delegate { string other=lines[index-1]; lines[index-1]=lines[index]; lines[index]=other; commit(); expandedStage=stageIndex; RenderToggle(); }); up.IsEnabled=index>0; tools.Children.Add(up);
                Button down=IconButton("\uE70D","マクロのステップを下へ",delegate { string other=lines[index+1]; lines[index+1]=lines[index]; lines[index]=other; commit(); expandedStage=stageIndex; RenderToggle(); }); down.IsEnabled=index<lines.Count-1; tools.Children.Add(down);
                tools.Children.Add(IconButton("\uE74D","マクロのステップを削除",delegate { lines.RemoveAt(index); commit(); expandedStage=stageIndex; RenderToggle(); })); parent.Children.Add(row);
            }
            StackPanel add=new StackPanel { Orientation=Orientation.Horizontal };
            Button key=EditorUi.Button("＋ キー",delegate { lines.Add("Space"); commit(); expandedStage=stageIndex; RenderToggle(); },false);
            Button delay=EditorUi.Button("＋ 待機",delegate { lines.Add("wait:200"); commit(); expandedStage=stageIndex; RenderToggle(); },false);
            key.IsEnabled=delay.IsEnabled=lines.Count<64; key.Padding=delay.Padding=new Thickness(10,7,10,7); add.Children.Add(key); add.Children.Add(delay); parent.Children.Add(add);
        }
    }
}

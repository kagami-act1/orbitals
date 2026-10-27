using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        void LoadMacro() {
            ActionItem item=EditingAction;
            BuildMacroEditor(macroRows,item.Value,delegate(string value) { if(value==item.Value) return; Remember(); item.Value=value; Changed("マクロを変更しました。「保存」で反映します。"); });
        }
        TextBox MacroTextInput(StackPanel parent,string label,string value,int limit,Action<string> changed) {
            TextBlock caption=EditorUi.Text(label,11,EditorUi.Muted); caption.Margin=new Thickness(0,8,0,5); parent.Children.Add(caption);
            TextBox input=EditorUi.Input(value); input.MaxLength=limit; parent.Children.Add(input); System.Windows.Automation.AutomationProperties.SetName(input,label);
            input.TextChanged+=delegate { changed(input.Text); }; return input;
        }
        void BuildMacroEditor(StackPanel parent,string text,Action<string> changed) {
            parent.Children.Clear(); List<MacroStep> steps;
            try { steps=MacroDefinition.Read(text); } catch(Exception ex) { parent.Children.Add(EditorUi.Text(ex.Message,12,EditorUi.Muted)); return; }
            StackPanel rows=new StackPanel(); parent.Children.Add(rows);
            Action commit=delegate { changed(MacroDefinition.Write(steps)); };
            Action render=null;
            render=delegate {
                EndKeyCapture(); rows.Children.Clear();
                for(int i=0;i<steps.Count;i++) {
                    int index=i; MacroStep step=steps[i]; StackPanel body=new StackPanel();
                    DockPanel head=new DockPanel { Margin=new Thickness(0,0,0,8) }; body.Children.Add(head);
                    StackPanel tools=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(tools,Dock.Right); head.Children.Add(tools);
                    Button up=IconButton("\uE70E","ステップを上へ",delegate { MacroStep other=steps[index-1]; steps[index-1]=step; steps[index]=other; commit(); render(); }); up.IsEnabled=i>0; tools.Children.Add(up);
                    Button down=IconButton("\uE70D","ステップを下へ",delegate { MacroStep other=steps[index+1]; steps[index+1]=step; steps[index]=other; commit(); render(); }); down.IsEnabled=i<steps.Count-1; tools.Children.Add(down);
                    Button remove=IconButton("\uE74D","ステップを削除",delegate { steps.RemoveAt(index); commit(); render(); }); remove.Margin=new Thickness(0); tools.Children.Add(remove);
                    TextBlock number=EditorUi.Text("ステップ "+(i+1),12,EditorUi.Mint); number.VerticalAlignment=VerticalAlignment.Center; head.Children.Add(number);
                    ComboBox kind=new ComboBox { ItemsSource=MacroDefinition.Names,SelectedIndex=Array.IndexOf(MacroDefinition.Kinds,step.Kind),Padding=new Thickness(7),Margin=new Thickness(0,0,0,6) }; body.Children.Add(kind);
                    System.Windows.Automation.AutomationProperties.SetName(kind,"ステップ"+(i+1)+"の操作の種類");
                    kind.SelectionChanged+=delegate { if(kind.SelectedIndex<0) return; steps[index]=MacroDefinition.Create(MacroDefinition.Kinds[kind.SelectedIndex]); commit(); render(); };
                    if(step.Kind=="shortcut") {
                        TextBox input=RecordedKeyInput(step.Value); body.Children.Add(input); System.Windows.Automation.AutomationProperties.SetName(input,"ステップ"+(i+1)+"のキー"); input.TextChanged+=delegate { step.Value=input.Text; commit(); };
                        Button capture=EditorUi.Button("キーを記録",delegate { BeginKeyCapture(input); },false); capture.Margin=new Thickness(0,8,0,0); body.Children.Add(capture);
                    } else if(step.Kind=="wait") MacroTextInput(body,"待機時間（ms）",step.Delay.ToString(),5,delegate(string value) { int delay; Int32.TryParse(value,out delay); step.Delay=delay; commit(); });
                    else if(step.Kind=="wait-window") {
                        TextBox app=MacroTextInput(body,"アプリの実行ファイル名（任意）",step.Application,260,delegate(string value) { step.Application=value; commit(); });
                        Button browse=EditorUi.Button("exeを選ぶ…",delegate { var dialog=new Microsoft.Win32.OpenFileDialog { Filter="アプリ (*.exe)|*.exe",CheckFileExists=true }; if(dialog.ShowDialog(this)==true) app.Text=System.IO.Path.GetFileName(dialog.FileName); },false); browse.Margin=new Thickness(0,8,0,0); body.Children.Add(browse);
                        MacroTextInput(body,"ウィンドウのタイトルに含む文字（任意）",step.Title,512,delegate(string value) { step.Title=value; commit(); });
                        MacroTextInput(body,"待機の上限（ms）",step.Timeout.ToString(),5,delegate(string value) { int timeout; Int32.TryParse(value,out timeout); step.Timeout=timeout; commit(); });
                        TextBlock hint=EditorUi.Text("指定した条件のウィンドウが前面に出るまで待ち、そのウィンドウを以降の操作先にします。条件は1つ以上指定します。",11,EditorUi.Muted); hint.Margin=new Thickness(0,8,0,0); body.Children.Add(hint);
                    } else if(step.Kind!="volume") {
                        string[] codes,names; ActionOptions.Get(step.Kind,step.Value,out codes,out names,draft);
                        if(step.Kind=="mute") { codes=new[]{"on","off"}; names=new[]{"ミュートON","ミュートOFF"}; }
                        if(codes!=null) { ComboBox choice=new ComboBox { ItemsSource=names,SelectedIndex=Array.IndexOf(codes,step.Value),Padding=new Thickness(7) }; body.Children.Add(choice); string[] values=codes; choice.SelectionChanged+=delegate { if(choice.SelectedIndex>=0) { step.Value=values[choice.SelectedIndex]; commit(); } }; }
                        else {
                            TextBox input=MacroTextInput(body,step.Kind=="text"?"入力する文字":"実行する内容",step.Value,step.Kind=="text"?4096:8192,delegate(string value) { step.Value=value; commit(); });
                            if(step.Kind=="text") {
                                input.AcceptsReturn=true; input.TextWrapping=TextWrapping.Wrap; input.MinHeight=80; input.MaxHeight=180; input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
                                WrapPanel tokens=new WrapPanel { Margin=new Thickness(0,8,0,0) }; body.Children.Add(tokens);
                                foreach(string token in new[]{"{date}","{time}","{clipboard}"}) { string insert=token; Button add=EditorUi.Button(token=="{date}"?"日付":token=="{time}"?"時刻":"クリップボード",delegate { input.SelectedText=insert; input.CaretIndex=input.SelectionStart+input.SelectionLength; input.Focus(); },false); add.Padding=new Thickness(8,5,8,5); add.Margin=new Thickness(0,0,8,8); tokens.Children.Add(add); }
                            } else if(step.Kind=="launch" || step.Kind=="open") { Button browse=EditorUi.Button("ファイルを選ぶ…",delegate { var dialog=ActionFileDialog(step.Kind); if(dialog.ShowDialog(this)==true) input.Text=dialog.FileName; },false); browse.Margin=new Thickness(0,8,0,0); body.Children.Add(browse); TextBlock hint=EditorUi.Text("開いたアプリで操作を続ける場合は、次に「操作先のウィンドウを待つ」を追加します。",11,EditorUi.Muted); hint.Margin=new Thickness(0,8,0,0); body.Children.Add(hint); }
                        }
                    }
                    WrapPanel tests=new WrapPanel { Margin=new Thickness(0,12,0,0) }; body.Children.Add(tests);
                    foreach(bool single in new[]{true,false}) { bool only=single; Button test=EditorUi.Button(single?"このステップを試す":"ここから試す",delegate { EndKeyCapture(); string request=MacroDefinition.Write(only?new[]{steps[index]}:steps.Skip(index)); owner.TestMacro(request,delegate(string message) { Changed(message); }); },false); test.Padding=new Thickness(8,6,8,6); test.Margin=new Thickness(0,0,8,6); tests.Children.Add(test); }
                    Border card=Card(body); card.Padding=new Thickness(12); card.Margin=new Thickness(0,0,0,12); rows.Children.Add(card);
                }
            };
            render();
            TextBlock addLabel=EditorUi.Text("追加するステップ",12,EditorUi.Muted); addLabel.Margin=new Thickness(0,6,0,6); parent.Children.Add(addLabel);
            ComboBox addKind=new ComboBox { ItemsSource=MacroDefinition.Names,SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,0,0,8) }; parent.Children.Add(addKind);
            Button append=EditorUi.Button("＋ ステップを追加",delegate { if(steps.Count>=64) { Changed("ステップは64個までです。"); return; } steps.Add(MacroDefinition.Create(MacroDefinition.Kinds[Math.Max(0,addKind.SelectedIndex)])); commit(); render(); },false); append.Margin=new Thickness(0,0,0,8); parent.Children.Add(append);
            TextBlock testHint=EditorUi.Text("テストは3秒後に前面のアプリで実行します。Escで中止できます。待機は合計60秒までです。",11,EditorUi.Muted); parent.Children.Add(testHint);
        }
    }
}

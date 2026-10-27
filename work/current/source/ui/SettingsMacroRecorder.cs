using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        KeyboardMacroRecorder macroRecorder;
        DispatcherTimer macroRecordTimer;
        DateTime macroRecordStart;
        ActionItem macroRecordTarget;
        Button macroRecordButton;
        TextBlock macroRecordHint;
        TextBlock recordingStatus;
        Border recordingBar;
        StackPanel bottomButtons;
        int recordingVersion;
        void BuildRecordingBar(DockPanel root) {
            DockPanel content=new DockPanel(); StackPanel buttons=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(buttons,Dock.Right); content.Children.Add(buttons);
            buttons.Children.Add(EditorUi.Button("完了",delegate { StopMacroRecording(false); },true)); buttons.Children.Add(EditorUi.Button("中止",delegate { StopMacroRecording(true); },false));
            recordingStatus=EditorUi.Text("",13,EditorUi.Mint); recordingStatus.VerticalAlignment=VerticalAlignment.Center; content.Children.Add(recordingStatus);
            recordingBar=Card(content); recordingBar.Padding=new Thickness(12); recordingBar.Margin=new Thickness(0,0,0,14); recordingBar.BorderBrush=EditorUi.Mint; recordingBar.Background=EditorUi.Brush("#203B32"); recordingBar.Visibility=Visibility.Collapsed; DockPanel.SetDock(recordingBar,Dock.Top); root.Children.Add(recordingBar);
        }
        void BuildMacroRecorder(StackPanel parent) {
            macroRecordButton=EditorUi.Button("連続したキー操作を記録",delegate { if(macroRecordTimer!=null) StopMacroRecording(false); else StartMacroRecording(); },false); parent.Children.Add(macroRecordButton);
            macroRecordHint=EditorUi.Text("キー操作と待ち時間を記録します。マウス操作は含みません。開始は3秒後。F12で完了、Escで中止します。",11,EditorUi.Muted); macroRecordHint.Margin=new Thickness(0,8,0,12); parent.Children.Add(macroRecordHint);
        }
        void StartMacroRecording() {
            EndKeyCapture();
            if(EditingAction.Kind!="macro") return;
            macroRecordTarget=EditingAction; macroRecordStart=DateTime.UtcNow.AddSeconds(3); owner.MacroRecording=true;
            int version=++recordingVersion; recordingBar.Visibility=Visibility.Visible; workspace.IsEnabled=false; bottomButtons.IsEnabled=false; foreach(Button button in navigation) button.IsEnabled=false; navUndo.IsEnabled=false; navRedo.IsEnabled=false;
            macroRecordButton.Content="記録を中止"; macroRecordHint.Text="3秒後に記録を開始します。操作したいアプリへ移ってください。";
            recordingStatus.Text=macroRecordHint.Text;
            macroRecordTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
            macroRecordTimer.Tick+=delegate {
                if(DateTime.UtcNow<macroRecordStart) { recordingStatus.Text=macroRecordHint.Text=Math.Ceiling((macroRecordStart-DateTime.UtcNow).TotalSeconds)+"秒後に記録を開始します。操作するアプリへ移ってください。"; return; }
                if(macroRecorder==null) {
                    try { macroRecorder=new KeyboardMacroRecorder(); macroRecorder.Finished=delegate(bool cancel,string text) { if(version==recordingVersion) FinishMacroRecording(cancel,text); }; macroRecorder.Start(); macroRecordButton.Content="記録を完了"; }
                    catch(Exception ex) { StopMacroRecording(true); Changed(ex.Message); return; }
                }
                double elapsed=(DateTime.UtcNow-macroRecordStart).TotalSeconds;
                recordingStatus.Text=macroRecordHint.Text="記録中 · "+Math.Floor(elapsed)+"秒 / F12で完了、Escで中止";
                if(elapsed>=60) StopMacroRecording(false);
            };
            macroRecordTimer.Start(); Changed("キー操作の記録を準備しています。");
        }
        void StopMacroRecording(bool cancel) {
            if(macroRecordTimer==null && macroRecorder==null) return;
            string text=macroRecorder==null?"":macroRecorder.Stop(); FinishMacroRecording(cancel,text);
        }
        void FinishMacroRecording(bool cancel,string text) {
            recordingVersion++;
            if(macroRecordTimer!=null) { macroRecordTimer.Stop(); macroRecordTimer=null; }
            if(macroRecorder!=null) { macroRecorder.Dispose(); macroRecorder=null; } owner.MacroRecording=false;
            if(recordingBar!=null) { recordingBar.Visibility=Visibility.Collapsed; workspace.IsEnabled=true; bottomButtons.IsEnabled=true; foreach(Button button in navigation) button.IsEnabled=true; }
            if(macroRecordButton!=null) { macroRecordButton.Content="連続したキー操作を記録"; macroRecordHint.Text="キー操作と待ち時間を記録します。F12で完了、Escで中止します。"; }
            if(!cancel && !String.IsNullOrEmpty(text) && macroRecordTarget!=null) { Remember(); macroRecordTarget.Value=text; RefreshInspector(); Changed("キー操作をマクロに記録しました。「保存」で反映します。"); }
            else Changed(cancel?"キー操作の記録を中止しました。":"記録するキー操作がありませんでした。");
            macroRecordTarget=null;
            if(navUndo!=null) { navUndo.IsEnabled=history.Count>0; navRedo.IsEnabled=future.Count>0; }
            if(IsLoaded && IsVisible) { if(WindowState==WindowState.Minimized) WindowState=WindowState.Normal; Activate(); }
        }
    }
}

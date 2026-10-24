using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        KeyboardShortcutRecorder shortcutRecorder;
        bool keyCaptureReadOnly;
        static TextBox RecordedKeyInput(string value) {
            TextBox input=EditorUi.Input(value); input.MaxLength=48; input.IsReadOnly=true; input.BorderThickness=new Thickness(0); input.Padding=new Thickness(0); input.ToolTip="「キーを記録」で変更します。"; return input;
        }
        void BeginKeyCapture(TextBox target,bool invocation=false) {
            EndKeyCapture();
            try {
                owner.SetShortcutRecording(true);
                keyCapture=target; keyCaptureReadOnly=target.IsReadOnly; target.IsReadOnly=true; target.Focus();
                KeyboardShortcutRecorder recorder=new KeyboardShortcutRecorder(new WindowInteropHelper(this).Handle,invocation); shortcutRecorder=recorder;
                recorder.Invalid=delegate(string message) { Changed(message+" Escで中止。"); };
                recorder.Finished=delegate(bool cancelled,string value) {
                    if(!Object.ReferenceEquals(shortcutRecorder,recorder)) return;
                    TextBox input=keyCapture; EndKeyCapture();
                    if(cancelled) Changed("キーの記録を中止しました。");
                    else { input.Text=value; Changed("キーを記録しました。「保存」で反映します。"); }
                };
                recorder.Start(); Changed("指定するキーを押して離してください。Escで中止。");
            } catch(Exception ex) { EndKeyCapture(); Changed(ex.Message); }
        }
        void EndKeyCapture() {
            if(shortcutRecorder!=null) { shortcutRecorder.Dispose(); shortcutRecorder=null; }
            if(keyCapture!=null) { keyCapture.IsReadOnly=keyCaptureReadOnly; keyCapture=null; }
            owner.SetShortcutRecording(false);
        }
    }
}

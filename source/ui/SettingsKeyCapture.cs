using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        KeyboardShortcutRecorder shortcutRecorder;
        bool keyCaptureReadOnly;
        Brush keyCaptureBorder,keyCaptureBackground,keyCaptureForeground;
        object keyCaptureTooltip;
        static TextBox RecordedKeyInput(string value) {
            TextBox input=EditorUi.Input(value); input.MaxLength=48; input.IsReadOnly=true; input.BorderThickness=new Thickness(0); input.Padding=new Thickness(0); input.ToolTip="「キーを記録」で変更します。"; return input;
        }
        FrameworkElement InvocationKeyControls(TextBox input) {
            EditorButtonRow row=new EditorButtonRow { Margin=new Thickness(0,0,0,12) };
            row.Children.Add(EditorUi.Button("キーを記録",delegate { BeginKeyCapture(input,true); },false));
            Button choose=EditorUi.Button("キーを選ぶ",delegate { },false); choose.ToolTip="Pause／BreakやF13～F24など、キーボードにないキーを一覧から指定します。";
            System.Windows.Automation.AutomationProperties.SetName(choose,"キーボードにない呼び出しキーを選ぶ"); row.Children.Add(choose);
            choose.Click+=delegate {
                ContextMenu menu=new ContextMenu(); MenuItem pause=new MenuItem { Header="Pause／Break" }; pause.Click+=delegate { SelectInvocationKey(input,"Pause"); }; menu.Items.Add(pause);
                MenuItem functions=new MenuItem { Header="F13～F24" }; menu.Items.Add(functions);
                for(int i=13;i<=24;i++) { string value="F"+i; MenuItem function=new MenuItem { Header=value }; function.Click+=delegate { SelectInvocationKey(input,value); }; functions.Items.Add(function); }
                choose.ContextMenu=menu; menu.PlacementTarget=choose; menu.IsOpen=true;
            };
            input.ToolTip="「キーを記録」で押したキーを登録するか、「キーを選ぶ」でキーボードにないキーを指定します。";
            return row;
        }
        void SelectInvocationKey(TextBox input,string value) { EndKeyCapture(); input.Text=value; Changed("呼び出しキーを選びました。「保存」で反映します。マウスにも同じキーを割り当ててください。"); }
        void BeginKeyCapture(TextBox target,bool invocation=false) {
            EndKeyCapture();
            try {
                owner.SetShortcutRecording(true);
                keyCapture=target; keyCaptureReadOnly=target.IsReadOnly; keyCaptureBorder=target.BorderBrush; keyCaptureBackground=target.Background; keyCaptureForeground=target.Foreground; keyCaptureTooltip=target.ToolTip;
                target.IsReadOnly=true; target.BorderBrush=EditorUi.Mint; target.Background=EditorUi.Brush("#203B32"); target.Foreground=EditorUi.Mint; target.ToolTip="キーを記録中 · 登録するキーを押して離してください。Escで中止。"; target.Focus();
                KeyboardShortcutRecorder recorder=new KeyboardShortcutRecorder(new WindowInteropHelper(this).Handle,invocation); shortcutRecorder=recorder;
                recorder.Invalid=delegate(string message) { if(keyCapture!=null) keyCapture.ToolTip=message+" Escで中止。"; Changed("キーを記録中 · 別のキーを指定してください。"); };
                recorder.Finished=delegate(bool cancelled,string value) {
                    if(!Object.ReferenceEquals(shortcutRecorder,recorder)) return;
                    TextBox input=keyCapture; EndKeyCapture();
                    if(cancelled) Changed("キーの記録を中止しました。");
                    else { input.Text=value; Changed("キーを記録しました。「保存」で反映します。"); }
                };
                recorder.Start(); Changed("キーを記録中 · Escで中止。");
            } catch(Exception ex) { EndKeyCapture(); Changed(ex.Message); }
        }
        void EndKeyCapture() {
            if(shortcutRecorder!=null) { shortcutRecorder.Dispose(); shortcutRecorder=null; }
            if(keyCapture!=null) { keyCapture.IsReadOnly=keyCaptureReadOnly; keyCapture.BorderBrush=keyCaptureBorder; keyCapture.Background=keyCaptureBackground; keyCapture.Foreground=keyCaptureForeground; keyCapture.ToolTip=keyCaptureTooltip; keyCapture=null; keyCaptureBorder=null; keyCaptureBackground=null; keyCaptureForeground=null; keyCaptureTooltip=null; }
            owner.SetShortcutRecording(false);
        }
    }
}

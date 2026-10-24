using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    static class ExtendedActions {
        public static readonly string[] Kinds={"shortcut","open","volume","macro","hold","toggle","window","launch","text","search","pasteplain","microphone","output","orbitals","wheel","audiosession","audiodevice","window-size","stamp","settings","convert","plugin","stack","none"};
        public static readonly string[] Names={"キー / ショートカット","ファイル・フォルダー・URL","音量","マクロ","押している間キーを保持","複数段階トグル","ウィンドウ操作","アプリを起動 / 前面へ","定型文を入力","選択した文字を検索","書式なし貼り付け","マイクのミュート切替","音声出力先の選択画面","orbitalsの操作","開始位置でスクロール・ズーム","対象アプリの音量","音声出力先を直接切替","ウィンドウのサイズ指定","日付・時刻を入力","Windowsの設定を開く","ファイル変換","プラグイン","スタック","未割り当て"};
        public static readonly string[] WindowValues={"maximize","minimize","left","right","next-monitor","topmost","center"};
        public static readonly string[] WindowNames={"最大化 / 元に戻す","最小化","左半分に配置","右半分に配置","次のモニターへ移動","最前面を切替","画面の中央に移動"};
        public static readonly string[] WheelValues={"Ctrl+Up","Ctrl+Down","Up","Down","Left","Right"};
        public static readonly string[] SettingsValues={"home","display","sound","bluetooth","network","apps"};
        public static readonly string[] SettingsNames={"Windowsの設定","ディスプレイ","サウンド","Bluetoothとデバイス","ネットワーク","インストールされているアプリ"};
        public static bool Validate(ActionItem item) {
            switch(item.Kind) {
                case "window": if(!WindowValues.Contains(item.Value)) throw new Exception("ウィンドウ操作を選んでください。"); break;
                case "launch": string path=OpenTarget.Resolve(item.Value); if(!File.Exists(path) || !String.Equals(Path.GetExtension(path),".exe",StringComparison.OrdinalIgnoreCase)) throw new Exception("起動するexeファイルを選んでください。"); break;
                case "text": if(String.IsNullOrEmpty(item.Value) || item.Value.Length>4096 || item.Value.Any(c=>Char.IsControl(c) && c!='\n' && c!='\r' && c!='\t')) throw new Exception("定型文は1〜4096文字で指定してください。"); break;
                case "search": if(String.IsNullOrEmpty(item.Value) || !item.Value.Contains("{text}")) throw new Exception("検索URLに {text} を含めてください。"); Uri uri; if(!Uri.TryCreate(item.Value.Replace("{text}","test"),UriKind.Absolute,out uri) || uri.Scheme!="https" || String.IsNullOrEmpty(uri.Host) || item.Value.Any(Char.IsControl)) throw new Exception("検索URLにはhttpsを指定してください。"); break;
                case "orbitals": if(item.Value!="pause" && item.Value!="exclude") throw new Exception("一時停止かアプリを除外を選んでください。"); break;
                case "wheel": if(!WheelValues.Contains(item.Value)) throw new Exception("スクロール・ズームの方向を選んでください。"); break;
                case "audiosession": if(!new[]{"up","down","mute"}.Contains(item.Value)) throw new Exception("対象アプリの音量操作を選んでください。"); break;
                case "audiodevice": if(String.IsNullOrWhiteSpace(item.Value) || item.Value.Length>512 || item.Value.Any(Char.IsControl)) throw new Exception("音声出力先の機器を選んでください。"); break;
                case "window-size": WindowSize(item.Value); break;
                case "stamp": if(!new[]{"date","time","datetime"}.Contains(item.Value)) throw new Exception("日付・時刻の形式を選んでください。"); break;
                case "settings": if(!SettingsValues.Contains(item.Value)) throw new Exception("開く設定画面を選んでください。"); break;
                case "convert": if(!String.IsNullOrEmpty(item.Value) && !FileConversion.Operations.Contains(item.Value)) throw new Exception("ファイルの変換内容を選んでください。"); break;
                case "plugin": if(!ActionPlugins.ValidReference(item.Value)) throw new Exception("プラグインのアクションを選んでください。"); break;
                case "microphone": if(!String.IsNullOrEmpty(item.Value) && item.Value!="on" && item.Value!="off") throw new Exception("マイクは切り替え・ミュートON・ミュートOFFから選んでください。"); break;
                case "pasteplain": case "output": break;
                default: return false;
            }
            return true;
        }
        public static string DefaultValue(string kind) { return kind=="window"?"maximize":kind=="search"?"https://www.google.com/search?q={text}":kind=="orbitals"?"pause":kind=="wheel"?"Ctrl+Up":kind=="audiosession"?"up":kind=="window-size"?"1280x720":kind=="stamp"?"date":kind=="settings"?"home":""; }
        public static int[] WindowSize(string value) {
            string[] parts=(value??"").ToLowerInvariant().Replace('×','x').Split('x'); int width,height;
            if(parts.Length!=2 || !Int32.TryParse(parts[0],out width) || !Int32.TryParse(parts[1],out height) || width<100 || width>7680 || height<100 || height>4320) throw new Exception("幅x高さで入力してください。例：1280x720（幅100〜7680、高さ100〜4320）");
            return new[]{width,height};
        }
        public static string SettingsUri(string value) { string[] uris={"ms-settings:","ms-settings:display","ms-settings:sound","ms-settings:bluetooth","ms-settings:network-status","ms-settings:appsfeatures"}; int index=Array.IndexOf(SettingsValues,value); if(index<0) throw new Exception("未対応の設定画面です。"); return uris[index]; }
        public static string Hint(string kind) { if(kind=="convert") return "画像をドラッグしたまま呼び出しキーを押し、形式のplanetの上で離すと変換します。PDFの操作は詳細画面で指定します。"; return kind=="audiosession"?"呼び出したときに操作していたアプリの音量を変更します。":kind=="audiodevice"?"選んだ機器へ音声出力先を直接切り替えます。":kind=="window-size"?"幅x高さ（px）を指定します。例：1280x720。画面内に収めて中央に配置します。":kind=="stamp"?"実行した時点の日付・時刻を入力します。":kind=="settings"?"Windowsの設定画面を開きます。":kind=="search"?"選択した文字を {text} に入れて検索します。":kind=="launch"?"アプリのexeファイルを指定します。\n起動済みなら、そのウィンドウを前面に表示します。":kind=="text"?"改行を含む文字を入力します。クリップボードは変更しません。":kind=="output"?"Windows 11の音声出力先を選ぶ画面を開きます。":kind=="microphone"?"既定の通信用マイクのミュートを切り替えます。":kind=="window"?"操作を始めたウィンドウに適用します。":kind=="wheel"?"カーソルを操作開始位置へ戻して、選んだ方向に1目盛り回します。":kind=="orbitals"?"操作を選びます。アプリの除外はブラックリストに追加されます。":kind=="pasteplain"?"クリップボードの文字を、書式を付けずに貼り付けます。":""; }
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder name,ref int length);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd,int command);
        [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr hwnd,int command);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out Native.RECT rect);
        [DllImport("user32.dll")] static extern uint GetClipboardSequenceNumber();
        public static string Executable(IntPtr hwnd) { return Path.GetFileName(ProcessPath(hwnd)); }
        public static string ProcessPath(IntPtr hwnd) {
            uint pid; GetWindowThreadProcessId(hwnd,out pid); IntPtr handle=OpenProcess(0x1000,false,pid); if(handle==IntPtr.Zero) return "";
            try { StringBuilder name=new StringBuilder(32768); int length=name.Capacity; return QueryFullProcessImageName(handle,0,name,ref length)?name.ToString():""; } finally { CloseHandle(handle); }
        }
        public static void WindowAction(IntPtr hwnd,string operation) {
            if(operation=="maximize") { ShowWindowAsync(hwnd,IsZoomed(hwnd)?9:3); return; }
            if(operation=="minimize") { ShowWindowAsync(hwnd,6); return; }
            if(operation=="topmost") { if(!Native.SetWindowPos(hwnd,new IntPtr((Native.GetWindowLong(hwnd,-20)&8)!=0?-2:-1),0,0,0,0,0x13)) throw new Exception("最前面の切替に失敗しました。"); return; }
            Forms.Screen current=Forms.Screen.FromHandle(hwnd); System.Drawing.Rectangle area=current.WorkingArea;
            Native.RECT r; if(!GetWindowRect(hwnd,out r)) throw new Exception("ウィンドウの位置を取得できません。");
            int x=area.X,y=area.Y,w=area.Width/2,h=area.Height;
            if(operation=="center" || operation.StartsWith("size:",StringComparison.Ordinal)) {
                int[] size=operation=="center"?new[]{r.Right-r.Left,r.Bottom-r.Top}:WindowSize(operation.Substring(5));
                w=Math.Min(size[0],area.Width); h=Math.Min(size[1],area.Height); x=area.X+(area.Width-w)/2; y=area.Y+(area.Height-h)/2;
            }
            if(operation=="right") x+=w;
            if(operation=="next-monitor") {
                Forms.Screen[] screens=Forms.Screen.AllScreens; int index=Array.FindIndex(screens,s=>s.DeviceName==current.DeviceName);
                if(screens.Length<2) return;
                var next=screens[(index+1)%screens.Length].WorkingArea;
                w=Math.Min(r.Right-r.Left,next.Width); h=Math.Min(r.Bottom-r.Top,next.Height);
                x=next.X+Math.Max(0,Math.Min(r.Left-area.X,next.Width-w)); y=next.Y+Math.Max(0,Math.Min(r.Top-area.Y,next.Height-h));
            }
            ShowWindow(hwnd,9);
            if(!Native.SetWindowPos(hwnd,IntPtr.Zero,x,y,w,h,0x14)) throw new Exception("ウィンドウを移動できません。");
        }
        public static void Launch(string value) {
            string path=OpenTarget.Resolve(value);
            foreach(Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path))) {
                using(process) { try { IntPtr hwnd=process.MainWindowHandle; if(hwnd!=IntPtr.Zero && String.Equals(ProcessPath(hwnd),path,StringComparison.OrdinalIgnoreCase)) { if(IsIconic(hwnd)) ShowWindow(hwnd,9); if(!Native.SetForegroundWindow(hwnd)) throw new Exception("アプリを前面へ移動できません。"); return; } } catch(InvalidOperationException) { } }
            }
            Process.Start(new ProcessStartInfo(path) { UseShellExecute=true });
        }
        public static void Text(string text,IntPtr target) {
            if(text==null || text.Length>4096) throw new Exception("入力できる文字は4096文字までです。");
            for(int offset=0;offset<text.Length;offset+=64) {
                if(Native.GetForegroundWindow()!=target || (Native.GetAsyncKeyState(27)&0x8000)!=0) throw new Exception("文字入力を中止しました。");
                string part=text.Substring(offset,Math.Min(64,text.Length-offset)); Native.INPUT[] input=new Native.INPUT[part.Length*2];
                for(int i=0;i<part.Length;i++) {
                    input[i*2]=new Native.INPUT { type=1,data=new Native.INPUTUNION { key=new Native.KEYBDINPUT { scan=part[i],flags=4,extra=Native.OwnInput } } };
                    input[i*2+1]=new Native.INPUT { type=1,data=new Native.INPUTUNION { key=new Native.KEYBDINPUT { scan=part[i],flags=6,extra=Native.OwnInput } } };
                }
                if(Native.SendInput((uint)input.Length,input,Marshal.SizeOf(typeof(Native.INPUT)))!=input.Length) throw new Exception("文字を送信できません。操作先の権限を確認してください。");
            }
        }
        public static void Wheel(string value,Native.POINT origin,ActionPlayback playback) {
            if(!Native.SetCursorPos(origin.X,origin.Y)) throw new Exception("開始位置へ戻せません。");
            bool introduce=value.StartsWith("Ctrl+",StringComparison.Ordinal) && (Native.GetAsyncKeyState(17)&0x8000)==0;
            try {
                if(introduce) playback.StartHold("Ctrl");
                Native.INPUT wheel=WheelInput(value);
                if(Native.SendInput(1,new[]{wheel},Marshal.SizeOf(typeof(Native.INPUT)))!=1) throw new Exception("ホイールを送信できません。");
            } finally { if(introduce) playback.Stop(); }
        }
        public static Native.INPUT WheelInput(string value) {
            if(!WheelValues.Contains(value)) throw new Exception("スクロール方向が不正です。");
            bool horizontal=value=="Left" || value=="Right"; int delta=value=="Left" || value.EndsWith("Down",StringComparison.Ordinal)?-120:120;
            return new Native.INPUT { type=0,data=new Native.INPUTUNION { mouse=new Native.MOUSEINPUT { flags=horizontal?0x1000u:0x800u,mouseData=unchecked((uint)delta),extra=Native.OwnInput } } };
        }
        public static Action Search(string template,IntPtr target,Action pressCopy,Action<bool,string> finished) {
            IDataObject before=Clipboard.GetDataObject(); uint initial=GetClipboardSequenceNumber(); uint copied=0;
            DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(30) }; DateTime end=DateTime.UtcNow.AddSeconds(1.5); bool done=false;
            Action restore=delegate { if(copied!=0 && GetClipboardSequenceNumber()==copied) { if(before==null) Clipboard.Clear(); else Clipboard.SetDataObject(before,true); } };
            Action cancel=delegate { if(done) return; done=true; timer.Stop(); try { restore(); } catch { } };
            timer.Tick+=delegate {
                if(done) return;
                try {
                    if(Native.GetForegroundWindow()!=target || (Native.GetAsyncKeyState(27)&0x8000)!=0) { cancel(); finished(false,"検索を中止しました。"); return; }
                    uint current=GetClipboardSequenceNumber(); if(current==initial) { if(DateTime.UtcNow>end) { cancel(); finished(false,"選択した文字を取得できませんでした。"); } return; }
                    copied=current; string text=Clipboard.ContainsText()?Clipboard.GetText():"";
                    if(String.IsNullOrWhiteSpace(text) || text.Length>4096) { cancel(); finished(false,"1〜4096文字のテキストを選択してください。"); return; }
                    restore(); copied=0; done=true; timer.Stop();
                    Process.Start(new ProcessStartInfo(template.Replace("{text}",Uri.EscapeDataString(text))) { UseShellExecute=true }); finished(true,null);
                } catch(Exception ex) { cancel(); finished(false,ex.Message); }
            };
            pressCopy(); timer.Start(); return cancel;
        }
    }
}

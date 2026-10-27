using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]

namespace OrbitRing {
    public sealed class ActionItem {
        public string Label { get; set; }
        public string Kind { get; set; }
        public string Value { get; set; }
        string icon;
        public string Icon { get { return icon; } set { icon=CustomIcons.Share(value); } }
        public ToggleDefinition Toggle { get; set; }
        public MuteIcons MuteIcons { get; set; }
        public List<ActionItem> Children { get; set; }
        public string StackMode { get; set; }
        internal string AutomaticGlyph;
        internal string AutomaticIcon;
        public ActionItem() { }
        public ActionItem(string label, string kind, string value) { Label = label; Kind = kind; Value = value; }
        public ActionItem Clone() { return Json.Read<ActionItem>(Json.Write(this)); }
    }
    public sealed class Preferences {
        public int Version { get; set; }
        public OrbitPreferences Orbit { get; set; }
        public AppearancePreferences Appearance { get; set; }
        public string Hotkey { get; set; }
        public bool StartWithWindows { get; set; }
        public bool PlanetEnabled { get; set; }
        public bool PlanetWheelSwitch { get; set; }
        public List<ActionItem> Items { get; set; }
        public List<PlanetProfile> Planets { get; set; }
        public string MainPlanetName { get; set; }
        public List<ActionItem> Favorites { get; set; }
        public List<SavedGesture> GestureLibrary { get; set; }
        public List<ApplicationSettings> Applications { get; set; }
        public UpdatePreferences Updates { get; set; }
        public ConversionPreferences Conversion { get; set; }
        public List<ActionPlugin> Plugins { get; set; }
        public double RingScale { get; set; }
        public double ButtonSize { get; set; }
        public const double StandardRingScale=.75;
        public const double StandardButtonSize=82;
        [ScriptIgnore] public double ButtonPercent { get { return ButtonSize/StandardButtonSize*100; } set { ButtonSize=value/100*StandardButtonSize; } }
        [ScriptIgnore] public double RingPercent { get { return RingScale/StandardRingScale*100; } set { RingScale=value/100*StandardRingScale; } }
        public Preferences() { PlanetEnabled=true; PlanetWheelSwitch=true; RingScale = 1; ButtonSize = StandardButtonSize; Orbit=new OrbitPreferences(); Appearance=new AppearancePreferences(); Planets=new List<PlanetProfile>(); MainPlanetName="メイン"; Favorites=new List<ActionItem>(); GestureLibrary=new List<SavedGesture>(); Applications=new List<ApplicationSettings>(); Updates=new UpdatePreferences(); Conversion=new ConversionPreferences { ArchiveVersion=1 }; Plugins=new List<ActionPlugin>(); }
        public void Swap(int from, int to) {
            if (from < 0 || from >= Items.Count || to < 0 || to >= Items.Count) throw new ArgumentOutOfRangeException();
            ActionItem temp = Items[from]; Items[from] = Items[to]; Items[to] = temp;
        }
        public static Preferences Defaults() {
            return new Preferences { Version = 1, RingScale=StandardRingScale, Hotkey = "Ctrl+Alt+F9", Items = new List<ActionItem> {
                new ActionItem("コピー", "shortcut", "Ctrl+C"),
                new ActionItem("貼り付け", "shortcut", "Ctrl+V"),
                new ActionItem("元に戻す", "shortcut", "Ctrl+Z"),
                new ActionItem("保存", "shortcut", "Ctrl+S"),
                new ActionItem("フォルダー", "open", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                new ActionItem("画面切り取り", "shortcut", "Win+Shift+S"),
                new ActionItem("音量", "volume", ""),
                new ActionItem("すべて選択", "shortcut", "Ctrl+A")
            }};
        }
        public IEnumerable<ActionItem> AllActions() {
            foreach(ActionItem a in PlanetStack.Walk(Items)) yield return a;
            foreach(PlanetProfile profile in Planets) foreach(ActionItem a in PlanetStack.Walk(profile.Items)) yield return a;
            foreach(OrbitBinding b in Orbit.Bindings) foreach(ActionItem a in PlanetStack.Walk(new[]{b.Action})) yield return a;
            foreach(ApplicationSettings app in Applications) { foreach(ApplicationPlanet planet in app.Planets) foreach(ActionItem a in PlanetStack.Walk(planet.Items)) yield return a; foreach(OrbitBinding binding in app.Bindings) foreach(ActionItem a in PlanetStack.Walk(new[]{binding.Action})) yield return a; }
        }
        public Preferences Clone() { return Json.Read<Preferences>(Json.Write(this)); }
        public void Validate() {
            if (Version != 1) throw new Exception("この設定ファイルのバージョンには対応していません。");
            if (Double.IsNaN(RingScale) || Double.IsInfinity(RingScale) || RingScale < .375 || RingScale > 1.5)
                throw new Exception("リングの大きさは50〜200%で指定してください。");
            if (Double.IsNaN(ButtonSize) || Double.IsInfinity(ButtonSize) || ButtonSize < 60 || ButtonSize > 90)
                throw new Exception("ボタンの大きさは60〜90で指定してください。");
            if(Appearance==null) throw new Exception("外観の設定が空です。"); Appearance.Validate();
            if(Updates==null) throw new Exception("更新の設定が空です。"); Updates.Validate();
            if(Conversion==null) throw new Exception("変換用planetの設定が空です。"); Conversion.Validate();
            Chord h = Chord.Parse(Hotkey);
            ActionPlugins.ValidateAll(Plugins,h);
            if(!h.CanInvoke()) throw new Exception(Chord.InvocationHint);
            if (Items == null || Items.Count != 8) throw new Exception("リングには8つの操作が必要です。");
            HashSet<string> ids=new HashSet<string>();
            foreach(ActionItem item in Items) {
                ValidateAction(item,h);
                if(item.Kind=="toggle" && !ids.Add(item.Toggle.Id)) throw new Exception("トグルの識別子が重複しています。");
            }
            if(Orbit==null) throw new Exception("orbitの設定が空です。"); Orbit.Validate(h); ApplicationProfiles.Validate(this); UserContent.Validate(this); ids.Clear(); foreach(ActionItem a in AllActions()) if(a.Kind=="toggle" && !ids.Add(a.Toggle.Id)) throw new Exception("トグルの識別子が重複しています。");
            foreach(string key in UserContent.Hotkeys(this)) if(key!=Hotkey) ActionPlugins.ValidateAll(Plugins,Chord.Parse(key));
            Json.Write(this); // Enforce the same total size bound before saving.
        }
        internal static void ValidateAction(ActionItem item,Chord h,int depth=0) {
            if(item==null || String.IsNullOrWhiteSpace(item.Label) || item.Label.Length>20) throw new Exception("操作名は1～20文字で入力してください。");
            if(item.Value!=null && item.Value.Length>8192) throw new Exception("アクションの内容が長すぎます。");
            ActionIcons.Validate(item.Icon);
            if(item.MuteIcons!=null) item.MuteIcons.Validate();
            if(item.Kind=="none") return;
            if(item.Kind=="stack") {
                if(PlanetStack.Mode(item)!="level" && PlanetStack.Mode(item)!="arc") throw new Exception("スタックの開き方を選び直してください。");
                if(depth>=PlanetStack.MaxDepth || !PlanetStack.ValidChildren(item)) throw new Exception("スタックは3階層まで。階層型は8枠、弧状は32件までです。");
                foreach(ActionItem child in item.Children) ValidateAction(child,h,depth+1); return;
            }
            if(item.Kind=="toggle") {
                if(item.Toggle==null) throw new Exception("トグルの段階を設定してください。");
                item.Toggle.Validate(h);
            } else if(item.Kind=="shortcut" || item.Kind=="hold") {
                Chord c=Chord.Parse(item.Value);
                if(c.Key==h.Key && c.Modifiers==h.Modifiers) throw new Exception("操作とリングの呼び出しに同じキーは指定できません。");
            } else if(item.Kind=="macro") {
                foreach(MacroStep step in MacroStep.Parse(item.Value)) if(step.Key!=null) {
                    Chord c=Chord.Parse(step.Key);
                    if(c.Key==h.Key && c.Modifiers==h.Modifiers) throw new Exception("マクロにリングの呼び出しキーは指定できません。");
                }
            } else if(item.Kind=="mute") { if(item.Value!="on" && item.Value!="off") throw new Exception("ミュートはONかOFFで指定してください。"); }
            else if(item.Kind=="open") OpenTarget.Resolve(item.Value);
            else if(item.Kind!="volume" && !ExtendedActions.Validate(item)) throw new Exception("未対応の操作です: "+item.Kind);
        }
    }
    static class Json {
        public const int MaxCharacters=8*1024*1024, MaxFileBytes=32*1024*1024;
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength=MaxCharacters, RecursionLimit=32 }; }
        public static string Write(object value) { return Serializer().Serialize(value); }
        public static T Read<T>(string value) { return Serializer().Deserialize<T>(value); }
        public static T ReadFile<T>(string path) {
            using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(stream.Length>MaxFileBytes) throw new Exception("設定ファイルが大きすぎます。");
                using(StreamReader reader=new StreamReader(stream,Encoding.UTF8,true)) return Read<T>(reader.ReadToEnd());
            }
        }
    }
    public sealed class Chord {
        public uint Modifiers;
        public ushort Key;
        public const string InvocationHint="呼び出しにはCtrl・Alt・Shift・Winを含めるか、F1～F24・Pause・Breakを指定してください。";
        public bool CanInvoke() { return Modifiers!=0 || Key>=0x70 && Key<=0x87 || Key==0x13 || Key==0x03; }
        public static Chord Parse(string text) {
            if (String.IsNullOrWhiteSpace(text)) throw new Exception("ショートカットを入力してください。");
            if(text.Length>48) throw new Exception("ショートカットは48文字以内で指定してください。");
            string[] parts = text.Split('+');
            Chord c = new Chord();
            for (int i = 0; i < parts.Length - 1; i++) {
                uint m;
                switch (parts[i].Trim().ToUpperInvariant()) {
                    case "CTRL": case "CONTROL": m = 2; break;
                    case "ALT": m = 1; break;
                    case "SHIFT": m = 4; break;
                    case "WIN": case "WINDOWS": m = 8; break;
                    default: throw new Exception("修飾キーは Ctrl / Alt / Shift / Win を指定してください。");
                }
                if ((c.Modifiers & m) != 0) throw new Exception("修飾キーが重複しています。");
                c.Modifiers |= m;
            }
            string last = parts[parts.Length - 1].Trim();
            if (last.Length == 1 && Char.IsLetterOrDigit(last[0]) && last[0] < 128) c.Key = (ushort)Char.ToUpperInvariant(last[0]);
            else {
                int f;
                if (last.StartsWith("F", StringComparison.OrdinalIgnoreCase) && Int32.TryParse(last.Substring(1), out f) && f >= 1 && f <= 24) c.Key = (ushort)(0x6F + f);
                else {
                    Dictionary<string, ushort> names = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase) {
                        {"Ctrl",0x11},{"Control",0x11},{"Alt",0x12},{"Shift",0x10},{"Win",0x5B},
                        {"Space",0x20},{"Tab",9},{"Enter",13},{"Escape",27},{"Esc",27},{"Backspace",8},
                        {"Delete",46},{"Insert",45},{"Home",36},{"End",35},{"PageUp",33},{"PageDown",34},
                        {"Left",37},{"Up",38},{"Right",39},{"Down",40},{"PrintScreen",44},
                        {"Pause",0x13},{"PauseBreak",0x13},{"Pause/Break",0x13},{"Break",0x03},{"Cancel",0x03},
                        {"VolumeMute",0xAD},{"VolumeDown",0xAE},{"VolumeUp",0xAF},{"MediaPlayPause",0xB3},
                        {"MediaNextTrack",0xB0},{"MediaPreviousTrack",0xB1}
                    };
                    if (!names.TryGetValue(last, out c.Key)) {
                        Key key;
                        if(!Enum.TryParse<Key>(last,true,out key) || !Enum.IsDefined(typeof(Key),key) || key==System.Windows.Input.Key.None || key==System.Windows.Input.Key.System || key==System.Windows.Input.Key.ImeProcessed || key==System.Windows.Input.Key.DeadCharProcessed)
                            throw new Exception("キーを認識できません: " + last + "\n例: Ctrl+C、Space、F13");
                        int vk=KeyInterop.VirtualKeyFromKey(key);
                        if(vk<8 || vk>254) throw new Exception("このキーは指定できません。");
                        c.Key=(ushort)vk;
                    }
                }
            }
            return c;
        }
        public Native.INPUT[] Inputs() {
            List<ushort> keys = new List<ushort>();
            if ((Modifiers & 2) != 0) keys.Add(0x11);
            if ((Modifiers & 1) != 0) keys.Add(0x12);
            if ((Modifiers & 4) != 0) keys.Add(0x10);
            if ((Modifiers & 8) != 0) keys.Add(0x5B);
            if(!keys.Contains(Key)) keys.Add(Key);
            List<Native.INPUT> result = new List<Native.INPUT>();
            foreach (ushort key in keys) result.Add(Native.KeyInput(key, false));
            keys.Reverse();
            foreach (ushort key in keys) result.Add(Native.KeyInput(key, true));
            return result.ToArray();
        }
    }
    public static class Native {
        public static readonly UIntPtr OwnInput=new UIntPtr(0x4F524254);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int Size; public RECT Monitor, Work; public uint Flags; }
        [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx, dy; public uint mouseData, flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Explicit)] public struct INPUTUNION { [FieldOffset(0)] public MOUSEINPUT mouse; [FieldOffset(0)] public KEYBDINPUT key; }
        [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public INPUTUNION data; }
        [DllImport("user32.dll", SetLastError=true)] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll", SetLastError=true)] public static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT point, uint flags);
        [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint count, INPUT[] input, int size);
        public static INPUT KeyInput(ushort key, bool up) {
            bool extended = key==0x5B || key==0x5C || key==0xA3 || key==0xA5 || key==0x6F || key==0x90 || (key >= 0x21 && key <= 0x2E) || (key >= 0xAD && key <= 0xB3);
            return new INPUT { type = 1, data = new INPUTUNION { key = new KEYBDINPUT { vk = key, flags = (up ? 2u : 0u) | (extended ? 1u : 0u), extra=OwnInput } } };
        }
        public static bool ModifiersHeld() {
            return new int[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0);
        }
    }
    static class Ui {
        public static Brush Bg = Brush("#11151B"), Panel = Brush("#1B222B"), Line = Brush("#303A47"), Ink = Brush("#EDF3F6"), Muted = Brush("#9DAAB9"), Mint = Brush("#93E9C1");
        public static Brush Brush(string color) { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
        public static TextBlock Text(string value, double size, Brush color) { return new TextBlock { Text = value, FontSize = size, Foreground = color, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Segoe UI, Yu Gothic UI") }; }
        public static Button Button(string text, RoutedEventHandler action, bool primary) {
            Button b = new Button { Content = text, Padding = new Thickness(17,10,17,10), Margin = new Thickness(0,0,10,0), Background = primary ? Mint : Panel, Foreground = primary ? Bg : Ink, BorderBrush = Line, Cursor = Cursors.Hand, FontSize = 13, HorizontalContentAlignment=HorizontalAlignment.Center, VerticalContentAlignment=VerticalAlignment.Center };
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6)); border.SetValue(Border.BorderThicknessProperty,new Thickness(1));
            foreach(string prop in new string[]{"Background","BorderBrush","Padding"}) {
                DependencyProperty target=prop=="Background"?Border.BackgroundProperty:prop=="BorderBrush"?Border.BorderBrushProperty:Border.PaddingProperty;
                border.SetBinding(target,new System.Windows.Data.Binding(prop) { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            }
            FrameworkElementFactory presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new System.Windows.Data.Binding("HorizontalContentAlignment") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            presenter.SetBinding(ContentPresenter.VerticalAlignmentProperty,new System.Windows.Data.Binding("VerticalContentAlignment") { RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) }); border.AppendChild(presenter);
            ControlTemplate template=new ControlTemplate(typeof(Button)) { VisualTree=border };
            Trigger disabled=new Trigger { Property=UIElement.IsEnabledProperty, Value=false }; disabled.Setters.Add(new Setter(UIElement.OpacityProperty,.35)); template.Triggers.Add(disabled);
            Trigger hover=new Trigger { Property=UIElement.IsMouseOverProperty, Value=true }; hover.Setters.Add(new Setter(Control.BorderBrushProperty,Mint)); template.Triggers.Add(hover);
            b.Template=template;
            b.Click += action; return b;
        }
        public static TextBox Input(string value) {
            return new TextBox { Text = value ?? "", Background = Bg, Foreground = Ink, BorderBrush = Line, Padding = new Thickness(10,7,10,7), FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center };
        }
        public static string Glyph(ActionItem item) {
            if(!String.IsNullOrEmpty(item.Icon)) { string icon=ActionIcons.Find(item.Icon); if(icon!=null) return icon; }
            if(item.AutomaticGlyph!=null) return item.AutomaticGlyph;
            if (item.Kind == "stack") return "\uE8B7";
            if (item.Kind == "none") return "\uE710";
            if (item.Kind == "window") return item.Value=="minimize"?"\uE921":item.Value=="next-monitor"?"\uE7F4":"\uE922";
            if (item.Kind == "launch") return "\uE8A7";
            if (item.Kind == "text") return "\uE8D2";
            if (item.Kind == "search") return "\uE721";
            if (item.Kind == "pasteplain") return "\uE77F";
            if (item.Kind == "microphone") return "\uE720";
            if (item.Kind == "output") return "\uE7F5";
            if (item.Kind == "orbitals") return "\uE769";
            if (item.Kind == "wheel") return "\uE71E";
            if (item.Kind == "toggle") return "\uE8EE";
            if (item.Kind == "macro") return "\uE8EF";
            if (item.Kind == "hold") return "\uE765";
            if (item.Kind == "open") return "\uE8B7";
            if (item.Kind == "volume") return "\uE767";
            string value = (item.Value ?? "").ToUpperInvariant();
            switch (value) {
                case "CTRL+C": return "\uE8C8"; case "CTRL+V": return "\uE77F";
                case "CTRL+X": return "\uE8C6"; case "CTRL+Z": return "\uE7A7";
                case "CTRL+Y": return "\uE7A6"; case "CTRL+S": return "\uE74E";
                case "CTRL+A": return "\uE8B3"; case "WIN+SHIFT+S": return "\uE722";
                case "MEDIAPLAYPAUSE": return "\uE768"; case "MEDIANEXTTRACK": return "\uE893";
                case "MEDIAPREVIOUSTRACK": return "\uE892"; case "VOLUMEMUTE": return "\uE74F";
                case "VOLUMEUP": case "VOLUMEDOWN": return "\uE767"; case "CTRL+F": return "\uE721";
                case "WIN+E": return "\uE8B7"; case "ALT+LEFT": return "\uE72B"; case "ALT+RIGHT": return "\uE72A";
                default: return "\uE945";
            }
        }
    }
    sealed partial class Controller : IDisposable {
        public Preferences Config;
        readonly string configPath;
        public string DraftPath { get { return configPath+".draft.json"; } }
        HwndSource receiver;
        bool escapeRegistered;
        readonly List<int> selectionKeys = new List<int>();
        Forms.NotifyIcon tray;
        RingWindow ring;
        SettingsWindow settings;
        IntPtr target;
        DispatcherTimer monitor, pending;
        bool busy;
        readonly ActionPlayback playback=new ActionPlayback(new SystemActionInput());
        DispatcherTimer playbackTimer;
        bool holding;
        bool releaseWarningShown;
        readonly ToggleState toggles=new ToggleState();
        readonly PluginRunner pluginRunner=new PluginRunner();
        System.Drawing.Icon trayIcon;
        DispatcherTimer idleCleanup;
        Action playbackCompleted;
        public Preferences Presentation(IEnumerable<ActionItem> items=null) {
            List<ActionItem> actual=(items??ActiveItems??Config.Items).ToList();
            Preferences view=new Preferences { Version=Config.Version,RingScale=Config.RingScale,ButtonSize=Config.ButtonSize,Hotkey=Config.Hotkey,Appearance=Config.Appearance,Items=new List<ActionItem>(actual.Count) };
            bool? muted=actual.Any(item=>AudioMute.Supports(item) && !AudioMute.IsMicrophone(item))?SystemMute.TryRead():null;
            bool? microphone=actual.Any(item=>AudioMute.Supports(item) && AudioMute.IsMicrophone(item))?SystemMute.TryRead(true):null;
            for(int i=0;i<actual.Count;i++) {
                ActionItem item=actual[i];
                bool? state=AudioMute.IsMicrophone(item)?microphone:muted;
                if(state.HasValue) toggles.SynchronizeMute(item,state.Value);
                ActionItem display=toggles.Display(item);
                if(state.HasValue && AudioMute.Supports(item) && item.Kind!="toggle") display=AudioMute.Display(item,state.Value);
                view.Items.Add(display);
            }
            return view;
        }
        public void ResetToggles() { HideRing(); toggles.Reset(); }
        IntPtr playbackTarget;
        public Controller(bool diagnostic,bool background=false) {
            playback.MacroActions=new MacroActions(this);
            configPath=diagnostic?System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings.json"):AppPaths.PrepareSettings();
            Config=Preferences.Defaults(); ActiveItems=Config.Items; Diagnostic=diagnostic;
            if (diagnostic) return;
            string loadError=null;
            if(File.Exists(configPath)) { try { Preferences read=Json.ReadFile<Preferences>(configPath); if(read==null) throw new Exception("設定が空です。"); read.Validate(); PlanetStack.UpgradeLegacy(read); Config=read; } catch(Exception ex) { settingsLoadFailed=true; loadError="設定を読み込めませんでした。初期設定で起動します。\n元のファイルは保存するまで変更しません。\n"+ex.Message; } }
            ActiveItems=Config.Items;
            if(AppPaths.IsInstalled && Config.StartWithWindows) try { StartupRegistration.Write(StartupRegistration.Desired(true)); } catch(Exception ex) { loadError="ログイン時の自動起動を更新できませんでした。\n"+ex.Message; }
            HwndSourceParameters p=new HwndSourceParameters(InstanceActivation.ReceiverName) { ParentWindow=new IntPtr(-3), Width=0, Height=0, WindowStyle=0 };
            receiver=new HwndSource(p); receiver.AddHook(Message);
            bool registered=RegisterPlanetKeys(Config);
            trayIcon=AppIcon.Tray(AppIconState.Running,AppIcon.TraySize);
            tray=new Forms.NotifyIcon { Icon=trayIcon, Text="orbitals · "+Config.Hotkey, Visible=true };
            Forms.ContextMenuStrip menu=CreateTrayMenu();
            tray.ContextMenuStrip=menu; tray.MouseUp+=TrayMouseUp;
            idleCleanup=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
            idleCleanup.Tick+=delegate {
                idleCleanup.Stop();
                if(settings!=null || busy || conversionPlanets!=null || conversionActivity!=null || conversionJobs.Any(j=>j.Running) || (ring!=null && ring.IsVisible)) return;
                MaterialIcons.ReleaseCatalog(Config);
                // Collect released UI only after activity has settled.
                GC.Collect(2,GCCollectionMode.Forced,true,true);
            };
            playbackTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(15) };
            playbackTimer.Tick+=delegate {
                try {
                    bool valid=Native.IsWindow(playbackTarget) && Native.GetForegroundWindow()==playbackTarget;
                    bool released=(Native.GetAsyncKeyState(27)&0x8000)!=0 || (holding && (gestureHolding?(orbit==null || !orbit.HoldLive):(Native.GetAsyncKeyState(1)&0x8000)==0));
                    playback.Tick(DateTime.UtcNow,valid,released);
                    if(playback.Active && playback.StepNumber!=macroReportedStep) { macroReportedStep=playback.StepNumber; ReportMacro("ステップ "+playback.StepNumber+" / "+playback.StepCount+" を実行中 · Escで中止",false); }
                    if(!playback.Active) ReportMacro(playback.Completed?"マクロのテストが完了しました。":"マクロのテストを中止しました。",true);
                    if(!playback.Active) { if(playback.Completed && playbackCompleted!=null) { Action done=playbackCompleted; playbackCompleted=null; done(); } HideRing(); }
                } catch(Exception ex) { ReportMacro(ex.Message,true); HideRing(); if(!playback.NeedsRelease) Notify(ex.Message); }
            };
            monitor=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(100) };
            monitor.Tick+=delegate { if(ring!=null && ring.IsVisible) { IntPtr current=Native.GetForegroundWindow(); if(current!=target && current!=ring.Handle) HideRing(); } };
            InitializeOrbit(menu);
            InitializeFeatureMenus(menu);
            InitializeUpdates();
            RefreshTrayState();
            ConversionRunner.CleanupAbandonedAsync();
            if(!background) OpenSettings(); else idleCleanup.Start();
            if(!registered) Notify("呼び出しキーが他のアプリと競合しています。設定で別のキーに変更してください。");
            if(loadError!=null) OrbitDialog.Info(settings,"設定を読み込めませんでした",loadError);
        }
        IntPtr Message(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled) {
            if(InstanceActivation.SettingsMessage!=0 && msg==InstanceActivation.SettingsMessage) {
                handled=true; receiver.Dispatcher.BeginInvoke(new Action(OpenSettings));
            } else if(msg==0x0312) {
                int id=w.ToInt32();
                int planet;
                if(planetKeys.TryGetValue(id,out planet)) { handled=true; if(busy || conversionPlanets!=null || (ring!=null && ring.IsVisible)) HideRing(); else ShowRing(false,planet); }
                else if(id==100) { handled=true; if(InsideStack) BackStack(); else HideRing(); }
                else if(id>=200 && id<208) { handled=true; if(ring!=null) ring.ActivateSlot(id-200); }
            }
            return IntPtr.Zero;
        }
        public void ShowRing(bool preview,int planetIndex=0,string application=null,bool skipDragProbe=false,bool explicitSelection=false) {
            if(busy || MacroRecording || ShortcutRecording || paused) return;
            if(!Config.PlanetEnabled) { Notify("planetはOFFです。全体設定か通知領域のメニューからONにできます。"); return; }
            Native.POINT point;
            if(!Native.GetCursorPos(out point)) { Notify("カーソル位置を取得できませんでした。"); return; }
            ClearStack(); directPlanet=explicitSelection; target=Native.GetForegroundWindow(); actionOrigin=point;
            if(!preview && !ResolvePlanet(planetIndex)) return; if(preview) { activePlanet=planetIndex; planetApplication=application; ActiveItems=ApplicationProfiles.Items(Config,application,planetIndex); }
            if(!preview && !skipDragProbe && (Native.GetAsyncKeyState(1)&0x8000)!=0) { ShowConversionPlanets(point,true,"",delegate { ShowRing(false,planetIndex,application,true); }); return; }
            if(idleCleanup!=null) idleCleanup.Stop();
            if(ring!=null) ring.Retire();
            ring=new RingWindow(this); ring.Open(preview,point);
            if(!escapeRegistered) escapeRegistered=Native.RegisterHotKey(receiver.Handle,100,0x4000,27);
            if(selectionKeys.Count==0) for(int i=0;i<8;i++) if(Native.RegisterHotKey(receiver.Handle,200+i,0x4000,(uint)(0x31+i))) selectionKeys.Add(200+i);
            monitor.Start();
        }
        public void HideRing() {
            ReportMacro("マクロのテストを中止しました。",true); macroClipboard=null;
            CloseConversionPlanets();
            if(cancelSearch!=null) { Action cancel=cancelSearch; cancelSearch=null; cancel(); }
            gestureHolding=false;
            playbackCompleted=null;
            StopPending();
            if(playbackTimer!=null) playbackTimer.Stop();
            holding=false;
            try { playback.Stop(); releaseWarningShown=false; }
            catch(Exception ex) { if(!releaseWarningShown) { Notify(ex.Message); releaseWarningShown=true; } }
            // Keep retrying failed key-up events instead of abandoning held keys.
            busy=playback.NeedsRelease;
            if(busy && playbackTimer!=null) playbackTimer.Start();
            if(ring!=null) ring.EndHoldCapture();
            if(ring!=null) { ring.Retire(); ring=null; } if(monitor!=null) monitor.Stop();
            if(escapeRegistered) { Native.UnregisterHotKey(receiver.Handle,100); escapeRegistered=false; }
            foreach(int id in selectionKeys) Native.UnregisterHotKey(receiver.Handle,id); selectionKeys.Clear();
            ClearStack();
            if(settings==null && !busy && idleCleanup!=null) { idleCleanup.Stop(); idleCleanup.Start(); }
        }
        public void OpenSettings() {
            HideRing();
            if(idleCleanup!=null) idleCleanup.Stop();
            if(settings==null) {
                settings=new SettingsWindow(this);
                settings.Closed+=delegate { settings.ReleaseResources(); settings=null; idleCleanup.Start(); };
                settings.Show();
            }
            else { if(settings.WindowState==WindowState.Minimized) settings.WindowState=WindowState.Normal; settings.Show(); }
            settings.Activate();
        }
        public void Apply(Preferences next) {
            next.Validate();
            if(Diagnostic) { Config=next; ActiveItems=next.Items; settingsLoadFailed=false; return; }
            if(next.PlanetEnabled!=Config.PlanetEnabled || next.Orbit.Enabled!=Config.Orbit.Enabled) { HideRing(); CloseOrbitFrame(); }
            string previousStartup=StartupRegistration.Read(), desiredStartup=StartupRegistration.Desired(next.StartWithWindows);
            bool startupChanged=next.StartWithWindows!=Config.StartWithWindows || (next.StartWithWindows && previousStartup!=desiredStartup);
            ReplacePlanetKeys(next);
            try {
                if(startupChanged) StartupRegistration.Write(desiredStartup);
                string temp=configPath+".tmp";
                File.WriteAllText(temp,Json.Write(next),new UTF8Encoding(false));
                if(File.Exists(configPath)) File.Replace(temp,configPath,configPath+".bak"); else File.Move(temp,configPath);
            } catch { if(startupChanged) { try { StartupRegistration.Write(previousStartup); } catch { } } RegisterPlanetKeys(Config); throw; }
            toggles.Retain(next); Config=next; ActiveItems=next.Items; settingsLoadFailed=false; UpdateOrbitEngine(); RefreshFeatureMenus(); RefreshTrayState();
            if(settings==null) MaterialIcons.ReleaseCatalog(Config);
        }
        public void Execute(ActionItem item) {
            if(item.Kind=="none") return;
            if(item.Kind=="stack") { EnterStack(item); return; }
            if(AudioMute.IsToggle(item)) {
                try { toggles.SynchronizeMute(item,SystemMute.Read(AudioMute.IsMicrophone(item))); } catch(Exception ex) { HideRing(); Notify(ex.Message); return; }
            }
            Action complete; item=toggles.Prepare(item,out complete);
            if(item.Kind=="hold") { Notify("このアクションはマウスで押している間、キーを保持します。"); return; }
            IntPtr destination=target,overlay=ring==null?IntPtr.Zero:ring.Handle; HideRing(); busy=true;
            DateTime deadline=DateTime.UtcNow.AddSeconds(3);
            pending=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(20) };
            pending.Tick+=delegate {
                if(Native.ModifiersHeld()) { if(DateTime.UtcNow>deadline) { StopPending(); Notify("キーが押されたままのため中止しました。キーを離してから再度選択してください。"); } return; }
                StopPending();
                try {
                    if(!Native.IsWindow(destination)) { Notify("操作先のウィンドウが閉じられたため中止しました。"); return; }
                    IntPtr current=Native.GetForegroundWindow();
                    if(current!=destination && current!=overlay) { Notify("操作先が切り替わったため中止しました。"); return; }
                    if(current!=destination && !Native.SetForegroundWindow(destination)) { Notify("操作先に戻れませんでした。"); return; }
                    if(item.Kind=="plugin") {
                        string executable=ExtendedActions.Executable(destination);
                        PluginAction action=ActionPlugins.Resolve(Config,item.Value,executable);
                        if(action.Kind=="program") { RunPlugin(action,destination,executable,complete); return; }
                        item=new ActionItem(action.Label,action.Kind,action.Value);
                    }
                    if(ExecuteExtended(item,destination,complete)) return;
                    if(item.Kind=="mute") { SystemMute.Set(item.Value=="on"); if(complete!=null) complete(); return; }
                    if(item.Kind=="volume" || (item.Kind=="shortcut" && String.Equals(item.Value,"VolumeMute",StringComparison.OrdinalIgnoreCase))) { SystemMute.Toggle(); if(complete!=null) complete(); return; }
                    if(item.Kind=="open") { Process.Start(new ProcessStartInfo(OpenTarget.Resolve(item.Value)) { UseShellExecute=true }); if(complete!=null) complete(); return; }
                    if(item.Kind=="macro") {
                        StartMacroRun(item.Value,destination,complete);
                    } else { playback.Press(item.Value); if(complete!=null) complete(); }
                } catch(Exception ex) { HideRing(); if(!playback.NeedsRelease) Notify(ex.Message); }
            };
            pending.Start();
        }
        void StopPending() { if(pending!=null) { pending.Stop(); pending=null; } busy=false; }
        public bool BeginHold(ActionItem item) {
            if(busy || Native.ModifiersHeld()) { Notify("呼び出しキーを離してからアクションを長押ししてください。"); return false; }
            if(!Native.IsWindow(target) || Native.GetForegroundWindow()!=target) return false;
            try {
                playback.StartHold(item.Value); playbackTarget=target; holding=true; busy=true;
                // Restore the physical screen position before giving mouse input
                // back to the app, so wheel events hit the original pane.
                ring.RestoreInvocationPointer();
                // HideRing would release the held keys; only dismiss the overlay here.
                ring.YieldInputForHold();
                foreach(int id in selectionKeys) Native.UnregisterHotKey(receiver.Handle,id); selectionKeys.Clear();
                monitor.Stop(); playbackTimer.Start(); return true;
            }
            catch(Exception ex) { HideRing(); Notify(ex.Message); return false; }
        }
        public void Volume(string command) { if(busy || Native.ModifiersHeld()) return; try { playback.Press(command); } catch(Exception ex) { HideRing(); if(!playback.NeedsRelease) Notify(ex.Message); } }
        void Notify(string text) { if(tray!=null) { tray.BalloonTipTitle="orbitals"; tray.BalloonTipText=text; tray.ShowBalloonTip(4500); } }
        public void Dispose() {
            pluginsDisposed=true; pluginRunner.Dispose();
            CancelDropConversions();
            DisposeUpdates();
            if(orbit!=null) { orbit.Dispose(); orbit=null; } CloseOrbitFrame();
            StopPending(); HideRing(); if(idleCleanup!=null) idleCleanup.Stop();
            if(receiver!=null) { ClearPlanetKeys(); receiver.Dispose(); }
            if(tray!=null) { tray.Visible=false; tray.Dispose(); }
            if(trayIcon!=null) trayIcon.Dispose();
            if(stoppedTrayIcon!=null) stoppedTrayIcon.Dispose();
            if(errorTrayIcon!=null) errorTrayIcon.Dispose();
        }
    }
    static class Program {
        [STAThread] public static int Main(string[] args) {
            // Small static 2D surfaces: avoid retaining a D3D device while resident.
            RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly;
            if(args.Length==2 && args[0]=="--convert-job") return FileConversion.Worker(args[1]);
            if(args.Length>0 && args[0]=="--self-test") return SelfTest(args.Length>1?args[1]:"self-test.txt");
            if(args.Length>1 && args[0]=="--render-editor") {
                Application app=new Application(); using(Controller controller=new Controller(true)) { SettingsWindow editor=new SettingsWindow(controller); editor.Export(args[1]); } return 0;
            }
            if(args.Length>1 && args[0]=="--render") {
                Application app=new Application(); using(Controller controller=new Controller(true)) { RingWindow ring=new RingWindow(controller); ring.Export(args[1]); } return 0;
            }
            try { if(DesktopLaunch.Required()) { DesktopLaunch.Restart(args.Contains("--background")); return 0; } }
            catch(Exception ex) { OrbitDialog.Info(null,"通常の起動環境へ切り替えられませんでした","スタートメニューからOrbitalsを開いてください。設定は変更していません。\n"+ex.Message); return 1; }
            bool created;
            using(Mutex mutex=new Mutex(true,"Local\\OrbitRing.Personal.v1",out created)) {
                if(!created) {
                    if(args.Contains("--background") || InstanceActivation.RequestSettings()) return 0;
                    OrbitDialog.Info(null,"設定画面を開けませんでした","Orbitalsの起動または終了処理が終わってから、もう一度開いてください。"); return 1;
                }
                Application app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                Controller controller=null;
                app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) { if(controller!=null) controller.HideRing(); OrbitDialog.Info(null,"操作を完了できませんでした",e.Exception.Message); e.Handled=true; };
                try { controller=new Controller(false,args.Contains("--background")); app.Run(); return 0; }
                catch(Exception ex) { OrbitDialog.Info(null,"起動できませんでした",ex.Message); return 1; }
                finally { if(controller!=null) controller.Dispose(); }
            }
        }
        static void Check(bool value,string label,List<string> lines) { if(!value) throw new Exception(label); lines.Add("PASS "+label); }
        static int SelfTest(string output) {
            List<string> lines=new List<string>();
            try {
                Preferences defaults=Preferences.Defaults(); defaults.Validate(); lines.Add("PASS default preferences validation");
                Preferences copy=defaults.Clone(); Check(copy.Items.Count==8 && copy.Items[0].Label=="コピー","JSON round trip including Japanese",lines);
                Chord c=Chord.Parse(defaults.Hotkey); Check(c.Modifiers==3 && c.Key==0x78,"default trigger parsing",lines);
                Check(Chord.Parse("F13").Key==0x7C,"F13 mouse mapping",lines);
                Native.INPUT[] inputs=Chord.Parse("Ctrl+Shift+S").Inputs();
                Check(inputs.Length==6 && inputs[0].data.key.vk==17 && inputs[1].data.key.vk==16 && inputs[2].data.key.vk==83 && inputs[3].data.key.flags==2 && inputs[5].data.key.vk==17,"input sequence and reverse release ordering",lines);
                Check(Marshal.SizeOf(typeof(Native.INPUT))==(IntPtr.Size==8?40:28),"native INPUT structure size",lines);
                Check(RingSurface.Slot(0,-100)==0 && RingSurface.Slot(100,0)==2 && RingSurface.Slot(0,100)==4 && RingSurface.Slot(-100,0)==6,"ring cardinal directions",lines);
                Check(RingSurface.Slot(100,-100)==1 && RingSurface.Slot(-100,-100)==7,"ring diagonal directions",lines);
                bool rejected=false; try{Chord.Parse("Ctrl+InvalidKey");}catch{rejected=true;} Check(rejected,"invalid key rejected",lines);
                rejected=false; try{Chord.Parse("Ctrl+Ctrl+A");}catch{rejected=true;} Check(rejected,"duplicate modifier rejected",lines);
                copy=defaults.Clone(); copy.Items[0].Value=copy.Hotkey; rejected=false; try{copy.Validate();}catch{rejected=true;} Check(rejected,"recursive trigger rejected",lines);
                copy=defaults.Clone(); copy.Items.RemoveAt(0); rejected=false; try{copy.Validate();}catch{rejected=true;} Check(rejected,"incorrect item count rejected",lines);
                copy=defaults.Clone(); copy.Hotkey="A"; rejected=false; try{copy.Validate();}catch{rejected=true;} Check(rejected,"plain typing key cannot be global trigger",lines);
                Preferences legacy=Json.Read<Preferences>("{\"Version\":1,\"Hotkey\":\"F13\",\"Items\":"+Json.Write(defaults.Items)+"}");
                legacy.Validate(); Check(legacy.RingScale==1 && legacy.ButtonSize==Preferences.StandardButtonSize,"settings without a button size use the current standard geometry",lines);
                copy=defaults.Clone(); string first=Json.Write(copy.Items[0]),second=Json.Write(copy.Items[3]); copy.Swap(0,3);
                Check(Json.Write(copy.Items[0])==second && Json.Write(copy.Items[3])==first,"swap preserves complete action definitions",lines);
                copy.Swap(3,0); Check(Json.Write(copy)==Json.Write(defaults),"swap is reversible without losing actions",lines);
                copy.RingScale=1.35; copy.ButtonSize=86; Preferences restored=copy.Clone(); restored.Validate(); Check(restored.RingScale==1.35 && restored.ButtonSize==86,"size and button dimensions persist",lines);
                copy.RingScale=.35; rejected=false; try{copy.Validate();}catch{rejected=true;} Check(rejected,"out-of-range ring size rejected",lines);
                copy=defaults.Clone(); copy.ButtonSize=95; rejected=false; try{copy.Validate();}catch{rejected=true;} Check(rejected,"overlapping button sizes rejected",lines);
                LayoutEditor surface=new LayoutEditor(); surface.Bind(defaults,0);
                bool positions=true; for(int i=0;i<8;i++) positions &= surface.DropSlot(surface.Position(i))==i;
                Check(positions,"all eight drag targets match visual positions",lines);
                Check(surface.DropSlot(new Point(430,320))==-1 && surface.DropSlot(new Point(0,0))==-1,"drop outside buttons cancels reordering",lines);
                Check(RingSurface.ActionSlot(new Point(205,205))==-1 && RingSurface.ActionSlot(new Point(205,125.01))==-1,"center dead zone cancels",lines);
                Check(RingSurface.ActionSlot(new Point(205,125))==0,"inner boundary starts selection",lines);
                bool radial=true;
                for(int i=0;i<8;i++) {
                    Vector direction=RingSurface.Position(i)-new Point(205,205); direction.Normalize();
                    radial &= RingSurface.ActionSlot(new Point(205,205)+direction*300)==i;
                    radial &= RingSurface.ActionSlot(new Point(205,205)+direction*394)==-1;
                }
                Check(radial,"all directions retain selection beyond buttons and cancel outside",lines);
                Check(RingSurface.ActionSlot(new Point(598,205))==2 && RingSurface.ActionSlot(new Point(598.01,205))==-1,"outer cancel boundary",lines);
                Check(RingSurface.ActionSlot(new Point(500,205))==2 && RingSurface.ActionSlot(new Point(205,205))==-1 && RingSurface.ActionSlot(new Point(205,-90))==0,"cancel and reenter resolves current position without stale selection",lines);
                copy=defaults.Clone(); copy.RingScale=.375; copy.ButtonSize=60; copy.Validate(); copy.RingScale=1.5; copy.ButtonSize=90; copy.Validate(); lines.Add("PASS supported size boundaries");
                Check(defaults.RingScale==.75 && defaults.RingPercent==100,"new standard preserves former 75 percent physical size",lines);
                copy=defaults.Clone(); copy.RingPercent=200; Check(copy.RingScale==1.5 && copy.Clone().RingPercent==200,"display percentages round trip without double scaling",lines);
                Check(Json.Write(copy).IndexOf("RingPercent",StringComparison.Ordinal)<0,"display percent is not stored alongside legacy scale",lines);
                copy.RingScale=1.4; Check(Math.Abs(copy.RingPercent-186.6666666667)<.001,"existing custom sizes keep physical geometry",lines);
                RingSurface hoverSurface=new RingSurface(); hoverSurface.Bind(defaults);
                List<int> hovered=new List<int>(); hoverSurface.IconHovered=delegate(int index){hovered.Add(index);};
                hoverSurface.UpdatePointer(RingSurface.Position(0)); hoverSurface.UpdatePointer(new Point(205,-90)); hoverSurface.UpdatePointer(RingSurface.Position(1)); hoverSurface.UpdatePointer(new Point(205,205));
                Check(String.Join(",",hovered)=="0,-1,1,-1","labels follow icon enter leave switch and center cancel",lines);
                Check(((StackPanel)RingSurface.Bubble(defaults.Items[0],0,76).Content).Children.Count==1,"action buttons contain only the icon",lines);
                List<ActionPreset> presets=ActionCatalog.Create(); bool validPresets=true;
                foreach(ActionPreset preset in presets) {
                    if(preset.Item.Kind=="shortcut" && preset.Item.Value!="") Chord.Parse(preset.Item.Value);
                    ActionItem instance=preset.Create(); instance.Label="changed"; validPresets &= preset.Item.Label!="changed";
                }
                Check(validPresets && presets.Count>=48,"catalog assignments create independent action instances",lines);
                Check(presets[0].Matches("copy") && presets[0].Matches(" Ctrl+C ") && !presets[0].Matches("no-such-action"),"catalog search supports labels keys and aliases",lines);
                bool editorTargets=true;
                foreach(double scale in new double[]{.375,.75,1.5}) {
                    LayoutEditor editor=new LayoutEditor(); copy=defaults.Clone(); copy.RingScale=scale; copy.ButtonSize=90; editor.Bind(copy,0);
                    for(int i=0;i<8;i++) editorTargets &= editor.DropSlot(editor.Position(i))==i;
                    editorTargets &= editor.DropSlot(new Point(430,320))==-1 && editor.DropSlot(new Point(0,0))==-1;
                }
                Check(editorTargets,"editor drop targets stay usable at all runtime sizes",lines);
                lines.AddRange(ExecutionTests.Run());
                lines.AddRange(MacroExtensionTests.Run());
                lines.AddRange(EvenSpacingTests.Run());
                lines.AddRange(SafetyTests.Run());
                lines.AddRange(IconTests.Run());
                lines.AddRange(ToggleTests.Run());
                lines.AddRange(ReliabilityTests.Run());
                lines.AddRange(ShortcutCaptureTests.Run());
                lines.AddRange(FeatureSettingsTests.Run());
                lines.AddRange(TrayTests.Run());
                lines.AddRange(AppIconTests.Run());
                lines.AddRange(InstallationTests.Run());
                lines.AddRange(InstanceActivationTests.Run());
                lines.AddRange(OrbitTests.Run());
                lines.AddRange(GestureShapeTests.Run());
                lines.AddRange(GestureEditorTests.Run());
                lines.AddRange(GestureCleanupTests.Run());
                lines.AddRange(GesturePresetTests.Run());
                lines.AddRange(GesturePresetLayoutTests.Run());
                lines.AddRange(AppearanceTests.Run());
                lines.AddRange(PlanetOpeningTests.Run());
                lines.AddRange(RingCenterTests.Run());
                lines.AddRange(PlanetWheelTests.Run());
                lines.AddRange(ApplicationPlanetRoutingTests.Run());
                lines.AddRange(PlanetLinkTests.Run());
                lines.AddRange(GesturePresentationTests.Run());
                lines.AddRange(StackTests.Run());
                lines.AddRange(PlanetMenuTests.Run());
                lines.AddRange(ProductivityTests.Run());
                lines.AddRange(ApplicationLaunchTests.Run());
                lines.AddRange(OptionalPlanetShortcutTests.Run());
                lines.AddRange(CustomIconTests.Run());
                lines.AddRange(PlanetEditorTests.Run());
                lines.AddRange(EditorMoveTests.Run());
                lines.AddRange(StackMoveTests.Run());
                lines.AddRange(ArcStackTests.Run());
                lines.AddRange(StackEditingTests.Run());
                lines.AddRange(StackSpacingPreviewTests.Run());
                lines.AddRange(PlanetCustomizationTests.Run());
                lines.AddRange(SettingsLayoutTests.Run());
                lines.AddRange(SettingsOrganizationTests.Run());
                lines.AddRange(AudioMuteTests.Run());
                lines.AddRange(PluginTests.Run());
                lines.AddRange(ApplicationProfileTests.Run());
                lines.AddRange(GestureToleranceTests.Run());
                lines.AddRange(UpdateTests.Run());
                lines.AddRange(ConversionTests.Run());
                lines.AddRange(ConversionExpansionTests.Run());
                lines.AddRange(DocumentConversionTests.Run());
                lines.AddRange(ConversionPlatformTests.Run());
                lines.AddRange(DropConversionTests.Run());
                lines.AddRange(ArchiveTests.Run());
                lines.Add("All tests passed. No keyboard input was injected."); File.WriteAllLines(output,lines,Encoding.UTF8); return 0;
            } catch(Exception ex) { lines.Add("FAIL "+ex.Message); File.WriteAllLines(output,lines,Encoding.UTF8); return 1; }
        }
    }
}

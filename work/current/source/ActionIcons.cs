using System;

namespace OrbitRing {
    static class ActionIcons {
        public static bool IsText(string key) { return key!=null && key.StartsWith("text:",StringComparison.Ordinal); }
        public static string Text(string key) { return IsText(key)?key.Substring(5):null; }
        public static string TextKey(string value) { string key="text:"+(value??"").Trim(); Validate(key); return key; }
        public static bool UsesText(ActionItem item,bool all=false) { return all || IsText(item.Icon); }
        public static string Caption(ActionItem item) { return Text(item.Icon)??item.Label; }
        public static void Validate(string key) {
            if(String.IsNullOrEmpty(key)) return;
            if(IsText(key)) { string value=Text(key); if(String.IsNullOrWhiteSpace(value) || value.Length>16 || System.Linq.Enumerable.Any(value,Char.IsControl)) throw new Exception("表示する文字列は1〜16文字で入力してください。"); return; }
            if(CustomIcons.IsImage(key)) { CustomIcons.Read(key); return; }
            if(Find(key)==null) throw new Exception("アイコンを一覧から選ぶか、画像を選び直してください。");
        }
        public static readonly string[,] Choices={
            {"keyboard","キーボード","\uE765"},{"copy","コピー","\uE8C8"},{"paste","貼り付け","\uE77F"},{"cut","切り取り","\uE8C6"},
            {"undo","元に戻す","\uE7A7"},{"redo","やり直す","\uE7A6"},{"save","保存","\uE74E"},{"folder","フォルダー","\uE8B7"},
            {"star","スター","\uE734"},{"heart","ハート","\uEB51"},{"home","ホーム","\uE80F"},{"search","検索","\uE721"},
            {"play","再生","\uE768"},{"pause","一時停止","\uE769"},{"next","次へ","\uE893"},{"previous","前へ","\uE892"},
            {"sound","音量","\uE767"},{"mute","ミュート","\uE74F"},{"camera","カメラ","\uE722"},{"display","画面","\uE7F4"},
            {"macro","マクロ","\uE8EF"},{"clock","時計","\uE823"},{"check","チェック","\uE73E"},{"settings","歯車","\uE713"}
        };
        // Retained for settings written by earlier Orbit versions.
        public static string Find(string name) { for(int i=0;i<Choices.GetLength(0);i++) if(Choices[i,0]==name) return Choices[i,2]; MaterialIcon icon=MaterialIcons.Find(name); return icon==null?null:icon.Key; }
        public static string Label(string key) { if(IsText(key)) return "文字列："+Text(key); if(CustomIcons.IsImage(key)) return "自作の画像"; for(int i=0;i<Choices.GetLength(0);i++) if(Choices[i,0]==key) return Choices[i,1]; MaterialIcon icon=MaterialIcons.Find(key); return icon==null?"自動選択":icon.Label; }
        public static string DefaultKey(ActionItem item) {
            switch(item.Kind) {
                case "stack": return "ms:stacks";
                case "plugin": return "ms:extension";
                case "none": return "ms:add";
                case "macro": return "ms:playlist_play";
                case "hold": return "ms:keyboard_keys";
                case "toggle": return item.Value=="system-mute"?"ms:volume_off":item.Value=="microphone-mute"?"ms:mic_off":"ms:toggle_off";
                case "volume": return "ms:volume_up";
                case "launch": return "ms:rocket_launch";
                case "text": return "ms:description";
                case "search": return "ms:search";
                case "pasteplain": return "ms:content_paste_go";
                case "microphone": return item.Value=="off"?"ms:mic":"ms:mic_off";
                case "mute": return item.Value=="off"?"ms:volume_up":"ms:volume_off";
                case "output": return "ms:speaker";
                case "audiodevice": return "ms:speaker";
                case "audiosession": return item.Value=="mute"?"ms:volume_off":item.Value=="down"?"ms:volume_down":"ms:volume_up";
                case "window-size": return "ms:aspect_ratio";
                case "stamp": return item.Value=="time"?"ms:schedule":"ms:calendar_month";
                case "settings": return "ms:settings";
                case "convert": return "ms:transform";
                case "orbitals": return item.Value=="exclude"?"ms:block":"ms:pause_circle";
                case "wheel": return item.Value=="Ctrl+Up"?"ms:zoom_in":item.Value=="Ctrl+Down"?"ms:zoom_out":item.Value=="Up"?"ms:keyboard_double_arrow_up":item.Value=="Left"?"ms:keyboard_double_arrow_left":item.Value=="Right"?"ms:keyboard_double_arrow_right":"ms:keyboard_double_arrow_down";
                case "window": return item.Value=="minimize"?"ms:minimize":item.Value=="left"?"ms:left_panel_open":item.Value=="right"?"ms:right_panel_open":item.Value=="next-monitor"?"ms:desktop_windows":item.Value=="topmost"?"ms:push_pin":"ms:fullscreen";
                case "open":
                    Uri uri; if(Uri.TryCreate(item.Value,UriKind.Absolute,out uri) && (uri.Scheme=="https" || uri.Scheme=="http")) return "ms:public";
                    string target=item.Value??""; int separator=Math.Max(target.LastIndexOf('\\'),target.LastIndexOf('/'));
                    if(target.LastIndexOf('.')>separator+1 && target.LastIndexOf('.')<target.Length-1) return "ms:draft";
                    return "ms:folder";
            }
            switch((item.Value??"").ToUpperInvariant()) {
                case "CTRL+C": return "ms:content_copy"; case "CTRL+V": return "ms:content_paste"; case "CTRL+X": return "ms:content_cut";
                case "CTRL+Z": return "ms:undo"; case "CTRL+Y": return "ms:redo"; case "CTRL+S": return "ms:save"; case "CTRL+A": return "ms:select_all"; case "CTRL+F": return "ms:search";
                case "WIN+E": return "ms:folder"; case "WIN+SHIFT+S": return "ms:screenshot_region";
                case "ALT+LEFT": return "ms:arrow_back"; case "ALT+RIGHT": return "ms:arrow_forward";
                case "CTRL+TAB": return "ms:tab_move"; case "CTRL+SHIFT+TAB": return "ms:tab_move"; case "CTRL+W": return "ms:tab_close";
                case "CTRL+SHIFT+T": return "ms:tab_recent"; case "CTRL+T": return "ms:tab"; case "CTRL+R": return "ms:refresh";
                case "WIN+V": return "ms:content_paste_search"; case "WIN+OEMPERIOD": return "ms:sentiment_satisfied";
                case "CTRL+0": return "ms:zoom_out_map"; case "CTRL+HOME": return "ms:vertical_align_top"; case "CTRL+END": return "ms:vertical_align_bottom";
                case "WIN+TAB": case "WIN+D": case "WIN+CTRL+D": case "WIN+CTRL+LEFT": case "WIN+CTRL+RIGHT": case "WIN+CTRL+F4": return "ms:desktop_windows";
                case "PRINTSCREEN": case "ALT+PRINTSCREEN": return "ms:screenshot";
                case "WIN+S": return "ms:search"; case "WIN+N": return "ms:notifications"; case "WIN+A": return "ms:tune"; case "WIN+L": return "ms:lock";
                case "VOLUMEUP": return "ms:volume_up"; case "VOLUMEDOWN": return "ms:volume_down"; case "VOLUMEMUTE": return "ms:volume_off";
                case "MEDIAPLAYPAUSE": return "ms:play_pause"; case "MEDIANEXTTRACK": return "ms:skip_next"; case "MEDIAPREVIOUSTRACK": return "ms:skip_previous";
                default: return "ms:keyboard";
            }
        }
        public static string EffectiveKey(ActionItem item) { return !String.IsNullOrEmpty(item.Icon)?item.Icon:item.AutomaticIcon??DefaultKey(item); }
        public static System.Windows.FrameworkElement Visual(ActionItem item,double size) {
            if(IsText(item.Icon)) return RingSurface.TextLabel(Caption(item),Math.Max(36,size*2));
            if(CustomIcons.IsImage(item.Icon)) {
                try { return new System.Windows.Controls.Image { Source=CustomIcons.Read(item.Icon),Width=size,Height=size,Stretch=System.Windows.Media.Stretch.Uniform }; }
                catch { return Visual(new ActionItem(item.Label,item.Kind,item.Value),size); }
            }
            MaterialIcon icon=MaterialIcons.Find(EffectiveKey(item));
            if(icon!=null) return icon.Visual(size);
            return new System.Windows.Controls.TextBlock { Text=Ui.Glyph(item),FontSize=size,Foreground=Ui.Ink,FontFamily=new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),TextWrapping=System.Windows.TextWrapping.NoWrap };
        }
    }
}

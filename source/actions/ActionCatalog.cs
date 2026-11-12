using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    sealed class ActionPreset {
        public string Category,Keywords;
        public ActionItem Item;
        public ActionPreset(string category,string label,string kind,string value,string keywords) { Category=category; Item=new ActionItem(label,kind,value); Keywords=keywords; }
        public ActionPreset(string category,ActionItem item) { Category=category; Item=item; Keywords=""; }
        public ActionItem Create() {
            ActionItem item=UserContent.CopyAction(Item); if(item.Kind=="stack" && item.Children==null) return PlanetStack.Create();
            if(item.Kind=="toggle" && item.Toggle==null) {
                item.Toggle=item.Value=="system-mute"?ToggleDefinition.CreateMute():item.Value=="microphone-mute"?ToggleDefinition.CreateMicrophoneMute():ToggleDefinition.Create();
                item.Icon=item.Value=="system-mute"?"ms:volume_up":item.Value=="microphone-mute"?"ms:mic":"ms:toggle_off"; item.Value="";
            }
            return item;
        }
        public bool Matches(string query) { return (Category+" "+Item.Label+" "+Item.Value+" "+Keywords).IndexOf((query??"").Trim(),StringComparison.OrdinalIgnoreCase)>=0; }
    }
    static class ActionCatalog {
        public static readonly string[] Categories={"編集・入力","移動・タブ","スクロール・ズーム","ウィンドウ・デスクトップ","音声・メディア","ファイル・アプリ","Windowsのツール","カスタム操作","orbitals"};
        public static string CategoryFor(ActionItem item) {
            if(AudioMute.IsToggle(item)) return Categories[4];
            ActionPreset found=Create().FirstOrDefault(p=>p.Item.Kind==item.Kind && p.Item.Value==item.Value); if(found!=null) return found.Category;
            switch(item.Kind) {
                case "text": case "stamp": case "search": case "pasteplain": return Categories[0];
                case "wheel": return Categories[2];
                case "window": case "window-size": return Categories[3];
                case "volume": case "mute": case "microphone": case "output": case "audiosession": case "audiodevice": return Categories[4];
                case "open": case "launch": case "convert": return Categories[5];
                case "settings": return Categories[6];
                case "orbitals": case "planet": return Categories[8];
                default: return Categories[7];
            }
        }
        public static List<ActionPreset> Create() {
            return new List<ActionPreset> {
                new ActionPreset(Categories[0],"コピー","shortcut","Ctrl+C","copy"),
                new ActionPreset(Categories[0],"切り取り","shortcut","Ctrl+X","cut"),
                new ActionPreset(Categories[0],"貼り付け","shortcut","Ctrl+V","paste"),
                new ActionPreset(Categories[0],"書式なし貼り付け","pasteplain","","paste plain 書式"),
                new ActionPreset(Categories[0],"クリップボード履歴","shortcut","Win+V","clipboard history 履歴"),
                new ActionPreset(Categories[0],"元に戻す","shortcut","Ctrl+Z","undo"),
                new ActionPreset(Categories[0],"やり直す","shortcut","Ctrl+Y","redo"),
                new ActionPreset(Categories[0],"保存","shortcut","Ctrl+S","save"),
                new ActionPreset(Categories[0],"すべて選択","shortcut","Ctrl+A","select all"),
                new ActionPreset(Categories[0],"検索","shortcut","Ctrl+F","find search"),
                new ActionPreset(Categories[0],"選択した文字を検索","search","https://www.google.com/search?q={text}","search 選択 テキスト"),
                new ActionPreset(Categories[0],"定型文を入力","text","","text snippet 文字"),
                new ActionPreset(Categories[0],"絵文字・記号","shortcut","Win+OemPeriod","emoji 顔文字 記号"),
                new ActionPreset(Categories[0],"日付を入力","stamp","date","date 日付 今日"),
                new ActionPreset(Categories[0],"時刻を入力","stamp","time","time 時間 現在"),
                new ActionPreset(Categories[0],"日付と時刻を入力","stamp","datetime","timestamp タイムスタンプ"),
                new ActionPreset(Categories[1],"戻る","shortcut","Alt+Left","back"),
                new ActionPreset(Categories[1],"進む","shortcut","Alt+Right","forward"),
                new ActionPreset(Categories[1],"次のタブ","shortcut","Ctrl+Tab","tab"),
                new ActionPreset(Categories[1],"前のタブ","shortcut","Ctrl+Shift+Tab","tab"),
                new ActionPreset(Categories[1],"新規タブ","shortcut","Ctrl+T","tab new"),
                new ActionPreset(Categories[1],"タブを閉じる","shortcut","Ctrl+W","tab close"),
                new ActionPreset(Categories[1],"閉じたタブを戻す","shortcut","Ctrl+Shift+T","tab restore"),
                new ActionPreset(Categories[1],"再読み込み","shortcut","Ctrl+R","reload refresh"),
                new ActionPreset(Categories[1],"アドレス欄を選択","shortcut","Ctrl+L","address url アドレスバー"),
                new ActionPreset(Categories[1],"メニューバーを選択","shortcut","Alt","menu メニュー"),
                new ActionPreset(Categories[1],"上のフォルダーへ移動","shortcut","Alt+Up","parent 親 フォルダー"),
                new ActionPreset(Categories[2],"その場所で拡大","wheel","Ctrl+Up","zoom ズーム 拡大"),
                new ActionPreset(Categories[2],"その場所で縮小","wheel","Ctrl+Down","zoom ズーム 縮小"),
                new ActionPreset(Categories[2],"表示倍率を100％に戻す","shortcut","Ctrl+0","zoom reset リセット"),
                new ActionPreset(Categories[2],"上へスクロール","wheel","Up","scroll wheel 縦 ホイール"),
                new ActionPreset(Categories[2],"下へスクロール","wheel","Down","scroll wheel 縦 ホイール"),
                new ActionPreset(Categories[2],"左へスクロール","wheel","Left","horizontal scroll 横 ホイール"),
                new ActionPreset(Categories[2],"右へスクロール","wheel","Right","horizontal scroll 横 ホイール"),
                new ActionPreset(Categories[2],"ページの先頭へ","shortcut","Ctrl+Home","top 先頭"),
                new ActionPreset(Categories[2],"ページの末尾へ","shortcut","Ctrl+End","bottom 末尾"),
                new ActionPreset(Categories[3],"最大化 / 元に戻す","window","maximize","maximize"),
                new ActionPreset(Categories[3],"最小化","window","minimize","minimize"),
                new ActionPreset(Categories[3],"左半分に配置","window","left","snap"),
                new ActionPreset(Categories[3],"右半分に配置","window","right","snap"),
                new ActionPreset(Categories[3],"画面の中央に移動","window","center","center 中央"),
                new ActionPreset(Categories[3],"ウィンドウのサイズ指定","window-size","1280x720","resize 幅 高さ サイズ"),
                new ActionPreset(Categories[3],"次のモニターへ移動","window","next-monitor","monitor display"),
                new ActionPreset(Categories[3],"最前面を切替","window","topmost","always on top"),
                new ActionPreset(Categories[3],"タスクビュー","shortcut","Win+Tab","task view 一覧"),
                new ActionPreset(Categories[3],"デスクトップを表示","shortcut","Win+D","show desktop"),
                new ActionPreset(Categories[3],"デスクトップを作成","shortcut","Win+Ctrl+D","virtual desktop 仮想"),
                new ActionPreset(Categories[3],"次のデスクトップ","shortcut","Win+Ctrl+Right","virtual desktop 仮想"),
                new ActionPreset(Categories[3],"前のデスクトップ","shortcut","Win+Ctrl+Left","virtual desktop 仮想"),
                new ActionPreset(Categories[3],"デスクトップを閉じる","shortcut","Win+Ctrl+F4","virtual desktop 仮想"),
                new ActionPreset(Categories[4],"音量（ホイール）","volume","","volume 音量調整"),
                new ActionPreset(Categories[4],"ミュート切替","toggle","system-mute","mute 消音"),
                new ActionPreset(Categories[4],"音量を上げる","shortcut","VolumeUp","volume"),
                new ActionPreset(Categories[4],"音量を下げる","shortcut","VolumeDown","volume"),
                new ActionPreset(Categories[4],"対象アプリの音量を上げる","audiosession","up","app volume アプリ別"),
                new ActionPreset(Categories[4],"対象アプリの音量を下げる","audiosession","down","app volume アプリ別"),
                new ActionPreset(Categories[4],"対象アプリのミュート切替","audiosession","mute","app mute アプリ別"),
                new ActionPreset(Categories[4],"マイクのミュート切替","toggle","microphone-mute","microphone 通話 mute ON OFF"),
                new ActionPreset(Categories[4],"音声出力先を直接切替","audiodevice","","audio device スピーカー ヘッドホン"),
                new ActionPreset(Categories[4],"音声出力先の選択画面","output","","audio device 出力"),
                new ActionPreset(Categories[4],"再生 / 一時停止","shortcut","MediaPlayPause","play pause"),
                new ActionPreset(Categories[4],"次のトラック","shortcut","MediaNextTrack","next 音楽"),
                new ActionPreset(Categories[4],"前のトラック","shortcut","MediaPreviousTrack","previous 音楽"),
                new ActionPreset(Categories[5],"エクスプローラー","shortcut","Win+E","explorer folder フォルダー"),
                new ActionPreset(Categories[5],"ドキュメント","open",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"documents フォルダー"),
                new ActionPreset(Categories[5],"デスクトップフォルダー","open",Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"desktop フォルダー"),
                new ActionPreset(Categories[5],"ファイル・フォルダー・URL","open","","file folder url ウェブ"),
                new ActionPreset(Categories[5],"アプリを起動 / 前面へ","launch","","launch activate アプリ"),
                new ActionPreset(Categories[5],"ファイル変換","convert","","convert 変換 画像 PDF WebP PNG JPEG 結合 分割"),
                new ActionPreset(Categories[5],"画像をPDFにまとめる","convert","image-pdf","image PDF 変換"),
                new ActionPreset(Categories[5],"PDFを画像にする","convert","pdf-png","PDF PNG 変換"),
                new ActionPreset(Categories[5],"ZIPに圧縮","convert","zip","ZIP compress 圧縮 ファイル フォルダー まとめる"),
                new ActionPreset(Categories[5],"ZIPを展開","convert","zip-extract","ZIP extract 展開 解凍"),
                new ActionPreset(Categories[6],"画面切り取り","shortcut","Win+Shift+S","screenshot スクリーンショット"),
                new ActionPreset(Categories[6],"画面キャプチャ","shortcut","PrintScreen","screenshot capture 全画面 PrintScreen"),
                new ActionPreset(Categories[6],"ウィンドウの画像をコピー","shortcut","Alt+PrintScreen","screenshot capture ウィンドウ"),
                new ActionPreset(Categories[6],"Windowsの検索","shortcut","Win+S","Windows search"),
                new ActionPreset(Categories[6],"通知センター","shortcut","Win+N","notification アクションセンター"),
                new ActionPreset(Categories[6],"クイック設定","shortcut","Win+A","quick settings"),
                new ActionPreset(Categories[6],"タスクマネージャー","shortcut","Ctrl+Shift+Escape","task manager"),
                new ActionPreset(Categories[6],"PCをロック","shortcut","Win+L","lock"),
                new ActionPreset(Categories[6],"Windowsの設定","settings","home","settings 設定"),
                new ActionPreset(Categories[6],"ディスプレイ設定","settings","display","display ディスプレイ"),
                new ActionPreset(Categories[6],"サウンド設定","settings","sound","sound 音量"),
                new ActionPreset(Categories[6],"Bluetoothの設定","settings","bluetooth","bluetooth デバイス"),
                new ActionPreset(Categories[7],"キー / ショートカット","shortcut","","keyboard shortcut カスタム"),
                new ActionPreset(Categories[7],"押している間キーを保持","hold","Ctrl","hold 長押し キーボード"),
                new ActionPreset(Categories[7],"複数段階トグル","toggle","","ON OFF 切替 トグル toggle"),
                new ActionPreset(Categories[7],"マクロ","macro","Ctrl+C\nwait:200\nCtrl+V","macro 連続操作 記録 待機 キー"),
                new ActionPreset(Categories[7],"スタック","stack","","stack フォルダ まとめる"),
                new ActionPreset(Categories[8],"planetを開く","planet","","ring リング planet 呼び出す ジェスチャー"),
                new ActionPreset(Categories[8],"orbitalsを一時停止","orbitals","pause","pause 一時停止"),
                new ActionPreset(Categories[8],"現在のアプリを除外","orbitals","exclude","exclude ブラックリスト 対象外")
            };
        }
    }
}

using System;
using System.Linq;

namespace OrbitRing {
    static class ActionOptions {
        public static void Get(string kind,string current,out string[] codes,out string[] names,Preferences config=null) {
            codes=null; names=null;
            if(kind=="window") { codes=ExtendedActions.WindowValues; names=ExtendedActions.WindowNames; }
            else if(kind=="orbitals") { codes=new[]{"pause","exclude"}; names=new[]{"一時停止（通知領域から再開）","現在のアプリを除外"}; }
            else if(kind=="wheel") { codes=ExtendedActions.WheelValues; names=new[]{"Ctrl ＋ 上（拡大）","Ctrl ＋ 下（縮小）","上へスクロール","下へスクロール","左へスクロール","右へスクロール"}; }
            else if(kind=="audiosession") { codes=new[]{"up","down","mute"}; names=new[]{"音量を5％上げる","音量を5％下げる","ミュート切替"}; }
            else if(kind=="microphone") { codes=new[]{"","on","off"}; names=new[]{"ミュートを切り替え","ミュートON","ミュートOFF"}; }
            else if(kind=="stamp") { codes=new[]{"date","time","datetime"}; names=new[]{"日付（yyyy/MM/dd）","時刻（HH:mm）","日付と時刻"}; }
            else if(kind=="settings") { codes=ExtendedActions.SettingsValues; names=ExtendedActions.SettingsNames; }
            else if(kind=="convert") { codes=new[]{""}.Concat(FileConversion.Operations).ToArray(); names=new[]{"形式をドロップで選ぶ"}.Concat(FileConversion.Names).ToArray(); }
            else if(kind=="plugin" && config!=null) {
                var actions=ActionPlugins.Presets(config).ToList(); codes=actions.Select(p=>p.Item.Value).ToArray(); names=actions.Select(p=>p.Category+" / "+p.Item.Label).ToArray();
                if(!String.IsNullOrEmpty(current) && !codes.Contains(current)) { codes=new[]{current}.Concat(codes).ToArray(); names=new[]{"未登録・無効のアクション"}.Concat(names).ToArray(); }
            }
            else if(kind=="audiodevice") {
                try { var devices=AudioActions.Devices(); codes=devices.Select(d=>d.Id).ToArray(); names=devices.Select(d=>d.Name).ToArray(); }
                catch { codes=new string[0]; names=new string[0]; }
                if(!String.IsNullOrEmpty(current) && !codes.Contains(current)) { codes=new[]{current}.Concat(codes).ToArray(); names=new[]{"保存した機器（未接続）"}.Concat(names).ToArray(); }
            }
        }
    }
}

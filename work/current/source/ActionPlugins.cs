using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace OrbitRing {
    public sealed class PluginAction {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Icon { get; set; }
        public string Kind { get; set; }
        public string Value { get; set; }
        public string Executable { get; set; }
        public List<string> Arguments { get; set; }
        public PluginAction() { Arguments=new List<string>(); }
    }
    public sealed class ActionPlugin {
        public int SchemaVersion { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Application { get; set; }
        public bool Enabled { get; set; }
        public List<PluginAction> Actions { get; set; }
        public ActionPlugin() { SchemaVersion=1; Enabled=true; Actions=new List<PluginAction>(); }
    }
    static class ActionPlugins {
        public const int Maximum=16;
        public static bool ValidId(string id) { return id!=null && Regex.IsMatch(id,"\\A[a-z0-9][a-z0-9-]{0,39}\\z",RegexOptions.CultureInvariant); }
        public static bool ValidReference(string value) { string[] parts=(value??"").Split('/'); return parts.Length==2 && parts.All(ValidId); }
        public static void Validate(ActionPlugin plugin,Chord hotkey) {
            if(plugin==null || plugin.SchemaVersion!=1 || !ValidId(plugin.Id) || String.IsNullOrWhiteSpace(plugin.Name) || plugin.Name.Length>40 || plugin.Name.Any(Char.IsControl) || (!String.IsNullOrEmpty(plugin.Application) && !OrbitPreferences.ValidExecutable(plugin.Application)) || plugin.Actions==null || plugin.Actions.Count<1 || plugin.Actions.Count>64) throw new Exception("プラグインの名前・対象アプリ・アクション数を確認してください。");
            HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(PluginAction action in plugin.Actions) {
                if(action==null || !ValidId(action.Id) || !ids.Add(action.Id) || String.IsNullOrWhiteSpace(action.Label) || action.Label.Length>20 || action.Label.Any(Char.IsControl)) throw new Exception("プラグインのアクション名かIDが不正です。");
                ActionIcons.Validate(action.Icon);
                if(action.Kind=="program") {
                    if(String.IsNullOrWhiteSpace(action.Executable) || !Path.IsPathRooted(action.Executable) || !String.Equals(Path.GetExtension(action.Executable),".exe",StringComparison.OrdinalIgnoreCase) || action.Executable.Any(Char.IsControl) || action.Arguments==null || action.Arguments.Count>32 || action.Arguments.Any(a=>a==null || a.IndexOf('\0')>=0) || action.Arguments.Sum(a=>a.Length)>8192) throw new Exception("プログラムのexeパスと引数を確認してください。");
                } else {
                    if(action.Kind!="shortcut" && action.Kind!="macro" && action.Kind!="text") throw new Exception("プラグインの操作はshortcut・macro・text・programから選んでください。");
                    Preferences.ValidateAction(new ActionItem(action.Label,action.Kind,action.Value),hotkey);
                }
            }
        }
        public static void ValidateAll(List<ActionPlugin> plugins,Chord hotkey) {
            if(plugins==null || plugins.Count>Maximum) throw new Exception("プラグインは16件まで登録できます。");
            HashSet<string> ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(ActionPlugin plugin in plugins) { Validate(plugin,hotkey); if(!ids.Add(plugin.Id)) throw new Exception("プラグインのIDが重複しています。"); }
        }
        public static ActionPlugin Import(string path,Chord hotkey) {
            if(new FileInfo(path).Length>262144) throw new Exception("プラグイン定義は256KB以内にしてください。");
            ActionPlugin plugin=Json.ReadFile<ActionPlugin>(path); if(plugin==null || plugin.Actions==null) throw new Exception("プラグイン定義が空です。");
            string root=Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(path)))+Path.DirectorySeparatorChar;
            foreach(PluginAction action in plugin.Actions.Where(a=>a!=null && a.Kind=="program")) {
                if(String.IsNullOrWhiteSpace(action.Executable) || Path.IsPathRooted(action.Executable)) throw new Exception("定義ファイルからの相対パスでexeを指定してください。");
                string executable=Path.GetFullPath(Path.Combine(root,action.Executable));
                if(!executable.StartsWith(root,StringComparison.OrdinalIgnoreCase) || !File.Exists(executable)) throw new Exception("プラグインのフォルダー内に実行するexeを置いてください。");
                action.Executable=executable;
            }
            Validate(plugin,hotkey); return plugin;
        }
        public static PluginAction Resolve(Preferences config,string reference,string application) {
            if(!ValidReference(reference)) throw new Exception("プラグインのアクションを選び直してください。");
            string[] parts=reference.Split('/'); ActionPlugin plugin=config.Plugins.FirstOrDefault(p=>p.Id==parts[0]);
            if(plugin==null || !plugin.Enabled) throw new Exception("このプラグインは未登録か無効です。全体設定から確認してください。");
            if(!String.IsNullOrEmpty(plugin.Application) && !String.Equals(plugin.Application,application,StringComparison.OrdinalIgnoreCase)) throw new Exception("このアクションは「"+plugin.Application+"」で使用できます。");
            PluginAction action=plugin.Actions.FirstOrDefault(a=>a.Id==parts[1]); if(action==null) throw new Exception("プラグインにこのアクションが見つかりません。割り当て直してください。"); return action;
        }
        public static IEnumerable<ActionPreset> Presets(Preferences config) {
            foreach(ActionPlugin plugin in config.Plugins.Where(p=>p.Enabled)) foreach(PluginAction action in plugin.Actions) yield return new ActionPreset(plugin.Name,new ActionItem(action.Label,"plugin",plugin.Id+"/"+action.Id) { Icon=action.Icon });
        }
    }
}

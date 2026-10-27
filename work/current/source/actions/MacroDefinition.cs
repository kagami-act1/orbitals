using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace OrbitRing {
    sealed class MacroProgram {
        public int Version { get; set; }
        public List<MacroStep> Steps { get; set; }
    }
    static class MacroDefinition {
        public static readonly string[] Kinds={"shortcut","wait","launch","open","text","wait-window","window","window-size","stamp","audiosession","audiodevice","volume","mute","microphone","settings"};
        public static readonly string[] Names={"キー","時間を待つ","アプリを起動 / 前面へ","ファイル・フォルダー・URLを開く","文字を入力","操作先のウィンドウを待つ","ウィンドウ操作","ウィンドウのサイズ指定","日付・時刻を入力","対象アプリの音量","音声出力先を切替","音量のミュートを切替","音量のミュートON / OFF","マイクのミュート","Windowsの設定を開く"};
        public static string Name(string kind) { int index=Array.IndexOf(Kinds,kind); return index<0?kind:Names[index]; }
        public static MacroStep Create(string kind) { return new MacroStep { Kind=kind,Value=kind=="shortcut"?"Space":kind=="mute"?"on":ExtendedActions.DefaultValue(kind),Delay=kind=="wait"?200:0,Timeout=5000,Application="",Title="" }; }
        public static List<MacroStep> Read(string text) {
            if((text??"").TrimStart().StartsWith("{",StringComparison.Ordinal)) { MacroProgram program=Json.Read<MacroProgram>(text); if(program==null || program.Version!=1 || program.Steps==null) throw new Exception("このマクロの形式には対応していません。"); return program.Steps; }
            List<MacroStep> result=new List<MacroStep>();
            foreach(string raw in (text??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) { string line=raw.Trim(); if(line.Length==0) continue; if(line.StartsWith("wait:",StringComparison.OrdinalIgnoreCase)) { int delay; if(!Int32.TryParse(line.Substring(5).Trim(),out delay)) throw new Exception("待機時間を確認してください。"); result.Add(new MacroStep { Kind="wait",Delay=delay }); } else result.Add(new MacroStep { Kind="shortcut",Value=line }); }
            return result;
        }
        public static List<MacroStep> Parse(string text) {
            bool rich=(text??"").TrimStart().StartsWith("{",StringComparison.Ordinal);
            if(text!=null && text.Length>(rich?8192:4096)) throw new Exception("マクロの内容が長すぎます。");
            List<MacroStep> steps=Read(text); if(steps.Count<1 || steps.Count>64) throw new Exception("マクロには1〜64個のステップを指定してください。"); int total=0;
            foreach(MacroStep step in steps) {
                if(step==null || !Kinds.Contains(step.Kind)) throw new Exception("マクロのステップの種類を確認してください。");
                if(step.Kind=="wait") { if(step.Delay<1 || step.Delay>30000) throw new Exception("待機時間は1〜30000ミリ秒で指定してください。"); total+=step.Delay; }
                else if(step.Kind=="wait-window") {
                    if(String.IsNullOrWhiteSpace(step.Application) && String.IsNullOrWhiteSpace(step.Title)) throw new Exception("待つウィンドウのアプリ名、またはタイトルの一部を指定してください。");
                    if((step.Application??"").Length>260 || (step.Title??"").Length>512 || (step.Application??"").Any(Char.IsControl) || (step.Title??"").Any(Char.IsControl) || step.Timeout<100 || step.Timeout>30000) throw new Exception("ウィンドウの条件と待機の上限（100〜30000ミリ秒）を確認してください。");
                    if(!String.IsNullOrEmpty(step.Application) && (Path.GetFileName(step.Application)!=step.Application || !step.Application.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))) throw new Exception("待つアプリは実行ファイル名で指定してください。例：notepad.exe"); total+=step.Timeout;
                } else if(step.Kind=="shortcut") Chord.Parse(step.Value);
                else if(step.Kind=="open") OpenTarget.Resolve(step.Value);
                else if(step.Kind=="mute") { if(step.Value!="on" && step.Value!="off") throw new Exception("ミュートON / OFFを選んでください。"); }
                else if(step.Kind!="volume" && !ExtendedActions.Validate(new ActionItem("ステップ",step.Kind,step.Value))) throw new Exception("未対応のステップです。");
            }
            if(total>60000) throw new Exception("マクロの待機時間は合計60秒以内にしてください。"); return steps;
        }
        public static string Write(IEnumerable<MacroStep> source) {
            List<MacroStep> steps=source.ToList();
            if(steps.All(s=>s.Kind=="shortcut" || s.Kind=="wait")) return String.Join("\n",steps.Select(s=>s.Kind=="wait"?"wait:"+s.Delay:s.Value));
            var rows=new List<Dictionary<string,object>>();
            foreach(MacroStep step in steps) { var row=new Dictionary<string,object> { {"Kind",step.Kind} }; if(step.Kind=="wait") row.Add("Delay",step.Delay); else if(step.Kind=="wait-window") { row.Add("Application",step.Application); row.Add("Title",step.Title); row.Add("Timeout",step.Timeout); } else row.Add("Value",step.Value); rows.Add(row); }
            return Json.Write(new { Version=1,Steps=rows });
        }
        public static bool Matches(MacroStep step,string executable,string title) { return (String.IsNullOrEmpty(step.Application) || String.Equals(step.Application,executable,StringComparison.OrdinalIgnoreCase)) && (String.IsNullOrEmpty(step.Title) || (title??"").IndexOf(step.Title,StringComparison.OrdinalIgnoreCase)>=0); }
        public static string ExpandText(string text,DateTime time,string clipboard) {
            string result=(text??"").Replace("{date}",time.ToString("yyyy/MM/dd",CultureInfo.InvariantCulture)).Replace("{time}",time.ToString("HH:mm",CultureInfo.InvariantCulture)).Replace("{datetime}",time.ToString("yyyy/MM/dd HH:mm",CultureInfo.InvariantCulture)).Replace("{clipboard}",clipboard??"");
            if(result.Length>4096) throw new Exception("差し込み後の文字は4096文字までです。"); return result;
        }
    }
    interface IMacroActions {
        bool SelectWindow(MacroStep step);
        void Execute(MacroStep step);
    }
}

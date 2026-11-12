using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class PluginTests {
        static void Check(bool condition,string label,List<string> result) { if(!condition) throw new Exception(label); result.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static ActionPlugin Definition() { return new ActionPlugin { Id="sample",Name="サンプル",Application="browser.exe",Actions=new List<PluginAction> { new PluginAction { Id="save",Label="保存",Kind="shortcut",Value="Ctrl+S",Icon="ms:save" } } }; }
        static ActionPlugin Copy(ActionPlugin value) { return Json.Read<ActionPlugin>(Json.Write(value)); }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults(); ActionPlugin plugin=Definition(); config.Plugins.Add(plugin); config.Items[0]=new ActionItem("保存","plugin","sample/save"); config.Validate();
            Preferences restored=config.Clone(); restored.Validate(); Check(restored.Plugins.Count==1 && restored.Items[0].Value=="sample/save","plugin definitions and assigned references survive settings serialization",r);
            Check(ActionPlugins.Resolve(config,"sample/save","BROWSER.EXE").Value=="Ctrl+S" && Reject(delegate { ActionPlugins.Resolve(config,"sample/save","other.exe"); }),"application plugins execute only for their declared foreground application",r);
            plugin.Enabled=false; Check(!ActionPlugins.Presets(config).Any() && Reject(delegate { ActionPlugins.Resolve(config,"sample/save","browser.exe"); }),"disabled plugins are unavailable in both the picker and execution",r); plugin.Enabled=true;
            plugin.Application=null; Check(ActionPlugins.Resolve(config,"sample/save","other.exe").Id=="save","universal plugins work without an application restriction",r); plugin.Application="browser.exe";
            Check(ActionPlugins.Presets(config).Single().Create().Kind=="plugin" && ActionIcons.Find(ActionIcons.DefaultKey(config.Items[0]))!=null,"plugin actions participate in the existing action picker and icon system",r);
            foreach(string invalid in new[]{"", "../save","sample/save/extra","Sample/save","sample\\save"}) Check(!ActionPlugins.ValidReference(invalid),"invalid plugin reference is rejected: "+invalid,r);
            ActionPlugin broken=Copy(plugin); broken.Actions.Add(broken.Actions[0]); Check(Reject(delegate { ActionPlugins.Validate(broken,Chord.Parse(config.Hotkey)); }),"duplicate action identities cannot alias plugin commands",r);
            broken=Copy(plugin); broken.Actions[0].Value=config.Hotkey; Check(Reject(delegate { ActionPlugins.Validate(broken,Chord.Parse(config.Hotkey)); }),"plugin shortcuts cannot recursively call the main ring hotkey",r);
            Preferences extra=config.Clone(); PlanetProfile second=UserContent.NewPlanet(extra); second.Hotkey="F13"; second.HotkeyEnabled=true; extra.Planets.Add(second); extra.Plugins[0].Actions[0].Value=second.Hotkey; Check(Reject(extra.Validate),"plugin shortcuts cannot recursively call another planet hotkey",r);
            broken=Copy(plugin); broken.Application="../browser.exe"; Check(Reject(delegate { ActionPlugins.Validate(broken,new Chord()); }),"plugin application matching accepts executable names rather than paths",r);
            config.Plugins.Add(Copy(plugin)); Check(Reject(config.Validate),"duplicate plugin registrations are rejected",r); config.Plugins.RemoveAt(1);
            config.Plugins.Clear(); config.Validate(); Check(Reject(delegate { ActionPlugins.Resolve(config,"sample/save","browser.exe"); }),"removing a plugin leaves placed slots intact and unavailable instead of breaking settings",r); config.Plugins.Add(plugin);
            string directory=Path.Combine(Path.GetTempPath(),"orbitals-plugin-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string manifest=Path.Combine(directory,"plugin.json"),source=Path.Combine(directory,"Probe.cs"),executable=Path.Combine(directory,"Probe.exe");
            try {
                File.WriteAllText(manifest,Json.Write(plugin),new UTF8Encoding(false)); ActionPlugin imported=ActionPlugins.Import(manifest,Chord.Parse(config.Hotkey)); Check(imported.Actions[0].Value=="Ctrl+S" && Directory.GetFiles(directory).Length==1,"importing a shortcut plugin reads metadata without running a program",r);
                File.WriteAllText(source,"using System; using System.Text; using System.Threading; class Probe { static int Main(string[] args) { Console.InputEncoding=new UTF8Encoding(false); Console.OutputEncoding=new UTF8Encoding(false); string request=Console.ReadLine(); if(args.Length>0 && args[0]==\"slow\") Thread.Sleep(10000); if(args.Length>0 && args[0]==\"fail\") { Console.Error.Write(\"sample error\"); return 7; } if(args.Length>0 && args[0]==\"flood\") { Console.Write(new string('x',50000)); return 0; } Console.Write(request); foreach(string value in args) Console.Write(\"|\"+Convert.ToBase64String(Encoding.UTF8.GetBytes(value))); return 0; } }",new UTF8Encoding(false));
                string compiler=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe");
                using(Process build=Process.Start(new ProcessStartInfo(compiler,"/nologo /target:exe /out:"+PluginRunner.Quote(executable)+" "+PluginRunner.Quote(source)) { UseShellExecute=false,CreateNoWindow=true })) { if(!build.WaitForExit(10000) || build.ExitCode!=0) throw new Exception("Plugin test helper compilation failed."); }
                PluginAction program=new PluginAction { Id="code",Label="コード",Kind="program",Executable="Probe.exe",Arguments=new List<string> { "a b","quoted\"value", "C:\\end\\", "日本語" } }; plugin.Actions.Add(program); File.WriteAllText(manifest,Json.Write(plugin),new UTF8Encoding(false));
                imported=ActionPlugins.Import(manifest,Chord.Parse(config.Hotkey)); Check(imported.Actions[1].Executable==executable && imported.Actions[1].Arguments.SequenceEqual(program.Arguments),"code plugin imports resolve the local executable and preserve distinct arguments",r);
                program.Executable="../outside.exe"; File.WriteAllText(manifest,Json.Write(plugin)); Check(Reject(delegate { ActionPlugins.Import(manifest,new Chord()); }),"relative traversal outside a plugin folder is rejected",r);
                program.Executable=executable; File.WriteAllText(manifest,Json.Write(plugin)); Check(Reject(delegate { ActionPlugins.Import(manifest,new Chord()); }),"imported program paths must be relative to the selected definition",r);
                PluginRequest request=new PluginRequest { Version=1,ActionId="code",Application="アプリ.exe",WindowHandle="123",CursorX=-120,CursorY=500 }; string expected=Json.Write(request)+String.Concat(imported.Actions[1].Arguments.Select(a=>"|"+Convert.ToBase64String(Encoding.UTF8.GetBytes(a))));
                using(PluginRunner runner=new PluginRunner()) {
                    string response=runner.Run(imported.Actions[1],request).GetAwaiter().GetResult(); Check(response==expected.Substring(0,Math.Min(240,expected.Length)),"external plugins receive UTF-8 context and correctly quoted arguments without shell expansion",r);
                    PluginAction fail=Json.Read<PluginAction>(Json.Write(imported.Actions[1])); fail.Arguments=new List<string>{"fail"}; Check(Reject(delegate { runner.Run(fail,request).GetAwaiter().GetResult(); }),"external plugin failures do not propagate into the host action loop",r);
                    fail.Arguments[0]="flood"; Check(runner.Run(fail,request).GetAwaiter().GetResult().Length==240,"external plugin output is drained with a bounded notification size",r);
                }
                PluginRunner limited=new PluginRunner(); PluginAction slow=Json.Read<PluginAction>(Json.Write(imported.Actions[1])); slow.Arguments=new List<string>{"slow"}; Task<string> one=limited.Run(slow,request),two=limited.Run(slow,request);
                Check(Reject(delegate { limited.Run(slow,request).GetAwaiter().GetResult(); }),"external plugin concurrency is limited to two active processes",r);
                Stopwatch stopped=Stopwatch.StartNew(); limited.Dispose(); try { Task.WhenAll(one,two).GetAwaiter().GetResult(); } catch(OperationCanceledException) { }
                Check(one.IsCanceled && two.IsCanceled && stopped.ElapsedMilliseconds<2000,"closing the host promptly cancels its own external plugin processes",r);
                using(Controller owner=new Controller(true)) {
                    owner.Apply(Preferences.Defaults()); SettingsWindow editor=new SettingsWindow(owner); BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    typeof(SettingsWindow).GetMethod("AddPlugin",flags).Invoke(editor,new object[]{imported}); Preferences draft=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor);
                    Check(draft.Plugins.Count==1 && owner.Config.Plugins.Count==0,"plugin imports remain editable until settings are saved",r);
                    typeof(SettingsWindow).GetMethod("Undo",flags).Invoke(editor,null); draft=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor); Check(draft.Plugins.Count==0,"undo removes a newly imported plugin definition",r);
                    typeof(SettingsWindow).GetMethod("Redo",flags).Invoke(editor,null); draft=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor); Check(draft.Plugins.Count==1,"redo restores imported plugin metadata",r);
                    editor.Navigate(1); ComboBox picker=(ComboBox)typeof(SettingsWindow).GetField("catalogView",flags).GetValue(editor); picker.SelectedIndex=2;
                    TextBlock count=(TextBlock)typeof(SettingsWindow).GetField("catalogCount",flags).GetValue(editor); Check(count.Text.StartsWith("2 件"),"gesture and ring editors share the imported plugin action catalog",r);
                    typeof(SettingsWindow).GetMethod("RestoreSaved",flags).Invoke(editor,null); editor.ReleaseResources(); editor.Close();
                }
            } finally { foreach(string file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
            return r;
        }
    }
}

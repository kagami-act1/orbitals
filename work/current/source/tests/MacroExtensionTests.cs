using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class MacroExtensionTests {
        sealed class Input : IActionInput {
            public readonly List<ushort> Keys=new List<ushort>();
            public bool IsDown(ushort key) { return false; }
            public void Send(ushort key,bool up) { if(!up) Keys.Add(key); }
        }
        sealed class Host : IMacroActions {
            public bool Ready,Fail;
            public int Checks;
            public readonly List<string> Events=new List<string>();
            public bool SelectWindow(MacroStep step) { Checks++; return Ready; }
            public void Execute(MacroStep step) { if(Fail) throw new Exception("test failure"); Events.Add(step.Kind+":"+step.Value); }
        }
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject { if(root is T) yield return (T)root; foreach(object child in LogicalTreeHelper.GetChildren(root)) { DependencyObject element=child as DependencyObject; if(element!=null) foreach(T item in Children<T>(element)) yield return item; } }
        public static List<string> Run() {
            var result=new List<string>(); DateTime now=new DateTime(2026,10,27,12,34,0);
            string legacy="Ctrl+C\nwait:200\nCtrl+V";
            Check(MacroDefinition.Write(MacroStep.Parse(legacy))==legacy,"editing a legacy keyboard macro preserves its supported text format",result);
            MacroStep wait=new MacroStep { Kind="wait-window",Application="notepad.exe",Title="メモ",Timeout=500 };
            string text=MacroDefinition.Write(new[]{new MacroStep { Kind="text",Value="{date} {clipboard}" },wait,new MacroStep { Kind="shortcut",Value="Ctrl+S" }});
            var parsed=MacroStep.Parse(text);
            Check(parsed.Count==3 && parsed[0].Value=="{date} {clipboard}" && parsed[1].Timeout==500 && parsed[2].Key=="Ctrl+S","extended macros retain action values window conditions and legacy keyboard chords",result);
            Check(MacroDefinition.Matches(wait,"NOTEPAD.EXE","作業メモ - Notepad") && !MacroDefinition.Matches(wait,"other.exe","作業メモ") && !MacroDefinition.Matches(wait,"notepad.exe","other"),"window waits require both the executable and title conditions when supplied",result);
            Check(MacroDefinition.ExpandText("{date} {time} {clipboard}",now,"literal {date}")=="2026/10/27 12:34 literal {date}","text substitutions capture date time and clipboard without reinterpreting clipboard content",result);
            Check(Reject(delegate { MacroDefinition.ExpandText("{clipboard}",now,new string('x',4097)); }),"expanded text respects the input size limit",result);
            Check(Reject(delegate { MacroStep.Parse("{\"Version\":2,\"Steps\":[]}"); }) && Reject(delegate { MacroStep.Parse(MacroDefinition.Write(new[]{new MacroStep { Kind="macro",Value=legacy }})); }) && Reject(delegate { MacroStep.Parse(MacroDefinition.Write(new[]{new MacroStep { Kind="wait-window",Timeout=500 }})); }),"macro programs reject unknown versions recursive steps and unconstrained window waits",result);
            Check(Reject(delegate { MacroStep.Parse(MacroDefinition.Write(new[]{new MacroStep { Kind="wait-window",Application="C:\\notepad.exe",Timeout=500 }})); }) && Reject(delegate { MacroStep.Parse(MacroDefinition.Write(Enumerable.Repeat(new MacroStep { Kind="wait-window",Application="notepad.exe",Timeout=30000 },3))); }),"window waits reject paths and enforce a bounded total timeout",result);
            Preferences config=Preferences.Defaults(); config.Items[0]=new ActionItem("連続","macro",text); config.Validate(); Check(config.Clone().Items[0].Value==text,"extended macro programs survive preference serialization without changing their steps",result);
            config.Items[0].Value=MacroDefinition.Write(new[]{new MacroStep { Kind="text",Value="test" },new MacroStep { Kind="shortcut",Value=config.Hotkey }}); Check(Reject(config.Validate),"extended macro chords cannot invoke a planet recursively",result);
            Input input=new Input(); Host host=new Host(); ActionPlayback player=new ActionPlayback(input) { MacroActions=host };
            string program=MacroDefinition.Write(new[]{wait,new MacroStep { Kind="text",Value="hello" },new MacroStep { Kind="shortcut",Value="F8" }});
            player.StartMacro(program,now); player.Tick(now,false,false); player.Tick(now.AddMilliseconds(100),false,false);
            Check(player.Active && input.Keys.Count==0 && host.Events.Count==0 && host.Checks==2,"window waits tolerate launch focus transitions without sending input prematurely",result);
            host.Ready=true; player.Tick(now.AddMilliseconds(150),false,false); player.Tick(now.AddMilliseconds(180),true,false); player.Tick(now.AddMilliseconds(210),true,false); player.Tick(now.AddMilliseconds(240),true,false);
            Check(player.Completed && !player.Active && host.Events.SequenceEqual(new[]{"text:hello"}) && input.Keys.SequenceEqual(new ushort[]{0x77}),"matched window waits resume ordered actions and keys exactly once",result);
            host.Ready=false; player.StartMacro(program,now); player.Tick(now,false,false); bool timedOut=false;
            try { player.Tick(now.AddMilliseconds(500),false,false); } catch(Exception ex) { timedOut=ex.Message.Contains("ステップ 1"); }
            Check(timedOut && !player.Active && !player.NeedsRelease,"window wait timeout stops the macro and identifies the failed step",result);
            player.StartMacro(program,now); player.Tick(now,false,true); Check(!player.Active,"Escape cancels window waits before later actions",result);
            host.Ready=true; player.StartMacro(program,now); player.Tick(now,true,false); int before=host.Events.Count; player.Tick(now.AddMilliseconds(30),false,false);
            Check(!player.Active && host.Events.Count==before,"focus changes after retargeting cancel before typing into another window",result);
            host.Fail=true; player.StartMacro(MacroDefinition.Write(new[]{new MacroStep { Kind="text",Value="fail" },new MacroStep { Kind="shortcut",Value="F9" }}),now); int keys=input.Keys.Count;
            Check(Reject(delegate { player.Tick(now,true,false); }) && !player.Active && input.Keys.Count==keys,"failed action steps stop remaining key input",result);
            host.Fail=false; player.StartMacro(MacroDefinition.Write(new[]{new MacroStep { Kind="text",Value="last" }}),now); player.Tick(now,true,false); player.Tick(now.AddMilliseconds(30),false,false);
            Check(player.Completed,"a final action can complete after its own foreground transition",result);
            using(Controller owner=new Controller(true)) {
                Preferences sample=Preferences.Defaults(); sample.Items[0]=new ActionItem("連続","macro",text); owner.Apply(sample); string saved=Json.Write(owner.Config);
                SettingsWindow editor=new SettingsWindow(owner); BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
                try {
                    StackPanel rows=(StackPanel)typeof(SettingsWindow).GetField("macroRows",flags).GetValue(editor);
                    Check(Children<ComboBox>(rows).Count(c=>Object.ReferenceEquals(c.ItemsSource,MacroDefinition.Names))==4 && Children<TextBox>(rows).Any(t=>t.Text=="Ctrl+S" && t.IsReadOnly),"macro editor offers typed action steps while keeping keyboard entry in recording mode",result);
                    Button test=Children<Button>(rows).First(b=>Object.Equals(b.Content,"このステップを試す")); test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Check(Json.Write(owner.Config)==saved,"testing a step in diagnostic mode sends no input and does not alter saved settings",result);
                    ComboBox first=Children<ComboBox>(rows).First(c=>Object.ReferenceEquals(c.ItemsSource,MacroDefinition.Names)); first.SelectedIndex=Array.IndexOf(MacroDefinition.Kinds,"window");
                    Preferences draft=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor);
                    Check(MacroStep.Parse(draft.Items[0].Value)[0].Kind=="window" && Json.Write(owner.Config)==saved,"editing a macro step changes only the draft and preserves subsequent steps",result);
                    typeof(SettingsWindow).GetMethod("Undo",flags).Invoke(editor,null); draft=(Preferences)typeof(SettingsWindow).GetField("draft",flags).GetValue(editor);
                    Check(MacroStep.Parse(draft.Items[0].Value)[0].Kind=="text","undo restores extended macro step values and types",result);
                } finally { typeof(SettingsWindow).GetMethod("RestoreSaved",flags).Invoke(editor,null); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

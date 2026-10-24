using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class SettingsOrganizationTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(object target,string name) { return target.GetType().GetField(name,Private).GetValue(target); }
        static void Set(object target,string name,object value) { target.GetType().GetField(name,Private).SetValue(target,value); }
        static void Call(object target,string name,params object[] args) { target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static void Check(bool value,string label,List<string> result) { if(!value) throw new Exception(label); result.Add("PASS "+label); }
        static IEnumerable<T> Find<T>(DependencyObject parent) where T:DependencyObject { if(parent is T) yield return (T)parent; foreach(object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject item=child as DependencyObject; if(item!=null) foreach(T nested in Find<T>(item)) yield return nested; } }
        static IEnumerable<T> Settings<T>(SettingsWindow editor) where T:DependencyObject { return Find<T>((DependencyObject)Field(editor,"settingsPanel")); }
        static Preferences Draft(SettingsWindow editor) { return (Preferences)Field(editor,"draft"); }
        public static List<string> Run() {
            List<string> result=new List<string>();
            using(Controller owner=new Controller(true)) {
                owner.Apply(Preferences.Defaults()); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    string original=Json.Write(Draft(editor));
                    Check(!Find<Button>((DependencyObject)editor.Content).Any(b=>new[]{"前の画面に戻る","次の画面に進む"}.Contains(System.Windows.Automation.AutomationProperties.GetName(b))),"global screen history buttons are absent",result);
                    foreach(int page in new[]{0,1}) { editor.Navigate(page); FrameworkElement active=page==0?(FrameworkElement)Field(editor,"planetColumns"):(FrameworkElement)Field(editor,"orbitColumns"); Check(!Find<Expander>(active).Any(e=>e.Header as string=="呼び出すplanetのサイズ" || e.Header as string=="orbit全体の設定"),"global options are absent from the content editor on page "+page,result); }
                    editor.Navigate(3);
                    Check(Settings<CheckBox>(editor).Count(c=>(c.Content as string??"").EndsWith("を有効にする",StringComparison.Ordinal))==2 && Settings<ComboBox>(editor).Count(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="orbitの開始ボタン")==1,"input settings contain both feature switches and one gesture trigger selector",result);
                    Check(!Settings<CheckBox>(editor).Any(c=>c.Content as string=="軌跡を表示") && !Settings<Slider>(editor).Any(s=>System.Windows.Automation.AutomationProperties.GetName(s)=="リング全体の大きさ"),"input settings exclude visual controls",result);
                    Call(editor,"SelectSettingsCategory",1);
                    Check(Settings<Slider>(editor).Count(s=>System.Windows.Automation.AutomationProperties.GetName(s)=="リング全体の大きさ")==1 && Settings<CheckBox>(editor).Count(c=>c.Content as string=="軌跡を表示")==1 && !Settings<ComboBox>(editor).Any(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="orbitの開始ボタン"),"appearance contains ring sizing and trail visibility without duplicate input controls",result);
                    Call(editor,"SelectSettingsCategory",2);
                    Check(Settings<CheckBox>(editor).Count(c=>c.Content as string=="Windowsにログインしたら自動起動")==1 && Settings<Button>(editor).Any(b=>b.Content as string=="バックアップを保存") && !Settings<Slider>(editor).Any(),"management contains startup and backup without input or appearance controls",result);
                    Check(Json.Write(Draft(editor))==original,"switching setting categories does not modify preferences",result);
                    Call(editor,"SelectSettingsCategory",1); Slider ring=(Slider)Field(editor,"ringSize"),button=(Slider)Field(editor,"buttonSize"); ring.Value=120; button.Value=84;
                    Check(Math.Abs(Draft(editor).RingPercent-120)<.001 && Draft(editor).ButtonSize==84 && (double)Field(Field(editor,"stackSpacingPreview"),"buttonSize")==84,"moved size controls update preferences and the shared stack preview",result);
                    Call(editor,"Undo"); Check(Draft(editor).ButtonSize==82 && Math.Abs(Draft(editor).RingPercent-120)<.001,"size editing retains independent undo checkpoints",result);
                    Call(editor,"SelectSettingsCategory",0); ComboBox trigger=Settings<ComboBox>(editor).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="orbitの開始ボタン"); trigger.SelectedIndex=1;
                    Slider distance=Settings<Slider>(editor).Single(s=>System.Windows.Automation.AutomationProperties.GetName(s)=="orbitが反応するまでの移動距離"); distance.Value=72;
                    Check(Draft(editor).Orbit.Button=="Middle" && Draft(editor).Orbit.Distance==72,"moved gesture input controls update the shared runtime preferences",result);
                    Call(editor,"SelectSettingsCategory",1); CheckBox trail=Settings<CheckBox>(editor).Single(c=>c.Content as string=="軌跡を表示"); trail.IsChecked=false; trail.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    Check(!Draft(editor).Orbit.Trail && Draft(editor).Orbit.Button=="Middle","trail visibility changes without altering gesture input conditions",result);
                    Draft(editor).Validate();
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            using(Controller owner=new Controller(true)) {
                owner.Apply(Preferences.Defaults()); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    string original=Json.Write(Draft(editor));
                    foreach(int page in new[]{1,5,4,3,0}) editor.Navigate(page);
                    Check(Json.Write(Draft(editor))==original && ((System.Collections.ICollection)Field(editor,"history")).Count==0,"changing pages preserves configured actions and their undo history",result);
                    editor.Navigate(3); Call(editor,"SelectSettingsCategory",0);
                    TextBox invocation=(TextBox)Field(editor,"hotkey"); Check(invocation.IsReadOnly && invocation.Text==Draft(editor).Hotkey,"global invocation keys are displayed without allowing manual text entry",result);
                    editor.Navigate(0); Call(editor,"ShowSide",6);
                    TextBox planetKey=Find<TextBox>((DependencyObject)Field(editor,"planetPanel")).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="planetの呼び出しショートカット");
                    Check(planetKey.IsReadOnly && planetKey.Text==Draft(editor).Hotkey,"planet management also restricts key changes to recording",result);
                    ActionItem item=Draft(editor).Items[0]; item.Kind="shortcut"; item.Value="Ctrl+C"; Call(editor,"RefreshInspector");
                    Check(((TextBox)Field(editor,"actionValue")).IsReadOnly,"shortcut actions use recording instead of manual entry",result);
                    item.Kind="hold"; item.Value="Ctrl"; Call(editor,"RefreshInspector"); Check(((TextBox)Field(editor,"actionValue")).IsReadOnly,"held keys use recording instead of manual entry",result);
                    item.Kind="text"; item.Value="定型文"; Call(editor,"RefreshInspector"); Check(!((TextBox)Field(editor,"actionValue")).IsReadOnly,"non-key action text remains editable",result);
                    item.Kind="macro"; item.Value="Ctrl+C\nwait:200\nCtrl+V"; Call(editor,"RefreshInspector");
                    TextBox[] steps=Find<TextBox>((DependencyObject)Field(editor,"macroRows")).ToArray(); Check(steps.Length==3 && steps[0].IsReadOnly && !steps[1].IsReadOnly && steps[2].IsReadOnly,"macro key steps require recording while delay values remain editable",result);
                    item.Kind="toggle"; item.Toggle=ToggleDefinition.Create(); item.Toggle.Stages[0].Kind="shortcut"; item.Toggle.Stages[0].Value="F6"; item.Toggle.Stages[1].Kind="macro"; item.Toggle.Stages[1].Value="Ctrl+C\nwait:200\nCtrl+V"; Call(editor,"RefreshInspector");
                    TextBox[] toggleKeys=Find<TextBox>((DependencyObject)Field(editor,"togglePanel")).Where(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="1回目の操作" || System.Windows.Automation.AutomationProperties.GetName(t).EndsWith("のキー",StringComparison.Ordinal)).ToArray();
                    Check(toggleKeys.Length==3 && toggleKeys.All(t=>t.IsReadOnly),"toggle shortcut and macro keys also require recording",result);
                    Call(editor,"RestoreSaved"); original=Json.Write(Draft(editor)); Call(editor,"ShowSide",1); Call(editor,"ShowSide",3); Call(editor,"CloseIconPicker");
                    Check(((DockPanel)Field(editor,"detailPanel")).Visibility==Visibility.Visible && Json.Write(Draft(editor))==original,"completing icon selection returns to action details without changing settings",result);
                    editor.Navigate(5); Call(editor,"OpenConversionIconPicker",Draft(editor).Conversion.Images[5]); Call(editor,"CloseIconPicker");
                    Check(Field(editor,"iconConversionChoice")==null && ((DockPanel)Field(editor,"iconPanel")).Visibility==Visibility.Collapsed && Json.Write(Draft(editor))==original,"closing conversion appearance selection preserves destination settings",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

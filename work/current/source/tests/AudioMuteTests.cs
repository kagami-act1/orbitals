using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OrbitRing {
    static class AudioMuteTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(object target,string name) { return target.GetType().GetField(name,Private).GetValue(target); }
        static void Set(object target,string name,object value) { target.GetType().GetField(name,Private).SetValue(target,value); }
        static void Call(object target,string name,params object[] args) { target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static IEnumerable<T> Find<T>(DependencyObject parent) where T:DependencyObject { if(parent is T) yield return (T)parent; foreach(object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject item=child as DependencyObject; if(item!=null) foreach(T nested in Find<T>(item)) yield return nested; } }
        static void ClickBackground(UIElement source) {
            source.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) { RoutedEvent=Mouse.PreviewMouseDownEvent });
            source.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left) { RoutedEvent=Mouse.PreviewMouseUpEvent });
        }
        static Button StateButton(SettingsWindow editor,bool muted) { return Find<Button>((DependencyObject)Field(editor,"muteIconsPanel")).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==(muted?"ミュートON":"ミュートOFF")+"のアイコン・文字列を変更"); }
        public static List<string> Run() {
            List<string> r=new List<string>();
            ActionItem microphone=ActionCatalog.Create().Single(p=>p.Item.Label=="マイクのミュート切替").Create();
            Check(microphone.Kind=="toggle" && microphone.Toggle.MuteKind=="microphone" && !microphone.Toggle.IsSystemMute,"the microphone preset is a distinct synchronized two-state toggle",r);
            Preferences config=Preferences.Defaults(); config.Items[0]=microphone; config.Validate(); ToggleState state=new ToggleState(); Action complete=null;
            state.SynchronizeMute(microphone,false);
            Check(state.Display(microphone).Icon=="ms:mic" && state.Prepare(microphone,out complete).Kind=="microphone" && state.Prepare(microphone,out complete).Value=="on","an unmuted microphone displays its live state and selects mute ON",r);
            complete(); state.SynchronizeMute(microphone,true);
            Check(state.Display(microphone).Icon=="ms:mic_off" && state.Prepare(microphone,out complete).Value=="off","a muted microphone selects mute OFF with its own state icon",r);
            AudioMute.SetIcon(microphone,true,"text:SILENT"); microphone.Toggle.Stages.Reverse(); state.SynchronizeMute(microphone,true);
            Check(state.Display(microphone).Icon=="text:SILENT" && state.Prepare(microphone,out complete).Value=="off","external state synchronization preserves custom microphone icons and reordered stages",r);
            ActionItem speaker=ActionCatalog.Create().Single(p=>p.Item.Label=="ミュート切替").Create(); state.SynchronizeMute(speaker,false);
            Check(state.Display(speaker).Icon=="ms:volume_up" && state.Display(microphone).Icon=="text:SILENT","speaker and microphone toggle states remain independent",r);
            ActionItem legacy=new ActionItem("音量","volume","") { Icon="ms:star" }; AudioMute.SetIcon(legacy,true,"text:MUTE");
            Check(legacy.Kind=="volume" && AudioMute.Display(legacy,true).Icon=="text:MUTE" && AudioMute.Display(legacy,false).Icon=="ms:star","state customization preserves legacy volume behavior and the other state's custom icon",r);
            AudioMute.SetIcon(legacy,false,null);
            Check(ActionIcons.EffectiveKey(AudioMute.Display(legacy,false))=="ms:volume_up" && AudioMute.Display(legacy,true).Icon=="text:MUTE","automatic selection can be restored for one audio state independently",r);
            ActionItem existingMic=new ActionItem("マイク","microphone",""); string original=Json.Write(existingMic);
            Check(ActionIcons.EffectiveKey(AudioMute.Display(existingMic,false))=="ms:mic" && ActionIcons.EffectiveKey(AudioMute.Display(existingMic,true))=="ms:mic_off" && Json.Write(existingMic)==original,"existing microphone actions gain live state icons without a settings migration",r);
            config=Preferences.Defaults(); config.Items[0]=legacy; config.Items[1]=microphone; config.Items[4]=speaker; config.Validate(); Preferences restored=config.Clone(); restored.Validate();
            Check(restored.Items[0].MuteIcons.Muted=="text:MUTE" && AudioMute.StateAction(restored.Items[1],true).Icon=="text:SILENT","direct and toggle state icons survive complete preference round trips",r);
            restored.Items[0].MuteIcons.Unmuted="text:A\nB"; Check(Reject(restored.Validate),"invalid state icon text cannot enter saved configuration",r);
            Check(Reject(delegate { Preferences.ValidateAction(new ActionItem("マイク","microphone","invalid"),Chord.Parse("F13")); }),"microphone commands accept only toggle ON or OFF",r);
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    foreach(int index in new[]{0,1,4}) {
                        Set(editor,"selected",index); Call(editor,"RefreshAll"); Call(editor,"ShowSide",1);
                        Preferences draft=(Preferences)Field(editor,"draft"); string command=draft.Items[index].Kind+":"+draft.Items[index].Value;
                        string off=AudioMute.StateAction(draft.Items[index],false).Icon;
                        StateButton(editor,true).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Check(((DockPanel)Field(editor,"iconPanel")).Visibility==Visibility.Visible && ((TextBlock)Field(editor,"iconTitle")).Text.Contains("ミュートON"),"the audio ON state opens the full-size picker with an explicit target: "+index,r);
                        ((IconPicker)Field(editor,"iconPicker")).Selected("ms:favorite"); Call(editor,"CloseIconPicker");
                        Check(AudioMute.StateAction(draft.Items[index],true).Icon=="ms:favorite" && AudioMute.StateAction(draft.Items[index],false).Icon==off && command==draft.Items[index].Kind+":"+draft.Items[index].Value,"state icon selection changes only the targeted display and preserves the audio command: "+index,r);
                        StateButton(editor,false).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ((IconPicker)Field(editor,"iconPicker")).Selected("text:LIVE"); Call(editor,"CloseIconPicker");
                        Check(AudioMute.StateAction(draft.Items[index],false).Icon=="text:LIVE" && AudioMute.StateAction(draft.Items[index],true).Icon=="ms:favorite","the audio OFF state can use text independently of the ON icon: "+index,r);
                        Call(editor,"Undo"); draft=(Preferences)Field(editor,"draft"); Check(AudioMute.StateAction(draft.Items[index],false).Icon==off,"undo restores only the edited audio state display: "+index,r);
                    }
                    Preferences current=(Preferences)Field(editor,"draft"); current.Items[2]=new ActionItem("既存マイク","microphone",""); Set(editor,"selected",2); Call(editor,"RefreshAll"); Call(editor,"ShowSide",1);
                    Check(Find<Button>((DependencyObject)Field(editor,"muteIconsPanel")).Count()==2,"previously placed microphone actions expose both editable state icons",r);
                    current.Items[3]=PlanetStack.Create(new ActionItem("子","shortcut","F6")); Set(editor,"selected",3); Call(editor,"EnterEditorStack",3); Call(editor,"ShowSide",1); string before=Json.Write(current);
                    FrameworkElement root=(FrameworkElement)editor.Content; root.Measure(new Size(1268,780)); root.Arrange(new Rect(0,0,1268,780)); root.UpdateLayout();
                    LayoutEditor surface=(LayoutEditor)Field(editor,"surface"); ClickBackground(surface);
                    Check(((DockPanel)Field(editor,"catalogPanel")).Visibility==Visibility.Visible && ((List<int>)Field(editor,"stackPath")).SequenceEqual(new[]{3}) && Json.Write(current)==before,"clicking empty planet space returns to the catalog without changing stack context or settings",r);
                    Button action=surface.Children.OfType<Button>().First(); action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ClickBackground(action);
                    Check(((DockPanel)Field(editor,"detailPanel")).Visibility==Visibility.Visible,"action and label clicks keep their editor open instead of immediately returning",r);
                    PlanetEditorViewport viewport=Find<PlanetEditorViewport>((DependencyObject)Field(editor,"planetColumns")).Single(); ClickBackground(viewport);
                    Check(((DockPanel)Field(editor,"catalogPanel")).Visibility==Visibility.Visible,"empty space outside the scaled planet also returns to the catalog",r);
                    Call(editor,"ShowSide",1); Button zoom=Find<Button>(viewport).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="編集画面を拡大"); ClickBackground(zoom);
                    Check(((DockPanel)Field(editor,"detailPanel")).Visibility==Visibility.Visible,"viewport controls are excluded from empty-space navigation",r);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return r;
        }
    }
}

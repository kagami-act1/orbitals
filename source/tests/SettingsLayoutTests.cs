using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbitRing {
    static class SettingsLayoutTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static string missed;
        static object Field(object target,string name) { return target.GetType().GetField(name,Private).GetValue(target); }
        static object Call(object target,string name,params object[] args) { return target.GetType().GetMethod(name,Private).Invoke(target,args); }
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject { if(parent is T) yield return (T)parent; foreach(object child in LogicalTreeHelper.GetChildren(parent)) { DependencyObject element=child as DependencyObject; if(element!=null) foreach(T item in Descendants<T>(element)) yield return item; } }
        static void Layout(SettingsWindow editor,Size size) { FrameworkElement root=(FrameworkElement)editor.Content; root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout(); }
        static bool Reaches(FrameworkElement root,Button target) {
            foreach(double x in new[]{.12,.5,.88}) foreach(double y in new[]{.2,.5,.8}) {
                Point at=target.TranslatePoint(new Point(target.ActualWidth*x,target.ActualHeight*y),root); DependencyObject hit=null;
                VisualTreeHelper.HitTest(root,delegate(DependencyObject item) { UIElement element=item as UIElement; return element!=null && (element.Visibility!=Visibility.Visible || !element.IsHitTestVisible)?HitTestFilterBehavior.ContinueSkipSelfAndChildren:HitTestFilterBehavior.Continue; },delegate(HitTestResult item) { hit=item.VisualHit; return HitTestResultBehavior.Stop; },new PointHitTestParameters(at));
                string found=hit==null?"null":hit.GetType().Name; while(hit!=null && hit!=target) hit=VisualTreeHelper.GetParent(hit); if(hit!=target) { missed=" target="+target.Content+" point="+at+" found="+found+" visible="+root.IsVisible+" size="+target.RenderSize; return false; }
            } return true;
        }
        public static List<string> Run() {
            EditorButtonRow wrapped=new EditorButtonRow(); Button first=EditorUi.Button("最初",delegate { },false),second=EditorUi.Button("次",delegate { },false); first.Width=second.Width=100; wrapped.Children.Add(first); wrapped.Children.Add(second);
            wrapped.Measure(new Size(120,Double.PositiveInfinity)); wrapped.Arrange(new Rect(0,0,120,wrapped.DesiredSize.Height));
            if(second.TranslatePoint(new Point(),first).Y-first.ActualHeight<8-.01) throw new Exception("wrapped action buttons touch vertically");
            List<string> result=new List<string>(); ActionItem action=new ActionItem("実行する内容","shortcut","Ctrl+C") { Icon=ActionIcons.TextKey("COPY") }; ActionIcons.Validate(action.Icon);
            Check(true,"wrapped action buttons retain eight pixels between rows",result);
            Check(action.Clone().Icon=="text:COPY" && ActionIcons.Caption(action)=="COPY" && action.Label=="実行する内容" && action.Value=="Ctrl+C","custom display text persists independently of the action name and command",result);
            Check(Reject(delegate { ActionIcons.TextKey("  "); }) && Reject(delegate { ActionIcons.Validate("text:A\nB"); }) && Reject(delegate { ActionIcons.TextKey(new string('a',17)); }),"empty multiline and overlong display text cannot be saved",result);
            Preferences config=Preferences.Defaults(); config.Items[0]=action; config.Items[2]=PlanetStack.Create(); config.Items[2].Icon="text:TOOLS"; config.Items[2].Children[0]=action.Clone(); config.Items[3]=new ActionItem("状態","toggle","") { Toggle=ToggleDefinition.Create(),Icon="text:OFF" }; config.Items[3].Toggle.Stages[0].Icon="text:ON"; foreach(ToggleStage stage in config.Items[3].Toggle.Stages) stage.Value="F6"; config.Conversion.Images[0].Icon="text:PDF"; config.Validate();
            Preferences copy=config.Clone(); copy.Validate(); Check(copy.Items[2].Children[0].Icon=="text:COPY" && copy.Items[3].Toggle.Stages[0].Icon=="text:ON" && copy.Conversion.Images[0].Icon=="text:PDF","text visuals survive nested stacks toggle states and conversion settings",result);
            RingSurface ring=new RingSurface(); ring.Bind(copy); Check(((TextBlock)((StackPanel)ring.Children.OfType<Button>().First().Content).Children[0]).Text=="COPY","runtime planet renders the custom string at normal text size",result);
            Canvas conversion=ConversionPlanets.Drawing(copy.Conversion.Images,copy.Appearance); Check(((TextBlock)((StackPanel)((Button)conversion.Children[2]).Content).Children[0]).Text=="PDF" && conversion.Children[3].Visibility==Visibility.Collapsed,"conversion uses custom text without a duplicate outside caption",result);
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    foreach(Size size in new[]{new Size(1000,650),new Size(1268,780),new Size(1500,900)}) foreach(int page in new[]{5,0,1,4,3}) {
                        editor.Navigate(page); Layout(editor,size); FrameworkElement root=(FrameworkElement)editor.Content;
                        var controls=((List<Button>)Field(editor,"navigation")).Concat(new[]{(Button)Field(editor,"navUndo"),(Button)Field(editor,"navRedo")}).Where(b=>b.IsEnabled);
                        bool reachable=controls.All(b=>Reaches(root,b)); Check(reachable,"all tab and history button regions remain clickable on page "+page+" at "+size+(reachable?"":missed),result);
                        StackPanel footer=(StackPanel)Field(editor,"bottomButtons"); Check(footer.Children.OfType<Button>().All(b=>Reaches(root,b)),"page content never intercepts footer actions on page "+page+" at "+size,result);
                    }
                    editor.Navigate(0);
                    foreach(string kind in new[]{"open","launch","shortcut","hold"}) {
                        Preferences current=(Preferences)Field(editor,"draft"); current.Items[0].Kind=kind; current.Items[0].Value=kind=="open"?"https://example.com":kind=="hold"?"Ctrl":"F6";
                        Call(editor,"RefreshAll"); Call(editor,"ShowSide",1);
                        foreach(Size size in new[]{new Size(1000,650),new Size(1268,780)}) {
                            Layout(editor,size); Button bottom=(Button)Field(editor,kind=="open" || kind=="launch"?"browse":"recordAction"),stack=(Button)Field(editor,"stackWrap");
                            Check(bottom.Visibility==Visibility.Visible && stack.Visibility==Visibility.Visible && stack.TranslatePoint(new Point(),bottom).Y-bottom.ActualHeight>=16-.01,"action value controls retain their section gap before stack actions: "+kind+" at "+size,result);
                        }
                    }
                    Call(editor,"RestoreSaved"); editor.Navigate(1); Layout(editor,new Size(1000,650));
                    GestureDrawing drawing=(GestureDrawing)Field(editor,"gesturePreview"); ComboBox type=(ComboBox)Field(editor,"gestureType");
                    Check(drawing.ActualHeight>=100 && drawing.TranslatePoint(new Point(0,drawing.ActualHeight),type).Y<=0 && !Descendants<Expander>((FrameworkElement)editor.Content).Any(e=>e.Header as string=="orbit全体の設定"),"the gesture editor preserves drawing height with global controls moved to settings",result);
                    editor.Navigate(0); Call(editor,"ShowSide",3); IconPicker picker=(IconPicker)Field(editor,"iconPicker"); ((ComboBox)Field(picker,"displayMode")).SelectedIndex=0; Layout(editor,new Size(1268,780)); DockPanel panel=(DockPanel)Field(editor,"iconPanel");
                    ScrollViewer grid=(ScrollViewer)Field(picker,"scroll"); Check(panel.ActualWidth>1000 && grid.ViewportWidth>600 && grid.ViewportHeight>220,"normal planet icon selection uses the full editor width and a usable icon grid: "+panel.RenderSize+" / "+grid.ViewportWidth+" x "+grid.ViewportHeight,result);
                    ((ComboBox)Field(picker,"displayMode")).SelectedIndex=1; ((TextBox)Field(picker,"textValue")).Text="TEST"; ((Button)Field(picker,"useText")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Preferences draft=(Preferences)Field(editor,"draft"); Check(draft.Items[0].Icon=="text:TEST" && draft.Items[0].Value=="Ctrl+C","planet icon settings apply arbitrary text without changing the command",result);
                    Call(editor,"CloseIconPicker"); Call(editor,"Undo"); draft=(Preferences)Field(editor,"draft"); Check(draft.Items[0].Icon=="text:COPY","undo restores a previous text visual",result);
                    editor.Navigate(5); Call(editor,"OpenConversionIconPicker",draft.Conversion.Images[0]); ((ComboBox)Field(picker,"displayMode")).SelectedIndex=0; Layout(editor,new Size(1000,650));
                    Check(panel.Visibility==Visibility.Visible && panel.ActualWidth>900 && ((ScrollViewer)Field(picker,"scroll")).ActualWidth>600,"conversion opens the same full-width appearance picker",result);
                    ((ComboBox)Field(picker,"displayMode")).SelectedIndex=1; ((TextBox)Field(picker,"textValue")).Text="DOC"; ((Button)Field(picker,"useText")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); draft=(Preferences)Field(editor,"draft");
                    Check(draft.Conversion.Images[0].Icon=="text:DOC" && panel.Visibility==Visibility.Collapsed,"conversion text selection returns to destination settings",result);
                    Call(editor,"OpenConversionIconPicker",draft.Conversion.Images[0]); editor.Navigate(4); Layout(editor,new Size(1000,650)); Check(panel.Visibility==Visibility.Collapsed && Field(editor,"iconConversionChoice")==null,"leaving the picker clears its conversion target and input surface",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

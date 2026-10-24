using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        string editorApplication;
        DateTime runningAppsChecked;
        string[] runningApps;
        string[] RunningApplications() {
            if(runningApps!=null && (DateTime.UtcNow-runningAppsChecked).TotalSeconds<5) return runningApps;
            var names=new System.Collections.Generic.SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(Process process in Process.GetProcesses()) using(process) { try { if(process.Id!=owner.ProcessId && process.MainWindowHandle!=IntPtr.Zero) { string name=ExtendedActions.Executable(process.MainWindowHandle); if(OrbitPreferences.ValidExecutable(name)) names.Add(name); } } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } }
            runningApps=names.ToArray(); runningAppsChecked=DateTime.UtcNow; return runningApps;
        }
        StackPanel applicationScope;
        ApplicationSettings CurrentApplication { get { return ApplicationProfiles.Find(draft,editorApplication); } }
        System.Collections.Generic.List<ActionItem> EditorRootItems() { return ApplicationProfiles.Items(draft,editorApplication,editorPlanet); }
        void BuildApplicationScope(DockPanel root) { applicationScope=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(applicationScope,Dock.Top); root.Children.Add(applicationScope); }
        void UpdateApplicationScope() {
            applicationScope.Children.Clear(); ApplicationSettings app=CurrentApplication; bool visible=app!=null && (page==0 || page==1); applicationScope.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
            bool own=app==null || (page==0?app.OverridePlanet:app.OverrideOrbit); workspace.IsEnabled=!visible || own;
            if(!visible) return;
            TextBlock label=EditorUi.Text(app.Executable+(own?" の専用設定":" は共通設定を使用中"),13,EditorUi.Mint); label.VerticalAlignment=VerticalAlignment.Center; label.Margin=new Thickness(0,0,16,0); applicationScope.Children.Add(label);
            if(!own) applicationScope.Children.Add(EditorUi.Button("専用設定を作る",delegate { Remember(); if(page==0) ApplicationProfiles.EnablePlanet(draft,app); else ApplicationProfiles.EnableOrbit(draft,app); RefreshAll(); Changed("共通設定をコピーしました。このアプリ専用に編集できます。"); },true));
            applicationScope.Children.Add(EditorUi.Button("共通設定を編集",delegate { editorApplication=null; selected=0; gestureIndex=0; stackPath.Clear(); RefreshAll(); ShowSide(0); },false));
        }
        FrameworkElement BuildApplicationPage() {
            DockPanel panel=new DockPanel { MaxWidth=980,HorizontalAlignment=HorizontalAlignment.Stretch };
            StackPanel top=new StackPanel { Margin=new Thickness(0,0,0,18) }; DockPanel.SetDock(top,Dock.Top); panel.Children.Add(top);
            top.Children.Add(EditorUi.Text("アプリを追加",19,EditorUi.Ink));
            TextBlock help=EditorUi.Text("アクティブなアプリに合わせて、メインの呼び出しキーで表示するplanetを切り替えます。",13,EditorUi.Muted); help.Margin=new Thickness(0,8,0,20); top.Children.Add(help);
            Grid controls=new Grid(); top.Children.Add(controls);
            controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto }); controls.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(230) }); controls.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            StackPanel appField=Section("実行ファイル名"); appField.Margin=new Thickness(0,0,12,0); controls.Children.Add(appField);
            TextBox executable=EditorUi.Input(""); executable.MaxLength=260; executable.MinHeight=40; appField.Children.Add(executable); System.Windows.Automation.AutomationProperties.SetName(executable,"追加するアプリの実行ファイル名");
            ComboBox initialPlanet=ApplicationPlanetSelector(null); StackPanel planetField=Section("表示するplanet"); planetField.Margin=new Thickness(0,0,12,0); Grid.SetColumn(planetField,2); controls.Children.Add(planetField); planetField.Children.Add(initialPlanet);
            Button add=EditorUi.Button("＋ 追加",delegate {
                string name=executable.Text.Trim(); if(!OrbitPreferences.ValidExecutable(name)) { Changed("実行ファイル名を入力してください。例：vivaldi.exe"); return; }
                if(ApplicationProfiles.Find(draft,name)!=null) { Changed("このアプリは登録済みです。"); return; }
                if(draft.Applications.Count>=ApplicationProfiles.Maximum) { Changed("アプリ別設定は32件までです。"); return; }
                Remember(); draft.Applications.Add(new ApplicationSettings { Executable=name,PlanetId=SelectedPlanetId(initialPlanet) }); RefreshAll(); Changed(name+"を追加しました。「保存」で反映します。");
            },true); add.Margin=new Thickness(0); add.VerticalAlignment=VerticalAlignment.Bottom; Grid.SetColumn(add,3); controls.Children.Add(add);
            Button browse=EditorUi.Button("exeを選ぶ",delegate { Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog { Filter="アプリ (*.exe)|*.exe",CheckFileExists=true }; if(dialog.ShowDialog(this)==true) executable.Text=System.IO.Path.GetFileName(dialog.FileName); },false); browse.Margin=new Thickness(0,0,20,0); browse.VerticalAlignment=VerticalAlignment.Bottom; Grid.SetColumn(browse,1); controls.Children.Add(browse);
            ComboBox running=new ComboBox { Padding=new Thickness(8),Margin=new Thickness(0,14,0,0),ToolTip="現在ウィンドウを開いているアプリから選びます。" }; running.Items.Add("起動中のアプリから選ぶ");
            foreach(string name in RunningApplications()) running.Items.Add(name); running.SelectedIndex=0; running.SelectionChanged+=delegate { if(running.SelectedIndex>0) executable.Text=(string)running.SelectedItem; }; top.Children.Add(running);
            StackPanel list=new StackPanel(); panel.Children.Add(new ScrollViewer { Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
            if(draft.Applications.Count==0) { list.Children.Add(Card(EditorUi.Text("アプリ別設定はまだありません。共通設定はplanet・orbitの画面で編集できます。",14,EditorUi.Muted))); }
            foreach(ApplicationSettings app in draft.Applications.OrderBy(a=>a.Executable,StringComparer.OrdinalIgnoreCase)) {
                StackPanel body=new StackPanel(); Grid heading=new Grid(); body.Children.Add(heading);
                heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(230) }); heading.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
                TextBlock appName=EditorUi.Text(app.Executable,19,EditorUi.Ink); appName.VerticalAlignment=VerticalAlignment.Center; appName.TextTrimming=TextTrimming.CharacterEllipsis; appName.Margin=new Thickness(0,23,16,0); heading.Children.Add(appName);
                StackPanel selection=Section("表示するplanet"); selection.Margin=new Thickness(0,0,20,0); Grid.SetColumn(selection,1); heading.Children.Add(selection);
                ComboBox assigned=ApplicationPlanetSelector(app.PlanetId); selection.Children.Add(assigned); assigned.SelectionChanged+=delegate { string id=SelectedPlanetId(assigned); if(app.PlanetId==id) return; Remember(); app.PlanetId=id; Changed("表示するplanetを変更しました。「保存」で反映します。"); };
                Button remove=EditorUi.Button("削除",delegate { Remember(); draft.Applications.Remove(app); if(editorApplication==app.Executable) editorApplication=null; RefreshAll(); Changed("アプリ別設定を削除しました。「元に戻す」で戻せます。"); },false); remove.Margin=new Thickness(0); remove.VerticalAlignment=VerticalAlignment.Bottom; Grid.SetColumn(remove,2); heading.Children.Add(remove);
                AddApplicationFeatureToggles(body,app.Executable);
                WrapPanel options=new WrapPanel { Margin=new Thickness(0,12,0,0) }; body.Children.Add(options);
                CheckBox planet=new CheckBox { Content="planetを専用設定にする",IsChecked=app.OverridePlanet,Foreground=EditorUi.Ink,Margin=new Thickness(0,5,22,5) }; options.Children.Add(planet);
                planet.Click+=delegate { Remember(); if(planet.IsChecked==true) ApplicationProfiles.EnablePlanet(draft,app); else app.OverridePlanet=false; RefreshAll(); Changed("planetの共通・専用設定を変更しました。"); };
                CheckBox orbit=new CheckBox { Content="orbitを専用設定にする",IsChecked=app.OverrideOrbit,Foreground=EditorUi.Ink,Margin=new Thickness(0,5,0,5) }; options.Children.Add(orbit);
                orbit.Click+=delegate { Remember(); if(orbit.IsChecked==true) ApplicationProfiles.EnableOrbit(draft,app); else app.OverrideOrbit=false; RefreshAll(); Changed("orbitの共通・専用設定を変更しました。"); };
                WrapPanel edit=new EditorButtonRow { Margin=new Thickness(0,12,0,0) }; body.Children.Add(edit);
                edit.Children.Add(EditorUi.Button("planetを編集",delegate { OpenApplication(app,0); },false)); edit.Children.Add(EditorUi.Button("orbitを編集",delegate { OpenApplication(app,1); },false));
                Border card=Card(body); card.Margin=new Thickness(0,0,0,12); list.Children.Add(card);
            }
            Border blacklist=Card(BuildAppsPage()); blacklist.Margin=new Thickness(0,18,0,0); list.Children.Add(blacklist); return panel;
        }
        void OpenApplication(ApplicationSettings app,int targetPage) {
            bool enabled=targetPage==0?app.OverridePlanet:app.OverrideOrbit;
            if(!enabled) { Remember(); if(targetPage==0) ApplicationProfiles.EnablePlanet(draft,app); else ApplicationProfiles.EnableOrbit(draft,app); }
            editorApplication=app.Executable; editorPlanet=ApplicationProfiles.RuntimePlanet(draft,app.Executable,0); gestureIndex=0; selected=0; stackPath.Clear(); Navigate(targetPage);
            if(!enabled) Changed("共通設定をコピーしました。このアプリ専用に編集できます。");
        }
        ComboBox ApplicationPlanetSelector(string id) {
            ComboBox combo=new ComboBox { Padding=new Thickness(8),MinHeight=40 };
            combo.Items.Add("切り替えない"); for(int i=0;i<=draft.Planets.Count;i++) combo.Items.Add(UserContent.Name(draft,i));
            combo.SelectedIndex=String.IsNullOrEmpty(id)?0:Enumerable.Range(0,draft.Planets.Count+1).FirstOrDefault(i=>ApplicationProfiles.PlanetId(draft,i)==id)+1;
            System.Windows.Automation.AutomationProperties.SetName(combo,"表示するplanet"); return combo;
        }
        string SelectedPlanetId(ComboBox combo) { return combo.SelectedIndex<=0?null:ApplicationProfiles.PlanetId(draft,combo.SelectedIndex-1); }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        int settingsCategory;
        void SelectSettingsCategory(int category) { if(category<0 || category>2 || category==settingsCategory) return; settingsCategory=category; EndKeyCapture(); BuildGeneralSettings(); }
        void BuildGeneralSettings() {
            EndKeyCapture(); settingsPanel.Children.Clear(); appearanceTrail=null; stackSpacingPreview=null; hotkey=null; startup=null; ringSize=null; buttonSize=null; ringSizeLabel=null; buttonSizeLabel=null;
            Grid layout=new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(160) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(24) }); layout.ColumnDefinitions.Add(new ColumnDefinition()); settingsPanel.Children.Add(layout);
            StackPanel categories=new StackPanel(); layout.Children.Add(categories); string[] names={"操作","外観","起動・管理"};
            for(int i=0;i<names.Length;i++) { int index=i; Button button=EditorUi.Button(names[i],delegate { SelectSettingsCategory(index); },false); button.Margin=new Thickness(0,0,0,10); button.HorizontalContentAlignment=HorizontalAlignment.Left; if(index==settingsCategory) { button.Background=EditorUi.Brush("#355747"); button.BorderBrush=EditorUi.Mint; } System.Windows.Automation.AutomationProperties.SetName(button,"設定の分類："+names[i]); categories.Children.Add(button); }
            Grid columns=new Grid { Margin=new Thickness(0,0,12,16) }; columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(20) }); columns.ColumnDefinitions.Add(new ColumnDefinition());
            ScrollViewer scroll=new ScrollViewer { Content=columns,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled }; Grid.SetColumn(scroll,2); layout.Children.Add(scroll);
            StackPanel left=new StackPanel(),right=new StackPanel(); columns.Children.Add(left); Grid.SetColumn(right,2); columns.Children.Add(right);
            if(settingsCategory==0) BuildInputSettings(left,right); else if(settingsCategory==1) BuildAppearanceSettings(left,right); else BuildManagementSettings(left,right);
        }
        void BuildInputSettings(StackPanel left,StackPanel right) {
            StackPanel planet=new StackPanel(); planet.Children.Add(EditorUi.Text("planet（リング）",19,EditorUi.Ink));
            CheckBox enabled=new CheckBox { Content="planet（リング）を有効にする",IsChecked=draft.PlanetEnabled,Foreground=EditorUi.Ink,Margin=new Thickness(0,16,0,20) }; planet.Children.Add(enabled);
            enabled.Click+=delegate { Remember(); draft.PlanetEnabled=enabled.IsChecked==true; Changed("planetのON / OFFを変更しました。「保存」で反映します。"); };
            planet.Children.Add(EditorUi.Text("呼び出しショートカット",12,EditorUi.Muted)); hotkey=RecordedKeyInput(draft.Hotkey); hotkey.Margin=new Thickness(0,6,0,10); System.Windows.Automation.AutomationProperties.SetName(hotkey,"呼び出しキー"); planet.Children.Add(hotkey);
            hotkey.TextChanged+=delegate { if(syncing || draft.Hotkey==hotkey.Text) return; Remember(); draft.Hotkey=hotkey.Text; Changed("呼び出しキーを変更しました。"); };
            Button record=EditorUi.Button("キーを記録",delegate { BeginKeyCapture(hotkey,true); },false); record.Margin=new Thickness(0,0,0,12); planet.Children.Add(record);
            planet.Children.Add(EditorUi.Text("planetとドラッグ変換に共通です。マウスのボタンにも同じキーを割り当てます。",12,EditorUi.Muted)); left.Children.Add(Card(planet));
            StackPanel orbit=new StackPanel(); orbit.Children.Add(EditorUi.Text("orbit（ジェスチャー）",19,EditorUi.Ink));
            CheckBox gestureEnabled=new CheckBox { Content="orbit（ジェスチャー）を有効にする",IsChecked=draft.Orbit.Enabled,Foreground=EditorUi.Ink,Margin=new Thickness(0,16,0,20) }; orbit.Children.Add(gestureEnabled);
            gestureEnabled.Click+=delegate { Remember(); draft.Orbit.Enabled=gestureEnabled.IsChecked==true; Changed("orbitのON / OFFを変更しました。「保存」で反映します。"); };
            orbit.Children.Add(EditorUi.Text("押しながら操作するボタン",12,EditorUi.Muted)); ComboBox trigger=new ComboBox { ItemsSource=new[]{"右ボタン","中央ボタン","サイドボタン1","サイドボタン2"},SelectedIndex=Array.IndexOf(new[]{"Right","Middle","X1","X2"},draft.Orbit.Button),Padding=new Thickness(8),Margin=new Thickness(0,6,0,20) }; System.Windows.Automation.AutomationProperties.SetName(trigger,"orbitの開始ボタン"); orbit.Children.Add(trigger);
            trigger.SelectionChanged+=delegate { if(updatingWorkspace || trigger.SelectedIndex<0) return; string value=new[]{"Right","Middle","X1","X2"}[trigger.SelectedIndex]; if(draft.Orbit.Button==value) return; Remember(); draft.Orbit.Button=value; Changed("orbitの開始ボタンを変更しました。「保存」で反映します。"); };
            DockPanel distanceHeading=new DockPanel(); TextBlock valueLabel=EditorUi.Text(draft.Orbit.Distance+" px",12,EditorUi.Mint); DockPanel.SetDock(valueLabel,Dock.Right); distanceHeading.Children.Add(valueLabel); distanceHeading.Children.Add(EditorUi.Text("反応するまでの移動距離",12,EditorUi.Muted)); orbit.Children.Add(distanceHeading);
            Slider distance=new Slider { Minimum=12,Maximum=120,Value=draft.Orbit.Distance,IsSnapToTickEnabled=true,TickFrequency=2,Margin=new Thickness(0,8,0,12) }; System.Windows.Automation.AutomationProperties.SetName(distance,"orbitが反応するまでの移動距離"); orbit.Children.Add(distance);
            distance.ValueChanged+=delegate { if(updatingWorkspace) return; ResizeCheckpoint(); draft.Orbit.Distance=(int)distance.Value; valueLabel.Text=draft.Orbit.Distance+" px"; Changed("反応するまでの移動距離を変更しました。「保存」で反映します。"); };
            orbit.Children.Add(EditorUi.Text("短いクリックとジェスチャーを区別します。大きくすると、より動かすまで反応しません。",12,EditorUi.Muted)); right.Children.Add(Card(orbit));
        }
        void BuildAppearanceSettings(StackPanel left,StackPanel right) {
            StackPanel planet=new StackPanel(); planet.Children.Add(EditorUi.Text("planet（リング）",19,EditorUi.Ink)); stackSpacingPreview=new StackSpacingPreview(draft.Appearance,draft.ButtonSize); planet.Children.Add(stackSpacingPreview);
            ringSize=SizeRow(planet,"リング全体",50,200,5,out ringSizeLabel); buttonSize=SizeRow(planet,"ボタン",60,90,2,out buttonSizeLabel); ringSize.Value=draft.RingPercent; buttonSize.Value=draft.ButtonSize; SizeLabels();
            ringSize.ValueChanged+=delegate { if(syncing) return; ResizeCheckpoint(); draft.RingPercent=Math.Round(ringSize.Value); SizeLabels(); Changed("呼び出すリング全体の大きさを変更しました。"); };
            buttonSize.ValueChanged+=delegate { if(syncing) return; ResizeCheckpoint(); draft.ButtonSize=Math.Round(buttonSize.Value); SizeLabels(); stackSpacingPreview.Update(draft.Appearance,draft.ButtonSize); Changed("リングのボタンの大きさを変更しました。"); };
            Button standard=EditorUi.Button("標準の大きさ",delegate { if(draft.RingScale==Preferences.StandardRingScale && draft.ButtonSize==Preferences.StandardButtonSize) return; Remember(); draft.RingScale=Preferences.StandardRingScale; draft.ButtonSize=Preferences.StandardButtonSize; BuildGeneralSettings(); Changed("大きさを標準に戻しました。配置はそのままです。"); },false); standard.Margin=new Thickness(0,0,0,20); planet.Children.Add(standard);
            ComboBox labels=new ComboBox { ItemsSource=new[]{"アイコンを表示","文字のみを表示"},SelectedIndex=draft.Appearance.TextOnly?1:0,Padding=new Thickness(8),Margin=new Thickness(0,0,0,18) }; System.Windows.Automation.AutomationProperties.SetName(labels,"planetの表示方法"); planet.Children.Add(labels);
            labels.SelectionChanged+=delegate { bool text=labels.SelectedIndex==1; if(text==draft.Appearance.TextOnly) return; Remember(); draft.Appearance.TextOnly=text; AppearanceChanged(); };
            AddColor(planet,"リングの強調色",draft.Appearance.RingAccent,delegate(string color) { draft.Appearance.RingAccent=color; });
            AddSlider(planet,"リングの濃さ",35,100,draft.Appearance.RingOpacity*100,5,delegate(double value) { draft.Appearance.RingOpacity=value/100; },delegate(double value) { return value+"%"; });
            AddSlider(planet,"弧状スタックの間隔",PlanetStack.MinGap*2.5,PlanetStack.MaxGap*2.5,draft.Appearance.StackSpacing,5,delegate(double value) { draft.Appearance.StackSpacing=value; },delegate(double value) { return value.ToString("0"); });
            CheckBox animation=new CheckBox { Content="リングを開くときの演出",IsChecked=draft.Appearance.RingAnimation,Foreground=EditorUi.Ink,Margin=new Thickness(0,0,0,12) }; planet.Children.Add(animation); animation.Click+=delegate { Remember(); draft.Appearance.RingAnimation=animation.IsChecked==true; AppearanceChanged(); if(draft.Appearance.RingAnimation) PreviewRingReveal(); };
            Button preview=EditorUi.Button("演出を試す",delegate { PreviewRingReveal(); },false); preview.Margin=new Thickness(0); planet.Children.Add(preview); left.Children.Add(Card(planet));
            StackPanel orbit=new StackPanel(); orbit.Children.Add(EditorUi.Text("orbit（ジェスチャー）",19,EditorUi.Ink));
            CheckBox trail=new CheckBox { Content="軌跡を表示",IsChecked=draft.Orbit.Trail,Foreground=EditorUi.Ink,Margin=new Thickness(0,16,0,10) }; orbit.Children.Add(trail); trail.Click+=delegate { Remember(); draft.Orbit.Trail=trail.IsChecked==true; Changed("軌跡表示を変更しました。「保存」で反映します。"); };
            appearanceTrail=new GestureDrawing { Height=88,Margin=new Thickness(12),ToolTip="マウスを重ねると動きを確認できます。" }; appearanceTrail.Bind(new OrbitBinding { Pattern="DR" }); orbit.Children.Add(appearanceTrail);
            AddColor(orbit,"軌跡の色",draft.Appearance.TrailColor,delegate(string color) { draft.Appearance.TrailColor=color; }); AddSlider(orbit,"軌跡の太さ",1,8,draft.Appearance.TrailWidth,1,delegate(double value) { draft.Appearance.TrailWidth=value; },delegate(double value) { return value+" px"; }); AddSlider(orbit,"軌跡を残す長さ",0,800,draft.Appearance.TrailLength,40,delegate(double value) { draft.Appearance.TrailLength=value; },delegate(double value) { return value==0?"すべて表示":value+" px"; }); right.Children.Add(Card(orbit));
            Button reset=EditorUi.Button("外観を標準に戻す",delegate { Remember(); draft.Appearance=new AppearancePreferences(); BuildGeneralSettings(); Changed("外観を標準に戻しました。"); },false); reset.Content=EditorUi.Text("外観を標準に戻す",13,EditorUi.Ink); reset.Margin=new Thickness(0,18,0,0); right.Children.Add(reset); UpdateAppearancePreview();
        }
        void BuildManagementSettings(StackPanel left,StackPanel right) {
            StackPanel launch=new StackPanel(); launch.Children.Add(EditorUi.Text("起動",19,EditorUi.Ink)); startup=new CheckBox { Content="Windowsにログインしたら自動起動",Foreground=EditorUi.Ink,IsChecked=draft.StartWithWindows,Margin=new Thickness(0,16,0,12) }; launch.Children.Add(startup);
            startup.Click+=delegate { if(updatingWorkspace) return; Remember(); draft.StartWithWindows=startup.IsChecked==true; Changed("自動起動の設定を変更しました。「保存」で反映します。"); }; launch.Children.Add(EditorUi.Text(AppPaths.IsInstalled?"Windowsへのログイン時に、編集画面を出さずに常駐します。":"編集画面を出さずに常駐します。自動起動を使う場合はアプリのフォルダーを移動しないでください。",12,EditorUi.Muted)); left.Children.Add(Card(launch)); left.Children.Add(BuildConversionSettings()); left.Children.Add(BuildPluginSettings());
            StackPanel backup=new StackPanel(); backup.Children.Add(EditorUi.Text("バックアップ・復元",19,EditorUi.Ink)); TextBlock help=EditorUi.Text("リング・ジェスチャー・アクション・外観を、JSON（.json）でまとめて保存します。",13,EditorUi.Muted); help.Margin=new Thickness(0,14,0,16); backup.Children.Add(help);
            Button export=EditorUi.Button("バックアップを保存",delegate { ExportBackup(); },false); export.Margin=new Thickness(0,0,0,10); backup.Children.Add(export); Button import=EditorUi.Button("バックアップから復元",delegate { ImportBackup(); },false); import.Margin=new Thickness(0,0,0,14); backup.Children.Add(import); backup.Children.Add(EditorUi.Text("読み込んだ内容を確認してから「保存」で反映します。変更前の内容には「元に戻す」で戻せます。",12,EditorUi.Muted)); right.Children.Add(Card(backup));
        }
    }
}

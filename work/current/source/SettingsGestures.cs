using System;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        void UpdateGestureLabels() {
            if(gestureList==null || page!=1) return;
            bool old=updatingWorkspace; updatingWorkspace=true;
            gestureList.Items.Clear();
            foreach(OrbitBinding binding in CurrentBindings) {
                StackPanel row=new StackPanel();
                GestureDrawing preview=new GestureDrawing { Width=70,Height=30,Margin=new Thickness(0,0,0,4) }; preview.TriggerButton=draft.Orbit.Button; preview.SetAppearance(draft.Appearance); preview.Bind(binding); row.Children.Add(preview);
                StackPanel labels=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
                TextBlock name=EditorUi.Text(binding.Action.Label+(binding.Action.Kind=="none"?" · 未割り当て":"")+(binding.Modifier==""?"":" · "+binding.Modifier),12,EditorUi.Ink); name.TextAlignment=TextAlignment.Center; name.TextWrapping=TextWrapping.NoWrap; name.TextTrimming=TextTrimming.CharacterEllipsis; labels.Children.Add(name);
                row.Children.Add(labels);
                int index=gestureList.Items.Count;
                ListBoxItem item=new ListBoxItem { Content=row,ToolTip=binding.Action.Label+(GestureInput.Caption(binding.Pattern,draft.Orbit.Button)==""?"":"\n"+GestureInput.Caption(binding.Pattern,draft.Orbit.Button)+"\n設定画面の外で操作します。"),Width=136,Height=60,Padding=new Thickness(6,4,6,4),Margin=new Thickness(3) }; preview.AnimateOnHover(item); System.Windows.Automation.AutomationProperties.SetName(item,binding.Action.Label); ConnectGestureDrop(item,delegate { return index; }); gestureList.Items.Add(item);
            }
            gestureName.Text=gestureIndex>=0 && gestureIndex<CurrentBindings.Count?CurrentBindings[gestureIndex].Action.Label:"新しいジェスチャーを追加";
            gestureList.SelectedIndex=gestureIndex;
            if(gestureIndex>=0 && gestureIndex<gestureList.Items.Count) gestureList.ScrollIntoView(gestureList.Items[gestureIndex]);
            updatingWorkspace=old;
        }
        void RefreshGestureSelection() {
            bool old=updatingWorkspace; updatingWorkspace=true;
            bool valid=gestureIndex>=0 && gestureIndex<CurrentBindings.Count;
            side.Visibility=Visibility.Visible;
            OrbitBinding binding=valid?CurrentBindings[gestureIndex]:null; gesturePreview.TriggerButton=draft.Orbit.Button; gesturePreview.SetAppearance(draft.Appearance); gesturePreview.Bind(binding); gesturePreview.EditorView=valid;
            gestureName.Text=valid?binding.Action.Label:"新しいジェスチャーを追加";
            bool drawn=valid && binding.Pattern!="WheelUp" && binding.Pattern!="WheelDown" && binding.Pattern!="Rocker";
            gesturePreview.DrawingEnabled=editingGesture && drawn;
            gesturePreview.Cursor=gesturePreview.DrawingEnabled?System.Windows.Input.Cursors.Cross:System.Windows.Input.Cursors.Arrow;
            gesturePreview.ToolTip=editingGesture?"左ドラッグで形を描き直します。変更後は「完了」を押してください。":"点が描き始めです。マウスを重ねると描く順番を確認できます。\n形や操作条件を変更するには「編集」を押してください。";
            cleanDrawing.IsEnabled=editingGesture && drawn; restoreDrawing.IsEnabled=editingGesture && valid && binding.OriginalShape!=null;
            editGesture.IsEnabled=valid; editGesture.Content=editingGesture?"完了":"編集";
            editGesture.Background=editingGesture?EditorUi.Brush("#355747"):EditorUi.Panel; editGesture.BorderBrush=editingGesture?EditorUi.Mint:EditorUi.Line;
            gestureModifier.IsEnabled=valid; gestureType.IsEnabled=valid;
            gestureModifier.SelectedIndex=valid?Array.IndexOf(new[]{"","Ctrl","Alt","Shift"},binding.Modifier):0;
            gestureType.SelectedIndex=valid?Math.Max(0,Array.IndexOf(new[]{"","WheelUp","WheelDown","Rocker"},binding.Pattern)):0;
            gestureTestButton.Content=testingGesture?"テスト終了":"認識テスト";
            gestureSaveButton.IsEnabled=valid && !testingGesture;
            gesturePreview.EmptyHint=testingGesture?"左ドラッグで認識を確認":null;
            if(testingGesture) {
                gestureName.Text="認識テスト"; gesturePreview.Bind(null); gesturePreview.EditorView=true; gesturePreview.DrawingEnabled=true; gesturePreview.Cursor=System.Windows.Input.Cursors.Cross;
                gesturePreview.ToolTip="左ドラッグで認識を確認します。アクションは実行しません。";
                cleanDrawing.IsEnabled=false; restoreDrawing.IsEnabled=false; editGesture.IsEnabled=false; gestureModifier.IsEnabled=true; gestureType.IsEnabled=false; gestureModifier.SelectedIndex=Array.IndexOf(new[]{"","Ctrl","Alt","Shift"},testModifier);
            }
            RefreshGestureTolerance(binding,drawn); updatingWorkspace=old; if(testingGesture) ShowSide(7); else if(valid) { RefreshInspector(); ShowSide(1); } else ShowSide(0);
        }
        void ConnectGestureDrop(UIElement target,Func<int> index) {
            target.AllowDrop=true;
            target.DragOver+=delegate(object sender,DragEventArgs e) { e.Effects=EditorDrag.Active && EditorDrag.Item!=null && EditorDrag.Item.Kind!="stack" && EditorDrag.Item.Kind!="none"?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true; };
            target.Drop+=delegate(object sender,DragEventArgs e) {
                e.Handled=true; e.Effects=DragDropEffects.None;
                if(!EditorDrag.Active || EditorDrag.Item==null || EditorDrag.Item.Kind=="stack" || EditorDrag.Item.Kind=="none") return;
                gestureIndex=index(); Assign(selected,EditorDrag.Item.Clone()); e.Effects=DragDropEffects.Copy;
            };
        }
        Grid BuildOrbitPage() {
            editingGesture=false; testingGesture=false;
            Grid columns=EditorColumns();
            DockPanel left=new DockPanel(); columns.Children.Add(left);
            DockPanel heading=new DockPanel { Margin=new Thickness(0,0,0,10) }; DockPanel.SetDock(heading,Dock.Top); left.Children.Add(heading);
            StackPanel buttons=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(buttons,Dock.Right); heading.Children.Add(buttons);
            buttons.Children.Add(EditorUi.Button("＋ 追加",delegate { ShowSide(5); },false));
            buttons.Children.Add(IconButton("\uE74D","ジェスチャーを削除",delegate { if(gestureIndex<0 || gestureIndex>=CurrentBindings.Count) return; Remember(); CurrentBindings.RemoveAt(gestureIndex); gestureIndex=Math.Min(gestureIndex,CurrentBindings.Count-1); UpdateGestureLabels(); RefreshGestureSelection(); Changed("ジェスチャーを削除しました。"); }));
            Button restore=EditorUi.Button("保存内容に戻す",delegate { RestoreSaved(); },false); restore.Padding=new Thickness(12,7,12,7); restore.Margin=new Thickness(0); buttons.Children.Add(restore);
            StackPanel tools=new StackPanel { Orientation=Orientation.Horizontal }; heading.Children.Add(tools);
            gestureTestButton=EditorUi.Button("認識テスト",delegate { ToggleGestureTest(); },false); gestureTestButton.Padding=new Thickness(10,7,10,7); tools.Children.Add(gestureTestButton);
            gestureSaveButton=EditorUi.Button("プリセットに保存",delegate { ShowSide(8); },false); gestureSaveButton.Padding=new Thickness(10,7,10,7); tools.Children.Add(gestureSaveButton);
            Grid canvas=new Grid { Margin=new Thickness(16) }; canvas.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); canvas.RowDefinitions.Add(new RowDefinition()); canvas.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); canvas.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            DockPanel drawingTools=new DockPanel(); canvas.Children.Add(drawingTools);
            StackPanel cleanTools=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(cleanTools,Dock.Right); drawingTools.Children.Add(cleanTools);
            cleanDrawing=new CheckBox { Content="自動で補正",IsChecked=draft.Orbit.CleanDrawing,Foreground=EditorUi.Muted,VerticalAlignment=VerticalAlignment.Center,ToolTip="手ぶれを抑え、弧や曲線を滑らかに整えます。明確な角と描く順番は維持します。" }; cleanTools.Children.Add(cleanDrawing);
            cleanDrawing.Click+=delegate {
                if(updatingWorkspace || !editingGesture) return; Remember(); draft.Orbit.CleanDrawing=cleanDrawing.IsChecked==true;
                if(gestureIndex>=0 && gestureIndex<CurrentBindings.Count) { OrbitBinding selectedBinding=CurrentBindings[gestureIndex]; if(selectedBinding.Shape!=null) { if(selectedBinding.OriginalShape==null) selectedBinding.OriginalShape=selectedBinding.Shape; selectedBinding.Shape=draft.Orbit.CleanDrawing?GestureCleanup.Clean(selectedBinding.OriginalShape):selectedBinding.OriginalShape; } }
                UpdateGestureLabels(); RefreshGestureSelection(); Changed(draft.Orbit.CleanDrawing?"形を補正しました。「補正前に戻す」で元の形に戻せます。":"元の描画を使います。");
            };
            restoreDrawing=EditorUi.Button("補正前に戻す",delegate { if(!editingGesture || gestureIndex<0 || gestureIndex>=CurrentBindings.Count) return; OrbitBinding binding=CurrentBindings[gestureIndex]; if(binding.OriginalShape==null) return; Remember(); binding.Shape=binding.OriginalShape; draft.Orbit.CleanDrawing=false; cleanDrawing.IsChecked=false; UpdateGestureLabels(); RefreshGestureSelection(); Changed("補正前の描画に戻しました。"); },false); restoreDrawing.Padding=new Thickness(10,3,10,3); restoreDrawing.Margin=new Thickness(12,0,0,0); cleanTools.Children.Add(restoreDrawing);
            editGesture=EditorUi.Button("編集",delegate { editingGesture=!editingGesture; RefreshGestureSelection(); Changed(editingGesture?"左ドラッグで形を描き直せます。変更後は「完了」を押してください。":"ジェスチャーの編集を終了しました。変更は「保存」で反映します。"); },false); editGesture.Padding=new Thickness(12,3,12,3); editGesture.Margin=new Thickness(12,0,0,0); cleanTools.Children.Add(editGesture);
            gestureName=EditorUi.Text("",17,EditorUi.Ink); gestureName.FontWeight=FontWeights.SemiBold; drawingTools.Children.Add(gestureName);
            gesturePreview=new GestureDrawing { EditorView=true,Cursor=System.Windows.Input.Cursors.Arrow,Margin=new Thickness(12),MinHeight=100 };
            System.Windows.Automation.AutomationProperties.SetName(gesturePreview,"ジェスチャーの形。編集ボタンで描き直しを有効にする"); Grid.SetRow(gesturePreview,1); canvas.Children.Add(gesturePreview);
            gesturePreview.PreviewMouseLeftButtonDown+=delegate { if(testingGesture || gestureIndex<0 || gestureIndex>=CurrentBindings.Count) return; RefreshInspector(); ShowSide(1); };
            gesturePreview.Recorded=delegate(System.Collections.Generic.List<GesturePoint> shape) { if(testingGesture) { EvaluateGesture(); return; } if(!editingGesture || gestureIndex<0 || gestureIndex>=CurrentBindings.Count) return; Remember(); OrbitBinding binding=CurrentBindings[gestureIndex]; binding.OriginalShape=shape; binding.Shape=draft.Orbit.CleanDrawing?GestureCleanup.Clean(shape):shape; binding.Pattern="Shape"; UpdateGestureLabels(); RefreshGestureSelection(); Changed("形を変更しました。変更後は「完了」を押してください。"); };
            ConnectGestureDrop(gesturePreview,delegate { return gestureIndex; });
            Grid conditions=new Grid { Margin=new Thickness(0,0,0,12) }; conditions.ColumnDefinitions.Add(new ColumnDefinition()); conditions.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(16) }); conditions.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(conditions,2); canvas.Children.Add(conditions);
            conditions.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); conditions.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            FrameworkElement tolerance=BuildGestureTolerance(); Grid.SetRow(tolerance,1); Grid.SetColumnSpan(tolerance,3); conditions.Children.Add(tolerance);
            StackPanel types=Section("動きの種類"); conditions.Children.Add(types);
            gestureType=new ComboBox { ItemsSource=new[]{"マウスで形を描く","ボタンを押してホイール上","ボタンを押してホイール下","ボタンを押したまま左クリック"},Padding=new Thickness(8) }; types.Children.Add(gestureType);
            System.Windows.Automation.AutomationProperties.SetName(gestureType,"動きの種類");
            gestureType.SelectionChanged+=delegate {
                if(updatingWorkspace || testingGesture || gestureType.SelectedIndex<0 || gestureIndex<0 || gestureIndex>=CurrentBindings.Count) return;
                OrbitBinding binding=CurrentBindings[gestureIndex]; string pattern=new[]{"","WheelUp","WheelDown","Rocker"}[gestureType.SelectedIndex];
                if(pattern=="" && binding.Pattern!="WheelUp" && binding.Pattern!="WheelDown" && binding.Pattern!="Rocker" || pattern==binding.Pattern) return;
                Remember(); binding.Shape=null; binding.OriginalShape=null; binding.Pattern=pattern; editingGesture=pattern=="";
                UpdateGestureLabels(); RefreshGestureSelection(); Changed(editingGesture?"中央に形を描き、「完了」を押してください。":"動きの種類を変更しました。「保存」で反映します。");
            };
            StackPanel keys=Section("同時に押すキー"); Grid.SetColumn(keys,2); conditions.Children.Add(keys);
            gestureModifier=new ComboBox { ItemsSource=new[]{"なし","Ctrl を押しながら","Alt を押しながら","Shift を押しながら"},Padding=new Thickness(8),ToolTip="例：Ctrlを選ぶと、Ctrlを押しながら描いたときだけ実行します。" }; keys.Children.Add(gestureModifier);
            System.Windows.Automation.AutomationProperties.SetName(gestureModifier,"同時に押すキー");
            gestureModifier.SelectionChanged+=delegate { if(updatingWorkspace || gestureModifier.SelectedIndex<0) return; string modifier=new[]{"","Ctrl","Alt","Shift"}[gestureModifier.SelectedIndex]; if(testingGesture) { testModifier=modifier; return; } if(gestureIndex<0 || gestureIndex>=CurrentBindings.Count || CurrentBindings[gestureIndex].Modifier==modifier) return; Remember(); CurrentBindings[gestureIndex].Modifier=modifier; UpdateGestureLabels(); Changed("同時に押すキーを変更しました。「保存」で反映します。"); };
            FrameworkElementFactory wrap=new FrameworkElementFactory(typeof(WrapPanel));
            gestureList=new ListBox { ItemsPanel=new ItemsPanelTemplate(wrap),Height=140,Background=EditorUi.Bg,Foreground=EditorUi.Ink,BorderBrush=EditorUi.Line,BorderThickness=new Thickness(1),Padding=new Thickness(3) }; ScrollViewer.SetHorizontalScrollBarVisibility(gestureList,ScrollBarVisibility.Disabled); Grid.SetRow(gestureList,3); canvas.Children.Add(gestureList);
            gestureList.SelectionChanged+=delegate { if(updatingWorkspace) return; editingGesture=false; testingGesture=false; gestureIndex=gestureList.SelectedIndex; RefreshGestureSelection(); };
            Grid content=new Grid(); content.RowDefinitions.Add(new RowDefinition());
            Border frame=EditorFrame(canvas); frame.MinHeight=440; content.Children.Add(frame);
            left.Children.Add(new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalContentAlignment=VerticalAlignment.Stretch,Padding=new Thickness(0,0,8,8) });
            Grid.SetColumn(side,2); Grid.SetRow(side,0); columns.Children.Add(side);
            if(gestureIndex>=CurrentBindings.Count) gestureIndex=CurrentBindings.Count-1; UpdateGestureLabels(); RefreshGestureSelection(); return columns;
        }
    }
}

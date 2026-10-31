using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbitRing {
    sealed class EditSnapshot {
        public Preferences Config;
        public int Selected,Page,Gesture;
        public int Planet;
        public string Application;
        public long EstimatedBytes;
        public int[] StackPath;
    }
    sealed partial class SettingsWindow : Window {
        readonly Controller owner;
        Preferences draft;
        int selected;
        bool syncing, sliderGesture, sliderHistory;
        readonly List<EditSnapshot> history=new List<EditSnapshot>();
        LayoutEditor surface;
        readonly List<EditSnapshot> future=new List<EditSnapshot>();
        Border inspector;
        ScrollViewer inspectorScroll;
        TextBlock inspectorTitle;
        Grid side;
        DockPanel catalogPanel, detailPanel, settingsPanel, iconPanel;
        StackPanel macroRows, macroPanel, singleValuePanel;
        TextBox keyCapture;
        Button recordAction;
        IconPicker iconPicker;
        ConversionChoice iconConversionChoice;
        Button chooseIcon;
        TextBlock iconTitle;
        TextBox search;
        ComboBox catalogView;
        readonly HashSet<string> expandedCategories=new HashSet<string>();
        StackPanel catalogRows;
        TextBlock catalogCount, catalogHint;
        readonly List<ActionPreset> presets=ActionCatalog.Create();
        Slider ringSize, buttonSize;
        TextBlock status, ringSizeLabel, buttonSizeLabel, selectionLabel, valueHint;
        TextBox hotkey, actionName, actionValue;
        ComboBox actionKind;
        string[] actionKinds;
        Button undo, redo, browse;
        public SettingsWindow(Controller controller) {
            owner=controller; draft=owner.Config.Clone(); Icon=AppIcon.Image;
            Title="orbitals — planet"; Width=1320; Height=880; MinWidth=1000; MinHeight=700;
            MaxHeight=Math.Max(700,SystemParameters.WorkArea.Height); Height=Math.Min(Height,MaxHeight);
            WindowStartupLocation=WindowStartupLocation.CenterScreen; Background=EditorUi.Bg; Foreground=EditorUi.Ink; FontFamily=new FontFamily("Segoe UI, Yu Gothic UI");
            EditorUi.Apply(this); Build(); RefreshAll(); InitializeDraft();
            owner.UpdateChanged+=RefreshUpdateSettings; Closed+=delegate { owner.UpdateChanged-=RefreshUpdateSettings; };
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
                if(owner.MacroRecording) return;
                if(keyCapture!=null) { e.Handled=true; return; }
                if(!(Keyboard.FocusedElement is TextBox)) {
                    if(e.Key==Key.Z && Keyboard.Modifiers==ModifierKeys.Control) { Undo(); e.Handled=true; }
                    else if((e.Key==Key.Y && Keyboard.Modifiers==ModifierKeys.Control) || (e.Key==Key.Z && Keyboard.Modifiers==(ModifierKeys.Control|ModifierKeys.Shift))) { Redo(); e.Handled=true; }
                }
            };
            PreviewKeyUp+=delegate(object sender,KeyEventArgs e) { if(keyCapture!=null) e.Handled=true; };
            Deactivated+=delegate { EndKeyCapture(); };
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {
                owner.CancelMacroTest();
                StopMacroRecording(true);
                EndKeyCapture();
                if(!Dirty()) { ClearWorkingCopy(); return; }
                SaveChoice result=OrbitDialog.ConfirmSave(this);
                if(result==SaveChoice.Cancel || (result==SaveChoice.Save && !Save()) || (result==SaveChoice.KeepDraft && !SaveWorkingCopy())) e.Cancel=true;
                else if(result==SaveChoice.Discard) ClearWorkingCopy();
            };
        }
        public void ReleaseResources() {
            owner.CancelMacroTest();
            if(updatePopup!=null) { updatePopup.IsOpen=false; updatePopup.Child=null; }
            StopMacroRecording(true); EndKeyCapture(); if(draftTimer!=null) draftTimer.Stop();
            if(iconPicker!=null) iconPicker.ReleaseResources();
            history.Clear(); future.Clear(); keyCapture=null; Content=null;
            side.Children.Clear(); catalogRows.Children.Clear(); macroRows.Children.Clear(); togglePanel.Children.Clear(); muteIconsPanel.Children.Clear(); surface.Children.Clear();
            iconPanel.Children.Clear(); catalogPanel.Children.Clear(); detailPanel.Children.Clear(); settingsPanel.Children.Clear(); gesturePresetRows.Children.Clear(); gesturePresetPanel.Children.Clear(); gestureAddPanel.Children.Clear(); inspector.Child=null;
            planetPanel.Children.Clear(); gestureTestPanel.Children.Clear(); gestureSavePanel.Children.Clear();
            if(Application.Current!=null && Application.Current.MainWindow==this) Application.Current.MainWindow=null;
        }
        bool Dirty() { return Json.Write(draft)!=Json.Write(owner.Config); }
        void Remember() {
            future.Clear();
            history.Add(Snapshot());
            TrimHistory();
        }
        void TrimHistory() {
            const long budget=16L*1024*1024;
            while(history.Count+future.Count>100 || history.Sum(s=>s.EstimatedBytes)+future.Sum(s=>s.EstimatedBytes)>budget) {
                if(history.Count>1) history.RemoveAt(0); else if(future.Count>1) future.RemoveAt(0); else break;
            }
        }
        void Changed(string text) {
            bool dirty=Dirty(); status.Foreground=dirty?EditorUi.Mint:EditorUi.Muted;
            status.Text=(dirty?"未保存 · ":"")+text;
            undo.IsEnabled=history.Count>0; redo.IsEnabled=future.Count>0;
            if(navUndo!=null) { navUndo.IsEnabled=history.Count>0; navRedo.IsEnabled=future.Count>0; }
            ScheduleDraft();
        }
        void Undo() {
            if(history.Count==0) return;
            future.Add(Snapshot());
            EditSnapshot previous=history[history.Count-1]; history.RemoveAt(history.Count-1); draft=previous.Config; selected=previous.Selected; RestoreNavigation(previous);
            TrimHistory();
            RefreshAll(); Changed("ひとつ前の状態に戻しました。");
        }
        void Redo() {
            if(future.Count==0) return;
            history.Add(Snapshot());
            EditSnapshot next=future[future.Count-1]; future.RemoveAt(future.Count-1); draft=next.Config; selected=next.Selected; RestoreNavigation(next);
            TrimHistory();
            RefreshAll(); Changed("ひとつ先の状態に進みました。");
        }
        void MoveAction(int[] fromPath,int from,int[] toPath,int to) {
            EditorMove plan=EditorMove.Plan(EditorRootItems(),fromPath,from,toPath,to);
            if(!plan.Allowed) { Changed(plan.Error); return; }
            Remember(); plan.Apply(); SetEditorContext(plan.Path); selected=plan.Slot;
            RefreshAll(); ShowSide(1); Changed("項目を移動しました。「元に戻す」で戻せます。「保存」で実際のリングに反映します。");
        }
        void Swap(int from,int to) {
            if(from==to) return;
            Remember();
            if(CurrentPlanetItems[to].Kind=="stack" && CurrentPlanetItems[from].Kind!="stack" && CurrentPlanetItems[from].Kind!="none") {
                if(!PlanetStack.Add(CurrentPlanetItems[to],CurrentPlanetItems[from])) { Changed("スタックの上限です。階層型は8枠、弧状は32件までです。"); return; } CurrentPlanetItems[from]=PlanetStack.Empty();
            } else { ActionItem item=CurrentPlanetItems[from]; CurrentPlanetItems[from]=CurrentPlanetItems[to]; CurrentPlanetItems[to]=item; }
            selected=to; RefreshAll(); Changed("項目を入れ替えました。「保存」で実際のリングに反映します。");
        }
        void Assign(int index,ActionItem item) {
            Remember();
            if(page==0 && index==CurrentPlanetItems.Count && EditingArc() && index<PlanetStack.MaxArcItems) CurrentPlanetItems.Add(PlanetStack.Empty());
            if(page==1) { if(gestureIndex<0 || gestureIndex>=CurrentBindings.Count) { CurrentBindings.Add(new OrbitBinding { Pattern="",Action=item.Clone() }); gestureIndex=CurrentBindings.Count-1; } else CurrentBindings[gestureIndex].Action=item.Clone(); }
            else if(CurrentPlanetItems[index].Kind=="stack" && item.Kind!="stack") { if(!PlanetStack.Add(CurrentPlanetItems[index],item.Clone())) { Changed("スタックの上限です。階層型は8枠、弧状は32件までです。"); return; } }
            else CurrentPlanetItems[index]=item.Clone(); selected=index;
            RefreshAll(); ShowSide(1);
            Changed("「"+item.Label+"」を割り当てました。");
        }
        void Erase(int[] path,int index) {
            if(page!=0) return;
            IList<ActionItem> items=PlanetEditorLayout.Resolve(EditorRootItems(),path);
            if(index<0 || index>=items.Count || items[index].Kind=="none") return;
            string label=items[index].Label;
            Remember(); SetEditorContext(path);
            PlanetStack.Erase(items,index,EditingArc());
            selected=Math.Min(index,items.Count-1);
            RefreshAll(); ShowSide(0); Changed("「"+label+"」を消去しました。元に戻すこともできます。");
        }
        string surfaceStamp;
        void RefreshSurface() { if(page==0) { Preferences view=new Preferences { Items=EditorRootItems(),Appearance=draft.Appearance }; string stamp=Json.Write(view.Items)+Json.Write(view.Appearance)+selected+":"+String.Join(",",stackPath); if(stamp!=surfaceStamp) { surface.Bind(view,selected,stackPath.ToArray()); surfaceStamp=stamp; } } else if(page==1) UpdateGestureLabels(); }
        void RefreshAll() {
            iconStage=-1; iconMuteState=null; EndKeyCapture();
            UpdateWorkspace();
            if(page==0) selected=Math.Max(0,Math.Min(selected,CurrentPlanetItems.Count-1));
            syncing=true;
            if(hotkey!=null) hotkey.Text=draft.Hotkey; if(ringSize!=null) ringSize.Value=draft.RingPercent; if(buttonSize!=null) buttonSize.Value=draft.ButtonSize;
            syncing=false; if(page==0) { RefreshInspector(); RefreshSurface(); } SizeLabels(); undo.IsEnabled=history.Count>0; redo.IsEnabled=future.Count>0;
        }
        void RefreshInspector() {
            syncing=true; ActionItem item=EditingAction;
            selectionLabel.Visibility=page==1?Visibility.Collapsed:Visibility.Visible;
            selectionLabel.Text=page==1?"":page==0 && EditingArc()?"スタック内 · "+(selected+1)+"番目":draft.Appearance.EvenSpacing?(item.Kind=="none"?"新しいアクション":"配置 "+CurrentPlanetItems.Take(selected+1).Count(a=>a.Kind!="none")):(selected+1)+"  ·  "+new string[]{"上","右上","右","右下","下","左下","左","左上"}[selected];
            inspectorTitle.Text=item.Label;
            actionKinds=ExtendedActions.Kinds.Where(k=>page==1?k!="stack":k!="planet").ToArray();
            actionName.Text=item.Label; actionKind.ItemsSource=actionKinds.Select(k=>ExtendedActions.Names[Array.IndexOf(ExtendedActions.Kinds,k)]).ToArray(); actionKind.SelectedIndex=Math.Max(0,Array.IndexOf(actionKinds,item.Kind));
            actionValue.Text=item.Value; actionValue.IsEnabled=item.Kind!="volume";
            browse.Visibility=item.Kind=="open" || item.Kind=="launch"?Visibility.Visible:Visibility.Collapsed;
            valueHint.Text=item.Kind=="open"?"ファイル・フォルダーのパス、またはURLを指定します。":item.Kind=="volume"?"ホイールで音量を調整します。クリックでミュートを切り替えます。":item.Kind=="hold"?"押すとカーソルが呼び出し位置へ戻り、キーを保持します。\nその場所でホイール操作ができます。離すかEscで解除します。":item.Kind=="shortcut"?"例：Space、Ctrl+C。Ctrlだけの指定もできます。":"";
            string extraHint=ExtendedActions.Hint(item.Kind); if(extraHint!="") valueHint.Text=extraHint;
            bool keyOnly=item.Kind=="shortcut" || item.Kind=="hold";
            actionValue.IsReadOnly=keyOnly; actionValue.BorderThickness=new Thickness(keyOnly?0:1); actionValue.Padding=keyOnly?new Thickness(0):new Thickness(10,7,10,7); actionValue.ToolTip=keyOnly?"「キーを記録」で変更します。":null;
            actionValue.AcceptsReturn=item.Kind=="text"; actionValue.MinHeight=item.Kind=="text"?100:0;
            RefreshExtendedValues(item);
            recordAction.Visibility=item.Kind=="shortcut" || item.Kind=="hold"?Visibility.Visible:Visibility.Collapsed;
            singleValuePanel.Visibility=item.Kind=="macro" || item.Kind=="toggle" || item.Kind=="stack" || item.Kind=="none"?Visibility.Collapsed:Visibility.Visible;
            macroPanel.Visibility=item.Kind=="macro"?Visibility.Visible:Visibility.Collapsed;
            if(item.Kind=="macro") LoadMacro();
            togglePanel.Visibility=item.Kind=="toggle"?Visibility.Visible:Visibility.Collapsed;
            if(item.Kind=="toggle") RenderToggle();
            stackPanel.Visibility=page==0 && item.Kind=="stack"?Visibility.Visible:Visibility.Collapsed;
            stackMode.SelectedIndex=PlanetStack.Mode(item)=="arc"?1:0; RefreshStackModeHint();
            stackWrap.Visibility=page==0 && item.Kind!="stack" && stackPath.Count<PlanetStack.MaxDepth?Visibility.Visible:Visibility.Collapsed;
            stackMoves.Visibility=page==0?Visibility.Visible:Visibility.Collapsed; positionHelp.Visibility=page==0?Visibility.Visible:Visibility.Collapsed;
            RefreshIcons();
            syncing=false;
        }
        void SizeLabels() {
            if(ringSizeLabel!=null) ringSizeLabel.Text=Math.Round(draft.RingPercent)+"%";
            if(buttonSizeLabel!=null) buttonSizeLabel.Text=Math.Round(draft.ButtonPercent)+"%";
        }
        static StackPanel Section(string name) {
            StackPanel panel=new StackPanel(); TextBlock label=EditorUi.Text(name,12,EditorUi.Muted); label.Margin=new Thickness(0,0,0,8); panel.Children.Add(label); return panel;
        }
        static Border Card(UIElement child) { return new Border { Background=EditorUi.Panel, BorderBrush=EditorUi.Line, BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(14), Padding=new Thickness(20), Child=child }; }
        static Grid EditorColumns() { Grid columns=new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(24) }); columns.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(350) }); return columns; }
        static Border EditorFrame(UIElement child) { return new Border { Background=Ui.Brush("#0C1016"),BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Child=child,ClipToBounds=true }; }
        internal static Microsoft.Win32.OpenFileDialog ActionFileDialog(string kind) {
            return new Microsoft.Win32.OpenFileDialog { Title=kind=="launch"?"起動するアプリ・ショートカットを選ぶ":"開くファイルを選ぶ",Filter=kind=="launch"?"アプリ・ショートカット (*.exe;*.lnk)|*.exe;*.lnk":"すべてのファイル|*.*",CheckFileExists=true,Multiselect=false,DereferenceLinks=false };
        }
        void RestoreSaved() { Remember(); draft=owner.Config.Clone(); editorApplication=null; editorPlanet=0; selected=0; gestureIndex=0; stackPath.Clear(); RefreshAll(); Changed("保存済みの状態に戻しました。"); }
        void Build() {
            DockPanel root=new DockPanel { Margin=new Thickness(26), Background=EditorUi.Bg }; Content=root;
            DockPanel head=new DockPanel { Margin=new Thickness(0,0,0,18) }; DockPanel.SetDock(head,Dock.Top); root.Children.Add(head);
            Button gear=IconButton("\uE713","設定",delegate { Navigate(3); }); gear.Margin=new Thickness(0); if(owner.Diagnostic) gear.Visibility=Visibility.Collapsed; DockPanel.SetDock(gear,Dock.Right); head.Children.Add(gear);
            Button update=BuildUpdateButton(); DockPanel.SetDock(update,Dock.Right); head.Children.Add(update);
            StackPanel title=new StackPanel(); title.Children.Add(EditorUi.Text("O R B I T A L S",11,EditorUi.Mint));
            TextBlock headline=EditorUi.Text("planet — リングの設定",27,EditorUi.Ink); headline.FontWeight=FontWeights.SemiBold; headline.Margin=new Thickness(0,6,0,4); title.Children.Add(headline); pageHeading=headline;
            pageDescription=EditorUi.Text("ドラッグで割り当て・並べ替え。項目をクリックして内容を編集。",13,EditorUi.Muted); title.Children.Add(pageDescription); head.Children.Add(title);
            BuildNavigation(root);
            BuildRecordingBar(root);

            DockPanel footer=new DockPanel { Margin=new Thickness(0,18,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            bottomButtons=new StackPanel { Orientation=Orientation.Horizontal, HorizontalAlignment=HorizontalAlignment.Right }; DockPanel.SetDock(bottomButtons,Dock.Right);
            bottomButtons.Children.Add(EditorUi.Button("保存",delegate { Save(); },true));
            if(!owner.Diagnostic) bottomButtons.Children.Add(EditorUi.Button("保存して試す",delegate { if(!Save()) return; if(page==1) WindowState=WindowState.Minimized; else if(page==5) owner.OpenConversion(IntPtr.Zero); else owner.ShowRing(true,editorPlanet,editorApplication); },false));
            bottomButtons.Children.Add(EditorUi.Button(owner.Diagnostic?"閉じる":"終了",delegate { Close(); if(!IsVisible && !owner.Diagnostic) Application.Current.Shutdown(); },false)); footer.Children.Add(bottomButtons);
            status=EditorUi.Text("編集後は「保存」で反映します。",12,EditorUi.Muted); status.VerticalAlignment=VerticalAlignment.Center; status.Margin=new Thickness(0,0,12,0); footer.Children.Add(status);

            workspace=new Grid { ClipToBounds=true }; root.Children.Add(workspace);
            Grid columns=EditorColumns(); planetColumns=columns; workspace.Children.Add(columns);
            DockPanel left=new DockPanel(); columns.Children.Add(left);
            DockPanel tools=new DockPanel { Margin=new Thickness(0,0,0,10) }; DockPanel.SetDock(tools,Dock.Top); left.Children.Add(tools);
            StackPanel toolButtons=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(toolButtons,Dock.Right);
            undo=navUndo; redo=navRedo;
            Button restore=EditorUi.Button("保存内容に戻す",delegate { RestoreSaved(); },false); restore.Padding=new Thickness(12,7,12,7); restore.Margin=new Thickness(0); toolButtons.Children.Add(restore); tools.Children.Add(toolButtons);
            BuildPlanetSelector(tools);

            surface=new LayoutEditor();
            surface.SelectionChanged=delegate(int[] path,int index) { SetEditorContext(path); if(index==CurrentPlanetItems.Count && EditingArc() && index<PlanetStack.MaxArcItems) { Remember(); CurrentPlanetItems.Add(PlanetStack.Empty()); Changed("新しいアクションの枠を追加しました。"); } selected=index; iconStage=-1; RefreshAll(); ShowSide(1); };
            surface.Moved=MoveAction;
            surface.Inserted=delegate(int[] path,int before,ActionItem item,int from) {
                IList<ActionItem> items=PlanetEditorLayout.Resolve(EditorRootItems(),path); int index;
                List<ActionItem> insertedItems=EditorInsertion.Prepare(items,before,from<0?UserContent.CopyAction(item):items[from],from,path.Length,out index);
                if(insertedItems==null) return;
                Remember(); for(int i=0;i<items.Count;i++) items[i]=insertedItems[i];
                SetEditorContext(path); selected=index; RefreshAll(); ShowSide(1); Changed("項目の間に配置しました。「保存」で反映します。");
            };
            surface.Assigned=delegate(int[] path,int index,ActionItem item) { SetEditorContext(path); Assign(index,item); };
            surface.Entered=delegate(int[] path,int index) { SetEditorContext(path); EnterEditorStack(index); };
            surface.Erased=Erase;
            PlanetEditorViewport viewport=new PlanetEditorViewport(surface);
            Border previewFrame=EditorFrame(viewport); EnableBackgroundReturn(previewFrame); left.Children.Add(previewFrame);

            side=new Grid(); Grid.SetColumn(side,2); columns.Children.Add(side);
            catalogPanel=new DockPanel(); detailPanel=new DockPanel(); settingsPanel=new DockPanel(); iconPanel=new DockPanel();
            side.Children.Add(catalogPanel); side.Children.Add(detailPanel); side.Children.Add(settingsPanel); side.Children.Add(iconPanel);
            BuildGesturePresetPanel();
            BuildGestureTools();
            BuildPlanetPanel();
            DockPanel detailsHead=new DockPanel { Margin=new Thickness(0,0,0,16) }; DockPanel.SetDock(detailsHead,Dock.Top); detailPanel.Children.Add(detailsHead);
            Button catalog=EditorUi.Button("アクション一覧",delegate { ShowSide(0); },false); catalog.Padding=new Thickness(10,5,10,5); catalog.Margin=new Thickness(12,0,0,0); DockPanel.SetDock(catalog,Dock.Right); detailsHead.Children.Add(catalog);
            inspectorTitle=EditorUi.Text("アクションを編集",20,EditorUi.Ink); inspectorTitle.FontWeight=FontWeights.SemiBold; inspectorTitle.VerticalAlignment=VerticalAlignment.Center; detailsHead.Children.Add(inspectorTitle);
            StackPanel edit=new StackPanel(); selectionLabel=EditorUi.Text("1 · 上",12,EditorUi.Mint); edit.Children.Add(selectionLabel);
            TextBlock editTitle=EditorUi.Text("選んだ項目を編集",19,EditorUi.Ink); editTitle.FontWeight=FontWeights.SemiBold; editTitle.Margin=new Thickness(0,8,0,18); edit.Children.Add(editTitle);
            StackPanel name=Section("表示名"); actionName=EditorUi.Input(""); actionName.MaxLength=20; name.Children.Add(actionName); name.Margin=new Thickness(0,0,0,16); edit.Children.Add(name);
            chooseIcon=EditorUi.Button("",delegate { iconStage=-1; iconMuteState=null; ShowSide(3); },false); chooseIcon.Margin=new Thickness(0,0,0,16); edit.Children.Add(chooseIcon);
            muteIconsPanel=new StackPanel { Margin=new Thickness(0,0,0,16) }; edit.Children.Add(muteIconsPanel);
            Button favorite=EditorUi.Button("お気に入りに保存",delegate { SaveFavorite(); },false); favorite.Margin=new Thickness(0,0,0,16); edit.Children.Add(favorite);
            StackPanel kind=Section("操作の種類"); actionKind=new ComboBox { ItemsSource=ExtendedActions.Names, Padding=new Thickness(8), FontSize=13 }; kind.Children.Add(actionKind); kind.Margin=new Thickness(0,0,0,16); edit.Children.Add(kind);
            StackPanel value=Section("実行する内容"); value.Margin=new Thickness(0,0,0,16); singleValuePanel=value; actionValue=EditorUi.Input(""); actionValue.MaxLength=8192; value.Children.Add(actionValue); extendedValues=new ComboBox { Padding=new Thickness(8),Margin=new Thickness(0,8,0,0) }; value.Children.Add(extendedValues); extendedValues.SelectionChanged+=delegate { if(syncing || extendedValues.SelectedIndex<0 || extendedCodes==null) return; actionValue.Text=extendedCodes[extendedValues.SelectedIndex]; }; valueHint=EditorUi.Text("",11,EditorUi.Muted); valueHint.Margin=new Thickness(0,7,0,0); value.Children.Add(valueHint);
            recordAction=EditorUi.Button("キーを記録",delegate { BeginKeyCapture(actionValue); },false); recordAction.Margin=new Thickness(0,8,0,0); value.Children.Add(recordAction);
            browse=EditorUi.Button("ファイルを選ぶ…",delegate {
                Microsoft.Win32.OpenFileDialog dialog=ActionFileDialog(EditingAction.Kind);
                if(dialog.ShowDialog(this)==true) actionValue.Text=dialog.FileName;
            },false); browse.Margin=new Thickness(0,8,0,0); value.Children.Add(browse); edit.Children.Add(value);
            BuildStackEditor(edit);
            togglePanel=new StackPanel(); edit.Children.Add(togglePanel);
            macroPanel=new StackPanel(); edit.Children.Add(macroPanel);
            macroPanel.Children.Add(EditorUi.Text("上から順に実行 · Escで中止",12,EditorUi.Muted));
            macroRows=new StackPanel { Margin=new Thickness(0,10,0,10) }; macroPanel.Children.Add(macroRows);
            BuildMacroRecorder(macroPanel);
            TextBlock positionNote=EditorUi.Text("ドラッグの代わりに",11,EditorUi.Muted); positionNote.Margin=new Thickness(0,22,0,8); edit.Children.Add(positionNote);
            Grid moves=new Grid(); moves.ColumnDefinitions.Add(new ColumnDefinition()); moves.ColumnDefinitions.Add(new ColumnDefinition());
            Button previous=EditorUi.Button("← 隣と入替",delegate { Swap(selected,AdjacentSlot(-1)); },false); previous.Padding=new Thickness(8); previous.Margin=new Thickness(0,0,6,0); moves.Children.Add(previous);
            Button next=EditorUi.Button("隣と入替 →",delegate { Swap(selected,AdjacentSlot(1)); },false); next.Padding=new Thickness(8); next.Margin=new Thickness(0); Grid.SetColumn(next,1); moves.Children.Add(next); edit.Children.Add(moves); stackMoves=moves; positionHelp=positionNote;
            inspectorScroll=new ScrollViewer { Content=edit,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
            inspector=Card(inspectorScroll); inspector.Padding=new Thickness(16); detailPanel.Children.Add(inspector);

            BuildGeneralSettings();
            DockPanel iconHead=new DockPanel { Margin=new Thickness(0,0,0,16) }; DockPanel.SetDock(iconHead,Dock.Top); iconPanel.Children.Add(iconHead);
            Button doneIcon=EditorUi.Button("完了",delegate { CloseIconPicker(); },false); doneIcon.Margin=new Thickness(12,0,0,0); DockPanel.SetDock(doneIcon,Dock.Right); iconHead.Children.Add(doneIcon);
            System.Windows.Automation.AutomationProperties.SetName(doneIcon,"アイコン・文字列の選択を完了");
            iconTitle=EditorUi.Text("アイコン・文字列を選ぶ",20,EditorUi.Ink); iconTitle.VerticalAlignment=VerticalAlignment.Center; iconHead.Children.Add(iconTitle);
            BuildCatalog(catalogPanel); ShowSide(0); side.Children.Remove(settingsPanel); settingsPanel.HorizontalAlignment=HorizontalAlignment.Stretch; workspace.Children.Add(settingsPanel); settingsPanel.Visibility=Visibility.Collapsed;
            side.Children.Remove(iconPanel); iconPanel.Background=EditorUi.Bg; Panel.SetZIndex(iconPanel,2); workspace.Children.Add(iconPanel); iconPanel.Visibility=Visibility.Collapsed;

            actionName.TextChanged+=delegate { if(syncing || EditingAction.Label==actionName.Text) return; Remember(); EditingAction.Label=actionName.Text; inspectorTitle.Text=actionName.Text; RefreshSurface(); Changed("表示名を変更しました。"); };
            actionValue.TextChanged+=delegate { if(syncing || EditingAction.Value==actionValue.Text) return; Remember(); EditingAction.Value=actionValue.Text; RefreshSurface(); Changed("実行する内容を変更しました。"); };
            actionKind.SelectionChanged+=delegate { if(syncing) return; Remember(); string previousKind=EditingAction.Kind;
                if(actionKind.SelectedIndex<0) return; string nextKind=actionKinds[actionKind.SelectedIndex]; EditingAction.Kind=nextKind;
                iconStage=-1;
                if(nextKind=="stack") { EditingAction.Children=PlanetStack.Create().Children; EditingAction.StackMode="level"; EditingAction.Icon=null; } else { EditingAction.Children=null; EditingAction.StackMode=null; }
                if(nextKind=="toggle") { EditingAction.Toggle=ToggleDefinition.Create(); EditingAction.Icon="ms:toggle_off"; }
                else EditingAction.Toggle=null;
                if(nextKind=="macro") EditingAction.Value="Space\nwait:200\nSpace";
                else if(!((previousKind=="shortcut" || previousKind=="hold") && (nextKind=="shortcut" || nextKind=="hold"))) EditingAction.Value=nextKind=="hold"?"Ctrl":ExtendedActions.DefaultValue(nextKind);
                RefreshInspector(); RefreshSurface(); Changed("操作の種類を変更しました。"); };
            System.Windows.Automation.AutomationProperties.SetName(actionName,"選択した項目の表示名");
            System.Windows.Automation.AutomationProperties.SetName(actionValue,"選択した項目の内容");
            System.Windows.Automation.AutomationProperties.SetName(hotkey,"呼び出しキー");
        }
        static Button IconButton(string glyph,string name,RoutedEventHandler action) {
            Button b=EditorUi.Button("",action,false); b.Width=36; b.Height=36; b.Padding=new Thickness(0); b.ToolTip=name;
            b.Content=new TextBlock { Text=glyph,FontSize=17,FontFamily=new FontFamily("Segoe MDL2 Assets") };
            System.Windows.Automation.AutomationProperties.SetName(b,name); return b;
        }
        void RefreshIcons() {
            RefreshMuteIcons();
            chooseIcon.Visibility=page==0 && !AudioMute.Supports(EditingAction)?Visibility.Visible:Visibility.Collapsed;
            if(page!=0) return;
            StackPanel content=new StackPanel { Orientation=Orientation.Horizontal };
            FrameworkElement icon=ActionIcons.Visual(EditingAction,22); icon.Margin=new Thickness(0,0,10,0); icon.VerticalAlignment=VerticalAlignment.Center; content.Children.Add(icon);
            TextBlock label=Ui.Text(EditingAction.Kind=="toggle"?"初期状態のアイコン・文字列":"アイコン・文字列を変更",13,Ui.Ink); label.VerticalAlignment=VerticalAlignment.Center; content.Children.Add(label); chooseIcon.Content=content;
            chooseIcon.ToolTip=ActionIcons.Label(EditingAction.Icon);
            System.Windows.Automation.AutomationProperties.SetName(chooseIcon,"アイコン・文字列を変更");
            if(iconPicker!=null && iconPanel.Visibility==Visibility.Visible) iconPicker.Bind(IconTarget());
        }
        void EnsureIconPicker() {
            if(iconPicker!=null) return;
            iconPicker=new IconPicker(); iconPanel.Children.Add(iconPicker);
            iconPicker.Selected=delegate(string key) {
                if(iconConversionChoice!=null) { Remember(); iconConversionChoice.Icon=key; RefreshAll(); Changed("変換先のアイコン・文字列を変更しました。「保存」で反映します。"); return; }
                if(IconTarget().Icon==key) return;
                Remember();
                if(iconMuteState.HasValue) AudioMute.SetIcon(EditingAction,iconMuteState.Value,key);
                else if(iconStage<0) EditingAction.Icon=key; else EditingAction.Toggle.Stages[iconStage].Icon=key;
                RefreshIcons(); RefreshSurface();
                if(EditingAction.Kind=="toggle") RenderToggle();
                Changed(key==null?"アイコンを自動選択に戻しました。":"「"+ActionIcons.Label(key)+"」に変更しました。「保存」で反映します。");
            };
        }
        void OpenConversionIconPicker(ConversionChoice choice) { iconMuteState=null; iconTitle.Text="アイコン・文字列を選ぶ"; iconConversionChoice=choice; conversionSelected=draft.Conversion.List(conversionSource).IndexOf(choice); EnsureIconPicker(); iconPicker.Open(choice.Action()); appsPage.Visibility=Visibility.Collapsed; iconPanel.Visibility=Visibility.Visible; }
        void ShowSide(int mode) {
            if(page!=0 && page!=1) return;
            if(mode==3 && page!=0) mode=1;
            if((mode==4 || mode==5) && page!=1) mode=0;
            EndKeyCapture();
            catalogPanel.Visibility=mode==0?Visibility.Visible:Visibility.Collapsed;
            detailPanel.Visibility=mode==1?Visibility.Visible:Visibility.Collapsed;

            iconPanel.Visibility=mode==3?Visibility.Visible:Visibility.Collapsed;
            if(page==0) planetColumns.Visibility=mode==3?Visibility.Collapsed:Visibility.Visible;
            gesturePresetPanel.Visibility=mode==4?Visibility.Visible:Visibility.Collapsed;
            gestureAddPanel.Visibility=mode==5?Visibility.Visible:Visibility.Collapsed;
            planetPanel.Visibility=mode==6?Visibility.Visible:Visibility.Collapsed;
            gestureTestPanel.Visibility=mode==7?Visibility.Visible:Visibility.Collapsed;
            gestureSavePanel.Visibility=mode==8?Visibility.Visible:Visibility.Collapsed;
            if(mode==8) RefreshGestureSavePanel();
            if(mode==6) RefreshPlanetPanel();
            if(mode==4) RefreshGesturePresets();
            if(mode==0) RefreshCatalog();
            if(mode==3) { iconTitle.Text=iconMuteState.HasValue?(iconMuteState.Value?"ミュートON":"ミュートOFF")+"のアイコン・文字列":"アイコン・文字列を選ぶ"; EnsureIconPicker(); iconPicker.Open(IconTarget()); }
            if(mode==1) inspectorScroll.ScrollToTop();
        }
        void BuildCatalog(DockPanel right) {
            StackPanel header=new StackPanel { Margin=new Thickness(0,0,0,12) }; DockPanel.SetDock(header,Dock.Top); right.Children.Add(header);
            TextBlock heading=EditorUi.Text("アクション一覧",20,EditorUi.Ink); heading.FontWeight=FontWeights.SemiBold; header.Children.Add(heading);
            catalogView=new ComboBox { ItemsSource=new[]{"標準アクション","お気に入り","プラグイン"},SelectedIndex=0,Padding=new Thickness(6),Margin=new Thickness(0,10,0,0) }; header.Children.Add(catalogView);
            catalogView.SelectionChanged+=delegate { if(catalogRows!=null) RefreshCatalog(); };
            catalogHint=EditorUi.Text("",11,EditorUi.Muted); catalogHint.Margin=new Thickness(0,6,0,12); header.Children.Add(catalogHint);
            header.Children.Add(EditorUi.Text("アクションを検索",11,EditorUi.Muted));
            search=EditorUi.Input(""); search.Margin=new Thickness(0,5,0,6); header.Children.Add(search);
            System.Windows.Automation.AutomationProperties.SetName(search,"アクションを検索");
            catalogCount=EditorUi.Text("",11,EditorUi.Muted); header.Children.Add(catalogCount);
            catalogRows=new StackPanel();
            right.Children.Add(new ScrollViewer { Content=catalogRows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            search.TextChanged+=delegate { RefreshCatalog(); }; RefreshCatalog();
        }
        void RefreshCatalog() {
            catalogHint.Text=page==1?"中央や項目へドラッグ / クリックで選択中に割り当て":"リングへドラッグ / クリックで選択中の位置に割り当て";
            catalogRows.Children.Clear(); int count=0;
            bool favorites=catalogView.SelectedIndex==1;
            IEnumerable<ActionPreset> available=catalogView.SelectedIndex==2?ActionPlugins.Presets(draft):favorites?draft.Favorites.Select(a=>new ActionPreset(ActionCatalog.CategoryFor(a),a)):presets;
            available=available.ToList();
            foreach(string category in catalogView.SelectedIndex==2?available.Select(a=>a.Category).Distinct():ActionCatalog.Categories) {
                List<ActionPreset> matches=available.Where(p=>p.Category==category && p.Matches(search.Text) && (page!=1 || p.Item.Kind!="stack") && (p.Item.Kind!="planet" || page==1)).ToList();
                if(matches.Count==0) continue; count+=matches.Count;
                StackPanel items=new StackPanel { Margin=new Thickness(0,8,0,0) };
                bool populated=false; Action populate=delegate { if(populated) return; populated=true; MaterialIcons.Prepare(matches.Select(p=>ActionIcons.EffectiveKey(p.Item)));
                foreach(ActionPreset preset in matches) {
                    ActionPreset entry=preset; bool dragged=false; Point start=new Point();
                    Button row=EditorUi.Button("",delegate { if(!dragged) Assign(selected,entry.Create()); },false);
                    row.HorizontalContentAlignment=HorizontalAlignment.Stretch; row.FontWeight=FontWeights.Normal; row.Margin=new Thickness(0,0,0,6); row.Padding=new Thickness(11,10,11,10);
                    Grid body=new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(26) }); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(18) });
                    FrameworkElement icon=ActionIcons.Visual(entry.Item,18); icon.HorizontalAlignment=HorizontalAlignment.Left; icon.VerticalAlignment=VerticalAlignment.Center; body.Children.Add(icon);
                    TextBlock name=EditorUi.Text(entry.Item.Label,13,EditorUi.Ink); name.VerticalAlignment=VerticalAlignment.Center; Grid.SetColumn(name,1); body.Children.Add(name);
                    TextBlock grip=EditorUi.Text("⠿",16,EditorUi.Muted); Grid.SetColumn(grip,2); body.Children.Add(grip); row.Content=body;
                    row.ToolTip=entry.Item.Value==""?entry.Item.Kind=="volume"?"ホイールで音量調整。クリックでミュート。":"割り当てた後に内容を指定します。":entry.Item.Value;
                    System.Windows.Automation.AutomationProperties.SetName(row,entry.Item.Label+"を割り当て");
                    row.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { start=e.GetPosition(this); dragged=false; };
                    row.PreviewMouseMove+=delegate(object sender,MouseEventArgs e) {
                        if(dragged || e.LeftButton!=MouseButtonState.Pressed || (e.GetPosition(this)-start).Length<6) return;
                        dragged=true; row.ReleaseMouseCapture(); EditorDrag.Run(row,entry.Create()); e.Handled=true;
                    };
                    if(favorites) {
                        DockPanel favoriteRow=new DockPanel(); Button remove=IconButton("\uE74D","お気に入りから削除",delegate { Remember(); draft.Favorites.Remove(entry.Item); RefreshCatalog(); Changed("お気に入りから削除しました。配置したアクションは変更していません。"); }); remove.Margin=new Thickness(4,0,0,0); DockPanel.SetDock(remove,Dock.Right); favoriteRow.Children.Add(remove); favoriteRow.Children.Add(row); items.Children.Add(favoriteRow);
                    } else items.Children.Add(row);
                } };
                string group=category; bool open=!String.IsNullOrWhiteSpace(search.Text) || expandedCategories.Contains(category); if(open) populate();
                Expander section=new Expander { Header=category,IsExpanded=open,FontSize=13,FontWeight=FontWeights.SemiBold,Background=EditorUi.Panel,BorderBrush=EditorUi.Line,BorderThickness=new Thickness(1),Padding=new Thickness(10),Margin=new Thickness(0,0,0,8),Content=items }; section.Expanded+=delegate { populate(); expandedCategories.Add(group); }; section.Collapsed+=delegate { expandedCategories.Remove(group); }; catalogRows.Children.Add(section);
            }
            catalogCount.Text=count+" 件 · 編集中の操作は実行されません";
            if(count==0) catalogRows.Children.Add(EditorUi.Text(catalogView.SelectedIndex==2 && !draft.Plugins.Any(p=>p.Enabled)?"歯車 → 起動・管理の「プラグイン」から定義ファイルを読み込んでください。":favorites && draft.Favorites.Count==0?"選んだ項目の「お気に入りに保存」から追加できます。":"見つかりませんでした。\n別の操作名やキーで検索してください。",13,EditorUi.Muted));
        }
        void SaveFavorite() {
            try {
                if(EditingAction.Kind=="none") throw new Exception("先にアクションを割り当ててください。");
                if(draft.Favorites.Count>=UserContent.MaxFavorites) throw new Exception("お気に入りは64件までです。");
                foreach(string key in UserContent.Hotkeys(draft)) Preferences.ValidateAction(EditingAction,Chord.Parse(key));
                Remember(); draft.Favorites.Add(UserContent.CopyAction(EditingAction)); catalogView.SelectedIndex=1; search.Text=""; expandedCategories.Add(ActionCatalog.CategoryFor(EditingAction)); ShowSide(0); Changed("お気に入りに保存しました。配置すると独立したコピーになります。");
            } catch(Exception ex) { Changed(ex.Message); }
        }
        Slider SizeRow(StackPanel panel,string label,double minimum,double maximum,double tick,out TextBlock number) {
            Grid row=new Grid { Margin=new Thickness(0,0,0,12) }; row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(94) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(54) });
            TextBlock name=EditorUi.Text(label,13,EditorUi.Ink); name.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(name);
            Slider slider=new Slider { Minimum=minimum, Maximum=maximum, TickFrequency=tick, SmallChange=tick, LargeChange=tick*2, IsSnapToTickEnabled=true, IsMoveToPointEnabled=true, VerticalAlignment=VerticalAlignment.Center, Margin=new Thickness(0,0,12,0) }; Grid.SetColumn(slider,1); row.Children.Add(slider);
            System.Windows.Automation.AutomationProperties.SetName(slider,label+"の大きさ");
            number=EditorUi.Text("100%",13,EditorUi.Mint); number.VerticalAlignment=VerticalAlignment.Center; number.TextAlignment=TextAlignment.Right; Grid.SetColumn(number,2); row.Children.Add(number); panel.Children.Add(row);
            slider.PreviewMouseLeftButtonDown+=delegate { sliderGesture=true; sliderHistory=false; };
            slider.PreviewMouseLeftButtonUp+=delegate { sliderGesture=false; sliderHistory=false; };
            slider.LostMouseCapture+=delegate { sliderGesture=false; sliderHistory=false; };
            return slider;
        }
        void ResizeCheckpoint() { if(!sliderGesture || !sliderHistory) { Remember(); sliderHistory=true; } }
        bool Save() {
            EndKeyCapture();
            try { owner.Apply(draft.Clone()); ClearWorkingCopy(); RefreshStartupStatus(); Changed("保存しました。次の呼び出しから反映されます。"); return true; }
            catch(Exception ex) { status.Foreground=EditorUi.Brush("#FFB2A2"); status.Text=ex.Message; return false; }
        }
        public void Export(string path) { RingWindow.ExportElement((FrameworkElement)Content,path,1268,780); }
    }
}

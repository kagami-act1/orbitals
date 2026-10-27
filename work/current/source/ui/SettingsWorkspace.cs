using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        int page,gestureIndex;
        readonly List<int> stackPath=new List<int>();
        Grid workspace,planetColumns,orbitColumns;
        FrameworkElement appsPage;
        StackPanel stackStrip,stackPanel;
        Button stackWrap;
        ComboBox stackMode;
        TextBlock stackModeHint;
        UIElement stackMoves,positionHelp;
        Button stackBack,navUndo,navRedo;
        TextBlock stackLocation;
        TextBlock pageHeading,pageDescription;
        readonly List<Button> navigation=new List<Button>();
        ListBox gestureList;
        GestureDrawing gesturePreview;
        ComboBox gestureType;
        CheckBox cleanDrawing;
        Button restoreDrawing,editGesture;
        bool editingGesture;
        ComboBox gestureModifier;
        TextBlock gestureName;
        bool updatingWorkspace;
        CheckBox startup;
        List<OrbitBinding> CurrentBindings { get { return ApplicationProfiles.Orbit(draft,editorApplication).Bindings; } }
        List<ActionItem> CurrentPlanetItems {
            get {
                List<ActionItem> items=EditorRootItems();
                foreach(int slot in stackPath) { if(slot<0 || slot>=items.Count || items[slot].Kind!="stack") { stackPath.Clear(); return EditorRootItems(); } items=items[slot].Children; }
                return items;
            }
        }
        int AdjacentSlot(int direction) {
            if(!draft.Appearance.EvenSpacing || EditingArc()) return (selected+CurrentPlanetItems.Count+direction)%CurrentPlanetItems.Count;
            int[] occupied=Enumerable.Range(0,CurrentPlanetItems.Count).Where(i=>CurrentPlanetItems[i].Kind!="none").ToArray(); int index=Array.IndexOf(occupied,selected);
            return index<0 || occupied.Length<2?selected:occupied[(index+occupied.Length+direction)%occupied.Length];
        }
        ActionItem EditingAction { get { if(page==1) return gestureIndex>=0 && gestureIndex<CurrentBindings.Count?CurrentBindings[gestureIndex].Action:PlanetStack.Empty(); var items=CurrentPlanetItems; selected=Math.Max(0,Math.Min(selected,items.Count-1)); return items[selected]; } }
        EditSnapshot Snapshot() { string text=Json.Write(draft); return new EditSnapshot { Config=Json.Read<Preferences>(text),EstimatedBytes=text.Length*2L,Selected=selected,Page=page,Planet=editorPlanet,StackPath=stackPath.ToArray(),Gesture=gestureIndex,Application=editorApplication }; }
        void RestoreNavigation(EditSnapshot snapshot) { page=snapshot.Page; editorApplication=snapshot.Application; editorPlanet=snapshot.Planet; gestureIndex=snapshot.Gesture; stackPath.Clear(); if(snapshot.StackPath!=null) stackPath.AddRange(snapshot.StackPath); }
        void BuildNavigation(DockPanel root) {
            StackPanel bar=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,18) }; DockPanel.SetDock(bar,Dock.Top); root.Children.Add(bar);
            string[] labels={"planet  リング","orbit  ジェスチャー","アプリ","変換"};
            int[] pages={0,1,4,5};
            for(int i=0;i<labels.Length;i++) { int index=pages[i]; Button button=EditorUi.Button(labels[i],delegate { Navigate(index); },false); button.Padding=new Thickness(18,10,18,10); navigation.Add(button); bar.Children.Add(button); }
            navUndo=IconButton("\uE7A7","元に戻す · Ctrl+Z",delegate { Undo(); }); navUndo.Margin=new Thickness(20,0,8,0); bar.Children.Add(navUndo);
            navRedo=IconButton("\uE7A6","やり直す · Ctrl+Y",delegate { Redo(); }); bar.Children.Add(navRedo);
            stackStrip=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,14) }; DockPanel.SetDock(stackStrip,Dock.Top); root.Children.Add(stackStrip);
            stackBack=EditorUi.Button("← 上の階層",delegate { LeaveStack(); },false); stackStrip.Children.Add(stackBack);
            stackLocation=EditorUi.Text("",13,EditorUi.Muted); stackLocation.VerticalAlignment=VerticalAlignment.Center; stackLocation.Margin=new Thickness(10,0,0,0); stackLocation.MaxWidth=620; stackLocation.TextTrimming=TextTrimming.CharacterEllipsis; stackStrip.Children.Add(stackLocation);
            BuildApplicationScope(root);
        }
        public void Navigate(int next) {
            if(updatePopup!=null) updatePopup.IsOpen=false;
            StopMacroRecording(true);
            EndKeyCapture(); page=next; if(page!=0) { stackPath.Clear(); selected=0; }
            RefreshAll(); ShowSide(0);
        }
        static void Detach(UIElement child) { Panel parent=VisualTreeHelper.GetParent(child) as Panel; if(parent!=null) parent.Children.Remove(child); }
        void UpdateWorkspace() {
            if(workspace==null) return;
            iconPanel.Visibility=Visibility.Collapsed; iconConversionChoice=null;
            if(page==2) page=4;
            updatingWorkspace=true;
            try {
                stackStrip.Visibility=page==0 && stackPath.Count>0?Visibility.Visible:Visibility.Collapsed;
                if(page==0) { List<string> names=new List<string> { UserContent.Name(draft,editorPlanet) }; List<ActionItem> items=EditorRootItems(); foreach(int slot in stackPath) { names.Add(items[slot].Label); items=items[slot].Children; } stackLocation.Text=String.Join(" / ",names); }
                string[] titles={"planet — リングの設定","orbit — ジェスチャーの設定","ブラックリスト","全体設定","アプリ別設定","ファイル変換"};
                string[] descriptions={"ドラッグで割り当て・並べ替え。スタックはダブルクリックで中を編集します。","動きを登録し、実行するアクションを選びます。","planetとorbitを使わないアプリを登録します。","操作・外観・起動と管理をまとめて設定します。","アクティブなアプリに合わせて、planetとorbitの内容を切り替えます。","入力の種類ごとに、変換・圧縮・展開の順番・表示・名前・アイコンを編集します。"};
                pageHeading.Text=titles[page]; pageDescription.Text=descriptions[page]; Title="orbitals — "+(page==0?"planet":page==1?"orbit":titles[page]);
                int[] pages={0,1,4,5}; for(int i=0;i<navigation.Count;i++) { bool active=pages[i]==page; navigation[i].Background=active?EditorUi.Brush("#355747"):EditorUi.Panel; navigation[i].BorderBrush=active?EditorUi.Mint:EditorUi.Line; }
                Detach(side); if(orbitColumns!=null) { workspace.Children.Remove(orbitColumns); orbitColumns=null; }
                if(appsPage!=null) { workspace.Children.Remove(appsPage); appsPage=null; }
                planetColumns.Visibility=page==0?Visibility.Visible:Visibility.Collapsed; settingsPanel.Visibility=page==3?Visibility.Visible:Visibility.Collapsed;
                if(page==0) { side.Visibility=Visibility.Visible; Grid.SetColumn(side,2); Grid.SetRow(side,0); planetColumns.Children.Add(side); }
                else if(page==1) { orbitColumns=BuildOrbitPage(); workspace.Children.Add(orbitColumns); }
                else if(page==4) { appsPage=BuildApplicationPage(); workspace.Children.Add(appsPage); }
                else if(page==5) { appsPage=BuildConversionPage(); workspace.Children.Add(appsPage); }
                if(page==3) BuildGeneralSettings();
                UpdatePlanetSelector();
                UpdateApplicationScope();
                if(startup!=null) startup.IsChecked=draft.StartWithWindows;
                navUndo.IsEnabled=history.Count>0; navRedo.IsEnabled=future.Count>0;
            } finally { updatingWorkspace=false; }
        }
        void EnterEditorStack(int index) {
            if(page!=0 || CurrentPlanetItems[index].Kind!="stack" || stackPath.Count>=PlanetStack.MaxDepth) return; stackPath.Add(index); selected=0; RefreshAll(); ShowSide(0);
        }
        void SetEditorContext(int[] path) { stackPath.Clear(); stackPath.AddRange(path); selected=Math.Max(0,Math.Min(selected,CurrentPlanetItems.Count-1)); }
        bool EditingArc() { List<ActionItem> items=EditorRootItems(); ActionItem parent=null; foreach(int slot in stackPath) { parent=items[slot]; items=parent.Children; } return parent!=null && PlanetStack.Mode(parent)=="arc"; }
        void LeaveStack() { if(stackPath.Count==0) return; selected=stackPath[stackPath.Count-1]; stackPath.RemoveAt(stackPath.Count-1); RefreshAll(); ShowSide(1); }
        void BuildStackEditor(StackPanel parent) {
            stackPanel=new StackPanel { Margin=new Thickness(0,0,0,14) }; parent.Children.Add(stackPanel);
            stackPanel.Children.Add(EditorUi.Text("開き方",12,EditorUi.Muted));
            stackMode=new ComboBox { ItemsSource=new[]{"次の階層へ切り替え","外側に弧状に展開"},Padding=new Thickness(8),Margin=new Thickness(0,8,0,6) }; stackPanel.Children.Add(stackMode);
            stackModeHint=EditorUi.Text("",12,EditorUi.Muted); stackModeHint.Margin=new Thickness(0,0,0,14); stackPanel.Children.Add(stackModeHint);
            stackMode.SelectionChanged+=delegate { if(syncing || EditingAction.Kind!="stack" || stackMode.SelectedIndex<0) return; string mode=stackMode.SelectedIndex==1?"arc":"level"; if(PlanetStack.Mode(EditingAction)==mode) return; if(mode=="level" && EditingAction.Children.Count(a=>a.Kind!="none")>8) { syncing=true; stackMode.SelectedIndex=1; syncing=false; Changed("階層型へ変更するには、アクションを8件以下にしてください。"); return; } Remember(); if(mode=="level") { var occupied=EditingAction.Children.Where(a=>a.Kind!="none").ToList(); while(occupied.Count<8) occupied.Add(PlanetStack.Empty()); EditingAction.Children=occupied; } EditingAction.StackMode=mode; RefreshStackModeHint(); RefreshSurface(); Changed("スタックの開き方を変更しました。「保存」で反映します。"); };
            stackPanel.Children.Add(EditorUi.Button("スタックの中を編集",delegate { EnterEditorStack(selected); },true));
            TextBlock note=EditorUi.Text("弧状は「＋」で追加でき、32件まで並べられます。8件を超えると外側の弧が増えます。階層型は8枠、入れ子は3階層まで。",12,EditorUi.Muted); note.Margin=new Thickness(0,8,0,0); stackPanel.Children.Add(note);
            Button wrap=EditorUi.Button("このアクションをスタックにまとめる",delegate { if(page!=0 || stackPath.Count>=PlanetStack.MaxDepth) return; Remember(); CurrentPlanetItems[selected]=PlanetStack.Create(EditingAction); RefreshAll(); Changed("アクションをスタックにまとめました。"); },false); wrap.Margin=new Thickness(0,0,0,14); parent.Children.Add(wrap);
            stackWrap=wrap;
        }
        void RefreshStackModeHint() { stackModeHint.Text=stackMode.SelectedIndex==1?"元のリングを残し、スタックの外側にアクションを並べます。":"リング全体を、スタック内のアクションに切り替えます。"; }
    }
}

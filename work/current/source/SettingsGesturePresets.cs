using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        DockPanel gesturePresetPanel,gestureAddPanel;
        StackPanel gesturePresetRows;
        TextBox gesturePresetSearch;
        ComboBox gesturePresetCategory,gesturePresetSource;
        readonly System.Collections.Generic.List<GesturePreset> gesturePresets=GesturePresets.Create();
        void BuildGestureAddPanel() {
            gestureAddPanel=new DockPanel(); side.Children.Add(gestureAddPanel);
            StackPanel content=new StackPanel(); gestureAddPanel.Children.Add(content);
            DockPanel heading=new DockPanel { Margin=new Thickness(0,0,0,16) }; content.Children.Add(heading);
            AddPanelClose(heading,"ジェスチャーの追加を閉じる",0);
            TextBlock title=EditorUi.Text("ジェスチャーを追加",20,EditorUi.Ink); title.FontWeight=FontWeights.SemiBold; title.VerticalAlignment=VerticalAlignment.Center; heading.Children.Add(title);
            Button preset=EditorUi.Button("プリセットから選ぶ",delegate { ShowSide(4); },true); preset.Margin=new Thickness(0,0,0,8); content.Children.Add(preset);
            TextBlock presetHelp=EditorUi.Text("用意された形やボタン操作を使います。",12,EditorUi.Muted); presetHelp.Margin=new Thickness(0,0,0,22); content.Children.Add(presetHelp);
            Button custom=EditorUi.Button("自作ジェスチャーを追加",delegate {
                if(CurrentBindings.Count>=128) { Changed("ジェスチャーは128件までです。"); return; }
                Remember(); CurrentBindings.Add(new OrbitBinding { Pattern="",Action=new ActionItem("新しいジェスチャー","none","") }); gestureIndex=CurrentBindings.Count-1;
                RefreshAll(); ShowSide(0); Changed("「編集」を押して形を描き、右側からアクションを選んでください。");
            },false); custom.Margin=new Thickness(0,0,0,8); content.Children.Add(custom);
            content.Children.Add(EditorUi.Text("自分で描いた形を登録します。",12,EditorUi.Muted));
        }
        void BuildGesturePresetPanel() {
            BuildGestureAddPanel();
            gesturePresetPanel=new DockPanel(); side.Children.Add(gesturePresetPanel);
            StackPanel heading=new StackPanel(); DockPanel.SetDock(heading,Dock.Top); gesturePresetPanel.Children.Add(heading);
            DockPanel row=new DockPanel { Margin=new Thickness(0,0,0,14) }; heading.Children.Add(row);
            AddPanelClose(row,"プリセットの選択を閉じる",0);
            TextBlock title=EditorUi.Text("ジェスチャーのプリセット",20,EditorUi.Ink); title.FontWeight=FontWeights.SemiBold; title.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(title);
            heading.Children.Add(EditorUi.Text("動きだけを追加します。実行内容は後から選びます。\n削除した動きも追加し直せます。",12,EditorUi.Muted));
            TextBlock searchLabel=EditorUi.Text("プリセットを検索",12,EditorUi.Muted); searchLabel.Margin=new Thickness(0,12,0,0); heading.Children.Add(searchLabel);
            gesturePresetSearch=EditorUi.Input(""); gesturePresetSearch.Margin=new Thickness(0,6,0,8); System.Windows.Automation.AutomationProperties.SetName(gesturePresetSearch,"プリセットを検索"); gesturePresetSearch.ToolTip="名前でプリセットを検索"; heading.Children.Add(gesturePresetSearch);
            Grid filters=new Grid(); filters.ColumnDefinitions.Add(new ColumnDefinition()); filters.ColumnDefinitions.Add(new ColumnDefinition()); heading.Children.Add(filters);
            gesturePresetCategory=new ComboBox { ItemsSource=new[]{"すべての動き"}.Concat(GesturePresets.Categories),SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,0,8,12) }; filters.Children.Add(gesturePresetCategory); System.Windows.Automation.AutomationProperties.SetName(gesturePresetCategory,"プリセットの動きの種類");
            gesturePresetSource=new ComboBox { ItemsSource=new[]{"すべての登録元","標準","自分のプリセット"},SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12) }; Grid.SetColumn(gesturePresetSource,1); filters.Children.Add(gesturePresetSource); System.Windows.Automation.AutomationProperties.SetName(gesturePresetSource,"プリセットの登録元");
            gesturePresetRows=new StackPanel(); gesturePresetPanel.Children.Add(new ScrollViewer { Content=gesturePresetRows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            gesturePresetSearch.TextChanged+=delegate { RefreshGesturePresets(); }; gesturePresetCategory.SelectionChanged+=delegate { RefreshGesturePresets(); }; gesturePresetSource.SelectionChanged+=delegate { RefreshGesturePresets(); };
        }
        void RefreshGesturePresets() {
            if(gesturePresetRows==null) return; gesturePresetRows.Children.Clear(); int count=0;
            string category=gesturePresetCategory.SelectedIndex<=0?null:(string)gesturePresetCategory.SelectedItem;
            string previous=null;
            foreach(GesturePreset preset in gesturePresets.Concat(draft.GestureLibrary.Select(g=>g.Preset())).Where(p=>(category==null || p.Category==category) && (gesturePresetSource.SelectedIndex==0 || p.Id.StartsWith("user-",StringComparison.Ordinal)==(gesturePresetSource.SelectedIndex==2)) && p.Matches(gesturePresetSearch.Text)).OrderBy(p=>Array.IndexOf(GesturePresets.Categories,p.Category))) {
                if(previous!=preset.Category) { TextBlock group=EditorUi.Text(preset.Category,12,EditorUi.Mint); group.Margin=new Thickness(0,10,0,8); gesturePresetRows.Children.Add(group); previous=preset.Category; }
                int existing=GesturePresets.Existing(CurrentBindings,preset); OrbitBinding binding=preset.Create();
                Grid card=new Grid(); card.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(76) }); card.ColumnDefinitions.Add(new ColumnDefinition());
                GestureDrawing preview=new GestureDrawing { Width=64,Height=44,TriggerButton=draft.Orbit.Button,VerticalAlignment=VerticalAlignment.Center }; preview.SetAppearance(draft.Appearance); preview.Bind(binding); card.Children.Add(preview);
                StackPanel labels=new StackPanel { Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center }; Grid.SetColumn(labels,1); card.Children.Add(labels);
                labels.Children.Add(EditorUi.Text(preset.Name+(binding.Modifier==""?"":" · "+binding.Modifier),13,EditorUi.Ink));
                labels.Children.Add(EditorUi.Text(existing>=0?"登録済み · 選択する":"＋ 動きを追加",11,existing>=0?EditorUi.Muted:EditorUi.Mint));
                GesturePreset selectedPreset=preset;
                Button button=EditorUi.Button("",delegate { AddGesturePreset(selectedPreset); },false); button.Content=card; button.Padding=new Thickness(8); button.Margin=new Thickness(0,0,8,8); button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
                button.IsEnabled=existing>=0 || CurrentBindings.Count<128;
                button.ToolTip=existing>=0?"同じ動きの登録を開きます。現在のアクションは変更しません。":"動きを追加します。実行するアクションは未割り当てです。";
                System.Windows.Automation.AutomationProperties.SetName(button,preset.Name+(existing>=0?"の登録を開く":"を追加")); preview.AnimateOnHover(button);
                if(preset.Id.StartsWith("user-",StringComparison.Ordinal)) {
                    string id=preset.Id.Substring(5); DockPanel customRow=new DockPanel(); Button remove=IconButton("\uE74D","自分のプリセットから削除",delegate { Remember(); draft.GestureLibrary.RemoveAll(g=>g.Id==id); RefreshGesturePresets(); Changed("プリセットから削除しました。登録したジェスチャーは変更していません。"); }); remove.Margin=new Thickness(0,0,0,8); DockPanel.SetDock(remove,Dock.Right); customRow.Children.Add(remove); customRow.Children.Add(button); gesturePresetRows.Children.Add(customRow);
                } else gesturePresetRows.Children.Add(button); count++;
            }
            if(count==0) gesturePresetRows.Children.Add(EditorUi.Text("該当するプリセットはありません。",12,EditorUi.Muted));
            if(CurrentBindings.Count>=128) gesturePresetRows.Children.Add(EditorUi.Text("登録は128件までです。追加する場合は、使わない登録を削除してください。",12,EditorUi.Muted));
        }
        void AddGesturePreset(GesturePreset preset) {
            if(page!=1) return; int existing=GesturePresets.Existing(CurrentBindings,preset);
            if(existing>=0) { gestureIndex=existing; UpdateGestureLabels(); RefreshGestureSelection(); Changed("同じ形の登録を開きました。アクションは変更していません。"); return; }
            if(CurrentBindings.Count>=128) { Changed("ジェスチャーは128件までです。"); return; }
            Remember(); gestureIndex=GesturePresets.Add(CurrentBindings,preset); RefreshAll(); ShowSide(0); Changed("「"+preset.Name+"」を追加しました。右側からアクションを選んでください。");
        }
    }
}

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        bool testingGesture;
        string testModifier="";
        Button gestureTestButton,gestureSaveButton;
        DockPanel gestureTestPanel,gestureSavePanel;
        StackPanel gestureTestResults;
        void BuildGestureTools() {
            gestureTestPanel=new DockPanel(); side.Children.Add(gestureTestPanel);
            StackPanel content=new StackPanel(); gestureTestPanel.Children.Add(content);
            content.Children.Add(EditorUi.Text("認識テスト",20,EditorUi.Ink));
            TextBlock help=EditorUi.Text("中央に左ドラッグで描いてください。実行するアクションと保存済みの形は変更しません。",12,EditorUi.Muted); help.Margin=new Thickness(0,12,0,16); content.Children.Add(help);
            gestureTestResults=new StackPanel(); content.Children.Add(gestureTestResults);
            gestureSavePanel=new DockPanel(); side.Children.Add(gestureSavePanel);
        }
        void ToggleGestureTest() {
            testingGesture=!testingGesture; editingGesture=false;
            if(testingGesture) { testModifier=""; gestureTestResults.Children.Clear(); }
            RefreshGestureSelection(); Changed(testingGesture?"認識テスト中です。中央に描いてください。実際の操作は実行しません。":"認識テストを終了しました。");
        }
        void EvaluateGesture() {
            GestureAnalysis result=GestureAnalysis.Evaluate(ApplicationProfiles.Orbit(draft,editorApplication),gesturePreview.LastStroke,testModifier); gestureTestResults.Children.Clear();
            string label=result.Match!=null?"一致："+result.Match.Action.Label:result.Ambiguous?"似た登録が複数あるため実行しません。":"一致する登録はありません。";
            TextBlock heading=EditorUi.Text(label,16,result.Match==null?EditorUi.Muted:EditorUi.Mint); heading.Margin=new Thickness(0,0,0,16); gestureTestResults.Children.Add(heading);
            foreach(GestureCandidate candidate in result.Candidates) {
                StackPanel row=new StackPanel(); row.Children.Add(EditorUi.Text(candidate.Binding.Action.Label,14,EditorUi.Ink));
                row.Children.Add(EditorUi.Text("形のずれ "+Math.Round(candidate.Distance*100,1)+"％ / 許容 "+candidate.Binding.Tolerance+"％"+(candidate.Distance>candidate.Binding.MatchLimit?"（閾値を超過）":""),12,EditorUi.Muted));
                GestureDrawing preview=new GestureDrawing { Height=44,Margin=new Thickness(0,6,0,0) }; preview.SetAppearance(draft.Appearance); preview.Bind(candidate.Binding); row.Children.Add(preview);
                Border card=Card(row); card.Padding=new Thickness(12); card.Margin=new Thickness(0,0,0,10); gestureTestResults.Children.Add(card);
            }
            ShowSide(7); Changed(label+"（テストのみ）");
        }
        void RefreshGestureSavePanel() {
            gestureSavePanel.Children.Clear();
            StackPanel content=new StackPanel(); gestureSavePanel.Children.Add(content);
            DockPanel heading=new DockPanel { Margin=new Thickness(0,0,0,16) }; content.Children.Add(heading);
            AddPanelClose(heading,"プリセットの保存を閉じる",1);
            heading.Children.Add(EditorUi.Text("動きを保存",20,EditorUi.Ink));
            if(gestureIndex<0 || gestureIndex>=CurrentBindings.Count) return;
            OrbitBinding binding=CurrentBindings[gestureIndex];
            if(binding.Shape==null && !OrbitPreferences.ValidPattern(binding.Pattern)) { content.Children.Add(EditorUi.Text("先に「編集」で動きを登録してください。",13,EditorUi.Muted)); return; }
            int number=1; while(draft.GestureLibrary.Any(g=>g.Name=="自分の動き"+number)) number++;
            content.Children.Add(EditorUi.Text("名前",12,EditorUi.Muted)); TextBox name=EditorUi.Input("自分の動き"+number); name.MaxLength=20; content.Children.Add(name);
            TextBlock help=EditorUi.Text("形と操作条件だけを保存します。実行するアクションは含めません。",12,EditorUi.Muted); help.Margin=new Thickness(0,12,0,18); content.Children.Add(help);
            content.Children.Add(EditorUi.Button("プリセットに保存",delegate {
                if(String.IsNullOrWhiteSpace(name.Text) || draft.GestureLibrary.Any(g=>String.Equals(g.Name,name.Text,StringComparison.OrdinalIgnoreCase))) { Changed("重複しない名前を入力してください。"); return; }
                if(draft.GestureLibrary.Count>=UserContent.MaxGestures) { Changed("自分のプリセットは64件までです。"); return; }
                Remember(); draft.GestureLibrary.Add(SavedGesture.From(binding,name.Text)); ShowSide(4); gesturePresetCategory.SelectedIndex=0; gesturePresetSource.SelectedIndex=2; RefreshGesturePresets(); Changed("動きだけを自分のプリセットに保存しました。");
            },true));
        }
    }
}

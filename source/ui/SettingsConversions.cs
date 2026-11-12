using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        DropKind conversionSource=DropKind.Image;
        int conversionSelected;
        FrameworkElement BuildConversionSettings() {
            StackPanel body=new StackPanel(); body.Children.Add(EditorUi.Text("ドラッグして変換",19,EditorUi.Ink));
            TextBlock help=EditorUi.Text("ファイル・フォルダーをつかんだまま呼び出しキーを押し、変換・圧縮・展開の上で離します。元ファイルと同じ場所に保存し、場所が分からないときだけ保存先を尋ねます。",13,EditorUi.Muted); help.Margin=new Thickness(0,12,0,10); body.Children.Add(help);
            WrapPanel buttons=new EditorButtonRow { Margin=new Thickness(0,14,0,0) }; body.Children.Add(buttons);
            buttons.Children.Add(EditorUi.Button("ファイル変換を編集",delegate { Navigate(5); },false)); buttons.Children.Add(EditorUi.Button("変換の履歴",delegate { owner.ShowConversionActivity(); },false));
            Button detail=EditorUi.Button("PDFの結合・ページ抽出など",delegate { owner.OpenConversionDetails(IntPtr.Zero); },false); detail.Margin=new Thickness(0,8,0,0); detail.HorizontalAlignment=HorizontalAlignment.Left; body.Children.Add(detail);
            Border card=Card(body); card.Margin=new Thickness(0,18,0,0); return card;
        }
        FrameworkElement BuildConversionPage() {
            Grid columns=new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(24) }); columns.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(350) });
            DockPanel left=new DockPanel(); columns.Children.Add(left);
            DockPanel top=new DockPanel { Margin=new Thickness(0,0,0,14) }; DockPanel.SetDock(top,Dock.Top); left.Children.Add(top);
            Button add=EditorUi.Button("＋ 変換先を追加",delegate { },false); add.Margin=new Thickness(12,0,0,0); DockPanel.SetDock(add,Dock.Right); top.Children.Add(add);
            ComboBox source=new ComboBox { ItemsSource=ConversionCatalog.Sources.Select(ConversionCatalog.SourceName).ToArray(),SelectedIndex=Array.IndexOf(ConversionCatalog.Sources,conversionSource),Padding=new Thickness(10),MinWidth=200 }; top.Children.Add(source);
            source.SelectionChanged+=delegate { if(source.SelectedIndex<0) return; conversionSource=ConversionCatalog.Sources[source.SelectedIndex]; conversionSelected=0; RefreshAll(); };
            TextBlock note=EditorUi.Text(conversionSource==DropKind.Files?"複数のファイル・フォルダーを、1つのZIPにまとめます。":conversionSource==DropKind.Zip?"元のZIPと同じ場所に、同名のフォルダーへ展開します。同名のものがあれば連番を付けます。":"有効な操作を円周に並べます。PDFの結合はPDFを2件以上つかんだときに表示します。GIFは静止画、ICOは16・32・48・256pxで出力します。",12,EditorUi.Muted); note.Margin=new Thickness(0,12,0,0); DockPanel.SetDock(note,Dock.Bottom); left.Children.Add(note);
            if(DocumentConversion.IsDocument(conversionSource)) note.Text=(conversionSource==DropKind.Spreadsheet?"CSV・TSVはシートごとに、UTF-8で保存します。":conversionSource==DropKind.Presentation?"PNG・JPEGはスライドごとに保存します。":"Word・ODT・RTFをPDF・TXTへ変換します。DOCXへの変換は、元がDOCXのときは表示しません。")+" "+(DocumentConversion.Available(conversionSource)?"このPCのOfficeアプリ、またはLibreOfficeを使います。":"変換には対応するOfficeアプリ、またはLibreOfficeが必要です。現在はZIPだけ利用できます。");
            List<ConversionChoice> choices=draft.Conversion.List(conversionSource); conversionSelected=Math.Max(0,Math.Min(choices.Count-1,conversionSelected));
            add.IsEnabled=choices.Count<ConversionPreferences.MaxChoices;
            add.Click+=delegate { ContextMenu menu=new ContextMenu(); foreach(string operation in ConversionCatalog.Operations(conversionSource)) { string value=operation; MenuItem item=new MenuItem { Header=ConversionCatalog.Label(value) }; item.Click+=delegate { AddConversion(value); }; menu.Items.Add(item); } add.ContextMenu=menu; menu.PlacementTarget=add; menu.IsOpen=true; };
            List<ConversionChoice> visible=choices.Where(c=>c.Enabled).ToList(); Canvas preview=null;
            Grid holder=new Grid { ClipToBounds=true }; left.Children.Add(holder);
            Action redraw=delegate {
                holder.Children.Clear(); preview=ConversionPlanets.Drawing(visible,draft.Appearance,1,choices.Count==0?-1:visible.IndexOf(choices[conversionSelected]));
                TextBlock centerTitle,centerDetail; Border center=RingSurface.EditorCenterBody(draft.Appearance,out centerTitle,out centerDetail); centerTitle.Text="変換"; centerDetail.Text="ドラッグで並べ替え"; ConversionPlanets.Put(preview,center,ConversionPlanets.Center-73,ConversionPlanets.Center-73);
                double radius=ConversionPlanets.RingRadius(visible.Count); Rect bounds=new Rect(ConversionPlanets.Center-radius,ConversionPlanets.Center-radius,radius*2,radius*2);
                Rect[] labels=(Rect[])preview.Tag; for(int i=0;i<labels.Length;i++) if(preview.Children[i*2+3].Visibility==Visibility.Visible) bounds.Union(labels[i]); bounds.Inflate(20,20);
                Canvas scene=new Canvas { Width=bounds.Width,Height=bounds.Height,ClipToBounds=true }; Canvas.SetLeft(preview,-bounds.Left); Canvas.SetTop(preview,-bounds.Top); scene.Children.Add(preview);
                Viewbox view=new Viewbox { Child=scene,Stretch=Stretch.Uniform,MaxWidth=860,MaxHeight=640 }; holder.Children.Add(view);
                Point start=new Point(); int dragging=-1;
                preview.PreviewMouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e) { start=e.GetPosition(preview); dragging=ConversionPlanets.Hit(start,preview); };
                preview.PreviewMouseMove+=delegate(object s,MouseEventArgs e) { if(dragging<0 || e.LeftButton!=MouseButtonState.Pressed || (e.GetPosition(preview)-start).Length<6) return; int index=choices.IndexOf(visible[dragging]); dragging=-1; DragDrop.DoDragDrop(preview,new DataObject("orbitals-conversion-choice",index),DragDropEffects.Move); };
                preview.PreviewMouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e) { int hit=ConversionPlanets.Hit(e.GetPosition(preview),preview); if(hit>=0) { conversionSelected=choices.IndexOf(visible[hit]); RefreshAll(); } };
                preview.PreviewMouseRightButtonUp+=delegate(object s,MouseButtonEventArgs e) { int hit=ConversionPlanets.Hit(e.GetPosition(preview),preview); if(hit<0) return; ConversionChoice entry=visible[hit]; ContextMenu menu=new ContextMenu(); MenuItem remove=new MenuItem { Header="消去" }; remove.Click+=delegate { RemoveConversion(choices,entry); }; menu.Items.Add(remove); menu.PlacementTarget=preview; menu.IsOpen=true; e.Handled=true; };
                preview.AllowDrop=true; preview.DragOver+=delegate(object s,DragEventArgs e) { e.Effects=e.Data.GetDataPresent("orbitals-conversion-choice")?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; };
                preview.Drop+=delegate(object s,DragEventArgs e) { e.Handled=true; if(!e.Data.GetDataPresent("orbitals-conversion-choice")) return; int hit=ConversionPlanets.Hit(e.GetPosition(preview),preview); if(hit<0) return; MoveConversion(choices,(int)e.Data.GetData("orbitals-conversion-choice"),choices.IndexOf(visible[hit])); };
            }; redraw();
            Grid right=new Grid(); right.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star),MaxHeight=180 }); right.RowDefinitions.Add(new RowDefinition { Height=new GridLength(3,GridUnitType.Star) }); Grid.SetColumn(right,2); columns.Children.Add(right);
            ListBox list=new ListBox { Background=EditorUi.Bg,Foreground=EditorUi.Ink,BorderBrush=EditorUi.Line,Padding=new Thickness(6),Margin=new Thickness(0,0,0,16) }; right.Children.Add(list);
            foreach(ConversionChoice entry in choices) list.Items.Add((entry.Enabled?"":"非表示 · ")+entry.Label); list.SelectedIndex=conversionSelected;
            list.SelectionChanged+=delegate { if(list.SelectedIndex>=0 && list.SelectedIndex!=conversionSelected) { conversionSelected=list.SelectedIndex; RefreshAll(); } };
            DockPanel detail=new DockPanel(); Grid.SetRow(detail,1); right.Children.Add(detail); StackPanel fields=new StackPanel { Margin=new Thickness(0,0,12,20) }; detail.Children.Add(new ScrollViewer { Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            if(choices.Count==0) { fields.Children.Add(EditorUi.Text("変換先はありません。「＋ 変換先を追加」から形式を選んでください。",14,EditorUi.Muted)); AddConversionReset(fields); return columns; }
            ConversionChoice choice=choices[conversionSelected]; fields.Children.Add(EditorUi.Text(ConversionCatalog.Label(choice.Operation)+"の設定",18,EditorUi.Ink));
            WrapPanel moves=new EditorButtonRow { Margin=new Thickness(0,12,0,8) }; fields.Children.Add(moves); moves.Children.Add(EditorUi.Button("← 前へ",delegate { MoveConversion(choices,conversionSelected,conversionSelected-1); },false)); moves.Children.Add(EditorUi.Button("次へ →",delegate { MoveConversion(choices,conversionSelected,conversionSelected+1); },false));
            CheckBox enabled=new CheckBox { Content="この形式を表示する",Foreground=EditorUi.Ink,IsChecked=choice.Enabled,Margin=new Thickness(0,8,0,14) }; fields.Children.Add(enabled);
            enabled.Click+=delegate { Remember(); choice.Enabled=enabled.IsChecked==true; RefreshAll(); Changed("変換先の表示を変更しました。「保存」で反映します。"); };
            fields.Children.Add(EditorUi.Text("表示名",12,EditorUi.Muted)); TextBox name=EditorUi.Input(choice.Label); name.MaxLength=32; name.Margin=new Thickness(0,6,0,12); fields.Children.Add(name);
            name.TextChanged+=delegate { if(choice.Label==name.Text) return; Remember(); choice.Label=name.Text; list.Items[conversionSelected]=(choice.Enabled?"":"非表示 · ")+choice.Label; list.SelectedIndex=conversionSelected; redraw(); Changed("ファイル変換の名前を変更しました。「保存」で反映します。"); };
            Button icon=EditorUi.Button("アイコン・文字列を変更",delegate { OpenConversionIconPicker(choice); },false); icon.Margin=new Thickness(0,0,0,16); fields.Children.Add(icon);
            if(choice.Operation.EndsWith("jpeg",StringComparison.Ordinal) || choice.Operation.EndsWith("webp",StringComparison.Ordinal)) ConversionNumber(fields,"画質（1〜100）",choice.Quality,1,100,delegate(int value) { choice.Quality=value; });
            if(FileConversion.ImageSizing(choice.Operation)) ConversionNumber(fields,"長辺（px・0は元の大きさ）",choice.MaxEdge,0,16000,delegate(int value) { choice.MaxEdge=value; });
            if(FileConversion.PdfRaster(choice.Operation) || DocumentConversion.SlideImages(choice.Operation)) ConversionNumber(fields,"画像化の解像度（72〜300 dpi）",choice.Dpi,72,300,delegate(int value) { choice.Dpi=value; });
            if(FileConversion.Ocr(choice.Operation)) fields.Children.Add(EditorUi.Text("Windowsの表示言語で文字を認識します。"+(FileConversion.CopiesText(choice.Operation)?"ファイルは保存せず、認識した文字をコピーします。":"認識した文字をUTF-8のTXTとして保存します。"),12,EditorUi.Muted));
            Button removeChoice=EditorUi.Button("この変換先を削除",delegate { RemoveConversion(choices,choice); },false); removeChoice.Margin=new Thickness(0,8,0,0); fields.Children.Add(removeChoice);
            AddConversionReset(fields); return columns;
        }
        void AddConversion(string operation) { List<ConversionChoice> choices=draft.Conversion.List(conversionSource); if(choices.Count>=ConversionPreferences.MaxChoices || !ConversionCatalog.Operations(conversionSource).Contains(operation)) return; Remember(); choices.Add(new ConversionChoice { Operation=operation,Label=ConversionCatalog.Label(operation) }); conversionSelected=choices.Count-1; RefreshAll(); Changed("変換先を追加しました。「保存」で反映します。"); }
        void RemoveConversion(List<ConversionChoice> choices,ConversionChoice choice) { if(!choices.Contains(choice)) return; Remember(); choices.Remove(choice); conversionSelected=Math.Max(0,Math.Min(conversionSelected,choices.Count-1)); RefreshAll(); Changed("変換先を削除しました。「元に戻す」で戻せます。"); }
        void AddConversionReset(StackPanel fields) { Button reset=EditorUi.Button("この種類の並び・設定を標準に戻す",delegate { Remember(); draft.Conversion.Reset(conversionSource); conversionSelected=0; RefreshAll(); Changed("ファイル変換を標準に戻しました。"); },false); reset.Margin=new Thickness(0,18,0,0); fields.Children.Add(reset); }
        void MoveConversion(List<ConversionChoice> choices,int from,int to) { if(from<0 || from>=choices.Count || to<0 || to>=choices.Count || from==to) return; Remember(); ConversionChoice item=choices[from]; choices.RemoveAt(from); choices.Insert(to,item); conversionSelected=to; RefreshAll(); Changed("ファイル変換を並べ替えました。「保存」で反映します。"); }
        void ConversionNumber(StackPanel fields,string label,int value,int minimum,int maximum,Action<int> set) {
            fields.Children.Add(EditorUi.Text(label,12,EditorUi.Muted)); TextBox input=EditorUi.Input(value.ToString()); input.MaxLength=5; input.Margin=new Thickness(0,6,0,12); fields.Children.Add(input); int accepted=value;
            input.TextChanged+=delegate { int number; if(!Int32.TryParse(input.Text,out number) || number<minimum || number>maximum || number==accepted) return; Remember(); accepted=number; set(number); Changed("変換の設定を変更しました。「保存」で反映します。"); };
            input.LostKeyboardFocus+=delegate { input.Text=accepted.ToString(); };
        }
    }
}

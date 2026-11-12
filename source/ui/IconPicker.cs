using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class IconPicker : DockPanel {
        const int PageSize=48;
        readonly TextBox search,textValue;
        readonly ComboBox categories,displayMode;
        readonly UniformGrid tiles;
        readonly ScrollViewer scroll;
        readonly TextBlock count,currentLabel;
        readonly ContentControl currentVisual,textPreview;
        readonly Button previous,next,automatic,useText;
        readonly FrameworkElement imageTools,textTools,browser,textScene;
        readonly DispatcherTimer debounce;
        List<MaterialIcon> matches;
        ActionItem current;
        int page;
        bool ready;
        public Action<string> Selected;
        public IconPicker() {
            ClipToBounds=true;
            Grid layout=new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(240) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(24) }); layout.ColumnDefinitions.Add(new ColumnDefinition()); Children.Add(layout);
            StackPanel options=new StackPanel { Margin=new Thickness(0,0,0,12) }; layout.Children.Add(new ScrollViewer { Content=options,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,10,0) });
            currentVisual=new ContentControl { Height=64,HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,4,0,10) }; options.Children.Add(currentVisual);
            currentLabel=Ui.Text("",12,Ui.Muted); currentLabel.Margin=new Thickness(0,0,0,20); options.Children.Add(currentLabel);
            options.Children.Add(Ui.Text("表示するもの",12,Ui.Muted));
            displayMode=new ComboBox { ItemsSource=new[]{"アイコン","文字列"},SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,6,0,18) }; System.Windows.Automation.AutomationProperties.SetName(displayMode,"アイコンまたは文字列"); options.Children.Add(displayMode);
            StackPanel images=new StackPanel(); imageTools=images; options.Children.Add(images);
            automatic=Ui.Button("操作に合わせて自動選択",delegate { if(Selected!=null) Selected(null); },false); automatic.Margin=new Thickness(0,0,0,12); images.Children.Add(automatic);
            Button import=Ui.Button("画像ファイルから選ぶ…",delegate { ImportImage(); },false); import.Margin=new Thickness(0,0,0,6); images.Children.Add(import); images.Children.Add(Ui.Text("PNG / JPEG / ICO / BMP / GIF（静止画）",11,Ui.Muted));
            StackPanel text=new StackPanel(); textTools=text; options.Children.Add(text); text.Children.Add(Ui.Text("表示する文字列（1〜16文字）",12,Ui.Muted));
            textValue=Ui.Input(""); textValue.MaxLength=16; textValue.Margin=new Thickness(0,6,0,12); System.Windows.Automation.AutomationProperties.SetName(textValue,"アイコンの代わりに表示する文字列"); text.Children.Add(textValue);
            useText=Ui.Button("この文字列を使う",delegate { if(Selected!=null) Selected(ActionIcons.TextKey(textValue.Text)); },true); useText.Margin=new Thickness(0,0,0,12); text.Children.Add(useText); text.Children.Add(Ui.Text("アクションの名前や実行内容は変わりません。短い文字ほど読みやすく表示できます。",12,Ui.Muted));
            DockPanel catalog=new DockPanel(); browser=catalog; Grid.SetColumn(catalog,2); layout.Children.Add(catalog);
            Grid filters=new Grid { Margin=new Thickness(0,0,0,10) }; filters.ColumnDefinitions.Add(new ColumnDefinition()); filters.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(16) }); filters.ColumnDefinitions.Add(new ColumnDefinition()); SetDock(filters,Dock.Top); catalog.Children.Add(filters);
            StackPanel searchField=new StackPanel(); filters.Children.Add(searchField); searchField.Children.Add(Ui.Text("名前・用途で検索（日本語 / English）",12,Ui.Muted));
            Grid searchRow=new Grid { Margin=new Thickness(0,6,0,0) }; searchRow.ColumnDefinitions.Add(new ColumnDefinition()); searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(36) }); searchField.Children.Add(searchRow);
            search=Ui.Input(""); search.MaxLength=100; searchRow.Children.Add(search); System.Windows.Automation.AutomationProperties.SetName(search,"アイコンを検索");
            Button clear=Ui.Button("×",delegate { search.Clear(); search.Focus(); },false); clear.ToolTip="検索をクリア"; clear.Padding=new Thickness(0); clear.Margin=new Thickness(5,0,0,0); Grid.SetColumn(clear,1); searchRow.Children.Add(clear);
            StackPanel categoryField=new StackPanel(); Grid.SetColumn(categoryField,2); filters.Children.Add(categoryField); categoryField.Children.Add(Ui.Text("カテゴリ",12,Ui.Muted));
            categories=new ComboBox { Padding=new Thickness(8),FontSize=12,MaxDropDownHeight=420,Margin=new Thickness(0,6,0,0) }; categories.Items.Add(new IconCategory("","すべてのカテゴリ") { Count=MaterialIcons.All.Count }); foreach(IconCategory category in MaterialIcons.Categories) categories.Items.Add(category); categories.SelectedIndex=0; categoryField.Children.Add(categories); System.Windows.Automation.AutomationProperties.SetName(categories,"アイコンのカテゴリ");
            count=Ui.Text("",11,Ui.Muted); count.Margin=new Thickness(0,0,0,8); SetDock(count,Dock.Top); catalog.Children.Add(count);
            Grid pages=new Grid { Margin=new Thickness(0,12,0,0) }; pages.ColumnDefinitions.Add(new ColumnDefinition()); pages.ColumnDefinitions.Add(new ColumnDefinition()); SetDock(pages,Dock.Bottom); catalog.Children.Add(pages);
            previous=Ui.Button("← 前のページ",delegate { page--; Render(true); },false); previous.Margin=new Thickness(0,0,6,0); pages.Children.Add(previous); next=Ui.Button("次のページ →",delegate { page++; Render(true); },false); next.Margin=new Thickness(6,0,0,0); Grid.SetColumn(next,1); pages.Children.Add(next);
            tiles=new UniformGrid { Columns=6 }; scroll=new ScrollViewer { Content=tiles,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Padding=new Thickness(0,0,10,0) }; catalog.Children.Add(scroll); scroll.SizeChanged+=delegate { tiles.Columns=Math.Max(3,Math.Min(10,(int)(scroll.ActualWidth-24)/88)); };
            textPreview=new ContentControl { HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center }; Border textFrame=new Border { Background=Ui.Bg,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=textPreview }; textScene=textFrame; Grid.SetColumn(textFrame,2); layout.Children.Add(textFrame);
            debounce=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(140) }; debounce.Tick+=delegate { debounce.Stop(); Filter(); }; search.TextChanged+=delegate { if(ready) { debounce.Stop(); debounce.Start(); } }; categories.SelectionChanged+=delegate { if(ready) { debounce.Stop(); Filter(); } }; displayMode.SelectionChanged+=delegate { if(ready) UpdateMode(); }; textValue.TextChanged+=delegate { if(ready) UpdateTextPreview(); }; Unloaded+=delegate { debounce.Stop(); }; ready=true; UpdateMode(); Filter();
        }
        public void ReleaseResources() { debounce.Stop(); matches=null; current=null; tiles.Children.Clear(); currentVisual.Content=null; textPreview.Content=null; }
        public void Bind(ActionItem item) { current=item.Clone(); currentVisual.Content=ActionIcons.Visual(item,40); currentLabel.Text=ActionIcons.Label(item.Icon); automatic.BorderBrush=String.IsNullOrEmpty(item.Icon)?Ui.Mint:Ui.Line; displayMode.SelectedIndex=ActionIcons.IsText(item.Icon)?1:0; textValue.Text=ActionIcons.Text(item.Icon)??(item.Label??"").Substring(0,Math.Min(16,(item.Label??"").Length)); UpdateMode(); Render(); }
        public void Open(ActionItem item) { debounce.Stop(); Bind(item); Filter(); }
        void UpdateMode() { bool text=displayMode.SelectedIndex==1; imageTools.Visibility=browser.Visibility=text?Visibility.Collapsed:Visibility.Visible; textTools.Visibility=textScene.Visibility=text?Visibility.Visible:Visibility.Collapsed; UpdateTextPreview(); }
        void UpdateTextPreview() { useText.IsEnabled=!String.IsNullOrWhiteSpace(textValue.Text) && !textValue.Text.Any(Char.IsControl); textPreview.Content=RingSurface.Bubble(new ActionItem(textValue.Text,"none","") { Icon="text:"+textValue.Text },0,110); }
        void ImportImage() { Window owner=Window.GetWindow(this); try { Microsoft.Win32.OpenFileDialog dialog=new Microsoft.Win32.OpenFileDialog { Title="アイコンにする画像を選ぶ",Filter="画像ファイル (*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.gif",CheckFileExists=true,Multiselect=false }; if(dialog.ShowDialog(owner)!=true) return; string key=CustomIcons.Import(dialog.FileName); if(Selected!=null) Selected(key); } catch(Exception ex) { OrbitDialog.Info(owner,"画像を読み込めませんでした",ex.Message); } }
        void Filter() { IconCategory category=categories.SelectedItem as IconCategory; matches=MaterialIcons.Search(search.Text,category==null?"":category.Id); page=0; Render(true); }
        void Render(bool resetScroll=false) {
            if(matches==null) return; double offset=scroll.VerticalOffset; page=Math.Max(0,Math.Min(page,Math.Max(0,(matches.Count-1)/PageSize))); tiles.Children.Clear(); int start=page*PageSize,end=Math.Min(start+PageSize,matches.Count); count.Text=matches.Count==0?"見つかりません。検索語やカテゴリを変えてください。":(start+1)+"–"+end+" / "+matches.Count+" 個";
            for(int i=start;i<end;i++) { MaterialIcon icon=matches[i]; Button button=Ui.Button("",delegate { if(Selected!=null) Selected(icon.Key); },false); button.Height=80; button.Padding=new Thickness(4,8,4,6); button.Margin=new Thickness(3); StackPanel body=new StackPanel(); FrameworkElement visual=icon.Visual(30); visual.HorizontalAlignment=HorizontalAlignment.Center; body.Children.Add(visual); body.Children.Add(new TextBlock { Text=icon.Label,FontSize=11,Margin=new Thickness(0,7,0,0),TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=80,TextAlignment=TextAlignment.Center }); button.Content=body; button.ToolTip=icon.Label+"\n"+icon.Name; System.Windows.Automation.AutomationProperties.SetName(button,icon.Label+" · "+icon.Name); if(current!=null && current.Icon==icon.Key) { button.Background=Ui.Brush("#355747"); button.BorderBrush=Ui.Mint; } tiles.Children.Add(button); }
            previous.IsEnabled=page>0; next.IsEnabled=end<matches.Count; scroll.ScrollToVerticalOffset(resetScroll?0:offset);
        }
    }
}

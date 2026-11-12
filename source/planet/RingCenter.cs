using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OrbitRing {
    sealed class RingCenter : Border {
        readonly ContentControl symbol=new ContentControl { HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Focusable=false,IsHitTestVisible=false };
        readonly StackPanel pages=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,18),IsHitTestVisible=false };
        readonly Brush accent;
        string symbolKey;
        int pageIndex=-1,pageCount=-1;
        public RingCenter(AppearancePreferences appearance) {
            Width=146; Height=146; CornerRadius=new CornerRadius(73); Background=Ui.Bg; BorderBrush=Ui.Line; BorderThickness=new Thickness(1); accent=appearance.Accent;
            Grid body=new Grid(); body.Children.Add(symbol); body.Children.Add(pages); Child=body;
            SetPages(0,0); ShowCancel("");
        }
        void ShowSymbol(ActionItem item,string key,bool textOnly) {
            string displayKey=(textOnly?"caption:":"icon:")+key;
            if(symbolKey==displayKey) return;
            symbolKey=displayKey;
            if(textOnly) symbol.Content=new TextBlock { Text=key,FontFamily=new FontFamily("Segoe UI, Yu Gothic UI"),FontSize=32,FontWeight=FontWeights.Medium,Foreground=Ui.Ink,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=112,MaxHeight=80,VerticalAlignment=VerticalAlignment.Center };
            else symbol.Content=ActionIcons.Visual(item??new ActionItem("","none","") { Icon=key },44);
        }
        void Describe(string text) { AutomationProperties.SetName(this,text); ToolTip=text; }
        public void ShowCancel(string context) { ShowSymbol(null,"ms:close",false); Describe((String.IsNullOrEmpty(context)?"":context+" · ")+"クリックで閉じる"); }
        public void ShowBack(string name) { ShowSymbol(null,"ms:arrow_back",false); Describe("上の階層へ戻る"+(String.IsNullOrEmpty(name)?"":" · "+name)); }
        public void ShowAction(ActionItem item,bool textOnly=false) { bool text=ActionIcons.UsesText(item,textOnly); ShowSymbol(item,text?ActionIcons.Caption(item):ActionIcons.EffectiveKey(item),text); Describe(item.Label); }
        public void ShowUnavailable(string message) { ShowSymbol(null,"ms:block",false); Describe(message); }
        public void SetPages(int index,int count) {
            if(count<2 || index<0 || index>=count || count>UserContent.MaxPlanets) { index=0; count=0; }
            if(index==pageIndex && count==pageCount) return;
            pageIndex=index; pageCount=count; pages.Children.Clear(); pages.Visibility=count==0?Visibility.Collapsed:Visibility.Visible; symbol.Margin=new Thickness(0,0,0,count==0?0:8);
            for(int i=0;i<count;i++) pages.Children.Add(new Ellipse { Width=i==index?8:5,Height=i==index?8:5,Fill=i==index?accent:Ui.Muted,Opacity=i==index?1:.5,Margin=new Thickness(4,0,4,0),VerticalAlignment=VerticalAlignment.Center });
        }
    }
}

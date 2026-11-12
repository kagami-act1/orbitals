using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace OrbitRing {
    static class RingCenterTests {
        static void Check(bool value,string message,List<string> results) { if(!value) throw new Exception(message); results.Add("PASS "+message); }
        static RingCenter Center(RingSurface surface) { return surface.Children.OfType<RingCenter>().Single(); }
        static FrameworkElement Symbol(RingCenter center) { return (FrameworkElement)((Grid)center.Child).Children.OfType<ContentControl>().Single().Content; }
        static bool Icon(RingCenter center,string key) { Image expected=ActionIcons.Visual(new ActionItem("","none","") { Icon=key },44) as Image,actual=Symbol(center) as Image; return actual!=null && expected!=null && Object.ReferenceEquals(actual.Source,expected.Source); }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences config=Preferences.Defaults(); string original=Json.Write(config);
            RingSurface ring=new RingSurface(); ring.Bind(config); ring.SetPlanet("メイン","2 / 3",delegate(int step) { },1,3); RingCenter center=Center(ring);
            Check(Icon(center,"ms:close") && !((Grid)center.Child).Children.OfType<TextBlock>().Any() && Symbol(center).Width==44,"idle ring shows a large close symbol without explanatory text",result);
            StackPanel pages=((Grid)center.Child).Children.OfType<StackPanel>().Single();
            Check(pages.Children.Count==3 && ((FrameworkElement)pages.Children[1]).Width==8 && AutomationProperties.GetName(center).Contains("2 / 3"),"page dots highlight the current planet while its full identity stays accessible",result);
            Canvas input=new Canvas(); input.Children.Add(ring); ring.AttachPointerArea(input); Point[] positions=Enumerable.Range(0,8).Select(ring.IconPosition).ToArray();
            foreach(double scale in new[]{.375,.75,1.5}) {
                ring.SetScale(scale); input.Measure(new Size(900,900)); input.Arrange(new Rect(0,0,900,900)); input.UpdateLayout(); Size size=center.RenderSize; Point location=center.TranslatePoint(new Point(),input);
                ring.UpdatePointer(positions[0]);
                Check(Icon(center,ActionIcons.EffectiveKey(config.Items[0])) && AutomationProperties.GetName(center)=="コピー" && center.RenderSize==size && center.TranslatePoint(new Point(),input)==location && Enumerable.Range(0,8).All(i=>ring.IconPosition(i)==positions[i] && ring.IconSlot(positions[i])==i),"selection changes only the center symbol and keeps all targets fixed at scale "+scale,result);
                ring.UpdatePointer(new Point(205,205));
                Check(Icon(center,"ms:close") && RingSurface.ActionSlot(new Point(205,125.01))==-1 && RingSurface.ActionSlot(new Point(205,125))==0,"the cancel area remains unchanged at scale "+scale,result);
            }
            ring.StackTitle="編集"; ring.UpdatePointer(new Point(205,205));
            Check(Icon(center,"ms:arrow_back") && pages.Visibility==Visibility.Collapsed && AutomationProperties.GetName(center).Contains("上の階層へ戻る"),"stack navigation replaces the cancel symbol with a back arrow and hides page dots",result);
            ActionItem text=new ActionItem("貼り付け","shortcut","Ctrl+V") { Icon="text:PASTE" }; center.ShowAction(text);
            Check(Symbol(center) is TextBlock && ((TextBlock)Symbol(center)).Text=="PASTE" && ((TextBlock)Symbol(center)).FontSize*.375>=12,"explicit text icons retain readable lettering without adding small secondary text",result);
            center.ShowUnavailable("対応していないファイルです。"); Check(Icon(center,"ms:block") && AutomationProperties.GetName(center).Contains("対応していない"),"unavailable conversions use an identifiable symbol and retain the reason accessibly",result);
            foreach(double scale in new[]{.375,.75,1.5}) {
                var choices=ConversionCatalog.Defaults(false).Take(8).ToList(); Canvas drawing=ConversionPlanets.Drawing(choices,config.Appearance,scale,0,false,config.ButtonSize);
                Button caption=(Button)drawing.Children[3]; Rect bounds=((Rect[])drawing.Tag)[0];
                Check(caption.Visibility==Visibility.Visible && Math.Abs(((TextBlock)caption.Content).FontSize*scale-14)<.001 && ConversionPlanets.Hit(ConversionPlanets.Position(0,8),drawing)==0 && ConversionPlanets.Hit(new Point(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2),drawing)==0 && ConversionPlanets.Hit(new Point(ConversionPlanets.Center,ConversionPlanets.Center),drawing)==-1,"conversion captions stay readable outside the ring and preserve drop targets at scale "+scale,result);
            }
            Check(Json.Write(config)==original,"ring presentation changes leave action gesture and appearance settings untouched",result);
            return result;
        }
    }
}

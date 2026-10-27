using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class StackSpacingPreviewTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static T Field<T>(StackSpacingPreview preview,string name) { return (T)typeof(StackSpacingPreview).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(preview); }
        static double Radius(RingSurface surface,int index) { return (surface.IconPosition(index)-new Point(205,205)).Length; }
        public static List<string> Run() {
            List<string> r=new List<string>(); Preferences config=Preferences.Defaults(); config.ButtonSize=90;
            ActionItem nested=PlanetStack.Create(new ActionItem("内側の操作","shortcut","F7")); nested.StackMode="arc";
            ActionItem stack=PlanetStack.Create(nested); stack.StackMode="arc"; stack.Children[1]=new ActionItem("別の操作","shortcut","F8"); config.Items[2]=stack;
            string original=Json.Write(config.Items);
            StackSpacingPreview preview=new StackSpacingPreview(config.Appearance,config.ButtonSize); RingSurface surface=Field<RingSurface>(preview,"surface"); Canvas scene=Field<Canvas>(preview,"scene");
            Size frame=new Size(scene.Width,scene.Height); Point offset=new Point(Canvas.GetLeft(surface),Canvas.GetTop(surface)); Point root=surface.IconPosition(2);
            double low=0; bool stable=true;
            foreach(double gap in new[]{8.0,50.0,94.0,160.0}) {
                config.Appearance.StackGap=gap; preview.Update(config.Appearance);
                stable &= new Size(scene.Width,scene.Height)==frame && new Point(Canvas.GetLeft(surface),Canvas.GetTop(surface))==offset && surface.IconPosition(2)==root;
                Check(Math.Abs(Radius(surface,8)-(189+gap+50.5))<.00001,"spacing preview matches runtime geometry with the actual button size: "+gap,r);
                if(gap==8) low=Radius(surface,8);
            }
            Check(stable && Math.Abs(Radius(surface,8)-low-152)<.00001,"changing spacing moves the arc without moving the root or changing preview framing",r);
            Check(Json.Write(config.Items)==original,"the live spacing preview never rewrites the configured action tree",r);
            Check(surface.Children.OfType<Button>().Count()==11 && !preview.Children.OfType<ComboBox>().Any(),"the demo always shows one three-action arc without nested stacks or a selector",r);
            Preferences empty=Preferences.Defaults(); original=Json.Write(empty); preview=new StackSpacingPreview(empty.Appearance); surface=Field<RingSurface>(preview,"surface");
            Check(Radius(surface,8)>200 && Json.Write(empty)==original,"a sample arc is available without adding actions to settings",r);
            preview.Update(empty.Appearance); Check(Json.Write(empty)==original,"refreshing the sample leaves user settings untouched",r);
            empty.Appearance.RingAccent="#8DBEFF"; empty.Appearance.RingOpacity=.55; preview.Update(empty.Appearance);
            Check(surface.Opacity==.55,"the shared preview applies ring appearance to the same expanded stack",r);
            preview.PlayReveal(); scene=Field<Canvas>(preview,"scene");
            PlanetReveal reveal=System.Linq.Enumerable.Single(System.Linq.Enumerable.OfType<PlanetReveal>(scene.Children));
            Check(reveal.Running && Canvas.GetLeft(reveal)==Canvas.GetLeft(surface) && Canvas.GetTop(reveal)==Canvas.GetTop(surface) && surface.Opacity<.55,"opening animation uses the shared ring and stack preview with matching placement",r); reveal.Cancel();
            return r;
        }
    }
}

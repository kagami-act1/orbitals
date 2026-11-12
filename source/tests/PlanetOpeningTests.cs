using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OrbitRing {
    static class PlanetOpeningTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static Color ColorOf(Brush brush) { return ((SolidColorBrush)brush).Color; }
        public static List<string> Run() {
            List<string> r=new List<string>();
            foreach(bool arc in new[]{false,true}) {
                Preferences config=Preferences.Defaults(); config.Appearance.RingOpacity=.6;
                PlanetMenu menu=null;
                if(arc) {
                    config.Items[2]=PlanetStack.Create(new ActionItem("子の操作","shortcut","F6")); config.Items[2].StackMode="arc";
                    menu=new PlanetMenu(config.Items,config.ButtonSize); menu.Open(2); config.Items=menu.Targets.Select(t=>t.Item.Clone()).ToList();
                }
                string saved=Json.Write(config); RingSurface surface=new RingSurface(); surface.Bind(config,menu); surface.SetScale(.75);
                Canvas host=new Canvas(); host.Children.Add(surface);
                Button[] buttons=surface.Children.OfType<Button>().ToArray();
                Point[] points=Enumerable.Range(0,buttons.Length).Select(surface.IconPosition).ToArray();
                Rect[] bounds=buttons.Select(b=>new Rect(Canvas.GetLeft(b),Canvas.GetTop(b),b.Width,b.Height)).ToArray();
                Matrix layout=surface.LayoutTransform.Value,render=surface.RenderTransform.Value;
                Ellipse plate=surface.Children.OfType<Ellipse>().First(); Color plateColor=ColorOf(plate.Fill),buttonColor=ColorOf(buttons[0].Background);
                Brush material=buttons[0].Background; int count=surface.Children.Count;
                PlanetReveal reveal=PlanetReveal.Play(host,new Point(),.75,config.Appearance.Accent,surface);
                Check(Math.Abs(surface.Opacity-.432)<.00001 && surface.Visibility==Visibility.Visible,"opening keeps the body visible from its first frame: "+arc,r);
                reveal.Advance(TimeSpan.Zero);
                bool stable=true;
                foreach(int time in new[]{0,35,90,180,300,410}) {
                    reveal.Advance(TimeSpan.FromMilliseconds(time));
                    stable &= surface.Width==410 && surface.Height==410 && surface.LayoutTransform.Value==layout && surface.RenderTransform.Value==render && surface.Children.Count==count;
                    for(int i=0;i<buttons.Length;i++) stable &= surface.IconPosition(i)==points[i] && new Rect(Canvas.GetLeft(buttons[i]),Canvas.GetTop(buttons[i]),buttons[i].Width,buttons[i].Height)==bounds[i] && surface.IconSlot(points[i])==i;
                }
                Check(stable,"body animation preserves all positions sizes transforms and icon hit targets: "+arc,r);
                Check(Object.ReferenceEquals(buttons[0].Background,material) && ColorOf(plate.Fill)!=plateColor,"body lighting updates existing brushes without rebuilding controls: "+arc,r);
                surface.UpdatePointer(points[1]); Color selected=ColorOf(buttons[1].Background),edge=ColorOf(buttons[1].BorderBrush);
                surface.SetOpeningProgress(.4);
                Check(ColorOf(buttons[1].Background)==selected && ColorOf(buttons[1].BorderBrush)==edge && surface.IconSlot(points[1])==1,"hover feedback remains immediate during the body animation: "+arc,r);
                reveal.Cancel();
                Check(surface.Opacity==.6 && ColorOf(plate.Fill)==plateColor && ColorOf(buttons[0].Background)==buttonColor && host.Children.Count==1,"cancel restores configured opacity and material colors without removing the body: "+arc,r);
                reveal=PlanetReveal.Play(host,new Point(),.75,config.Appearance.Accent,surface); reveal.Advance(TimeSpan.Zero); reveal.Advance(TimeSpan.FromMilliseconds(PlanetReveal.DurationMilliseconds+1));
                Check(!reveal.Running && surface.Opacity==.6 && Json.Write(config)==saved,"completion restores the body and leaves saved settings unchanged: "+arc,r);
                PlanetReveal first=PlanetReveal.Play(host,new Point(),.75,config.Appearance.Accent,surface),second=PlanetReveal.Play(host,new Point(),.75,config.Appearance.Accent,surface); second.Cancel();
                Check(!first.Running && !second.Running && surface.Opacity==.6 && host.Children.Count==1,"repeated previews release the earlier animation and restore body appearance: "+arc,r);
            }
            Preferences disabled=Preferences.Defaults(); disabled.Appearance.RingAnimation=false; RingSurface unchanged=new RingSurface(); unchanged.Bind(disabled);
            Check(unchanged.Opacity==disabled.Appearance.RingOpacity,"a surface without an opening animation starts at its configured appearance",r);
            foreach(int direction in new[]{-1,1}) {
                Preferences config=Preferences.Defaults(); RingSurface surface=new RingSurface(); surface.Bind(config); Canvas host=new Canvas(); host.Children.Add(surface);
                Point[] points=Enumerable.Range(0,8).Select(surface.IconPosition).ToArray(); Matrix layout=surface.LayoutTransform.Value,render=surface.RenderTransform.Value;
                PlanetReveal transition=PlanetReveal.PlaySwitch(host,new Point(30,50),.75,config.Appearance.Accent,surface,direction);
                transition.Advance(TimeSpan.Zero); transition.Advance(TimeSpan.FromMilliseconds(120));
                Check(!transition.IsHitTestVisible && !transition.Focusable && Canvas.GetLeft(transition)==30 && Canvas.GetTop(transition)==50 && surface.LayoutTransform.Value==layout && surface.RenderTransform.Value==render && points.Select((p,i)=>surface.IconPosition(i)==p && surface.IconSlot(p)==i).All(v=>v),"switching decoration preserves the center and all immediately usable action positions: "+direction,r);
                transition.Advance(TimeSpan.FromMilliseconds(PlanetReveal.SwitchDurationMilliseconds+1));
                Check(!transition.Running && host.Children.Count==1 && surface.Opacity==config.Appearance.RingOpacity,"switching completes quickly and releases its frame callback: "+direction,r);
            }
            return r;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class PlanetWheelTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string name,List<string> result) { if(!value) throw new Exception(name); result.Add("PASS "+name); }
        public static List<string> Run() {
            List<string> result=new List<string>(); PlanetWheel wheel=new PlanetWheel();
            Check(wheel.Scroll(-40)==0 && wheel.Scroll(-40)==0 && wheel.Scroll(-40)==1 && wheel.Scroll(120)==-1,"high-resolution wheel input accumulates one notch and has consistent up/down direction",result);
            wheel.Scroll(-70); Check(wheel.Scroll(60)==0 && wheel.Scroll(60)==-1,"reversing the wheel discards an incomplete step in the previous direction",result); wheel.Scroll(-60); wheel.Reset(); Check(wheel.Scroll(-60)==0,"leaving the center clears partial wheel movement",result);
            Check(PlanetWheel.Next(0,3,-1)==2 && PlanetWheel.Next(2,3,1)==0 && PlanetWheel.Next(0,1,4)==0 && PlanetWheel.Next(2,3,7)==0,"planet switching wraps in both directions including multi-notch input",result);
            Check(PlanetWheel.InCenter(new Point(205,205)) && PlanetWheel.InCenter(new Point(205,125.01)) && !PlanetWheel.InCenter(new Point(205,125)) && !PlanetWheel.InCenter(RingSurface.Position(6)),"only the central dead zone switches planets and action wheel targets stay separate",result);
            Preferences config=Preferences.Defaults(); config.Planets.Add(UserContent.NewPlanet(config)); config.Planets.Add(UserContent.NewPlanet(config));
            ApplicationSettings app=new ApplicationSettings { Executable="example.exe" }; config.Applications.Add(app); ApplicationProfiles.EnablePlanet(config,app); app.Planets[0].Items[0]=new ActionItem("アプリ専用","shortcut","F6"); config.Validate(); string original=Json.Write(config);
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); typeof(Controller).GetField("planetApplication",Private).SetValue(owner,"example.exe"); typeof(Controller).GetField("activePlanet",Private).SetValue(owner,2);
                RingWindow window=new RingWindow(owner); typeof(Controller).GetField("ring",Private).SetValue(owner,window);
                window.Rebuild(); RingSurface before=(RingSurface)typeof(RingWindow).GetField("surface",Private).GetValue(window); Canvas.SetLeft(before,60); Canvas.SetTop(before,40);
                owner.SwitchPlanet(1); RingSurface after=(RingSurface)typeof(RingWindow).GetField("surface",Private).GetValue(window);
                Check(owner.PlanetName==config.MainPlanetName && owner.PlanetPosition=="1 / 3" && owner.ActiveItems[0].Label=="アプリ専用","ordinary planets cycle from the invoked planet and retain per-app action overrides",result);
                Check(Canvas.GetLeft(after)==60 && Canvas.GetTop(after)==40 && after.LayoutTransform.Value==before.LayoutTransform.Value && after.IconPosition(0)==before.IconPosition(0),"switching rebuilds actions without moving the center or button positions",result);
                RingCenter center=(RingCenter)typeof(RingSurface).GetField("center",Private).GetValue(after);
                string description=System.Windows.Automation.AutomationProperties.GetName(center);
                StackPanel pages=((Grid)center.Child).Children.OfType<StackPanel>().Single();
                Check(description.Contains(config.MainPlanetName) && description.Contains("1 / 3") && description.Contains("ホイール") && pages.Children.Count==3 && ((FrameworkElement)pages.Children[0]).Width==8,"the center exposes the current planet accessibly and marks its filtered position with dots",result);
                Canvas area=(Canvas)typeof(RingWindow).GetField("pointerArea",Private).GetValue(window); PlanetReveal previous=area.Children.OfType<PlanetReveal>().FirstOrDefault();
                config.Appearance.PlanetSwitchAnimation=false; owner.SwitchPlanet(1); RingSurface plain=(RingSurface)typeof(RingWindow).GetField("surface",Private).GetValue(window); Canvas nextArea=(Canvas)typeof(RingWindow).GetField("pointerArea",Private).GetValue(window);
                Check(!nextArea.Children.OfType<PlanetReveal>().Any() && plain.Opacity==config.Appearance.RingOpacity && (previous==null || !previous.Running),"disabling switching decoration keeps the new ring fully visible and cancels the previous frame callback",result);
                owner.SwitchPlanet(-1); config.Appearance.PlanetSwitchAnimation=true;
                config.PlanetWheelSwitch=false; owner.SwitchPlanet(1); Check(owner.PlanetPosition=="1 / 3","disabled wheel switching leaves the active planet unchanged",result); config.PlanetWheelSwitch=true;
                app.Planets[0].Items[1]=PlanetStack.Create(new ActionItem("子","shortcut","F7")); app.Planets[0].Items[1].StackMode="arc"; window.ChangePlanet(); window.OpenStack(owner.ActiveItems[1]); owner.SwitchPlanet(1);
                Check(owner.InsideStack && owner.PlanetPosition=="1 / 3","the stack's center keeps its back operation instead of changing planets",result);
                window.Retire(); typeof(Controller).GetField("ring",Private).SetValue(owner,null);
                app.Planets[0].Items[1]=UserContent.CopyAction(config.Items[1]);
                Check(Json.Write(config)==original,"planet cycling does not rewrite saved app assignments actions or shortcuts",result);
            }
            Preferences restored=config.Clone(); restored.PlanetWheelSwitch=false; restored=restored.Clone(); Check(!restored.PlanetWheelSwitch,"the wheel preference persists independently",result);
            return result;
        }
    }
}

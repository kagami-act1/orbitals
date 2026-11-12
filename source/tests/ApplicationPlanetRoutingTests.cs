using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace OrbitRing {
    static class ApplicationPlanetRoutingTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string text,List<string> result) { if(!value) throw new Exception(text); result.Add("PASS "+text); }
        static void Set(object owner,string name,object value) { owner.GetType().GetField(name,Private).SetValue(owner,value); }
        static object Field(object owner,string name) { return owner.GetType().GetField(name,Private).GetValue(owner); }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences config=Preferences.Defaults();
            for(int i=0;i<4;i++) config.Planets.Add(UserContent.NewPlanet(config));
            ApplicationSettings app=new ApplicationSettings { Executable="Claude.exe",PlanetId=config.Planets[0].Id },other=new ApplicationSettings { Executable="vivaldi.exe",PlanetId=config.Planets[2].Id };
            config.Applications.Add(app); config.Applications.Add(other); config.Validate();
            Check(Enumerable.Range(0,5).All(requested=>ApplicationProfiles.RuntimePlanet(config,"CLAUDE.EXE",requested)==1 && ApplicationProfiles.RuntimePlanet(config,"Vivaldi.exe",requested)==3),"every invocation shortcut selects the active app's assigned planet for Claude and Vivaldi",result);
            Check(Enumerable.Range(0,5).All(requested=>ApplicationProfiles.RuntimePlanet(config,"unregistered.exe",requested)==requested),"unassigned apps keep the explicitly requested planet",result);
            Check(ApplicationProfiles.WheelPlanets(config,"unregistered.exe",0).SequenceEqual(new[]{0,2,4}),"ordinary wheel navigation skips all app-assigned extra planets",result);
            Check(ApplicationProfiles.WheelPlanets(config,"Claude.exe",1).SequenceEqual(new[]{1}) && ApplicationProfiles.WheelPlanets(config,"unregistered.exe",1).SequenceEqual(new[]{1}),"assigned planets remain fixed both in their app and when opened directly",result);
            string saved=Json.Write(config);
            using(Controller owner=new Controller(true)) {
                owner.Apply(config); Set(owner,"planetApplication","unregistered.exe"); Set(owner,"activePlanet",0);
                RingWindow window=new RingWindow(owner); Set(owner,"ring",window); window.Rebuild();
                owner.SwitchPlanet(1); Check(owner.PlanetName==config.Planets[1].Name && owner.PlanetPosition=="2 / 3","runtime navigation and its counter use only ordinary planets",result);
                owner.SwitchPlanet(1); Check(owner.PlanetName==config.Planets[3].Name,"a second wheel step skips the next app-assigned planet",result);
                owner.SwitchPlanet(1); owner.SwitchPlanet(-1); Check(owner.PlanetName==config.Planets[3].Name,"filtered wheel navigation wraps in both directions",result);
                Set(owner,"planetApplication","Claude.exe"); Set(owner,"activePlanet",1); owner.ActiveItems=ApplicationProfiles.Items(config,"Claude.exe",1); window.ChangePlanet(); owner.SwitchPlanet(1); owner.SwitchPlanet(-1);
                RingSurface surface=(RingSurface)Field(window,"surface"); RingCenter center=(RingCenter)Field(surface,"center"); string context=System.Windows.Automation.AutomationProperties.GetName(center);
                Check(!owner.CanSwitchPlanet && owner.PlanetName==config.Planets[0].Name && surface.PlanetRequested==null && context.Contains(config.Planets[0].Name) && context.Contains("アプリ専用") && !context.Contains("ホイール") && ((Grid)center.Child).Children.OfType<StackPanel>().Single().Visibility==Visibility.Collapsed,"a fixed app planet keeps its accessible context without offering wheel navigation or page dots",result);
                window.Retire(); Set(owner,"ring",null);
            }
            Check(Json.Write(config)==saved,"routing and wheel changes leave all saved mappings and action content intact",result);
            config.Applications.Add(new ApplicationSettings { Executable="shared.exe",PlanetId=app.PlanetId });
            Check(ApplicationProfiles.WheelPlanets(config,"unknown.exe",0).SequenceEqual(new[]{0,2,4}),"multiple apps sharing a planet do not duplicate or reorder the ordinary cycle",result);
            config.Applications.Add(new ApplicationSettings { Executable="main.exe",PlanetId="main" });
            Check(ApplicationProfiles.RuntimePlanet(config,"main.exe",4)==0 && ApplicationProfiles.WheelPlanets(config,"unknown.exe",0).SequenceEqual(new[]{0,2,4}),"an app may pin the main planet without removing the common main ring for other apps",result);
            config.Applications.RemoveAll(a=>a.PlanetId==app.PlanetId);
            Check(ApplicationProfiles.WheelPlanets(config,"unknown.exe",0).SequenceEqual(new[]{0,1,2,4}),"removing the final app assignment returns that planet to ordinary wheel navigation",result);
            using(Controller owner=new Controller(true)) {
                Preferences actual=Preferences.Defaults(); actual.Planets.Add(UserContent.NewPlanet(actual)); actual.Planets.Add(UserContent.NewPlanet(actual));
                Window target=new Window(); IntPtr handle=new WindowInteropHelper(target).EnsureHandle();
                try {
                    string executable=ExtendedActions.Executable(handle); Check(!String.IsNullOrEmpty(executable),"the actual target window yields its executable for app matching",result);
                    ApplicationSettings profile=new ApplicationSettings { Executable=executable.ToUpperInvariant(),PlanetId=actual.Planets[0].Id }; actual.Applications.Add(profile); ApplicationProfiles.EnablePlanet(actual,profile);
                    ApplicationProfiles.Items(actual,executable,1)[0]=new ActionItem("対象アプリ専用","shortcut","F6"); owner.Apply(actual); Set(owner,"target",handle);
                    foreach(int requested in new[]{0,2}) {
                        bool resolved=(bool)typeof(Controller).GetMethod("ResolvePlanet",Private).Invoke(owner,new object[]{requested});
                        Check(resolved && (int)Field(owner,"activePlanet")==1 && owner.ActiveItems[0].Label=="対象アプリ専用","the window-to-process runtime path selects the assigned layout for invocation "+requested,result);
                    }
                } finally { target.Close(); }
            }
            Preferences restored=config.Clone(); restored.Validate(); Check(ApplicationProfiles.WheelPlanets(restored,"unknown.exe",0).SequenceEqual(new[]{0,1,2,4}),"existing mapping data needs no migration or preference rewrite",result);
            return result;
        }
    }
}

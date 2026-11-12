using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class PlanetCustomizationTests {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static object Field(SettingsWindow editor,string name) { return typeof(SettingsWindow).GetField(name,Private).GetValue(editor); }
        static object Call(SettingsWindow editor,string name,params object[] args) { return typeof(SettingsWindow).GetMethod(name,Private).Invoke(editor,args); }
        static void Check(bool condition,string message,List<string> result) { if(!condition) throw new Exception(message); result.Add("PASS "+message); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> result=new List<string>(); Preferences config=Preferences.Defaults();
            Check(config.ButtonSize==82 && config.ButtonPercent==100 && config.Appearance.StackGap==20 && config.Appearance.StackSpacing==50,"new size rulers use the requested existing physical dimensions",result);
            config.ButtonSize=76; config.Appearance.StackGap=50; Preferences copy=config.Clone(); copy.Validate();
            Check(copy.ButtonSize==76 && copy.Appearance.StackGap==50 && copy.Appearance.StackSpacing==125 && !Json.Write(copy).Contains("StackSpacing") && !Json.Write(copy).Contains("ButtonPercent"),"size ruler changes preserve stored physical geometry without duplicate serialized fields",result);
            config.Appearance.TextOnly=true; copy=config.Clone(); Check(copy.Appearance.TextOnly,"text-only appearance persists in settings and backups",result);
            RingSurface ring=new RingSurface(); ring.Bind(config);
            Check(ring.Children.OfType<Button>().All(b=>((StackPanel)b.Content).Children[0] is TextBlock) && ring.Children.OfType<Button>().First().ToolTip!=null,"live planet buttons display their action names while retaining tooltips",result);
            LayoutEditor editorSurface=new LayoutEditor(); editorSurface.Bind(config,0,new int[0]);
            Check(editorSurface.Children.OfType<Button>().Count()==8 && editorSurface.Children.OfType<Button>().All(b=>b.Content is TextBlock && b.ContextMenu!=null),"text-only layout editing keeps every slot and erase command without duplicate outside labels",result);
            foreach(int count in new[]{0,1,2,3,8,10,16,24,32}) {
                List<ConversionChoice> choices=Enumerable.Range(0,count).Select(i=>new ConversionChoice { Operation="jpeg",Label="JPEG "+i,Quality=50+i }).ToList();
                Canvas drawing=ConversionPlanets.Drawing(choices,config.Appearance); Rect[] labels=(Rect[])drawing.Tag;
                bool correct=labels.Length==count && ConversionPlanets.Hit(new Point(ConversionPlanets.Center,ConversionPlanets.Center),drawing)==-1;
                for(int i=0;i<count;i++) { Button button=(Button)drawing.Children[i*2+2]; correct &= ((TextBlock)((StackPanel)button.Content).Children[0]).Text==choices[i].Label && drawing.Children[i*2+3].Visibility==Visibility.Collapsed && ConversionPlanets.Hit(ConversionPlanets.Position(i,count),drawing)==i; if(count>1) correct &= (ConversionPlanets.Position(i,count)-ConversionPlanets.Position((i+1)%count,count)).Length>button.Width+7; }
                Check(correct,"conversion text buttons remain selectable and separate with destination count "+count,result);
            }
            Check(ConversionPlanets.OrbitRadius(3)==ConversionPlanets.OrbitRadius(32) && (ConversionPlanets.Position(0,3)-ConversionPlanets.Position(1,3)).Length>(ConversionPlanets.Position(0,12)-ConversionPlanets.Position(1,12)).Length,"conversion menus distribute fewer or more destinations on the same circle",result);
            config.Conversion.Images=new List<ConversionChoice> { new ConversionChoice { Operation="jpeg",Label="高画質",Quality=95 },new ConversionChoice { Operation="jpeg",Label="軽量",Quality=50 } }; config.Conversion.Pdf.Clear(); copy=config.Clone(); copy.Validate();
            Check(copy.Conversion.Images.Count==2 && copy.Conversion.Images[1].Quality==50 && copy.Conversion.Pdf.Count==0,"duplicate formats with independent quality and empty menus survive saving",result);
            copy.Conversion.Images[0].Enabled=false; copy.Conversion.Images[1].Enabled=false; copy.Validate(); Check(copy.Conversion.Choices(false).Count==0,"hidden conversion destinations stay disabled without being recreated",result);
            copy.Conversion.Images=Enumerable.Range(0,33).Select(i=>new ConversionChoice { Operation="png",Label="PNG" }).ToList(); Check(Reject(delegate { copy.Validate(); }),"conversion menus enforce their bounded destination limit",result);
            using(Controller owner=new Controller(true)) {
                config=Preferences.Defaults(); owner.Apply(config); SettingsWindow editor=new SettingsWindow(owner);
                try {
                    StackPanel rows=(StackPanel)Field(editor,"catalogRows"); Expander first=rows.Children.OfType<Expander>().First();
                    Check(((StackPanel)first.Content).Children.Count==0,"collapsed action groups defer their row and icon creation",result);
                    first.IsExpanded=true; Check(((StackPanel)first.Content).Children.Count>0,"expanding an action group creates usable rows on demand",result);
                    LayoutEditor surface=(LayoutEditor)Field(editor,"surface"); object original=surface.Children.OfType<Button>().First();
                    editor.Navigate(5); editor.Navigate(4); editor.Navigate(0); Check(Object.ReferenceEquals(original,surface.Children.OfType<Button>().First()),"switching tabs retains unchanged planet visuals",result);
                    editor.Navigate(5); Preferences draft=(Preferences)Field(editor,"draft"); int count=draft.Conversion.Images.Count;
                    Call(editor,"AddConversion","jpeg"); draft=(Preferences)Field(editor,"draft"); Check(draft.Conversion.Images.Count==count+1 && draft.Conversion.Images.Last().Operation=="jpeg","conversion editor adds a separately configurable destination",result);
                    Call(editor,"Undo"); draft=(Preferences)Field(editor,"draft"); Check(draft.Conversion.Images.Count==count,"undo removes an added conversion destination",result);
                    Call(editor,"Redo"); draft=(Preferences)Field(editor,"draft"); Call(editor,"RemoveConversion",draft.Conversion.Images,draft.Conversion.Images.Last());
                    draft=(Preferences)Field(editor,"draft"); Check(draft.Conversion.Images.Count==count,"conversion editor removes the selected destination",result);
                    draft.Conversion.Images.Clear(); Call(editor,"RefreshAll"); Call(editor,"AddConversion","png"); draft=(Preferences)Field(editor,"draft"); Check(draft.Conversion.Images.Count==1 && draft.Conversion.Images[0].Operation=="png","an empty conversion editor can add its first destination",result);
                    draft.GestureLibrary.Add(SavedGesture.From(GesturePresets.Create().Single(p=>p.Id=="w").Create(),"自分のW")); editor.Navigate(1); Call(editor,"ShowSide",4);
                    ComboBox source=(ComboBox)Field(editor,"gesturePresetSource"),category=(ComboBox)Field(editor,"gesturePresetCategory"); source.SelectedIndex=2; category.SelectedItem="折れ線";
                    StackPanel presetRows=(StackPanel)Field(editor,"gesturePresetRows"); Check(presetRows.Children.OfType<DockPanel>().Count()==1 && !presetRows.Children.OfType<Button>().Any(),"movement type and custom source filters combine independently",result);
                } finally { Call(editor,"RestoreSaved"); editor.ReleaseResources(); editor.Close(); }
            }
            return result;
        }
    }
}

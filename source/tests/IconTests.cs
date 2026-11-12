using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace OrbitRing {
    static class IconTests {
        static void Check(bool condition,string name,List<string> results) { if(!condition) throw new Exception(name); results.Add("PASS "+name); }
        public static List<string> Run() {
            List<string> results=new List<string>();
            Check(MaterialIcons.All.Count==4001 && MaterialIcons.All.Select(i=>i.Key).Distinct().Count()==4001,"all 4001 material icons have unique persistent keys",results);
            Check(MaterialIcons.Categories.Count==18 && MaterialIcons.Categories.Sum(c=>c.Count)==MaterialIcons.All.Count && MaterialIcons.All.All(i=>MaterialIcons.Categories.Count(c=>c.Id==i.Category)==1),"MECE catalog covers every icon in exactly one of 18 categories",results);
            bool valid=true;
            foreach(MaterialIcon icon in MaterialIcons.All) {
                Rect bounds=icon.Shape.Bounds;
                valid &= icon.Shape.IsFrozen && !bounds.IsEmpty && bounds.Width>0 && bounds.Height>0 && !Double.IsNaN(bounds.X) && !Double.IsInfinity(bounds.Width);
            }
            Check(valid,"all bundled vector paths parse and contain visible geometry",results);
            Check(MaterialIcons.Search("音量","").Any(i=>i.Name=="volume_up") && MaterialIcons.Search("カメラ","").Any(i=>i.Name=="photo_camera"),"Japanese keywords find volume and camera icons",results);
            Check(!MaterialIcons.Search("音量","").Any(i=>i.Name=="propane" || i.Name=="counter_6"),"Japanese audio search excludes capacity and unrelated numbered icons",results);
            Check(MaterialIcons.Search("ＣＡＭＥＲＡ","").Any(i=>i.Name=="photo_camera") && MaterialIcons.Search("photo camera","").Any(i=>i.Name=="photo_camera"),"search normalizes full width text and combines terms",results);
            Check(MaterialIcons.Search("camera_alt","").Count==1,"upstream alias finds one deduplicated icon",results);
            Check(MaterialIcons.Search("", "time").Count==MaterialIcons.Categories.Single(c=>c.Id=="time").Count && MaterialIcons.Search("", "time").All(i=>i.Category=="time"),"category filtering matches complete disjoint membership",results);
            Check(MaterialIcons.Search("no_such_icon_000","").Count==0,"unknown icon query has an empty result",results);
            Preferences p=Preferences.Defaults(); p.Items[0].Icon="ms:home"; p.Validate(); Preferences copy=p.Clone(); copy.Swap(0,4); copy.Validate();
            Check(copy.Items[4].Icon=="ms:home" && ActionIcons.Visual(copy.Items[4],28) is Image,"material selection persists through JSON cloning and reordering",results);
            copy.Items[4].Icon="star"; copy.Validate();
            Check(ActionIcons.Visual(copy.Items[4],28) is TextBlock,"legacy icon settings remain readable without migration",results);
            copy.Items[4].Icon="ms:unknown_not_an_icon"; bool rejected=false; try { copy.Validate(); } catch { rejected=true; }
            Check(rejected,"unknown material icon keys are rejected",results);
            Check(RingSurface.Bubble(p.Items[0],0,76).Content is StackPanel && ((StackPanel)RingSurface.Bubble(p.Items[0],0,76).Content).Children[0] is Image,"runtime ring uses the selected material vector",results);
            foreach(ActionPreset preset in ActionCatalog.Create()) { ActionItem action=preset.Create(); string icon=ActionIcons.DefaultKey(action); Check(MaterialIcons.Find(icon)!=null && ActionIcons.Visual(action,24) is Image,"built-in action has a bundled material icon: "+action.Label,results); }
            ActionItem folder=new ActionItem("フォルダー","open",Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            Check(ActionIcons.DefaultKey(folder)=="ms:folder" && ActionIcons.DefaultKey(new ActionItem("エクスプローラー","shortcut","Win+E"))=="ms:folder" && ActionIcons.DefaultKey(PlanetStack.Create())=="ms:stacks","stack icon is distinct from folder and explorer icons",results);
            folder.Icon="ms:home"; Check(ActionIcons.EffectiveKey(folder)=="ms:home","manual icons take priority over new automatic defaults",results);
            Check(ActionIcons.DefaultKey(new ActionItem("ウェブ","open","https://example.com"))=="ms:public" && ActionIcons.DefaultKey(new ActionItem("文書","open",@"C:\Test\note.txt"))=="ms:draft","automatic open icon distinguishes folders web pages and files",results);
            return results;
        }
    }
}

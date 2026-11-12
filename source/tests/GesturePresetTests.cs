using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class GesturePresetTests {
        static void Check(bool condition,string label,List<string> results) { if(!condition) throw new Exception(label); results.Add("PASS "+label); }
        public static List<string> Run() {
            List<string> r=new List<string>(); List<GesturePreset> catalog=GesturePresets.Create();
            Check(catalog.Count==27 && catalog.Select(p=>p.Id).Distinct().Count()==catalog.Count,"gesture preset IDs are unique and library remains available",r);
            Preferences prefs=Preferences.Defaults(); prefs.Orbit.Bindings=catalog.Select(p=>p.Create()).ToList(); prefs.Validate();
            Check(prefs.Orbit.Bindings.Count==27,"all movement presets validate together without shape collisions",r);
            Check(new[]{"w","z","n","inverted-v","circle-clockwise","circle-counterclockwise"}.All(id=>catalog.Any(p=>p.Id==id)),"letter and clockwise and counterclockwise circle presets are available without action assignments",r);
            Check(prefs.Orbit.Bindings.All(b=>b.Action.Kind=="none" && b.Action.Value=="" && b.Action.Toggle==null && b.Action.Icon==null),"movement presets carry no executable action or icon",r);
            prefs=prefs.Clone(); prefs.Validate();
            Check(prefs.Orbit.Bindings.All(b=>b.Action.Kind=="none"),"unassigned movements survive saving and reloading",r);
            List<OrbitBinding> initial=Preferences.Defaults().Orbit.Bindings;
            Check(initial.Count==10 && initial[0].Pattern=="L" && initial[0].Action.Value=="Alt+Left" && initial.Last().Pattern=="Rocker","initial action assignments are independent from the movement library",r);
            GesturePreset back=catalog.Single(p=>p.Id=="left"); initial.RemoveAt(0); int index=GesturePresets.Add(initial,back);
            Check(initial.Count==10 && initial[index].Pattern=="L" && initial[index].Action.Kind=="none","restoring a deleted movement does not restore its old action",r);
            initial[index].Action=new ActionItem("自分の設定","shortcut","F8"); int same=GesturePresets.Add(initial,back);
            Check(same==index && initial.Count==10 && initial[same].Action.Value=="F8","selecting an existing movement preserves its assigned action",r);
            GesturePreset curve=catalog.Single(p=>p.Id=="c"); OrbitBinding firstCopy=curve.Create(),secondCopy=curve.Create(); firstCopy.Shape[0].X+=.2; firstCopy.Action=new ActionItem("自分の操作","shortcut","F7");
            Check(Json.Write(secondCopy)==Json.Write(curve.Create()),"editing an added movement or action never changes the preset",r);
            List<OrbitBinding> modified=new List<OrbitBinding> { catalog.Single(p=>p.Id=="shift-left-click").Create() };
            Check(GesturePresets.Existing(modified,catalog.Single(p=>p.Id=="left-click"))<0,"preset duplicate checks distinguish modifier conditions",r);
            OrbitBinding shapeBack=new OrbitBinding { Pattern="Shape",Shape=GestureShape.Template(back.Create()),Action=new ActionItem("別の操作","shortcut","F6") };
            Check(GesturePresets.Existing(new[]{shapeBack},back)==0,"custom drawing matching a preset is detected before adding",r);
            initial=Preferences.Defaults().Orbit.Bindings; while(initial.Count<128) initial.Add(initial[0]); bool limited=false; try { GesturePresets.Add(initial,curve); } catch(InvalidOperationException) { limited=true; }
            Check(limited && GesturePresets.Add(initial,back)==0,"full library rejects new additions but allows opening an existing movement",r);
            initial=new List<OrbitBinding>(); GesturePresets.Add(initial,curve); initial.Clear(); GesturePresets.Add(initial,curve);
            Check(initial.Count==1 && initial[0].Shape!=null && initial[0].Action.Kind=="none","curved movement can be deleted and restored without an action",r);
            Check(catalog.All(p=>GesturePresets.Categories.Contains(p.Category)) && catalog.Single(p=>p.Id=="left").Matches("左へ") && !catalog.Any(p=>p.Matches("コピー")),"preset discovery uses movement names and categories",r);
            Check(catalog.Select(p=>Array.IndexOf(GesturePresets.Categories,p.Category)).SequenceEqual(catalog.Select(p=>Array.IndexOf(GesturePresets.Categories,p.Category)).OrderBy(i=>i)),"movement categories form contiguous ordered groups",r);
            Check(catalog.All(p=>SavedGesture.From(p.Create(),p.Name).Preset().Category==p.Category),"saved movement classification is independent of standard or custom origin",r);
            foreach(GesturePreset preset in catalog.Where(p=>p.Create().Shape!=null)) {
                OrbitBinding binding=preset.Create(); bool ambiguous; Point[] path=binding.Shape.Select(p=>new Point(p.X,p.Y)).ToArray();
                Check(GestureShape.Match(new[]{binding},path,"",out ambiguous)==binding,"curved preset recognizes its ordered stroke: "+preset.Id,r);
            }
            return r;
        }
    }
}

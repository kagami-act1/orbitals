using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    static class StackTests {
        static void Check(bool value,string label,List<string> result) { if(!value) throw new Exception(label); result.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> results=new List<string>(); Preferences p=Preferences.Defaults();
            string original=Json.Write(p.Items[0]); p.Items[0]=PlanetStack.Create(p.Items[0]); p.Validate();
            Check(p.Items[0].Children.Count==8 && Json.Write(p.Items[0].Children[0])==original,"wrapping an action preserves its definition",results);
            Check(PlanetStack.Add(p.Items[0],new ActionItem("貼り付け","shortcut","Ctrl+V")) && p.Items[0].Children[1].Value=="Ctrl+V","dropping into a stack fills the next empty slot",results);
            p.Items[0].Children[2]=PlanetStack.Create(); p.Items[0].Children[2].Children[0]=PlanetStack.Create(); p.Validate();
            Preferences copy=p.Clone(); copy.Validate(); Check(copy.Items[0].Children[2].Children[0].Kind=="stack","three levels of folders round trip",results);
            copy.Items[0].Children[2].Children[0].Children[0]=PlanetStack.Create(); Check(Reject(copy.Validate),"fourth folder level rejected",results);
            copy=p.Clone(); copy.Items[0].Children.RemoveAt(0); Check(Reject(copy.Validate),"malformed stack size rejected",results);
            ActionItem full=PlanetStack.Create(); for(int i=0;i<8;i++) full.Children[i]=new ActionItem("キー","shortcut","F6"); string before=Json.Write(full); Check(!PlanetStack.Add(full,new ActionItem("別","shortcut","F7")) && before==Json.Write(full),"full stack rejects additions without losing actions",results);
            ActionItem toggle=ActionCatalog.Create().Single(a=>a.Item.Label=="ミュート切替").Create(); p.Items[0].Children[3]=toggle; p.Validate();
            Check(p.AllActions().Any(a=>a.Kind=="toggle" && a.Toggle.Id==toggle.Toggle.Id),"nested toggles participate in state retention",results);
            copy=p.Clone(); string id=copy.Items[0].Children[3].Toggle.Id; PlanetStack.NewIdentities(copy.Items); Check(copy.Items[0].Children[3].Toggle.Id!=id,"copied planets have independent nested toggle identities",results);
            copy=p.Clone(); copy.Items[1]=copy.Items[0].Children[3].Clone(); Check(Reject(copy.Validate),"duplicate toggle identity across stack boundaries rejected",results);
            copy=p.Clone(); copy.Orbit.Bindings[0].Action=PlanetStack.Create(); Check(Reject(copy.Validate),"folders are limited to planet",results);
            copy=p.Clone(); copy.Items[0].Children[3].Toggle.Stages[0].Kind="stack"; Check(Reject(copy.Validate),"toggle stages cannot open nested folder state",results);
            Check(StartupRegistration.Command(@"C:\Test Folder\orbitals.exe")=="\"C:\\Test Folder\\orbitals.exe\" --background","startup command quotes portable executable and starts resident",results);
            Check(!Preferences.Defaults().StartWithWindows,"startup remains opt-in",results);
            return results;
        }
    }
}

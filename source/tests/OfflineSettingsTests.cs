using System;
using System.Collections.Generic;
using System.IO;

namespace OrbitRing {
    static class OfflineSettingsTests {
        static void Check(bool value,string label,List<string> result) { if(!value) throw new Exception(label); result.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> result=new List<string>();
            string root=Path.Combine(Path.GetTempPath(),"orbitals-offline-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string document=Path.Combine(root,"document.txt"),exe=Path.Combine(root,"app.exe"),shortcut=Path.Combine(root,"app.lnk"),folder=Path.Combine(root,"documents"),settings=Path.Combine(root,"settings.json");
            try {
                File.WriteAllText(document,"source"); File.WriteAllText(exe,""); File.WriteAllText(shortcut,""); Directory.CreateDirectory(folder);
                Preferences config=Preferences.Defaults(); config.Hotkey="Ctrl+Home";
                config.Items[0]=new ActionItem("文書","open",document);
                config.Items[1]=new ActionItem("アプリ","launch",exe);
                config.Items[2]=new ActionItem("リンク","launch",shortcut);
                config.Items[3]=new ActionItem("マクロ","macro",MacroDefinition.Write(new[]{new MacroStep { Kind="open",Value=document },new MacroStep { Kind="launch",Value=shortcut },new MacroStep { Kind="shortcut",Value="Home" }}));
                ToggleDefinition toggle=ToggleDefinition.Create(); toggle.Stages[0].Kind="launch"; toggle.Stages[0].Value=exe; toggle.Stages[1].Value="End";
                config.Items[4]=new ActionItem("切替","toggle","") { Toggle=toggle };
                PlanetProfile planet=UserContent.NewPlanet(config); planet.Hotkey="F13"; planet.HotkeyEnabled=true; planet.Items[0]=new ActionItem("フォルダー","open",folder); config.Planets.Add(planet);
                config.Favorites.Add(new ActionItem("お気に入り","launch",exe));
                config.Validate(); string saved=Json.Write(config); File.WriteAllText(settings,saved);
                File.Delete(document); File.Delete(exe); File.Delete(shortcut); Directory.Delete(folder);
                Preferences reopened=Json.ReadFile<Preferences>(settings); reopened.Validate();
                Check(Json.Write(reopened)==saved && reopened.Items[0].Value==document && reopened.Planets[0].Items[0].Value==folder,"unavailable files apps shortcuts and folders do not discard saved ring or planet settings",result);
                Check(reopened.Items[3].Value==config.Items[3].Value && reopened.Items[4].Toggle.Stages[0].Value==exe && reopened.Favorites[0].Value==exe,"offline paths in macros toggle stages and favorites survive loading unchanged",result);
                Check(reopened.Hotkey=="Ctrl+Home" && reopened.Planets[0].Hotkey=="F13" && MacroStep.Parse(reopened.Items[3].Value)[2].Value=="Home","logical Home and F13 bindings remain intact with unavailable external targets",result);
                Check(Reject(delegate { OpenTarget.Resolve(document); }) && Reject(delegate { ApplicationLaunch.StartInfo(exe); }) && Reject(delegate { ApplicationLaunch.StartInfo(shortcut); }),"unavailable targets are still rejected before opening or launching an action",result);
                File.WriteAllText(document,"returned"); File.WriteAllText(exe,""); File.WriteAllText(shortcut,""); Directory.CreateDirectory(folder);
                Check(OpenTarget.Resolve(document)==document && ApplicationLaunch.StartInfo(exe).FileName==exe && ApplicationLaunch.StartInfo(shortcut).FileName==shortcut,"reconnected targets resolve again without editing saved actions",result);
                reopened.Items[0].Value="relative.txt"; Check(Reject(reopened.Validate),"offline tolerance does not allow relative paths into settings",result);
                reopened.Items[0].Value=document+":stream"; Check(Reject(reopened.Validate),"offline tolerance preserves alternate-stream restrictions",result);
                reopened.Items[0].Value=document; reopened.Items[1].Value="https://example.com/app.exe"; Check(Reject(reopened.Validate),"application settings cannot replace a local executable with a web URL",result);
                Check(AppPaths.RecordSettingsLoadFailure(settings,new IOException("offline diagnostic fixture")) && File.ReadAllText(Path.Combine(root,"settings-load-error.log")).Contains("offline diagnostic fixture") && File.ReadAllText(settings)==saved,"load error diagnostics record the cause without changing the settings file",result);
                Check(!AppPaths.RecordSettingsLoadFailure(Path.Combine(root,"missing","settings.json"),new IOException("original error")),"a diagnostic write failure cannot replace the original settings failure",result);
            } finally { Directory.Delete(root,true); }
            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace OrbitRing {
    static class ApplicationLaunchTests {
        static void Check(bool value,string name,List<string> results) { if(!value) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> results=new List<string>();
            string directory=Path.Combine(Path.GetTempPath(),"orbitals-launch-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            string shortcut=Path.Combine(directory,"browser private.exe.LNK"),exe=Path.Combine(directory,"browser app.exe"),text=Path.Combine(directory,"notes.txt");
            try {
                File.WriteAllText(shortcut,""); File.WriteAllText(exe,""); File.WriteAllText(text,"");
                Check(ExtendedActions.Validate(new ActionItem("Private","launch",shortcut)),"application actions accept shortcut files with compound names and uppercase extensions",results);
                var start=ApplicationLaunch.StartInfo(shortcut);
                Check(start.UseShellExecute && start.FileName==shortcut && String.IsNullOrEmpty(start.Arguments) && String.IsNullOrEmpty(start.WorkingDirectory),"shortcut requests preserve the link for Windows to apply its target arguments and working directory",results);
                Check(ApplicationLaunch.Resolve("\""+shortcut+"\"")==shortcut && ApplicationLaunch.Resolve(exe)==exe,"quoted pasted paths and existing executable paths remain supported",results);
                Check(Reject(delegate { ApplicationLaunch.Resolve("\""+exe+"\" --private"); }) && Reject(delegate { ApplicationLaunch.Resolve(text); }) && Reject(delegate { ApplicationLaunch.Resolve(directory); }) && Reject(delegate { ApplicationLaunch.Resolve("https://example.com/app.exe"); }),"application actions reject embedded arguments non-app files directories and web URLs",results);
                Preferences config=Preferences.Defaults(); config.Items[0]=new ActionItem("Private","launch",shortcut); config.Validate(); Preferences clone=config.Clone(); clone.Validate();
                Check(clone.Items[0].Value==shortcut,"shortcut application actions survive settings serialization without resolving away launch options",results);
                ToggleStage stage=new ToggleStage { Label="Private",Kind="launch",Value=shortcut }; Check(ExtendedActions.Validate(stage.Action()),"toggle stages accept the same application shortcut targets",results);
                var picker=SettingsWindow.ActionFileDialog("launch"); var open=SettingsWindow.ActionFileDialog("open");
                Check(!picker.DereferenceLinks && picker.CheckFileExists && !picker.Multiselect && picker.Filter.Contains("*.lnk") && !open.DereferenceLinks,"application and generic file pickers retain shortcut paths instead of replacing them with targets",results);
                File.Delete(shortcut); Check(Reject(delegate { ApplicationLaunch.StartInfo(shortcut); }),"removed shortcut targets are revalidated before launching",results);
            } finally { foreach(string path in new[]{shortcut,exe,text}) if(File.Exists(path)) File.Delete(path); Directory.Delete(directory); }
            return results;
        }
    }
}

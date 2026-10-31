using System;
using System.Collections.Generic;
using System.IO;

namespace OrbitRing {
    static class InstallationTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        public static List<string> Run() {
            List<string> r=new List<string>(); string root=Path.Combine(Path.GetTempPath(),"OrbitalsInstallTest-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            string data=@"C:\Users\User\AppData\Local",file=data+@"\Orbitals\settings.json",redirected=@"\\?\"+data+@"\Packages\Example.App_123\LocalCache\Local\Orbitals\settings.json";
            Check(DesktopLaunch.IsRedirected(file,redirected,data),"package-private data is distinguished from the same logical desktop settings path",r);
            Check(DesktopLaunch.IsRedirected(file.ToUpperInvariant(),redirected.ToLowerInvariant(),data),"virtualized settings detection follows Windows path casing rules",r);
            Check(!DesktopLaunch.IsRedirected(file,@"\\?\"+file,data) && !DesktopLaunch.IsRedirected(file,@"D:\Profiles\User\Orbitals\settings.json",data),"ordinary desktop paths and redirected user-profile drives do not cause a relaunch",r);
            Check(!DesktopLaunch.IsRedirected(file,data+@"\Packages\Example.App_123\LocalCache\Local\Other\settings.json",data) && !DesktopLaunch.IsRedirected(redirected.Substring(4),redirected,data),"only a matching implicit package redirect triggers normal desktop startup",r);
            string command=StartupRegistration.Command(@"C:\Program Files\Orbitals\orbitals.exe");
            Check(StartupRegistration.Evaluate(true,command,command,null)==StartupState.Registered,"a matching background startup entry works without a Windows override",r);
            Check(StartupRegistration.Evaluate(true,command.ToUpperInvariant(),command,null)==StartupState.Registered,"startup status recognizes Windows path casing",r);
            byte[] approval=new byte[12]; approval[0]=3; approval[4]=91;
            Check(StartupRegistration.Evaluate(true,command,command,approval)==StartupState.DisabledByWindows,"Windows startup disablement is reported despite the app preference being enabled",r);
            approval[0]=2;
            Check(StartupRegistration.Evaluate(true,command,command,approval)==StartupState.Registered,"Windows startup approval is recognized independently of timestamp bytes",r);
            approval[0]=99;
            Check(StartupRegistration.Evaluate(true,command,command,approval)==StartupState.Unknown && StartupRegistration.Evaluate(true,command,command,new byte[]{2})==StartupState.Unknown && StartupRegistration.Evaluate(true,command,command,"2")==StartupState.Unknown,"unrecognized or malformed Windows approval data is never reported as enabled",r);
            Check(StartupRegistration.Evaluate(true,null,command,null)==StartupState.Missing && StartupRegistration.Evaluate(true,command.Replace("Program Files","Old"),command,null)==StartupState.DifferentLocation,"missing and outdated startup registrations are distinguished",r);
            Check(StartupRegistration.Evaluate(false,null,command,null)==StartupState.Off && StartupRegistration.Evaluate(false,command,command,null)==StartupState.LeftRegistered,"a disabled preference does not hide a stale Windows startup entry",r);
            Check(StartupRegistration.Evaluate(true,command.Replace(" --background",""),command,null)==StartupState.DifferentLocation,"a startup entry that opens the editor instead of staying resident needs repair",r);
            try {
                string source=Path.Combine(root,"portable"),local=Path.Combine(root,"local"),destination=AppPaths.DirectoryFor(source,local,true); Directory.CreateDirectory(source);
                Check(AppPaths.DirectoryFor(source,local,false)==source && destination==Path.Combine(local,"Orbitals"),"installed data is separated from program files while portable data stays beside the executable",r);
                Preferences config=Preferences.Defaults(); config.PlanetEnabled=false; config.Orbit.Enabled=false; config.Orbit.Distance=36;
                config.Orbit.Bindings=new List<OrbitBinding> { new OrbitBinding { Pattern="U",Action=new ActionItem("ペースト","shortcut","Ctrl+V") },new OrbitBinding { Pattern="D",Action=new ActionItem("コピー","shortcut","Ctrl+C"),Tolerance=21 } };
                config.Items[0]=new ActionItem("保持","hold","Ctrl"); string original=Json.Write(config);
                File.WriteAllText(Path.Combine(source,"settings.json"),original); File.WriteAllText(Path.Combine(source,"settings.json.bak"),"backup"); File.WriteAllText(Path.Combine(source,"settings.json.draft.json"),"draft");
                AppPaths.Migrate(source,destination);
                Check(File.ReadAllText(Path.Combine(destination,"settings.json"))==original && File.ReadAllText(Path.Combine(destination,"settings.json.bak"))=="backup" && File.ReadAllText(Path.Combine(destination,"settings.json.draft.json"))=="draft","installation migration retains saved settings backup and unsaved draft",r);
                Check(File.ReadAllText(Path.Combine(source,"settings.json"))==original,"migrating settings leaves the portable copy intact",r);
                for(int i=0;i<3;i++) { Preferences reopened=Json.ReadFile<Preferences>(Path.Combine(destination,"settings.json")); reopened.Validate(); Check(Json.Write(reopened)==original && reopened.Orbit.Bindings.Count==2 && reopened.Orbit.Bindings[1].Tolerance==21 && reopened.Orbit.Distance==36,"reopening installed settings retains customized gestures thresholds and ring actions: "+i,r); }
                File.WriteAllText(Path.Combine(destination,"settings.json"),"new installed settings"); File.Delete(Path.Combine(destination,"settings.json.draft.json")); AppPaths.Migrate(source,destination);
                Check(File.ReadAllText(Path.Combine(destination,"settings.json"))=="new installed settings" && !File.Exists(Path.Combine(destination,"settings.json.draft.json")),"subsequent starts never overwrite installed settings or resurrect stale portable drafts",r);
                AppPaths.Migrate(source,source); Check(File.ReadAllText(Path.Combine(source,"settings.json"))==original,"same-directory migration is harmless",r);
            } finally { Directory.Delete(root,true); }
            return r;
        }
    }
}

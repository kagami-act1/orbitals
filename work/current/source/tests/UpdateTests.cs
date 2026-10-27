using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    static class UpdateTests {
        static void Check(bool value,string label,List<string> results) { if(!value) throw new Exception(label); results.Add("PASS "+label); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static string Release(string tag,string url=null,bool draft=false,bool prerelease=false,string name="orbitals-Windows11.zip",string state="uploaded",long size=1024) {
            return Json.Write(new { tag_name=tag,draft=draft,prerelease=prerelease,html_url="https://untrusted.example/",assets=new[]{new { name=name,state=state,size=size,browser_download_url=url??("https://github.com/example/orbitals/releases/download/"+tag+"/"+name) }} });
        }
        public static List<string> Run() {
            List<string> r=new List<string>();
            Check(typeof(UpdateService).Assembly.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute),false).Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName==".NETFramework,Version=v4.8" && System.Net.ServicePointManager.SecurityProtocol==(System.Net.SecurityProtocolType)0,"the standalone executable negotiates secure connections using current Windows TLS defaults",r);
            Check(UpdateService.Repository("example/orbitals")=="example/orbitals" && UpdateService.Repository(" https://github.com/example/orbitals.git/ ")=="example/orbitals","update sources accept repository names and canonical GitHub URLs",r);
            foreach(string source in new[]{"","https://github.com.evil.test/a/b","http://github.com/a/b","https://user@github.com/a/b","https://github.com:8443/a/b","https://github.com/a/b?token=x","https://github.com/a/b#fragment","a/b/c","../repo","a/..","a%2fb/c","https://github.com/a/b/releases"}) Check(UpdateService.Repository(source)==null,"untrusted update source is rejected: "+source,r);
            Check(UpdateService.VersionTag("v0.27")==new Version(0,27,0,0) && UpdateService.VersionTag("0.27.0.1")==new Version(0,27,0,1),"release comparison pads missing version components consistently",r);
            foreach(string tag in new[]{"v1.0.0-beta","latest","1.0.0+build","1.2.3.4.5","1.-2.3","999999999999.0","v1.0/../file"}) Check(UpdateService.VersionTag(tag)==null,"nonrelease tag cannot become an update: "+tag,r);
            UpdateResult result=UpdateService.Parse("example/orbitals",Release("v99.0.0"));
            Check(result.Available && result.Version==new Version(99,0,0,0) && result.Page=="https://github.com/example/orbitals/releases/tag/v99.0.0","a newer release with the expected uploaded asset is offered on its trusted GitHub page",r);
            Check(!UpdateService.Parse("example/orbitals",Release("v"+UpdateService.Current.ToString(3))).Available && !UpdateService.Parse("example/orbitals",Release("v0.0.1")).Available,"current and older releases never prompt an update",r);
            Check(!UpdateService.Parse("example/orbitals",Release("v99.0",draft:true)).Available && !UpdateService.Parse("example/orbitals",Release("v99.0",prerelease:true)).Available,"draft and prerelease versions are excluded",r);
            Check(!UpdateService.Parse("example/orbitals",Release("v99.0",name:"other.zip")).Available && !UpdateService.Parse("example/orbitals",Release("v99.0",state:"new")).Available && !UpdateService.Parse("example/orbitals",Release("v99.0",size:0)).Available,"missing unrelated incomplete and empty assets cannot advertise a usable update",r);
            Check(!UpdateService.Parse("example/orbitals",Release("v99.0","https://evil.example/orbitals-Windows11.zip")).Available && !UpdateService.Parse("example/orbitals",Release("v99.0","https://github.com/other/orbitals/releases/download/v99.0/orbitals-Windows11.zip")).Available,"release assets must belong to the configured repository and version",r);
            Check(UpdateService.Parse("example/orbitals",Release("v99.0",name:"orbitals-Setup.exe")).Available,"installer releases use the same update notification channel",r);
            Check(UpdateService.Parse("example/orbitals",Release("v99.0",name:"orbitals-Portable-Windows11.zip")).Available && UpdateService.Parse("example/orbitals",Release("v99.0",name:"orbitals-Windows11.zip")).Available,"portable releases accept the explicit Portable name and the earlier ZIP name",r);
            Check(!UpdateService.Parse("example/orbitals",Release("v99.0","https://evil.example/orbitals-Portable-Windows11.zip",name:"orbitals-Portable-Windows11.zip")).Available && !UpdateService.Parse("example/orbitals",Release("v99.0",name:"orbitals-Portable-Windows11.zip",state:"new")).Available,"the Portable name cannot bypass download-origin or upload-completion checks",r);
            Check(Reject(delegate { UpdateService.Parse("example/orbitals",new string('x',512*1024+1)); }) && Reject(delegate { UpdateService.Parse("example/orbitals","{"); }),"oversized and malformed network metadata are rejected",r);
            Preferences config=Preferences.Defaults(); config.Updates.Repository="example/orbitals"; Preferences restored=config.Clone(); restored.Validate();
            Check(restored.Updates.Repository=="example/orbitals" && !Json.Write(restored.Updates).Contains("Automatic"),"update source survives backup without an obsolete automatic-check option",r);
            config.Updates.Repository="file:///settings.json"; Check(Reject(config.Validate),"invalid update sources cannot enter saved settings",r);
            Preferences legacy=Json.Read<Preferences>("{\"Version\":1,\"Hotkey\":\"F13\",\"Items\":"+Json.Write(config.Items)+"}"); legacy.Validate();
            Check(legacy.Updates!=null && legacy.Updates.Repository=="","older settings receive update defaults without inventing a remote destination",r);
            UpdatePreferences previous=Json.Read<UpdatePreferences>("{\"Repository\":\"example/orbitals\",\"Automatic\":true}"); previous.Validate();
            Check(previous.Repository=="example/orbitals" && !Json.Write(previous).Contains("Automatic"),"legacy automatic-update settings load safely as manual checks",r);
            return r;
        }
    }
}

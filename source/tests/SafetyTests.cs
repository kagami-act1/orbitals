using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OrbitRing {
    static class SafetyTests {
        static void Check(bool condition,string name,List<string> results) { if(!condition) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> results=new List<string>();
            string folder=Path.Combine(Path.GetTempPath(),"OrbitRing-tests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string file=Path.Combine(folder,"example file.txt"),json=Path.Combine(folder,"settings.json"),previous=Environment.CurrentDirectory;
            try {
                File.WriteAllText(file,"test"); Environment.CurrentDirectory=folder;
                Check(OpenTarget.Resolve(file)==file && OpenTarget.Resolve(folder)==folder,"absolute files with spaces and directories resolve",results);
                Check(Reject(delegate { OpenTarget.Resolve("example file.txt"); }) && Reject(delegate { OpenTarget.Resolve(file.Substring(0,2)+"example file.txt"); }) && Reject(delegate { OpenTarget.Resolve(file.Substring(2)); }),"existing relative drive-relative and rooted-relative paths rejected",results);
                Check(Reject(delegate { OpenTarget.Resolve("javascript:alert(1)"); }) && Reject(delegate { OpenTarget.Resolve("ms-settings:display"); }) && Reject(delegate { OpenTarget.Resolve(new Uri(file).AbsoluteUri); }),"unsupported URL schemes rejected",results);
                Check(Reject(delegate { OpenTarget.Resolve(@"\\.\CON"); }) && Reject(delegate { OpenTarget.Resolve(@"\\?\"+file); }) && Reject(delegate { OpenTarget.Resolve(file+":stream"); }),"device namespaces and alternate data streams rejected",results);
                Check(Reject(delegate { OpenTarget.Resolve(file+"\n"); }) && Reject(delegate { OpenTarget.Resolve("\""+file+"\" /argument"); }),"control characters and command-line strings rejected",results);
                string url="https://example.com/%TEMP%/?q=a%20b";
                Check(OpenTarget.Resolve(url)==url && OpenTarget.Resolve("http://example.com/")=="http://example.com/","web URLs preserve percent content without environment expansion",results);
                File.Delete(file);
                Check(Reject(delegate { OpenTarget.Resolve(file); }),"missing launch targets rejected on execution-time revalidation",results);
                Check(Reject(delegate { Chord.Parse(new string(' ',49)+"A"); }) && Reject(delegate { MacroStep.Parse(new string(' ',4097)+"A"); }),"oversized key and macro inputs rejected before parsing",results);
                Check(MacroStep.Parse(String.Join("\n",Enumerable.Repeat("Ctrl+Shift+A",64))).Count==64,"maximum supported macro remains valid",results);
                Check(Reject(delegate { Json.Read<object>(new string(' ',Json.MaxCharacters+1)); }),"oversized JSON text rejected",results);
                Check(Reject(delegate { Json.Read<object>(new string('[',32)+"0"+new string(']',32)); }),"deeply nested JSON rejected",results);
                File.WriteAllText(json,Json.Write(Preferences.Defaults()));
                Check(Json.ReadFile<Preferences>(json).Items.Count==8,"bounded settings reader accepts existing format",results);
                using(FileStream stream=File.Create(json)) stream.SetLength(Json.MaxFileBytes+1);
                Check(Reject(delegate { Json.ReadFile<Preferences>(json); }),"oversized settings file rejected before reading content",results);
            } finally {
                Environment.CurrentDirectory=previous;
                File.Delete(file); File.Delete(json); Directory.Delete(folder);
            }
            return results;
        }
    }
}

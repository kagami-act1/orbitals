using System;
using System.IO;

namespace OrbitRing {
    static class AppPaths {
        public static bool IsInstalled { get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installation.json")); } }
        public static string DataDirectory { get { return DirectoryFor(AppDomain.CurrentDomain.BaseDirectory,Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),IsInstalled); } }
        internal static string DirectoryFor(string executableDirectory,string localAppData,bool installed) { return installed?Path.Combine(localAppData,"Orbitals"):executableDirectory; }
        public static string PrepareSettings() {
            string directory=DataDirectory; Directory.CreateDirectory(directory);
            string settings=Path.Combine(directory,"settings.json");
            if(IsInstalled) Migrate(AppDomain.CurrentDomain.BaseDirectory,directory);
            return settings;
        }
        internal static void Migrate(string source,string destination) {
            if(String.Equals(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar),Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)) return;
            Directory.CreateDirectory(destination);
            if(File.Exists(Path.Combine(destination,"settings.json"))) return;
            foreach(string name in new[]{"settings.json.bak","settings.json.draft.json","settings.json"}) {
                string from=Path.Combine(source,name),to=Path.Combine(destination,name);
                if(File.Exists(from) && !File.Exists(to)) File.Copy(from,to,false);
            }
        }
    }
}

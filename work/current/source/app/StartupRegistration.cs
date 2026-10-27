using System;
using Microsoft.Win32;

namespace OrbitRing {
    static class StartupRegistration {
        const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run", ValueName="orbitals";
        public static string Command(string executable) { return "\""+executable+"\" --background"; }
        public static string Read() { using(RegistryKey key=Registry.CurrentUser.OpenSubKey(KeyPath)) return key==null?null:key.GetValue(ValueName) as string; }
        public static void Write(string command) {
            using(RegistryKey key=Registry.CurrentUser.CreateSubKey(KeyPath)) {
                if(key==null) throw new Exception("ログイン時の自動起動設定にアクセスできません。");
                if(command==null) key.DeleteValue(ValueName,false); else key.SetValue(ValueName,command,RegistryValueKind.String);
            }
        }
        public static string Desired(bool enabled) { return enabled?Command(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orbitals.exe")):null; }
    }
}

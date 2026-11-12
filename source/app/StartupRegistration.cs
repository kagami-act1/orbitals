using System;
using Microsoft.Win32;

namespace OrbitRing {
    enum StartupState { Off, Registered, Missing, DifferentLocation, LeftRegistered, DisabledByWindows, Unknown }
    static class StartupRegistration {
        const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run", ValueName="orbitals";
        const string ApprovalPath=@"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        public static string Command(string executable) { return "\""+executable+"\" --background"; }
        public static string Read() { using(RegistryKey key=Registry.CurrentUser.OpenSubKey(KeyPath)) return key==null?null:key.GetValue(ValueName) as string; }
        public static void Write(string command) {
            using(RegistryKey key=command==null?Registry.CurrentUser.OpenSubKey(KeyPath,true):Registry.CurrentUser.CreateSubKey(KeyPath)) {
                if(key==null && command==null) return;
                if(key==null) throw new Exception("ログイン時の自動起動設定にアクセスできません。");
                if(command==null) key.DeleteValue(ValueName,false); else key.SetValue(ValueName,command,RegistryValueKind.String);
            }
        }
        public static string Desired(bool enabled) { return enabled?Command(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orbitals.exe")):null; }
        internal static StartupState Evaluate(bool enabled,string registered,string desired,object approval) {
            if(!enabled) return String.IsNullOrEmpty(registered)?StartupState.Off:StartupState.LeftRegistered;
            if(String.IsNullOrEmpty(registered)) return StartupState.Missing;
            if(!String.Equals(registered.Trim(),desired,StringComparison.OrdinalIgnoreCase)) return StartupState.DifferentLocation;
            if(approval==null) return StartupState.Registered;
            byte[] state=approval as byte[];
            if(state==null || state.Length!=12) return StartupState.Unknown;
            uint flag=BitConverter.ToUInt32(state,0);
            if(flag==3) return StartupState.DisabledByWindows;
            if(flag==2) return StartupState.Registered;
            return StartupState.Unknown;
        }
        public static StartupState Status(bool enabled) {
            string registered=Read();
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(ApprovalPath)) return Evaluate(enabled,registered,Desired(true),key==null?null:key.GetValue(ValueName));
        }
        public static string Description(StartupState state) {
            switch(state) {
                case StartupState.Off:return "Windowsへの自動起動登録はありません。";
                case StartupState.Registered:return "Windowsに登録済みです。";
                case StartupState.Missing:return "Windowsへの登録がありません。「保存」で登録します。";
                case StartupState.DifferentLocation:return "以前の起動先が登録されています。「保存」で更新します。";
                case StartupState.LeftRegistered:return "Windowsへの登録が残っています。「保存」で解除します。";
                case StartupState.DisabledByWindows:return "Windows側で無効になっています。スタートアップ設定でOrbitalsをONにしてください。";
                default:return "Windows側の状態を確認できません。スタートアップ設定で確認してください。";
            }
        }
    }
}

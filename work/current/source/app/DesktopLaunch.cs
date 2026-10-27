using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace OrbitRing {
    static class DesktopLaunch {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetFinalPathNameByHandle(IntPtr file,StringBuilder path,uint count,uint flags);
        internal static bool IsRedirected(string path,string physical,string localAppData) {
            string root=Path.GetFullPath(localAppData).TrimEnd(Path.DirectorySeparatorChar)+"\\";
            string logical=Path.GetFullPath(path),actual=physical??""; if(actual.StartsWith("\\\\?\\",StringComparison.Ordinal)) actual=actual.Substring(4);
            if(!logical.StartsWith(root,StringComparison.OrdinalIgnoreCase) || logical.StartsWith(root+"Packages\\",StringComparison.OrdinalIgnoreCase) || !actual.StartsWith(root+"Packages\\",StringComparison.OrdinalIgnoreCase)) return false;
            int familyEnd=actual.IndexOf('\\',root.Length+"Packages\\".Length);
            return familyEnd>=0 && String.Equals(actual.Substring(familyEnd+1),"LocalCache\\Local\\"+logical.Substring(root.Length),StringComparison.OrdinalIgnoreCase);
        }
        static bool IsRedirectedFile(string path,string localAppData) {
            if(!File.Exists(path)) return false;
            using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                StringBuilder physical=new StringBuilder(32768);
                if(GetFinalPathNameByHandle(stream.SafeFileHandle.DangerousGetHandle(),physical,(uint)physical.Capacity,0)==0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return IsRedirected(path,physical.ToString(),localAppData);
            }
        }
        public static bool Required() {
            if(!AppPaths.IsInstalled) return false;
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return IsRedirectedFile(Path.Combine(AppPaths.DataDirectory,"settings.json"),local) || IsRedirectedFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"installation.json"),local);
        }
        static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        internal static void Open(string executable,string arguments,bool background) {
            object shell=null,windows=null,desktop=null,document=null,application=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true)); windows=((dynamic)shell).Windows();
                object location=0,root=0; int handle;
                desktop=((dynamic)windows).FindWindowSW(ref location,ref root,8,out handle,1);
                if(desktop==null) throw new Exception("Windowsのデスクトップを取得できませんでした。");
                document=((dynamic)desktop).Document; application=((dynamic)document).Application;
                ((dynamic)application).ShellExecute(executable,arguments,Path.GetDirectoryName(executable),"open",background?0:1);
            } finally { Release(application); Release(document); Release(desktop); Release(windows); Release(shell); }
        }
        public static void Restart(bool background) { Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"orbitals.exe"),background?"--background":"",background); }
    }
}

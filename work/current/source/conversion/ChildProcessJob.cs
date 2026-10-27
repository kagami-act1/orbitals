using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OrbitRing {
    sealed class ChildProcessJob : IDisposable {
        IntPtr handle;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job,int information,IntPtr data,uint length);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        public ChildProcessJob(Process process,long memory=1024L*1024*1024) {
            handle=CreateJobObject(IntPtr.Zero,null); IntPtr data=Marshal.AllocHGlobal(144);
            try {
                for(int i=0;i<144;i++) Marshal.WriteByte(data,i,0);
                Marshal.WriteInt32(data,16,0x2200); Marshal.WriteInt64(data,120,memory);
                if(handle==IntPtr.Zero || !SetInformationJobObject(handle,9,data,144) || (!AssignProcessToJobObject(handle,process.Handle) && !process.HasExited)) throw new Exception("変換の実行環境を準備できませんでした。");
            } catch { if(!process.HasExited) process.Kill(); Dispose(); throw; } finally { Marshal.FreeHGlobal(data); }
        }
        public void Dispose() { if(handle!=IntPtr.Zero) { CloseHandle(handle); handle=IntPtr.Zero; } }
    }
}

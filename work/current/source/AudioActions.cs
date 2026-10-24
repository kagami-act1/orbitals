using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OrbitRing {
    sealed class AudioDeviceChoice { public string Id,Name; }
    static class AudioActions {
        [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class Enumerator { }
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDevices {
            [PreserveSig] int EnumAudioEndpoints(int flow,uint mask,out ICollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id,out IDevice device);
        }
        [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ICollection { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index,out IDevice device); }
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDevice {
            [PreserveSig] int Activate(ref Guid id,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object value);
            [PreserveSig] int OpenPropertyStore(uint access,out IProperties properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out uint state);
        }
        [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid Format; public uint Id; }
        [StructLayout(LayoutKind.Explicit,Size=24)] struct PropertyValue { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IProperties {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint index,out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key,out PropertyValue value);
        }
        [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropertyValue value);
        [ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] class Policy { }
        [ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IPolicy {
            [PreserveSig] int GetMixFormat(IntPtr device,IntPtr format);
            [PreserveSig] int GetDeviceFormat(IntPtr device,int defaultFormat,IntPtr format);
            [PreserveSig] int ResetDeviceFormat(IntPtr device);
            [PreserveSig] int SetDeviceFormat(IntPtr device,IntPtr endpoint,IntPtr mix);
            [PreserveSig] int GetProcessingPeriod(IntPtr device,int defaultPeriod,IntPtr period,IntPtr minimum);
            [PreserveSig] int SetProcessingPeriod(IntPtr device,IntPtr period);
            [PreserveSig] int GetShareMode(IntPtr device,IntPtr mode);
            [PreserveSig] int SetShareMode(IntPtr device,IntPtr mode);
            [PreserveSig] int GetPropertyValue(IntPtr device,IntPtr key,IntPtr value);
            [PreserveSig] int SetPropertyValue(IntPtr device,IntPtr key,IntPtr value);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string device,int role);
        }
        [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISessionManager {
            [PreserveSig] int GetAudioSessionControl(ref Guid id,uint flags,out IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(ref Guid id,uint flags,out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out ISessions sessions);
        }
        [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISessions { [PreserveSig] int GetCount(out int count); [PreserveSig] int GetSession(int index,out IControl control); }
        [ComImport,Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IControl {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
            [PreserveSig] int SetDisplayName(IntPtr name,IntPtr context);
            [PreserveSig] int GetIconPath(out IntPtr path);
            [PreserveSig] int SetIconPath(IntPtr path,IntPtr context);
            [PreserveSig] int GetGroupingParam(out Guid group);
            [PreserveSig] int SetGroupingParam(ref Guid group,IntPtr context);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
        }
        [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IControl2 {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName(out IntPtr name);
            [PreserveSig] int SetDisplayName(IntPtr name,IntPtr context);
            [PreserveSig] int GetIconPath(out IntPtr path);
            [PreserveSig] int SetIconPath(IntPtr path,IntPtr context);
            [PreserveSig] int GetGroupingParam(out Guid group);
            [PreserveSig] int SetGroupingParam(ref Guid group,IntPtr context);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr events);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr events);
            [PreserveSig] int GetSessionIdentifier(out IntPtr id);
            [PreserveSig] int GetSessionInstanceIdentifier(out IntPtr id);
            [PreserveSig] int GetProcessId(out uint id);
        }
        [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IVolume {
            [PreserveSig] int SetMasterVolume(float volume,ref Guid context);
            [PreserveSig] int GetMasterVolume(out float volume);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute,ref Guid context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
        static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        static string DeviceName(IDevice device) {
            IProperties properties=null; PropertyValue value=new PropertyValue();
            try { Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0,out properties)); PropertyKey key=new PropertyKey { Format=new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),Id=14 }; Marshal.ThrowExceptionForHR(properties.GetValue(ref key,out value)); return value.Type==31?Marshal.PtrToStringUni(value.Pointer):"音声出力機器"; }
            finally { PropVariantClear(ref value); Release(properties); }
        }
        public static List<AudioDeviceChoice> Devices() {
            object enumerator=null; ICollection collection=null; List<AudioDeviceChoice> result=new List<AudioDeviceChoice>();
            try {
                enumerator=new Enumerator(); Marshal.ThrowExceptionForHR(((IDevices)enumerator).EnumAudioEndpoints(0,1,out collection)); uint count; Marshal.ThrowExceptionForHR(collection.GetCount(out count));
                for(uint i=0;i<count;i++) { IDevice device=null; try { Marshal.ThrowExceptionForHR(collection.Item(i,out device)); string id; Marshal.ThrowExceptionForHR(device.GetId(out id)); result.Add(new AudioDeviceChoice { Id=id,Name=DeviceName(device) }); } finally { Release(device); } }
                return result;
            } finally { Release(collection); Release(enumerator); }
        }
        public static void SelectDevice(string id) {
            if(!Devices().Any(d=>d.Id==id)) throw new Exception("選択した音声出力機器が接続されていません。");
            object policy=null,enumerator=null; IDevice current=null;
            try {
                policy=new Policy(); IPolicy config=(IPolicy)policy;
                Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(id,0)); Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(id,1));
                enumerator=new Enumerator(); Marshal.ThrowExceptionForHR(((IDevices)enumerator).GetDefaultAudioEndpoint(0,1,out current)); string actual; Marshal.ThrowExceptionForHR(current.GetId(out actual));
                if(actual!=id) throw new Exception("音声出力先の切り替えを確認できませんでした。");
            } catch(COMException ex) { throw new Exception("音声出力先を切り替えられませんでした。機器の接続を確認してください。",ex); }
            finally { Release(current); Release(enumerator); Release(policy); }
        }
        sealed class Session { public IControl Control; public IVolume Volume; }
        public static float AdjustedVolume(float current,string operation) { return Math.Max(0,Math.Min(1,current+(operation=="up"?.05f:-.05f))); }
        public static void ChangeApplication(IntPtr target,string operation) {
            if(!new[]{"up","down","mute"}.Contains(operation)) throw new Exception("音量の操作が不正です。");
            uint targetId; ExtendedActions.GetWindowThreadProcessId(target,out targetId); string executable=Path.GetFileNameWithoutExtension(ExtendedActions.Executable(target));
            List<Session> matches=new List<Session>(); object enumerator=null; ICollection devices=null;
            try {
                enumerator=new Enumerator(); Marshal.ThrowExceptionForHR(((IDevices)enumerator).EnumAudioEndpoints(0,1,out devices)); uint count; Marshal.ThrowExceptionForHR(devices.GetCount(out count));
                for(uint i=0;i<count;i++) {
                    IDevice device=null; object manager=null; ISessions sessions=null;
                    try {
                        Marshal.ThrowExceptionForHR(devices.Item(i,out device)); Guid iid=typeof(ISessionManager).GUID; Marshal.ThrowExceptionForHR(device.Activate(ref iid,23,IntPtr.Zero,out manager));
                        Marshal.ThrowExceptionForHR(((ISessionManager)manager).GetSessionEnumerator(out sessions)); int n; Marshal.ThrowExceptionForHR(sessions.GetCount(out n));
                        for(int j=0;j<n;j++) {
                            IControl control=null;
                            try {
                                Marshal.ThrowExceptionForHR(sessions.GetSession(j,out control)); uint pid; Marshal.ThrowExceptionForHR(((IControl2)control).GetProcessId(out pid));
                                bool same=pid!=0 && pid==targetId;
                                if(!same && pid!=0 && !String.IsNullOrEmpty(executable)) { try { using(Process process=Process.GetProcessById((int)pid)) same=String.Equals(process.ProcessName,executable,StringComparison.OrdinalIgnoreCase); } catch(ArgumentException) { } }
                                if(same) { matches.Add(new Session { Control=control,Volume=(IVolume)control }); control=null; }
                            } catch(COMException) { } finally { Release(control); }
                        }
                    } finally { Release(sessions); Release(manager); Release(device); }
                }
                if(matches.Count==0) throw new Exception("このアプリの音声が見つかりません。音を再生してから試してください。");
                Guid context=Guid.Empty; bool mute=false;
                if(operation=="mute") foreach(Session session in matches) { bool current; Marshal.ThrowExceptionForHR(session.Volume.GetMute(out current)); if(!current) mute=true; }
                foreach(Session session in matches) {
                    if(operation=="mute") Marshal.ThrowExceptionForHR(session.Volume.SetMute(mute,ref context));
                    else { float volume; Marshal.ThrowExceptionForHR(session.Volume.GetMasterVolume(out volume)); Marshal.ThrowExceptionForHR(session.Volume.SetMasterVolume(AdjustedVolume(volume,operation),ref context)); }
                }
            } finally { foreach(Session session in matches) Release(session.Control); Release(devices); Release(enumerator); }
        }
    }
}

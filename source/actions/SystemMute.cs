using System;
using System.Runtime.InteropServices;

namespace OrbitRing {
    // Default output endpoint or default communications microphone.
    static class SystemMute {
        [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class DeviceEnumerator { }
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDeviceEnumerator {
            [PreserveSig] int EnumAudioEndpoints(int flow,uint mask,out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IDevice device);
        }
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IDevice { [PreserveSig] int Activate(ref Guid id,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object value); }
        [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IEndpointVolume {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float value,IntPtr context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float value,IntPtr context);
            [PreserveSig] int GetMasterVolumeLevel(out float value);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float value);
            [PreserveSig] int SetChannelVolumeLevel(uint channel,float value,IntPtr context);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel,float value,IntPtr context);
            [PreserveSig] int GetChannelVolumeLevel(uint channel,out float value);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel,out float value);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool value,IntPtr context);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool value);
        }
        static bool Access(bool? requested,bool invert,int flow=0) {
            object enumerator=null,endpoint=null; IDevice device=null;
            try {
                enumerator=new DeviceEnumerator();
                Marshal.ThrowExceptionForHR(((IDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(flow,flow==1?2:1,out device));
                Guid id=typeof(IEndpointVolume).GUID;
                Marshal.ThrowExceptionForHR(device.Activate(ref id,23,IntPtr.Zero,out endpoint));
                IEndpointVolume volume=(IEndpointVolume)endpoint; bool current;
                Marshal.ThrowExceptionForHR(volume.GetMute(out current));
                if(invert || requested.HasValue) {
                    bool next=invert?!current:requested.Value;
                    Marshal.ThrowExceptionForHR(volume.SetMute(next,IntPtr.Zero));
                    Marshal.ThrowExceptionForHR(volume.GetMute(out current));
                    if(current!=next) throw new Exception("ミュート状態の変更を確認できませんでした。");
                }
                return current;
            } catch(COMException ex) { throw new Exception(flow==1?"マイクのミュート状態を取得・変更できません。通信用の入力機器を確認してください。":"音声出力のミュート状態を取得・変更できません。出力機器を確認してください。",ex); }
            finally {
                if(endpoint!=null) Marshal.ReleaseComObject(endpoint);
                if(device!=null) Marshal.ReleaseComObject(device);
                if(enumerator!=null) Marshal.ReleaseComObject(enumerator);
            }
        }
        public static void ToggleMicrophone() { Access(null,true,1); }
        public static bool Read(bool microphone=false) { return Access(null,false,microphone?1:0); }
        public static bool? TryRead(bool microphone=false) { try { return Read(microphone); } catch { return null; } }
        public static void Set(bool muted,bool microphone=false) { Access(muted,false,microphone?1:0); }
        public static void Toggle() { Access(null,true); }
    }
}

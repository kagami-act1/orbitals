using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class MacroCaptureBuffer {
        readonly List<string> lines=new List<string>();
        long previous;
        int totalWait;
        public string Text { get { return String.Join("\n",lines); } }
        public bool Add(string chord,long milliseconds) {
            Chord.Parse(chord); if(milliseconds<0) return false;
            long delay=lines.Count==0?0:Math.Max(0,milliseconds-previous-30);
            if(delay>60000-totalWait) return false;
            int waits=(int)((delay+29999)/30000);
            if(lines.Count+waits+1>64) return false;
            while(delay>0) { int part=(int)Math.Min(30000,delay); lines.Add("wait:"+part); totalWait+=part; delay-=part; }
            lines.Add(chord); previous=milliseconds; return true;
        }
    }
    sealed class KeyboardMacroRecorder : IDisposable {
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [StructLayout(LayoutKind.Sequential)] struct KeyData { public uint Key,Scan,Flags,Time; public UIntPtr Extra; }
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string module);
        readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
        readonly Stopwatch watch=new Stopwatch();
        readonly MacroCaptureBuffer buffer=new MacroCaptureBuffer();
        readonly HashSet<ushort> modifiers=new HashSet<ushort>();
        HookProc callback;
        IntPtr hook;
        ushort endKey;
        bool usedModifier,finishing;
        public Action<bool,string> Finished;
        public void Start() {
            foreach(ushort key in new ushort[]{0xA0,0xA1,0xA2,0xA3,0xA4,0xA5,0x5B,0x5C}) if((Native.GetAsyncKeyState(key)&0x8000)!=0) modifiers.Add(key);
            usedModifier=modifiers.Count>0;
            callback=Keyboard; hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) throw new Exception("キー操作の記録を開始できませんでした。"); watch.Start();
        }
        static string Modifier(ushort key) { return key==0x11 || key==0xA2 || key==0xA3?"Ctrl":key==0x10 || key==0xA0 || key==0xA1?"Shift":key==0x12 || key==0xA4 || key==0xA5?"Alt":key==0x5B || key==0x5C?"Win":null; }
        public static string ModifierChord(IEnumerable<ushort> keys,ushort released) { string last=Modifier(released); return String.Join("",new[]{"Ctrl","Alt","Shift","Win"}.Where(name=>name!=last && keys.Any(k=>Modifier(k)==name)).Select(name=>name+"+"))+last; }
        string ChordName(ushort key) {
            string prefix=""; foreach(string name in new[]{"Ctrl","Alt","Shift","Win"}) if(modifiers.Any(k=>Modifier(k)==name)) prefix+=name+"+";
            Key value=KeyInterop.KeyFromVirtualKey(key); string last=value.ToString(); if(last.Length==2 && last[0]=='D' && Char.IsDigit(last[1])) last=last.Substring(1);
            if(last=="Return") last="Enter"; if(last=="Back") last="Backspace"; if(last=="Prior") last="PageUp"; if(last=="Next") last="PageDown"; if(last=="Snapshot") last="PrintScreen";
            return prefix+last;
        }
        void Add(string chord) { if(!buffer.Add(chord,watch.ElapsedMilliseconds)) Finish(false); }
        void Finish(bool cancel) {
            if(finishing) return; finishing=true;
            dispatcher.BeginInvoke(new Action(delegate { Dispose(); if(Finished!=null) Finished(cancel,buffer.Text); }));
        }
        IntPtr Keyboard(int code,IntPtr message,IntPtr pointer) {
            if(code<0 || finishing) return CallNextHookEx(hook,code,message,pointer);
            KeyData data=(KeyData)Marshal.PtrToStructure(pointer,typeof(KeyData));
            if((data.Flags&0x10)!=0 || data.Extra==Native.OwnInput) return CallNextHookEx(hook,code,message,pointer);
            ushort key=(ushort)data.Key; bool up=message.ToInt32()==0x101 || message.ToInt32()==0x105;
            try {
                if(key==0x7B || key==27) { if(!up) endKey=key; else if(endKey==key) Finish(key==27); return new IntPtr(1); }
                if(endKey!=0) return CallNextHookEx(hook,code,message,pointer);
                string modifier=Modifier(key);
                if(modifier!=null) { if(!up) modifiers.Add(key); else { if(!usedModifier && modifiers.Contains(key)) { Add(ModifierChord(modifiers,key)); usedModifier=true; } modifiers.Remove(key); if(modifiers.Count==0) usedModifier=false; } }
                else if(!up) { usedModifier=modifiers.Count>0; string chord=ChordName(key); Chord.Parse(chord); Add(chord); }
            } catch { Finish(true); }
            return CallNextHookEx(hook,code,message,pointer);
        }
        public string Stop() { finishing=true; Dispose(); return buffer.Text; }
        public void Dispose() { if(hook!=IntPtr.Zero) { UnhookWindowsHookEx(hook); hook=IntPtr.Zero; } watch.Stop(); }
    }
}

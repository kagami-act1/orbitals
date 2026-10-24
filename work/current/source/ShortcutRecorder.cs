using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class ShortcutCapture {
        readonly HashSet<ushort> down=new HashSet<ushort>();
        readonly bool trigger;
        public string Value { get; private set; }
        public string Error { get; private set; }
        public bool Cancelled { get; private set; }
        public bool Finished { get; private set; }
        public ShortcutCapture(bool invocation,IEnumerable<ushort> held) { trigger=invocation; foreach(ushort key in held) down.Add(key); }
        public static string Modifier(ushort key) { return key==17 || key==162 || key==163?"Ctrl":key==16 || key==160 || key==161?"Shift":key==18 || key==164 || key==165?"Alt":key==91 || key==92?"Win":null; }
        public static string Name(ushort key) {
            string name=KeyInterop.KeyFromVirtualKey(key).ToString(); if(name.Length==2 && name[0]=='D' && Char.IsDigit(name[1])) name=name.Substring(1);
            return name=="Return"?"Enter":name=="Back"?"Backspace":name=="Prior"?"PageUp":name=="Next"?"PageDown":name=="Snapshot"?"PrintScreen":name;
        }
        void Accept(string text) {
            Chord chord=Chord.Parse(text);
            if(trigger && chord.Modifiers==0 && (chord.Key<0x70 || chord.Key>0x87)) { Error="呼び出しにはCtrl・Alt・Shift・Winを含めるか、F1～F24を指定してください。"; return; }
            Value=text;
        }
        public void Feed(ushort key,bool up) {
            if(Finished) return; Error=null;
            if(!up) {
                if(!down.Add(key)) return;
                if(key==27) Cancelled=true;
                else if(Value==null && !Cancelled && Modifier(key)==null) {
                    string prefix=String.Concat(new[]{"Ctrl","Alt","Shift","Win"}.Where(m=>down.Any(k=>Modifier(k)==m)).Select(m=>m+"+"));
                    try { Accept(prefix+Name(key)); } catch(Exception ex) { Error=ex.Message; }
                }
            } else {
                if(!trigger && !Cancelled && Value==null && Modifier(key)!=null && down.Contains(key) && down.All(k=>Modifier(k)!=null)) {
                    try { Accept(KeyboardMacroRecorder.ModifierChord(down,key)); } catch(Exception ex) { Error=ex.Message; }
                }
                down.Remove(key);
                if(down.Count==0 && (Value!=null || Cancelled)) Finished=true;
            }
        }
    }
    sealed class KeyboardShortcutRecorder : IDisposable {
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        [StructLayout(LayoutKind.Sequential)] struct KeyData { public uint Key,Scan,Flags,Time; public UIntPtr Extra; }
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr message,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string module);
        readonly Dispatcher dispatcher=Dispatcher.CurrentDispatcher;
        readonly HashSet<ushort> heldBefore=new HashSet<ushort>();
        readonly HashSet<ushort> blocked=new HashSet<ushort>();
        readonly IntPtr window;
        readonly ShortcutCapture capture;
        HookProc callback;
        IntPtr hook;
        bool finishing;
        public Action<bool,string> Finished;
        public Action<string> Invalid;
        public KeyboardShortcutRecorder(IntPtr target,bool invocation) {
            window=target;
            foreach(ushort key in new ushort[]{160,161,162,163,164,165,91,92}) if((Native.GetAsyncKeyState(key)&0x8000)!=0) heldBefore.Add(key);
            capture=new ShortcutCapture(invocation,heldBefore);
        }
        public void Start() { callback=Keyboard; hook=SetWindowsHookEx(13,callback,GetModuleHandle(null),0); if(hook==IntPtr.Zero) throw new Exception("キーの記録を開始できませんでした。"); }
        void Finish(bool cancelled) {
            if(finishing) return; finishing=true;
            dispatcher.BeginInvoke(new Action(delegate { Dispose(); Action<bool,string> finished=Finished; if(finished!=null) finished(cancelled,capture.Value); }));
        }
        IntPtr Keyboard(int code,IntPtr message,IntPtr pointer) {
            if(code<0 || finishing) return CallNextHookEx(hook,code,message,pointer);
            if(Native.GetForegroundWindow()!=window) { Finish(true); return CallNextHookEx(hook,code,message,pointer); }
            KeyData data=(KeyData)Marshal.PtrToStructure(pointer,typeof(KeyData));
            bool up=message.ToInt32()==0x101 || message.ToInt32()==0x105; ushort key=(ushort)data.Key;
            if(!up && !heldBefore.Contains(key)) blocked.Add(key);
            bool releaseExisting=up && (heldBefore.Remove(key) || !blocked.Remove(key));
            try {
                capture.Feed(key,up);
                if(capture.Error!=null) { string error=capture.Error; dispatcher.BeginInvoke(new Action(delegate { if(!finishing && Invalid!=null) Invalid(error); })); }
                if(capture.Finished) Finish(capture.Cancelled);
            } catch { Finish(true); }
            return releaseExisting?CallNextHookEx(hook,code,message,pointer):new IntPtr(1);
        }
        public void Dispose() { finishing=true; if(hook!=IntPtr.Zero) { UnhookWindowsHookEx(hook); hook=IntPtr.Zero; } }
    }
}

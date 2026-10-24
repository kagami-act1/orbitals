using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace OrbitRing {
    sealed class OrbitFrame {
        public int Session;
        public Native.POINT Origin;
        public Point[] Points;
        public string Hint;
        public bool Visible;
    }
    // Hooks have their own message loop. No WPF, scripts, IO, or action execution
    // on the callback path; the UI consumes at most one queued frame at a time.
    sealed class OrbitEngine : IDisposable {
        delegate IntPtr HookProc(int code,IntPtr w,IntPtr l);
        [StructLayout(LayoutKind.Sequential)] struct MouseData { public Native.POINT Point; public uint Data,Flags,Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct KeyboardData { public uint Key,Scan,Flags,Time; public UIntPtr Extra; }
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr w,IntPtr l);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
        [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,IntPtr w,IntPtr l);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
        readonly Controller owner;
        readonly Dispatcher dispatcher;
        readonly Thread thread;
        readonly ManualResetEvent ready=new ManualResetEvent(false);
        readonly object gate=new object();
        HookProc mouseProc,keyProc;
        IntPtr mouseHook,keyHook;
        uint threadId;
        volatile Preferences config;
        volatile bool stopped;
        Exception startupError;
        Forms.Timer guard;
        Forms.Control hookQueue;
        bool active,usedChord,hold,swallowLeftUp,swallowEscapeUp;
        volatile bool cancelled;
        volatile bool holdLive;
        int session,buttonVk,frameQueued,pathRevision,recognizedRevision;
        uint started,lastPublished;
        OrbitBinding currentMatch;
        string executable,modifier;
        IntPtr destination;
        Native.POINT origin;
        DirectionStroke stroke;
        OrbitPreferences current;
        readonly List<Point> points=new List<Point>(128);
        OrbitFrame frame;
        public bool HoldLive { get { return holdLive; } }
        public OrbitEngine(Controller value) {
            owner=value; dispatcher=Dispatcher.CurrentDispatcher; config=owner.Config;
            thread=new Thread(Run) { IsBackground=true,Name="orbit input" }; thread.SetApartmentState(ApartmentState.STA); thread.Start();
            if(!ready.WaitOne(5000)) throw new Exception("orbitの入力監視を開始できません。");
            if(startupError!=null) throw startupError;
        }
        public void Update(Preferences value) { config=value; }
        void Run() {
            try {
                threadId=GetCurrentThreadId();
                // Create the thread's message queue before publishing ready.
                using(Forms.Control queue=new Forms.Control()) {
                    hookQueue=queue;
                    IntPtr handle=queue.Handle;
                    mouseProc=Mouse; keyProc=Keyboard;
                    mouseHook=SetWindowsHookEx(14,mouseProc,GetModuleHandle(null),0);
                    keyHook=SetWindowsHookEx(13,keyProc,GetModuleHandle(null),0);
                    if(mouseHook==IntPtr.Zero || keyHook==IntPtr.Zero) throw new Exception("orbitの入力監視を登録できません。");
                    guard=new Forms.Timer { Interval=80 };
                    guard.Tick+=delegate {
                        if(!active) { guard.Stop(); return; }
                        // Suppressed button-downs do not update Windows' async key
                        // state. Only the matching hook button-up ends a stroke.
                        if(Native.GetForegroundWindow()!=destination || unchecked((uint)Environment.TickCount-started)>(hold?120000u:10000u) || !owner.OrbitAllowed) { Cancel(); return; }
                        if(!cancelled && !hold && !usedChord && pathRevision!=recognizedRevision && GestureShape.Extent(points)>=current.Distance) {
                            recognizedRevision=pathRevision;
                            currentMatch=current.ResolveDrawing(executable,stroke.Pattern,points,modifier);
                            if(currentMatch!=null && currentMatch.Action.Kind=="hold") { hold=true; holdLive=true; Publish(false,""); Dispatch(currentMatch.Action,true); }
                            else Publish(current.Trail,currentMatch==null?"未登録":currentMatch.Action.Label);
                        }
                    };
                    ready.Set(); Forms.Application.Run();
                }
            } catch(Exception ex) { startupError=ex; ready.Set(); }
            finally { holdLive=false; if(guard!=null) guard.Dispose(); if(mouseHook!=IntPtr.Zero) UnhookWindowsHookEx(mouseHook); if(keyHook!=IntPtr.Zero) UnhookWindowsHookEx(keyHook); }
        }
        static int Vk(string button) { return button=="Right"?2:button=="Middle"?4:button=="X1"?5:6; }
        static int MouseButton(int msg,uint data) { return msg==0x204 || msg==0x205?2:msg==0x207 || msg==0x208?4:msg==0x20B || msg==0x20C?((data>>16)==1?5:6):0; }
        static bool Down(int msg) { return msg==0x204 || msg==0x207 || msg==0x20B; }
        static bool Up(int msg) { return msg==0x205 || msg==0x208 || msg==0x20C; }
        static string Modifier() { if((Native.GetAsyncKeyState(17)&0x8000)!=0) return "Ctrl"; if((Native.GetAsyncKeyState(18)&0x8000)!=0) return "Alt"; if((Native.GetAsyncKeyState(16)&0x8000)!=0) return "Shift"; return ""; }
        IntPtr Mouse(int code,IntPtr w,IntPtr l) {
            if(code<0 || stopped) return CallNextHookEx(mouseHook,code,w,l);
            int msg=w.ToInt32();
            if(!active && !swallowLeftUp && !Down(msg)) return CallNextHookEx(mouseHook,code,w,l);
            MouseData data=(MouseData)Marshal.PtrToStructure(l,typeof(MouseData));
            if(data.Extra==Native.OwnInput) return CallNextHookEx(mouseHook,code,w,l);
            try {
                if(msg==0x202 && swallowLeftUp) { swallowLeftUp=false; return new IntPtr(1); }
                if(!active) {
                    Preferences snapshot=config; OrbitPreferences next=snapshot.Orbit;
                    if(!next.Enabled || !owner.OrbitAllowed || !Down(msg) || MouseButton(msg,data.Data)!=Vk(next.Button) || (Native.GetAsyncKeyState(1)&0x8000)!=0) return CallNextHookEx(mouseHook,code,w,l);
                    IntPtr target=Native.GetForegroundWindow(); uint pid; ExtendedActions.GetWindowThreadProcessId(target,out pid);
                    if(pid==owner.ProcessId) return CallNextHookEx(mouseHook,code,w,l);
                    string exe=ExtendedActions.Executable(target);
                    if(String.IsNullOrEmpty(exe) || next.IsOrbitExcluded(exe)) return CallNextHookEx(mouseHook,code,w,l);
                    current=ApplicationProfiles.Orbit(snapshot,exe); destination=target; executable=exe; modifier=Modifier(); origin=data.Point; buttonVk=Vk(next.Button);
                    active=true; cancelled=false; usedChord=false; hold=false; holdLive=false; currentMatch=null; pathRevision=0; recognizedRevision=-1; started=unchecked((uint)Environment.TickCount); lastPublished=started; session++;
                    stroke=new DirectionStroke(new Point(origin.X,origin.Y),next.Distance); points.Clear(); points.Add(new Point(origin.X,origin.Y)); guard.Start();
                    return new IntPtr(1);
                }
                if(Native.GetForegroundWindow()!=destination || !owner.OrbitAllowed) Cancel();
                if(Up(msg) && MouseButton(msg,data.Data)==buttonVk) { if(!hold) GestureShape.Append(points,new Point(data.Point.X,data.Point.Y)); End(true); return new IntPtr(1); }
                if(cancelled) return CallNextHookEx(mouseHook,code,w,l);
                if(msg==0x200 && !hold && !usedChord) {
                    Point p=new Point(data.Point.X,data.Point.Y);
                    if((points[points.Count-1]-p).Length<2) return CallNextHookEx(mouseHook,code,w,l);
                    stroke.Add(p); GestureShape.Append(points,p); pathRevision++;
                    uint now=unchecked((uint)Environment.TickCount);
                    if(unchecked(now-lastPublished)>=16 && GestureShape.Extent(points)>=current.Distance) { lastPublished=now; Publish(current.Trail,currentMatch==null?"未登録":currentMatch.Action.Label); }
                } else if(!hold && (msg==0x20A || msg==0x201)) {
                    string pattern=GestureInput.ChordPattern(msg,data.Data);
                    OrbitBinding binding=current.Resolve(executable,pattern,modifier);
                    if(binding!=null) {
                        usedChord=true; if(msg==0x201) swallowLeftUp=true;
                        Dispatch(binding.Action,false); Publish(false,""); return new IntPtr(1);
                    }
                    Cancel();
                }
            } catch { Cancel(); }
            return CallNextHookEx(mouseHook,code,w,l);
        }
        IntPtr Keyboard(int code,IntPtr w,IntPtr l) {
            if(code>=0 && !stopped && (active || swallowEscapeUp)) {
                KeyboardData data=(KeyboardData)Marshal.PtrToStructure(l,typeof(KeyboardData));
                if(data.Key==27 && data.Extra!=Native.OwnInput) {
                    bool up=w.ToInt32()==0x101 || w.ToInt32()==0x105;
                    if(up && swallowEscapeUp) { swallowEscapeUp=false; return new IntPtr(1); }
                    if(!up && active) { swallowEscapeUp=true; Cancel(); return new IntPtr(1); }
                }
            }
            return CallNextHookEx(keyHook,code,w,l);
        }
        void Dispatch(ActionItem action,bool holding) {
            if(action.Kind=="none") return;
            ActionItem value=action; IntPtr target=destination; Native.POINT start=origin; int id=session;
            dispatcher.BeginInvoke(new Action(delegate { if(!stopped && OrbitPolicy.MayDispatch(id,session,cancelled,holding,holdLive)) owner.ExecuteOrbit(value,target,start,holding); }));
        }
        void Cancel() { cancelled=true; holdLive=false; Publish(false,""); }
        void End(bool released) {
            OrbitRelease decision=OrbitPolicy.Release(cancelled,hold,usedChord,false,GestureShape.Extent(points)>=current.Distance?"drawing":"",released);
            bool click=decision==OrbitRelease.Click;
            OrbitBinding binding=decision==OrbitRelease.Action?current.ResolveDrawing(executable,stroke.Pattern,points,modifier):null;
            active=false; holdLive=false; guard.Stop(); Publish(false,"");
            if(released && binding!=null && binding.Action.Kind!="hold") Dispatch(binding.Action,false);
            else if(released && click && Native.GetForegroundWindow()==destination) {
                uint down=buttonVk==2?8u:buttonVk==4?32u:128u,up=down*2;
                uint extra=buttonVk==5?1u:buttonVk==6?2u:0;
                Native.INPUT[] input={new Native.INPUT {type=0,data=new Native.INPUTUNION {mouse=new Native.MOUSEINPUT {flags=down,mouseData=extra,extra=Native.OwnInput}}},new Native.INPUT {type=0,data=new Native.INPUTUNION {mouse=new Native.MOUSEINPUT {flags=up,mouseData=extra,extra=Native.OwnInput}}}};
                // Replay only after the suppressed physical up has left the hook.
                // Reentrant injection here can lose the down transition in clients.
                IntPtr clickTarget=destination; int clickSession=session;
                hookQueue.BeginInvoke(new Action(delegate {
                    if(!stopped && !active && clickSession==session && Native.GetForegroundWindow()==clickTarget)
                        Native.SendInput(2,input,Marshal.SizeOf(typeof(Native.INPUT)));
                }));
            }
        }
        void Publish(bool visible,string hint) {
            lock(gate) { frame=new OrbitFrame { Session=session,Origin=origin,Visible=visible,Hint=hint,Points=visible?points.ToArray():null }; }
            if(Interlocked.Exchange(ref frameQueued,1)!=0) return;
            dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {
                OrbitFrame value; lock(gate) { value=frame; Interlocked.Exchange(ref frameQueued,0); }
                if(!stopped) owner.ShowOrbitFrame(value);
            }));
        }
        public void Dispose() { stopped=true; holdLive=false; if(threadId!=0) PostThreadMessage(threadId,0x12,IntPtr.Zero,IntPtr.Zero); thread.Join(1000); ready.Dispose(); }
    }
}

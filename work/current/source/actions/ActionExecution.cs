using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    sealed class MacroStep {
        public string Kind { get; set; }
        public string Value { get; set; }
        public int Delay { get; set; }
        public string Application { get; set; }
        public string Title { get; set; }
        public int Timeout { get; set; }
        [System.Web.Script.Serialization.ScriptIgnore] public string Key { get { return Kind=="shortcut"?Value:null; } set { Kind="shortcut"; Value=value; } }
        public static List<MacroStep> Parse(string text) { return MacroDefinition.Parse(text); }
    }
    // Injectable input boundary lets cancellation and key release be tested without typing.
    interface IActionInput { bool IsDown(ushort key); void Send(ushort key,bool up); }
    sealed class SystemActionInput : IActionInput {
        public bool IsDown(ushort key) { return (Native.GetAsyncKeyState(key)&0x8000)!=0; }
        public void Send(ushort key,bool up) {
            Native.INPUT[] input={Native.KeyInput(key,up)};
            if(Native.SendInput(1,input,System.Runtime.InteropServices.Marshal.SizeOf(typeof(Native.INPUT)))!=1)
                throw new Exception("キーを送信できませんでした。操作先の権限を確認してください。");
        }
    }
    sealed class ActionPlayback {
        readonly IActionInput input;
        readonly List<ushort> held=new List<ushort>();
        List<MacroStep> steps; int index; DateTime due;
        DateTime? windowDeadline;
        public IMacroActions MacroActions { get; set; }
        public int StepNumber { get { return steps==null?0:Math.Min(index+1,steps.Count); } }
        public int StepCount { get { return steps==null?0:steps.Count; } }
        public bool Active { get; private set; }
        public bool Completed { get; private set; }
        public bool NeedsRelease { get { return held.Count>0; } }
        public ActionPlayback(IActionInput value) { input=value; }
        void Down(Chord chord) {
            foreach(Native.INPUT key in chord.Inputs().Where(v=>(v.data.key.flags&2)==0)) {
                ushort vk=key.data.key.vk;
                if(input.IsDown(vk)) throw new Exception("指定キーが既に押されています。キーを離してから試してください。");
                held.Add(vk); input.Send(vk,false);
            }
        }
        void Release() {
            // Keep failed releases tracked for the next cleanup attempt.
            Exception error=null;
            for(int i=held.Count-1;i>=0;i--) try { input.Send(held[i],true); held.RemoveAt(i); } catch(Exception ex) { error=ex; }
            if(error!=null) throw error;
        }
        public void StartHold(string key) {
            Stop(); Active=true;
            try { Down(Chord.Parse(key)); } catch { Stop(); throw; }
        }
        public void Press(string key) {
            Stop();
            try { Down(Chord.Parse(key)); Release(); } catch { Stop(); throw; }
        }
        public void StartMacro(string text,DateTime now) {
            Stop(); steps=MacroStep.Parse(text); index=0; due=now; Active=true;
        }
        public void Tick(DateTime now,bool targetValid,bool released) {
            if(!Active && NeedsRelease) { Stop(); return; }
            if(!Active) return;
            bool waitingWindow=steps!=null && index<steps.Count && steps[index].Kind=="wait-window";
            if(released || (!targetValid && !waitingWindow && (steps==null || index<steps.Count))) { Stop(); return; }
            if(steps==null || now<due) return;
            try {
                if(index>=steps.Count) { Stop(); Completed=true; return; }
                MacroStep step=steps[index];
                if(step.Kind=="wait-window") {
                    if(MacroActions==null) throw new Exception("ウィンドウ待機を開始できません。");
                    if(!windowDeadline.HasValue) windowDeadline=now.AddMilliseconds(step.Timeout);
                    if(!MacroActions.SelectWindow(step)) { if(now>=windowDeadline.Value) throw new TimeoutException("指定したウィンドウが前面に表示されませんでした。"); return; }
                    windowDeadline=null; index++; due=now.AddMilliseconds(30); return;
                }
                index++;
                if(step.Key!=null) { Down(Chord.Parse(step.Key)); Release(); due=now.AddMilliseconds(30); }
                else if(step.Kind=="wait") due=now.AddMilliseconds(step.Delay);
                else { if(MacroActions==null) throw new Exception("このステップを実行できません。"); MacroActions.Execute(step); due=now.AddMilliseconds(30); }
            } catch(Exception ex) { int failed=waitingWindow?index+1:index; Stop(); throw new Exception("ステップ "+failed+"："+ex.Message,ex); }
        }
        public void Stop() { Completed=false; Active=false; steps=null; windowDeadline=null; Release(); }
    }
}

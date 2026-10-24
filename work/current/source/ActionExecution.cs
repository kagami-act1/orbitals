using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    sealed class MacroStep {
        public string Key;
        public int Delay;
        public static List<MacroStep> Parse(string text) {
            if(text!=null && text.Length>4096) throw new Exception("マクロの内容が長すぎます。");
            List<MacroStep> result=new List<MacroStep>(); int total=0;
            foreach(string raw in (text??"").Split(new char[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)) {
                string line=raw.Trim(); if(line.Length==0) continue;
                if(result.Count==64) throw new Exception("マクロには1〜64個のステップを指定してください。");
                if(line.StartsWith("wait:",StringComparison.OrdinalIgnoreCase)) {
                    int delay; if(!Int32.TryParse(line.Substring(5).Trim(),out delay) || delay<1 || delay>30000) throw new Exception("待機時間は1〜30000ミリ秒で指定してください。");
                    total+=delay; result.Add(new MacroStep { Delay=delay });
                } else { Chord.Parse(line); result.Add(new MacroStep { Key=line }); }
            }
            if(result.Count==0) throw new Exception("マクロには1〜64個のステップを指定してください。");
            if(total>60000) throw new Exception("マクロの待機時間は合計60秒以内にしてください。");
            return result;
        }
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
            if(!targetValid || released) { Stop(); return; }
            if(steps==null || now<due) return;
            try {
                if(index>=steps.Count) { Stop(); Completed=true; return; }
                MacroStep step=steps[index++];
                if(step.Key!=null) { Down(Chord.Parse(step.Key)); Release(); due=now.AddMilliseconds(30); }
                else due=now.AddMilliseconds(step.Delay);
            } catch { Stop(); throw; }
        }
        public void Stop() { Completed=false; Active=false; steps=null; Release(); }
    }
}

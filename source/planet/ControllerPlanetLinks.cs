using System;
using System.Linq;
using System.Windows.Threading;

namespace OrbitRing {
    sealed partial class Controller {
        void QueueLinkedPlanet(string reference,IntPtr destination,OrbitEngine gesture=null) {
            int? index=PlanetLinks.Index(Config,reference);
            if(!index.HasValue) { Notify("指定したplanetが見つかりません。orbitのアクションで開くplanetを選び直してください。"); return; }
            if(Diagnostic) return;
            Func<bool> current=gesture==null?null:gesture.ActionGuard();
            HideRing(); CloseOrbitFrame(); busy=true;
            if(!escapeRegistered) escapeRegistered=Native.RegisterHotKey(receiver.Handle,100,0x4000,27);
            DateTime deadline=DateTime.UtcNow.AddSeconds(3);
            pending=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(20) };
            pending.Tick+=delegate {
                if((current!=null && !current()) || !Native.IsWindow(destination) || Native.GetForegroundWindow()!=destination || (Native.GetAsyncKeyState(27)&0x8000)!=0) { HideRing(); return; }
                bool pressed=(gesture!=null && gesture.TriggerDown) || Native.ModifiersHeld() || new[]{1,2,4,5,6}.Any(key=>(Native.GetAsyncKeyState(key)&0x8000)!=0);
                if(pressed) { if(DateTime.UtcNow>=deadline) { HideRing(); Notify("操作中のボタンを離してから、ジェスチャーをやり直してください。"); } return; }
                int? selectedPlanet=PlanetLinks.Index(Config,reference); HideRing();
                if(!selectedPlanet.HasValue) { Notify("指定したplanetが削除されています。開くplanetを選び直してください。"); return; }
                ShowRing(false,selectedPlanet.Value,null,true,true);
            }; pending.Start();
        }
    }
}

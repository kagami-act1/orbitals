using System;
using System.Linq;
using System.Windows.Threading;

namespace OrbitRing {
    sealed partial class SettingsWindow {
        DispatcherTimer draftTimer;
        void InitializeDraft() {
            draftTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(650) };
            draftTimer.Tick+=delegate { draftTimer.Stop(); SaveWorkingCopy(); };
            if(owner.Diagnostic) return;
            try {
                WorkingCopyRecord record=WorkingCopy.Read(owner.DraftPath); if(record==null) return;
                if(WorkingCopy.Fingerprint(record.Config)==WorkingCopy.Fingerprint(owner.Config)) { WorkingCopy.Delete(owner.DraftPath); return; }
                draft=record.Config; editorApplication=record.Application; page=Math.Max(0,Math.Min(5,record.Page)); editorPlanet=Math.Max(0,Math.Min(draft.Planets.Count,record.Planet)); selected=Math.Max(0,Math.Min(7,record.Selected)); gestureIndex=Math.Max(0,Math.Min(CurrentBindings.Count-1,record.Gesture));
                stackPath.Clear(); foreach(int slot in record.StackPath) { if(slot<0 || slot>=CurrentPlanetItems.Count || CurrentPlanetItems[slot]==null || CurrentPlanetItems[slot].Kind!="stack" || !PlanetStack.ValidChildren(CurrentPlanetItems[slot])) break; stackPath.Add(slot); } selected=Math.Max(0,Math.Min(record.Selected,CurrentPlanetItems.Count-1));
                RefreshAll(); Changed("下書きを復元しました。「保存」するまで実際の操作には反映されません。");
            } catch(Exception ex) { status.Text="下書きを復元できませんでした。"+ex.Message; }
        }
        void ScheduleDraft() { if(owner.Diagnostic || draftTimer==null) return; draftTimer.Stop(); draftTimer.Start(); }
        bool SaveWorkingCopy() {
            if(owner.Diagnostic) return true;
            try {
                if(!Dirty()) WorkingCopy.Delete(owner.DraftPath);
                else WorkingCopy.Write(owner.DraftPath,new WorkingCopyRecord { Version=1,SavedFingerprint=WorkingCopy.Fingerprint(owner.Config),Config=draft,Page=page,Planet=editorPlanet,Gesture=gestureIndex,Selected=selected,StackPath=stackPath.ToArray(),Application=editorApplication });
                return true;
            } catch(Exception ex) { status.Text="下書きを保存できませんでした。"+ex.Message; return false; }
        }
        void ClearWorkingCopy() { if(!owner.Diagnostic) WorkingCopy.Delete(owner.DraftPath); }
    }
}

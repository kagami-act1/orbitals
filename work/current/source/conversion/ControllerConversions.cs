using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class ConversionActivity {
        public string Name,Status,Directory,Output;
        public readonly CancellationTokenSource Cancel=new CancellationTokenSource();
        public bool Running=true;
        public ConversionResult Result;
        public void RequestCancel() { if(!Running) return; Status="キャンセルしています…"; Cancel.Cancel(); try { if(System.IO.Directory.Exists(Directory)) File.WriteAllText(Path.Combine(Directory,"cancel"),""); } catch(IOException) { } }
    }
    sealed partial class Controller {
        ConversionSession conversionPlanets;
        ConversionActivityWindow conversionActivity;
        readonly List<ConversionActivity> conversionJobs=new List<ConversionActivity>();
        readonly SemaphoreSlim conversionGate=new SemaphoreSlim(1,1);
        DispatcherTimer conversionProgress;
        bool disposingConversions;
        public void OpenConversion(IntPtr source,string initial="") {
            if(initial=="pdf-extract") { OpenConversionDetails(source,initial); return; }
            Native.POINT point; if(!Native.GetCursorPos(out point)) { Notify("カーソル位置を取得できませんでした。"); return; }
            ShowConversionPlanets(point,false,initial);
        }
        void ShowConversionPlanets(Native.POINT point,bool probe=false,string initial="",Action fallback=null) {
            HideRing(); if(MacroRecording || ShortcutRecording) return; if(idleCleanup!=null) idleCleanup.Stop();
            ConversionSession session=new ConversionSession(Config,point,probe,initial,QueueDroppedFiles,PickConversionFiles,fallback); conversionPlanets=session;
            session.Closed+=delegate { if(Object.ReferenceEquals(conversionPlanets,session)) conversionPlanets=null; if(escapeRegistered && ring==null && conversionPlanets==null) { Native.UnregisterHotKey(receiver.Handle,100); escapeRegistered=false; } if(settings==null && idleCleanup!=null) idleCleanup.Start(); };
            if(receiver!=null && !escapeRegistered) escapeRegistered=Native.RegisterHotKey(receiver.Handle,100,0x4000,27);
        }
        void CloseConversionPlanets() { if(conversionPlanets!=null) { if(disposingConversions) conversionPlanets.Close(); else conversionPlanets.Cancel(); } }
        void PickConversionFiles(ConversionChoice choice) {
            var dialog=new Microsoft.Win32.OpenFileDialog { Title=choice.Label+"：ファイルを選択",Filter=choice.Operation=="zip-extract"?"ZIPファイル|*.zip":choice.Operation=="zip"?"すべてのファイル|*.*":FileConversion.Filter,Multiselect=true };
            if(dialog.ShowDialog()!=true) return;
            System.Windows.DataObject data=new System.Windows.DataObject(System.Windows.DataFormats.FileDrop,dialog.FileNames); DropOffer offer=DropFiles.Inspect(data);
            if(offer.Kind==DropKind.None || !ConversionCatalog.Supports(choice.Operation,offer.Kind,offer.Count)) { OrbitDialog.Info(null,"ファイルを選び直してください",offer.Kind==DropKind.None?offer.Reason:"選んだファイルはこの操作で処理できません。"); return; }
            DropCapture capture=new DropCapture(data,offer); if(!QueueDroppedFiles(capture,offer,choice)) capture.Dispose();
        }
        bool QueueDroppedFiles(DropCapture capture,DropOffer offer,ConversionChoice choice) {
            if(conversionJobs.Count(j=>j.Running)>=3) { Notify("3件を変換中です。完了してから、もう一度ドロップしてください。"); return false; }
            ConversionActivity activity=new ConversionActivity { Name=(offer.Count>1?offer.Count+"件":offer.Names==null?"画像":offer.Names[0])+" → "+choice.Label,Status="ファイルを受け取っています…",Directory=Path.Combine(Path.GetTempPath(),"orbitals-drop-"+Guid.NewGuid().ToString("N")) };
            conversionJobs.Insert(0,activity); while(conversionJobs.Count>15) { ConversionActivity old=conversionJobs.LastOrDefault(j=>!j.Running); if(old==null) break; old.Cancel.Dispose(); conversionJobs.Remove(old); }
            // The OLE Drop callback must return before COM sources are read or a destination dialog opens.
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate { ConvertDroppedFiles(capture,choice,activity); })); return true;
        }
        async void ConvertDroppedFiles(DropCapture capture,ConversionChoice choice,ConversionActivity activity) {
            try {
                ShowConversionActivity(true); StartConversionProgress(); ConversionRunner.PrepareDropDirectory(activity.Directory);
                DroppedFiles files=await capture.CaptureAsync(Path.Combine(activity.Directory,"input"),activity.Cancel.Token);
                if(disposingConversions) return; activity.Cancel.Token.ThrowIfCancellationRequested();
                bool copy=FileConversion.CopiesText(choice.Operation); string output=copy?null:files.SourceFolder;
                if(output==null && !copy) { activity.Status="保存先を選んでください"; RefreshConversionActivity(); output=ConversionDestination.Choose(conversionActivity); if(output==null) throw new OperationCanceledException(); activity.Cancel.Token.ThrowIfCancellationRequested(); }
                activity.Output=output;
                ConversionJob job=new ConversionJob { Files=files.Files,Operation=choice.Operation,Output=output,Quality=choice.Quality,MaxEdge=choice.MaxEdge,Dpi=choice.Dpi,Pages="" };
                activity.Status="順番を待っています…"; RefreshConversionActivity(); await conversionGate.WaitAsync(activity.Cancel.Token);
                try { activity.Status="変換しています…"; RefreshConversionActivity(); activity.Result=await ConversionRunner.RunAsync(job,activity.Directory,false,activity.Cancel.Token); }
                finally { conversionGate.Release(); }
                if(copy && !activity.Result.Cancelled && activity.Result.Errors.Count==0 && !String.IsNullOrEmpty(activity.Result.Text)) Clipboard.SetText(activity.Result.Text);
                activity.Status=activity.Result.Files.Count+"件を保存"+(activity.Result.Cancelled?" · 残りをキャンセル":"")+(activity.Result.Errors.Count>0?" · "+activity.Result.Errors.Count+"件でエラー":"");
                if(copy && !activity.Result.Cancelled && activity.Result.Errors.Count==0) activity.Status="認識した文字をコピーしました";
                if(activity.Result.Files.Count==0 && activity.Result.Errors.Count>0) activity.Status="変換できませんでした";
            } catch(OperationCanceledException) { activity.Status="キャンセルしました"; }
            catch(Exception ex) { activity.Status="変換できませんでした"; activity.Result=new ConversionResult(); activity.Result.Errors.Add(ex.Message); }
            finally { capture.Dispose(); activity.Running=false; ConversionRunner.RemoveDropDirectory(activity.Directory); RefreshConversionActivity(); if(!disposingConversions && conversionJobs.All(j=>!j.Running)) { if(conversionProgress!=null) conversionProgress.Stop(); if(settings==null && idleCleanup!=null) idleCleanup.Start(); } }
        }
        void StartConversionProgress() {
            if(conversionProgress==null) { conversionProgress=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(500) }; conversionProgress.Tick+=delegate { foreach(ConversionActivity activity in conversionJobs.Where(j=>j.Running && !j.Cancel.IsCancellationRequested)) try { string progress=Path.Combine(activity.Directory,"progress"); if(File.Exists(progress)) activity.Status="変換中 · "+File.ReadAllText(progress); } catch(IOException) { } RefreshConversionActivity(); }; }
            conversionProgress.Start();
        }
        void RefreshConversionActivity() { if(!disposingConversions && conversionActivity!=null) conversionActivity.Refresh(); }
        public void ShowConversionActivity(bool automatic=false) {
            if(conversionActivity!=null) { conversionActivity.Refresh(); if(!automatic) conversionActivity.Activate(); return; }
            conversionActivity=new ConversionActivityWindow(conversionJobs,automatic); conversionActivity.Closed+=delegate { conversionActivity=null; if(settings==null && idleCleanup!=null) idleCleanup.Start(); }; conversionActivity.Show();
        }
        void CancelDropConversions() { disposingConversions=true; if(conversionProgress!=null) conversionProgress.Stop(); foreach(ConversionActivity activity in conversionJobs) activity.RequestCancel(); if(conversionActivity!=null) conversionActivity.Close(); }
    }
}

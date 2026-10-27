using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OrbitRing {
    sealed partial class Controller {
        CancellationTokenSource updateCancellation=new CancellationTokenSource();
        bool checkingUpdate,updatesDisposed;
        public UpdateResult LatestUpdate { get; private set; }
        public string UpdateMessage { get; private set; }
        public string UpdateRepository { get; private set; }
        public bool CheckingUpdate { get { return checkingUpdate; } }
        public event Action UpdateChanged;
        void InitializeUpdates() {
            if(String.IsNullOrEmpty(Config.Updates.Repository)) {
                try { Config.Updates.Repository=UpdateService.DefaultRepository(); } catch(Exception ex) { UpdateMessage=ex.Message; }
            }
        }
        public async Task CheckForUpdates() {
            if(checkingUpdate || updatesDisposed || Diagnostic) return;
            string repository=Config.Updates.Repository;
            checkingUpdate=true; UpdateRepository=UpdateService.Repository(repository); LatestUpdate=null; UpdateMessage="更新を確認しています…"; UpdatesChanged();
            try {
                UpdateResult result=await UpdateService.Fetch(repository,updateCancellation.Token);
                if(updatesDisposed) return;
                LatestUpdate=result; UpdateMessage=result.Available?"バージョン "+result.Version.ToString(3)+" を利用できます。":result.Version==null?"配布ファイル付きの正式リリースはまだありません。":"最新のバージョンです。";
            } catch(Exception ex) {
                if(updatesDisposed) return;
                UpdateMessage=ex is TaskCanceledException?"更新の確認が時間内に完了しませんでした。":ex is System.Net.Http.HttpRequestException?"接続できませんでした。ネットワークを確認してください。":ex.Message;
            } finally { checkingUpdate=false; if(!updatesDisposed) UpdatesChanged(); }
        }
        void UpdatesChanged() { Action changed=UpdateChanged; if(changed!=null) changed(); }
        public void OpenUpdatePage(string repository) {
            UpdateResult update=LatestUpdate;
            if(update==null || !update.Available || !String.Equals(update.Repository,UpdateService.Repository(repository),StringComparison.OrdinalIgnoreCase)) return;
            try { Process.Start(new ProcessStartInfo(update.Page) { UseShellExecute=true }); } catch(Exception ex) { OrbitDialog.Info(settings,"更新ページを開けませんでした",ex.Message); }
        }
        void DisposeUpdates() { updatesDisposed=true; updateCancellation.Cancel(); updateCancellation.Dispose(); UpdateChanged=null; }
    }
}

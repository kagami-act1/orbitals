using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OrbitRing {
    public sealed class PluginRequest {
        public int Version { get; set; }
        public string ActionId { get; set; }
        public string Application { get; set; }
        public string WindowHandle { get; set; }
        public int CursorX { get; set; }
        public int CursorY { get; set; }
    }
    sealed class PluginRunner : IDisposable {
        readonly CancellationTokenSource cancellation=new CancellationTokenSource();
        int active;
        internal static string Quote(string value) {
            StringBuilder result=new StringBuilder("\""); int slashes=0;
            foreach(char c in value) {
                if(c=='\\') { slashes++; continue; }
                if(c=='\"') { result.Append('\\',slashes*2+1); result.Append(c); }
                else { result.Append('\\',slashes); result.Append(c); }
                slashes=0;
            }
            result.Append('\\',slashes*2); return result.Append('"').ToString();
        }
        static async Task<string> Drain(StreamReader reader) {
            char[] buffer=new char[512]; StringBuilder text=new StringBuilder(); int read;
            while((read=await reader.ReadAsync(buffer,0,buffer.Length).ConfigureAwait(false))>0) if(text.Length<2048) text.Append(buffer,0,Math.Min(read,2048-text.Length));
            return new string(text.ToString().Where(c=>!Char.IsControl(c) || c=='\n').ToArray()).Trim();
        }
        static void ObserveFailure(Task task) { task.ContinueWith(delegate(Task finished) { var observed=finished.Exception; },TaskContinuationOptions.OnlyOnFaulted); }
        public async Task<string> Run(PluginAction action,PluginRequest request) {
            if(Interlocked.Increment(ref active)>2) { Interlocked.Decrement(ref active); throw new Exception("プラグインは同時に2件まで実行できます。"); }
            try {
                if(cancellation.IsCancellationRequested) throw new OperationCanceledException();
                if(!File.Exists(action.Executable)) throw new Exception("プラグインのexeが見つかりません。全体設定から読み込み直してください。");
                using(Process process=new Process()) {
                    process.StartInfo=new ProcessStartInfo(action.Executable,String.Join(" ",action.Arguments.Select(Quote))) { UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(action.Executable),RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8 };
                    process.Start();
                    Task<string> output=Drain(process.StandardOutput),error=Drain(process.StandardError);
                    ObserveFailure(output); ObserveFailure(error);
                    bool timedOut=false,canceled=false;
                    try {
                        byte[] input=Encoding.UTF8.GetBytes(Json.Write(request)+"\n"); await process.StandardInput.BaseStream.WriteAsync(input,0,input.Length).ConfigureAwait(false); process.StandardInput.Close();
                        DateTime deadline=DateTime.UtcNow.AddSeconds(30);
                        while(!process.HasExited && DateTime.UtcNow<deadline && !cancellation.IsCancellationRequested) await Task.Delay(50).ConfigureAwait(false);
                        canceled=cancellation.IsCancellationRequested; timedOut=!canceled && !process.HasExited;
                    } finally { if(!process.HasExited) { try { process.Kill(); } catch(InvalidOperationException) { } } }
                    // A descendant might retain inherited pipes. Never wait indefinitely for them.
                    Task streams=Task.WhenAll(output,error);
                    string message="",details="";
                    if(await Task.WhenAny(streams,Task.Delay(1000)).ConfigureAwait(false)==streams) {
                        try { message=await output.ConfigureAwait(false); details=await error.ConfigureAwait(false); } catch(IOException) { } catch(ObjectDisposedException) { }
                    } else {
                        ObserveFailure(streams);
                        throw new Exception("プラグインの出力が終了しませんでした。実行プログラムを確認してください。");
                    }
                    if(canceled) throw new OperationCanceledException();
                    if(timedOut) throw new Exception("プラグインが30秒以内に終了しなかったため停止しました。");
                    if(process.ExitCode!=0) throw new Exception("プラグインの処理に失敗しました。"+(details.Length==0?"":"\n"+details.Substring(0,Math.Min(240,details.Length))));
                    return message.Substring(0,Math.Min(240,message.Length));
                }
            } finally { Interlocked.Decrement(ref active); }
        }
        public void Dispose() { cancellation.Cancel(); }
    }
}

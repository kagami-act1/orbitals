using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace OrbitRing {
    public sealed class ConversionLease { public int ProcessId { get; set; } public long StartedUtc { get; set; } }
    static class ConversionRunner {
        static bool DropDirectory(string path) { string full=Path.GetFullPath(path); Guid id; return String.Equals(Path.GetDirectoryName(full),Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase) && Path.GetFileName(full).StartsWith("orbitals-drop-",StringComparison.Ordinal) && Guid.TryParseExact(Path.GetFileName(full).Substring(14),"N",out id); }
        public static void PrepareDropDirectory(string directory) {
            if(!DropDirectory(directory)) throw new InvalidOperationException("変換用の一時フォルダーではありません。"); Directory.CreateDirectory(directory);
            using(Process process=Process.GetCurrentProcess()) File.WriteAllText(Path.Combine(directory,"owner.json"),Json.Write(new ConversionLease { ProcessId=process.Id,StartedUtc=process.StartTime.ToUniversalTime().Ticks }),new UTF8Encoding(false));
        }
        internal static bool CleanupAbandoned(string directory) {
            if(!DropDirectory(directory) || !Directory.Exists(directory) || (File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0) return false;
            string owner=Path.Combine(directory,"owner.json");
            if(File.Exists(owner)) {
                if(new FileInfo(owner).Length>1024) return false; ConversionLease lease=Json.ReadFile<ConversionLease>(owner); if(lease==null || lease.ProcessId<=0 || lease.StartedUtc<=0) return false;
                try { using(Process process=Process.GetProcessById(lease.ProcessId)) if(process.StartTime.ToUniversalTime().Ticks==lease.StartedUtc) return false; } catch(ArgumentException) { } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { return false; }
            } else if(!File.Exists(Path.Combine(directory,"job.json")) || Directory.GetLastWriteTimeUtc(directory)>DateTime.UtcNow.AddDays(-1)) return false;
            ConversionProcesses.Cleanup(directory); RemoveDropDirectory(directory); return !Directory.Exists(directory);
        }
        public static void CleanupAbandonedAsync() { Task.Run(delegate { try { int checkedCount=0; foreach(string directory in Directory.EnumerateDirectories(Path.GetTempPath(),"orbitals-drop-*")) { if(++checkedCount>256) break; try { CleanupAbandoned(directory); } catch(IOException) { } catch(UnauthorizedAccessException) { } catch(ArgumentException) { } catch(InvalidOperationException) { } } } catch(IOException) { } catch(UnauthorizedAccessException) { } }); }
        public static Task<ConversionResult> RunAsync(ConversionJob job,string directory,bool removeWhenFinished=false,CancellationToken cancel=default(CancellationToken)) {
            FileConversion.Validate(job);
            string path=Path.Combine(directory,"job.json"); File.WriteAllText(path,Json.Write(job),new UTF8Encoding(false));
            return Task.Run(delegate {
                try {
                    cancel.ThrowIfCancellationRequested();
                    using(Process worker=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--convert-job "+FileConversion.Quote(path)) { UseShellExecute=false,CreateNoWindow=true })) {
                        DateTime deadline=DateTime.UtcNow.AddMinutes(20); DateTime? stopping=null;
                        try {
                            while(!worker.WaitForExit(150)) {
                                bool stop=cancel.IsCancellationRequested || File.Exists(Path.Combine(directory,"cancel")) || !Directory.Exists(directory);
                                if(stop) { if(!stopping.HasValue) { stopping=DateTime.UtcNow; if(Directory.Exists(directory)) File.WriteAllText(Path.Combine(directory,"cancel"),""); } if((DateTime.UtcNow-stopping.Value).TotalSeconds>4) { worker.Kill(); worker.WaitForExit(); return new ConversionResult { Cancelled=true }; } }
                                if(DateTime.UtcNow>deadline) throw new TimeoutException("変換に時間がかかりすぎたため中止しました。ファイルを分けて変換してください。");
                            }
                        } catch { if(!worker.HasExited) { worker.Kill(); worker.WaitForExit(); } throw; }
                    }
                    string result=Path.Combine(directory,"result.json"); if(!File.Exists(result)) throw new Exception("変換処理を完了できませんでした。"); return Json.ReadFile<ConversionResult>(result);
                } finally { if(Directory.Exists(directory)) ConversionProcesses.Cleanup(directory); if(removeWhenFinished) RemoveDropDirectory(directory); }
            });
        }
        public static void RemoveDropDirectory(string directory) {
            string full=Path.GetFullPath(directory);
            if(!DropDirectory(full)) throw new InvalidOperationException("変換用の一時フォルダーではありません。");
            try { if(Directory.Exists(full)) Directory.Delete(full,true); } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
    }
}

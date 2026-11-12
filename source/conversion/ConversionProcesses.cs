using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrbitRing {
    static class ConversionProcesses {
        static readonly HashSet<string> names=new HashSet<string>(new[]{"image-codec","cwebp","dwebp","resvg","winword","excel","powerpnt","soffice","soffice.bin"},StringComparer.OrdinalIgnoreCase);
        public static string Track(Process process,string directory) {
            string path=Path.Combine(directory,"child-"+process.Id+".json");
            File.WriteAllText(path,Json.Write(new ConversionLease { ProcessId=process.Id,StartedUtc=process.StartTime.ToUniversalTime().Ticks }),new UTF8Encoding(false)); return path;
        }
        public static void Cleanup(string directory) {
            int current; using(Process process=Process.GetCurrentProcess()) current=process.Id;
            foreach(string path in Directory.EnumerateFiles(directory,"child-*.json").Take(256)) try {
                if(new FileInfo(path).Length>1024) continue;
                ConversionLease lease=Json.ReadFile<ConversionLease>(path); if(lease==null || lease.ProcessId<=0 || lease.ProcessId==current) continue;
                using(Process process=Process.GetProcessById(lease.ProcessId)) if(process.StartTime.ToUniversalTime().Ticks==lease.StartedUtc && names.Contains(process.ProcessName) && !process.HasExited) {
                    if((process.ProcessName.Equals("WINWORD",StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("EXCEL",StringComparison.OrdinalIgnoreCase) || process.ProcessName.Equals("POWERPNT",StringComparison.OrdinalIgnoreCase)) && process.MainWindowHandle!=IntPtr.Zero) continue;
                    process.Kill(); process.WaitForExit(2000);
                }
            } catch(ArgumentException) { } catch(InvalidOperationException) { } catch(System.ComponentModel.Win32Exception) { } catch(IOException) { }
        }
        internal static async Task<string> ReadOutput(TextReader reader,int limit) {
            char[] buffer=new char[2048]; StringBuilder captured=new StringBuilder(); int count;
            while((count=await reader.ReadAsync(buffer,0,buffer.Length).ConfigureAwait(false))>0) if(captured.Length<limit) captured.Append(buffer,0,Math.Min(count,limit-captured.Length));
            return captured.ToString();
        }
        public static void Run(string executable,IEnumerable<string> arguments,string directory,string error,int seconds=120,long memory=1024L*1024*1024) {
            FileConversion.Cancel(directory);
            using(Process process=Process.Start(new ProcessStartInfo(executable,String.Join(" ",arguments.Select(FileConversion.Quote))) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,WorkingDirectory=directory })) using(ChildProcessJob job=new ChildProcessJob(process,memory)) {
                string lease=Track(process,directory); var errors=ReadOutput(process.StandardError,4096); var output=ReadOutput(process.StandardOutput,0); DateTime deadline=DateTime.UtcNow.AddSeconds(seconds);
                try {
                    while(!process.WaitForExit(100)) { FileConversion.Cancel(directory); if(DateTime.UtcNow>deadline) throw new TimeoutException("変換に時間がかかりすぎたため中止しました。"); }
                    process.WaitForExit(); string detail=errors.GetAwaiter().GetResult(); output.GetAwaiter().GetResult();
                    if(process.ExitCode!=0) throw new Exception(error+(String.IsNullOrWhiteSpace(detail)?"":"\n"+detail.Substring(0,Math.Min(600,detail.Length))));
                } finally { if(!process.HasExited) { process.Kill(); process.WaitForExit(); } if(File.Exists(lease)) File.Delete(lease); }
            }
        }
    }
}

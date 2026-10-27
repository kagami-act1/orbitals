using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace OrbitRing {
    static class ArchiveConversion {
        internal const int MaximumEntries=10000;
        internal const long MaximumBytes=512L*1024*1024;
        public static bool Operation(string value) { return value=="zip" || value=="zip-extract"; }
        public static bool ZipFile(string path) { return String.Equals(Path.GetExtension(path),".zip",StringComparison.OrdinalIgnoreCase); }
        static void Ordinary(string path) { if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) throw new IOException("リンクやジャンクションは圧縮・展開の対象にできません："+Path.GetFileName(path)); }
        public static void Validate(ConversionJob job) {
            if(job.Files==null || job.Files.Count<1 || job.Files.Count>128 || !Operation(job.Operation) || String.IsNullOrWhiteSpace(job.Output) || !Directory.Exists(job.Output)) throw new Exception("処理するファイルと保存先を確認してください。");
            foreach(string file in job.Files) { if(!Path.IsPathRooted(file) || (!File.Exists(file) && !Directory.Exists(file))) throw new Exception("ファイルが見つかりません："+Path.GetFileName(file)); Ordinary(file); if(job.Operation=="zip-extract" && (!File.Exists(file) || !ZipFile(file))) throw new Exception("展開するZIPファイルを選んでください。"); if(job.Operation=="zip" && Directory.Exists(file) && (Path.GetFullPath(job.Output).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar).StartsWith(Path.GetFullPath(file).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new Exception("ZIPの保存先は、圧縮するフォルダーの外を選んでください。"); }
        }
        internal static string Unique(string directory,string name,bool folder) {
            string stem=folder?name:Path.GetFileNameWithoutExtension(name),extension=folder?"":Path.GetExtension(name);
            for(int i=0;i<10000;i++) { string path=Path.Combine(directory,stem+(i==0?"":" ("+i+")")+extension); if(!File.Exists(path) && !Directory.Exists(path)) return path; }
            throw new IOException("同じ名前のファイルが多すぎます。");
        }
        internal static void CopyInput(string source,string destination,ref long remaining,ref int count,CancellationToken cancel) {
            cancel.ThrowIfCancellationRequested(); Ordinary(source); if(++count>MaximumEntries) throw new Exception("圧縮するファイル・フォルダーは合計10000件までです。");
            if(Directory.Exists(source)) {
                string sourceRoot=Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(Path.GetFullPath(destination).StartsWith(sourceRoot,StringComparison.OrdinalIgnoreCase)) throw new Exception("一時保存先を含むフォルダーは圧縮できません。");
                Directory.CreateDirectory(destination);
                foreach(string child in Directory.EnumerateFileSystemEntries(source)) CopyInput(child,Path.Combine(destination,Path.GetFileName(child)),ref remaining,ref count,cancel);
            } else using(Stream input=File.OpenRead(source)) using(Stream output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) Copy(input,output,ref remaining,delegate { cancel.ThrowIfCancellationRequested(); });
        }
        static void Copy(Stream input,Stream output,ref long remaining,Action check) {
            byte[] buffer=new byte[65536]; int read;
            while(true) { check(); read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining+1)); if(read==0) return; if(read>remaining) throw new IOException("一度に処理するファイルは展開後も合計512MBまでです。"); output.Write(buffer,0,read); remaining-=read; }
        }
        static void Add(ZipArchive archive,string source,string name,ref long remaining,ref int count,Action check,Action<string> status) {
            check(); Ordinary(source); if(++count>MaximumEntries) throw new Exception("圧縮するファイル・フォルダーは合計10000件までです。"); status(name);
            if(Directory.Exists(source)) { archive.CreateEntry(name.TrimEnd('/')+"/"); foreach(string path in Directory.EnumerateFileSystemEntries(source)) Add(archive,path,name.TrimEnd('/')+"/"+Path.GetFileName(path),ref remaining,ref count,check,status); }
            else { ZipArchiveEntry entry=archive.CreateEntry(name,CompressionLevel.Optimal); DateTime time=File.GetLastWriteTime(source); if(time.Year>=1980 && time.Year<=2107) entry.LastWriteTime=new DateTimeOffset(time); using(Stream input=File.OpenRead(source)) using(Stream output=entry.Open()) Copy(input,output,ref remaining,check); }
        }
        static string Compress(ConversionJob job,Action check,Action<string> status) {
            string staged=Path.Combine(job.Output,".orbitals-"+Guid.NewGuid().ToString("N")+".partial"),name=Path.GetFileName(job.Files[0].TrimEnd(Path.DirectorySeparatorChar));
            if(File.Exists(job.Files[0])) name=Path.GetFileNameWithoutExtension(name);
            name=DropFiles.SafeName(name)+(job.Files.Count>1?"-まとめ":"")+".zip";
            try {
                using(Stream stream=new FileStream(staged,FileMode.CreateNew,FileAccess.Write,FileShare.None)) using(ZipArchive archive=new ZipArchive(stream,ZipArchiveMode.Create)) {
                    long remaining=MaximumBytes; int count=0; HashSet<string> names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach(string source in job.Files) {
                        string root=Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar)); if(String.IsNullOrEmpty(root)) throw new Exception("ドライブ全体は圧縮できません。");
                        string stem=Directory.Exists(source)?root:Path.GetFileNameWithoutExtension(root),extension=Directory.Exists(source)?"":Path.GetExtension(root); int suffix=1;
                        while(!names.Add(root)) root=stem+" ("+(suffix++)+")"+extension;
                        Add(archive,source,root,ref remaining,ref count,check,status);
                    }
                }
                check(); string destination=Unique(job.Output,name,false); File.Move(staged,destination); return destination;
            } finally { if(File.Exists(staged)) File.Delete(staged); }
        }
        sealed class Entry { public ZipArchiveEntry Source; public string Relative; public bool Directory; }
        static List<Entry> Inspect(ZipArchive archive) {
            if(archive.Entries.Count>MaximumEntries) throw new Exception("ZIP内のファイル・フォルダーは10000件までです。");
            List<Entry> entries=new List<Entry>(); Dictionary<string,bool> paths=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase); long bytes=0;
            foreach(ZipArchiveEntry entry in archive.Entries) {
                string name=entry.FullName.Replace('\\','/'); bool directory=name.EndsWith("/",StringComparison.Ordinal); string[] parts=name.TrimEnd('/').Split('/');
                if(String.IsNullOrWhiteSpace(name) || name.StartsWith("/",StringComparison.Ordinal) || parts.Any(p=>String.IsNullOrWhiteSpace(p) || p=="." || p==".." || p.EndsWith(".",StringComparison.Ordinal) || p.EndsWith(" ",StringComparison.Ordinal) || p.IndexOfAny(Path.GetInvalidFileNameChars())>=0 || p.Any(Char.IsControl) || IsDevice(p))) throw new InvalidDataException("ZIPに安全に展開できないファイル名が含まれています。");
                if(((entry.ExternalAttributes>>16)&0xF000)==0xA000 || (entry.ExternalAttributes&0x400)!=0) throw new InvalidDataException("リンクを含むZIPは展開できません。");
                string relative=String.Join(Path.DirectorySeparatorChar.ToString(),parts);
                if(relative.Length>220 || paths.ContainsKey(relative)) throw new InvalidDataException("ZIPに重複した名前、または長すぎる名前が含まれています。");
                paths.Add(relative,directory); if(entry.Length<0 || entry.Length>MaximumBytes-bytes) throw new InvalidDataException("展開後の合計サイズは512MBまでです。"); bytes+=entry.Length;
                entries.Add(new Entry { Source=entry,Relative=relative,Directory=directory });
            }
            foreach(Entry entry in entries) { string parent=Path.GetDirectoryName(entry.Relative); while(!String.IsNullOrEmpty(parent)) { bool directory; if(paths.TryGetValue(parent,out directory) && !directory) throw new InvalidDataException("ZIP内のファイル名とフォルダー名が競合しています。"); parent=Path.GetDirectoryName(parent); } }
            return entries;
        }
        static bool IsDevice(string value) { string stem=value.Split('.')[0].ToUpperInvariant(); return new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem); }
        static string Extract(string source,string output,ref long remaining,Action check,Action<string> status) {
            string staged=Path.Combine(output,".orbitals-"+Guid.NewGuid().ToString("N")+".partial"),root=staged+Path.DirectorySeparatorChar;
            try {
                using(Stream input=File.OpenRead(source)) using(ZipArchive archive=new ZipArchive(input,ZipArchiveMode.Read)) {
                    List<Entry> entries=Inspect(archive); Directory.CreateDirectory(staged);
                    foreach(Entry entry in entries) {
                        check(); status(Path.GetFileName(source)+" · "+entry.Relative); string destination=Path.GetFullPath(Path.Combine(staged,entry.Relative));
                        if(!destination.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIPの外へ展開するパスは使用できません。");
                        if(entry.Directory) Directory.CreateDirectory(destination);
                        else { Directory.CreateDirectory(Path.GetDirectoryName(destination)); using(Stream content=entry.Source.Open()) using(Stream file=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) Copy(content,file,ref remaining,check); }
                    }
                }
                check(); string target=Unique(output,DropFiles.SafeName(Path.GetFileNameWithoutExtension(source)),true); Directory.Move(staged,target); return target;
            } finally { if(Directory.Exists(staged)) Directory.Delete(staged,true); }
        }
        public static ConversionResult Run(ConversionJob job,Action check,Action<string> status) {
            ConversionResult result=new ConversionResult();
            try {
                if(job.Operation=="zip") result.Files.Add(Compress(job,check,status));
                else { long remaining=MaximumBytes; foreach(string source in job.Files) { check(); try { result.Files.Add(Extract(source,job.Output,ref remaining,check,status)); } catch(OperationCanceledException) { throw; } catch(Exception ex) { result.Errors.Add(Path.GetFileName(source)+"："+ex.Message); } } }
            } catch(OperationCanceledException) { result.Cancelled=true; } catch(Exception ex) { result.Errors.Add(ex.Message); }
            return result;
        }
    }
}

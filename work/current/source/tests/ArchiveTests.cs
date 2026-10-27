using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;

namespace OrbitRing {
    static class ArchiveTests {
        static void Check(bool value,string name,List<string> result) { if(!value) throw new Exception(name); result.Add("PASS "+name); }
        static ConversionJob Job(string operation,string output,params string[] files) { return new ConversionJob { Operation=operation,Output=output,Files=files.ToList(),Quality=90,Dpi=150 }; }
        static string MakeZip(string directory,string[] names,bool link=false) {
            string file=Path.Combine(directory,Guid.NewGuid().ToString("N")+".zip"); using(Stream stream=File.Create(file)) using(ZipArchive zip=new ZipArchive(stream,ZipArchiveMode.Create)) foreach(string name in names) { ZipArchiveEntry entry=zip.CreateEntry(name); if(link) entry.ExternalAttributes=unchecked((int)0xA1FF0000); using(StreamWriter writer=new StreamWriter(entry.Open(),new UTF8Encoding(false))) writer.Write("test"); } return file;
        }
        public static List<string> Run() {
            List<string> result=new List<string>(); string root=Path.Combine(Path.GetTempPath(),"orbitals-archive-tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try {
                string source=Path.Combine(root,"source"),output=Path.Combine(root,"output"),capture=Path.Combine(root,"capture"); Directory.CreateDirectory(source); Directory.CreateDirectory(output);
                string folder=Path.Combine(source,"資料"),empty=Path.Combine(folder,"空フォルダー"),file=Path.Combine(folder,"メモ.txt"); Directory.CreateDirectory(empty); File.WriteAllText(file,"日本語の内容",new UTF8Encoding(false)); File.WriteAllBytes(Path.Combine(folder,"空.txt"),new byte[0]);
                DropOffer offer=DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{folder})); DroppedFiles snapshot=DropFiles.Capture(new DataObject(DataFormats.FileDrop,new[]{folder}),capture);
                Check(offer.Kind==DropKind.Files && snapshot.Files.Count==1 && File.ReadAllText(Path.Combine(snapshot.Files[0],"メモ.txt"))=="日本語の内容" && Directory.Exists(Path.Combine(snapshot.Files[0],"空フォルダー")),"folder drops snapshot nested content empty files and empty folders",result);
                ConversionResult packed=FileConversion.Run(Job("zip",output,snapshot.Files[0]),root); Check(packed.Errors.Count==0 && packed.Files.Count==1 && File.ReadAllText(file)=="日本語の内容","ZIP compression accepts a folder snapshot and leaves original content intact",result);
                byte[] original=File.ReadAllBytes(packed.Files.Single()); using(ZipArchive zip=ZipFile.OpenRead(packed.Files[0])) Check(zip.GetEntry("資料/メモ.txt")!=null && zip.GetEntry("資料/空フォルダー/")!=null && zip.GetEntry("資料/空.txt").Length==0,"ZIP output preserves Japanese paths nested layout and empty entries",result);
                ConversionResult extracted=FileConversion.Run(Job("zip-extract",output,packed.Files[0]),root); Check(extracted.Errors.Count==0 && File.ReadAllText(Path.Combine(extracted.Files.Single(),"資料","メモ.txt"))=="日本語の内容" && File.ReadAllBytes(packed.Files[0]).SequenceEqual(original),"ZIP extraction commits a separate folder and preserves archive bytes",result);
                ConversionResult repeated=FileConversion.Run(Job("zip-extract",output,packed.Files[0]),root),repacked=FileConversion.Run(Job("zip",output,snapshot.Files[0]),root);
                Check(repeated.Errors.Count==0 && repeated.Files.Single()!=extracted.Files.Single() && repacked.Errors.Count==0 && repacked.Files.Single()!=packed.Files.Single() && File.ReadAllBytes(packed.Files[0]).SequenceEqual(original),"existing archives and folders are never overwritten by repeated operations",result);
                string other=Path.Combine(source,"other"); Directory.CreateDirectory(other); string a=Path.Combine(source,"same.txt"),b=Path.Combine(other,"same.txt"); File.WriteAllText(a,"first"); File.WriteAllText(b,"second");
                bool recursive=false; try { FileConversion.Validate(Job("zip",folder,folder)); } catch { recursive=true; } Check(recursive,"archive output cannot be recursively included in its own source folder",result);
                ConversionResult combined=FileConversion.Run(Job("zip",output,a,b),root); using(ZipArchive zip=ZipFile.OpenRead(combined.Files.Single())) Check(zip.GetEntry("same.txt")!=null && zip.GetEntry("same (1).txt")!=null,"multiple selected files form one archive with distinct names for colliding roots",result);
                foreach(string[] names in new[]{new[]{"../escape.txt"},new[]{"/absolute.txt"},new[]{"C:/outside.txt"},new[]{"folder/../../escape.txt"},new[]{"file:stream"},new[]{"CON.txt"},new[]{"trailing."},new[]{"a.txt","A.TXT"},new[]{"a","a/b.txt"}}) {
                    string bad=MakeZip(root,names); ConversionResult rejected=FileConversion.Run(Job("zip-extract",output,bad),root); Check(rejected.Errors.Count==1 && rejected.Files.Count==0 && !Directory.GetFileSystemEntries(output,".orbitals-*").Any(),"unsafe ZIP paths or conflicting names are rejected before commit: "+String.Join(",",names),result);
                }
                string symlink=MakeZip(root,new[]{"link"},true); Check(FileConversion.Run(Job("zip-extract",output,symlink),root).Errors.Count==1,"ZIP symbolic-link entries cannot create filesystem links",result);
                string bomb=MakeZip(root,new[]{"large.txt"}); byte[] bombBytes=File.ReadAllBytes(bomb); for(int i=0;i<bombBytes.Length-46;i++) if(BitConverter.ToUInt32(bombBytes,i)==0x02014b50) { Array.Copy(BitConverter.GetBytes((uint)(ArchiveConversion.MaximumBytes+1)),0,bombBytes,i+24,4); break; } File.WriteAllBytes(bomb,bombBytes);
                Check(FileConversion.Run(Job("zip-extract",output,bomb),root).Errors.Count==1,"declared expanded size is bounded before data is decompressed",result);
                int calls=0; ConversionResult cancelled=ArchiveConversion.Run(Job("zip",output,folder),delegate { if(++calls>2) throw new OperationCanceledException(); },delegate { }); Check(cancelled.Cancelled && cancelled.Files.Count==0 && !Directory.GetFileSystemEntries(output,".orbitals-*").Any(),"cancelled compression removes partial output without touching source files",result);
                ConversionResult emptyArchive=FileConversion.Run(Job("zip-extract",output,MakeZip(root,new string[0])),root); Check(emptyArchive.Errors.Count==0 && Directory.Exists(emptyArchive.Files.Single()),"an empty ZIP produces an empty destination folder",result);
                string worker=Path.Combine(Path.GetTempPath(),"orbitals-drop-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(worker); ConversionResult background=ConversionRunner.RunAsync(Job("zip",output,a),worker,true).GetAwaiter().GetResult(); Check(background.Files.Count==1 && background.Errors.Count==0 && !Directory.Exists(worker),"archive jobs use the existing disposable background worker and cleanup",result);
                Check(DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{packed.Files[0]})).Kind==DropKind.Zip && ConversionCatalog.Supports("zip-extract",DropKind.Zip,1) && !ConversionCatalog.Supports("png",DropKind.Files,1),"input-aware menus offer extraction only for ZIP and never image conversion for arbitrary files",result);
                Preferences legacy=Preferences.Defaults(); string json=Json.Write(legacy); legacy=Json.Read<Preferences>(json.Replace("\"ArchiveVersion\":1,", "").Replace(",\"ArchiveVersion\":1", "")); legacy.Conversion.Images=new List<ConversionChoice> { new ConversionChoice { Operation="jpeg",Label="自分の設定",Quality=47 } }; legacy.Validate();
                Check(legacy.Conversion.Images.Count==2 && legacy.Conversion.Images[0].Quality==47 && legacy.Conversion.Images[1].Operation=="zip","older custom conversion lists gain ZIP without losing names quality or ordering",result); legacy.Conversion.Images.RemoveAt(1); legacy=legacy.Clone(); legacy.Validate(); Check(legacy.Conversion.Images.Count==1,"removing the new ZIP choice stays removed after saving and reopening",result);
                Preferences config=Preferences.Defaults(); config.Conversion.Archives.Add(new ConversionChoice { Operation="zip",Label="もう一度圧縮",Icon="text:ZIP" }); config.Conversion.Files[0].Label="まとめる"; config.Conversion.Files[0].Enabled=false; config.Validate(); config=config.Clone(); config.Validate(); Check(config.Conversion.Archives.Count==2 && config.Conversion.Archives[1].Icon=="text:ZIP" && config.Conversion.Files[0].Label=="まとめる" && !config.Conversion.Files[0].Enabled,"archive menu order labels text icons and visibility survive settings round trips",result);
            } finally { Directory.Delete(root,true); }
            return result;
        }
    }
}

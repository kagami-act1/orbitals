using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace OrbitRing {
    interface IDocumentConverter : IDisposable {
        List<string> Convert(string source,ConversionJob job,string stage,string directory,Action cancel);
    }
    static class DocumentConversion {
        static readonly string[] word={"word-pdf","word-text","word-docx"},sheets={"sheet-pdf","sheet-csv","sheet-tsv"},slides={"slides-pdf","slides-png","slides-jpeg"};
        public static readonly string[] AllOperations=word.Concat(sheets).Concat(slides).ToArray();
        static readonly Dictionary<DropKind,bool> installed=new Dictionary<DropKind,bool>();
        public static bool IsDocument(DropKind kind) { return kind==DropKind.Document || kind==DropKind.Spreadsheet || kind==DropKind.Presentation; }
        public static DropKind Kind(string path) {
            switch(Path.GetExtension(path??"").ToLowerInvariant()) {
                case ".doc": case ".docx": case ".odt": case ".rtf": return DropKind.Document;
                case ".xls": case ".xlsx": case ".ods": return DropKind.Spreadsheet;
                case ".ppt": case ".pptx": case ".odp": return DropKind.Presentation;
                default:return DropKind.None;
            }
        }
        public static DropKind CommonKind(IEnumerable<string> paths) { DropKind[] kinds=paths.Select(Kind).Distinct().ToArray(); return kinds.Length==1 && IsDocument(kinds[0])?kinds[0]:DropKind.None; }
        public static string[] Operations(DropKind kind) { return kind==DropKind.Document?word:kind==DropKind.Spreadsheet?sheets:kind==DropKind.Presentation?slides:new string[0]; }
        public static bool Operation(string value) { return AllOperations.Contains(value); }
        public static DropKind Source(string operation) { return word.Contains(operation)?DropKind.Document:sheets.Contains(operation)?DropKind.Spreadsheet:slides.Contains(operation)?DropKind.Presentation:DropKind.None; }
        public static bool SlideImages(string operation) { return operation=="slides-png" || operation=="slides-jpeg"; }
        public static string Label(string operation) { return operation.EndsWith("-pdf",StringComparison.Ordinal)?"PDF":operation=="word-text"?"TXT":operation=="word-docx"?"DOCX":operation=="sheet-csv"?"CSV（シートごと）":operation=="sheet-tsv"?"TSV（シートごと）":operation=="slides-png"?"PNG（スライドごと）":"JPEG（スライドごと）"; }
        public static bool Available(DropKind kind) {
            if(!IsDocument(kind)) return true;
            bool value; lock(installed) { if(!installed.TryGetValue(kind,out value)) { value=OfficeDocumentConverter.TypeFor(kind)!=null || LibreOfficeConverter.Available; installed.Add(kind,value); } } return value;
        }
        public static bool Supports(string operation,DropKind kind,IEnumerable<string> names) { return !Operation(operation) || Source(operation)==kind && Available(kind) && (operation!="word-docx" || names==null || names.All(n=>!Path.GetExtension(n).Equals(".docx",StringComparison.OrdinalIgnoreCase))); }
        public static void Validate(ConversionJob job) {
            if(job.Files==null || job.Files.Count<1 || job.Files.Count>128 || !Operation(job.Operation) || String.IsNullOrWhiteSpace(job.Output) || !Directory.Exists(job.Output)) throw new Exception("変換する文書と保存先を選んでください（128件まで）。");
            if(job.Quality<1 || job.Quality>100 || job.MaxEdge<0 || job.MaxEdge>16000 || job.Dpi<72 || job.Dpi>300) throw new Exception("画質・画像サイズ・解像度を確認してください。");
            DropKind kind=Source(job.Operation);
            if(!Available(kind)) throw new Exception("この変換には、対応するMicrosoft Officeアプリ、またはLibreOfficeが必要です。");
            foreach(string path in job.Files) if(!Path.IsPathRooted(path) || !File.Exists(path) || Kind(path)!=kind || new FileInfo(path).Length==0 || new FileInfo(path).Length>512L*1024*1024 || !Supports(job.Operation,kind,new[]{path})) throw new Exception("この操作で変換できる文書を選んでください："+Path.GetFileName(path));
        }
        static IDocumentConverter Create(DropKind kind,string directory) {
            Type type=OfficeDocumentConverter.TypeFor(kind);
            if(type!=null) try { return new OfficeDocumentConverter(type,kind,directory); } catch(COMException) { if(!LibreOfficeConverter.Available) throw; }
            return new LibreOfficeConverter();
        }
        public static ConversionResult Run(ConversionJob job,string directory,Action cancel,Action<string> status) {
            ConversionResult result=new ConversionResult();
            try { using(IDocumentConverter converter=Create(Source(job.Operation),directory)) foreach(string source in job.Files) {
                cancel(); status(Path.GetFileName(source)); string stage=Path.Combine(directory,"document-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
                try {
                    List<string> outputs=converter.Convert(source,job,stage,directory,cancel); string stem=Path.GetFileNameWithoutExtension(source);
                    if(result.Files.Count+outputs.Count>1000) throw new Exception("出力は一度に1000ファイルまでです。");
                    for(int i=0;i<outputs.Count;i++) {
                        cancel(); string suffix=SlideImages(job.Operation)?"-"+(i+1).ToString("D3"):outputs.Count>1?"-"+DropFiles.SafeName(Path.GetFileNameWithoutExtension(outputs[i])):"";
                        result.Files.Add(FileConversion.Commit(outputs[i],job.Output,stem+suffix+Path.GetExtension(outputs[i])));
                    }
                } catch(OperationCanceledException) { throw; } catch(Exception ex) { result.Errors.Add(Path.GetFileName(source)+"："+Error(ex)); }
            } } catch(OperationCanceledException) { result.Cancelled=true; } catch(Exception ex) { result.Errors.Add(Error(ex)); }
            return result;
        }
        internal static string Error(Exception ex) { while(ex is System.Reflection.TargetInvocationException && ex.InnerException!=null) ex=ex.InnerException; return ex.Message; }
    }
}

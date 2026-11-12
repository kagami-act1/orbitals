using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace OrbitRing {
    sealed class OfficeDocumentConverter : IDocumentConverter {
        const int WordPdf=17,WordDocx=12,WordEncodedText=7,Utf8=65001;
        const int ExcelPdf=0,ExcelCsvUtf8=62,ExcelUnicodeText=42,PowerPointPdf=32;
        const int DisableMacros=3,OfficeTrue=-1,OfficeFalse=0;
        readonly DropKind kind;
        object instance,security,alerts;
        string lease;
        bool owned,configured;
        public static Type TypeFor(DropKind kind) { return !DocumentConversion.IsDocument(kind)?null:Type.GetTypeFromProgID(kind==DropKind.Document?"Word.Application":kind==DropKind.Spreadsheet?"Excel.Application":"PowerPoint.Application"); }
        static void Release(object value) { if(value!=null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }
        public OfficeDocumentConverter(Type type,DropKind source,string directory) {
            kind=source; string name=kind==DropKind.Document?"WINWORD":kind==DropKind.Spreadsheet?"EXCEL":"POWERPNT";
            HashSet<int> previous=new HashSet<int>(); foreach(Process process in Process.GetProcessesByName(name)) using(process) previous.Add(process.Id);
            try {
                instance=Activator.CreateInstance(type); dynamic app=instance; List<Process> created=new List<Process>();
                foreach(Process process in Process.GetProcessesByName(name)) { if(!previous.Contains(process.Id)) created.Add(process); else process.Dispose(); }
                try { owned=created.Count==1; if(owned) lease=ConversionProcesses.Track(created[0],directory); } finally { foreach(Process process in created) process.Dispose(); }
                security=app.AutomationSecurity; alerts=app.DisplayAlerts; configured=true; app.AutomationSecurity=DisableMacros; app.DisplayAlerts=kind==DropKind.Spreadsheet?(object)false:kind==DropKind.Presentation?(object)1:(object)0;
                if(owned && kind!=DropKind.Presentation) app.Visible=false;
            } catch { Dispose(); throw; }
        }
        public List<string> Convert(string source,ConversionJob job,string stage,string directory,Action cancel) {
            object collection=null,document=null;
            try {
                dynamic app=instance; string operation=job.Operation;
                string output=Path.Combine(stage,operation=="word-text"?"document.txt":operation=="word-docx"?"document.docx":"document.pdf");
                if(kind==DropKind.Document) {
                    collection=app.Documents; dynamic documents=collection; document=documents.Open(FileName:source,ConfirmConversions:false,ReadOnly:true,AddToRecentFiles:false,PasswordDocument:"",PasswordTemplate:"",Visible:false,OpenAndRepair:false,NoEncodingDialog:true); dynamic doc=document;
                    if(operation=="word-pdf") doc.ExportAsFixedFormat(output,WordPdf,false);
                    else if(operation=="word-docx") doc.SaveAs2(FileName:output,FileFormat:WordDocx,AddToRecentFiles:false);
                    else doc.SaveAs2(FileName:output,FileFormat:WordEncodedText,AddToRecentFiles:false,Encoding:Utf8,InsertLineBreaks:false,AllowSubstitutions:false,LineEnding:0);
                } else if(kind==DropKind.Spreadsheet) {
                    collection=app.Workbooks; dynamic books=collection; document=books.Open(Filename:source,UpdateLinks:0,ReadOnly:true,Password:"",WriteResPassword:"",IgnoreReadOnlyRecommended:true,AddToMru:false); dynamic book=document;
                    if(operation=="sheet-pdf") book.ExportAsFixedFormat(ExcelPdf,output);
                    else return SheetFiles(book,app,operation,stage,cancel);
                } else {
                    collection=app.Presentations; dynamic presentations=collection; document=presentations.Open(source,OfficeTrue,OfficeFalse,OfficeFalse);
                    if(DocumentConversion.SlideImages(operation)) return SlideFiles(document,job,stage,directory,cancel);
                    ((dynamic)document).SaveAs(output,PowerPointPdf,OfficeFalse);
                }
                if(!File.Exists(output) || new FileInfo(output).Length==0) throw new Exception("文書の変換結果が空です。"); return new List<string> {output};
            } finally { if(document!=null) try { if(kind==DropKind.Presentation) ((dynamic)document).Close(); else ((dynamic)document).Close(false); } finally { Release(document); Release(collection); } else Release(collection); }
        }
        static List<string> SheetFiles(dynamic workbook,dynamic app,string operation,string stage,Action cancel) {
            List<string> files=new List<string>(); object sheets=workbook.Worksheets;
            try {
                int count=((dynamic)sheets).Count; if(count>256) throw new Exception("シートは256枚までです。");
                for(int i=1;i<=count;i++) {
                    cancel(); object sheet=((dynamic)sheets).Item[i],copy=null;
                    try {
                        string name=DropFiles.SafeName(((dynamic)sheet).Name); ((dynamic)sheet).Copy(); copy=app.ActiveWorkbook;
                        string file=ArchiveConversion.Unique(stage,name+(operation=="sheet-csv"?".csv":".tsv"),false);
                        ((dynamic)copy).SaveAs(file,operation=="sheet-csv"?ExcelCsvUtf8:ExcelUnicodeText); files.Add(file);
                    } finally { if(copy!=null) try { ((dynamic)copy).Close(false); } finally { Release(copy); } Release(sheet); }
                }
            } finally { Release(sheets); }
            if(operation=="sheet-tsv") foreach(string file in files) { cancel(); string text=File.ReadAllText(file,Encoding.Unicode); File.WriteAllText(file,text,new UTF8Encoding(false)); }
            if(files.Count==0) throw new Exception("変換するワークシートがありません。"); return files;
        }
        internal static Size SlidePixels(double width,double height,int dpi,int edge) {
            width=width*dpi/72; height=height*dpi/72;
            if(width<=0 || height<=0 || Double.IsNaN(width) || Double.IsNaN(height) || Double.IsInfinity(width) || Double.IsInfinity(height)) throw new InvalidDataException("スライドの大きさが不正です。");
            double scale=edge>0?Math.Min(1,edge/Math.Max(width,height)):1;
            scale=Math.Min(scale,Math.Sqrt(40000000/(width*height)));
            return new Size(Math.Max(1,Math.Round(width*scale)),Math.Max(1,Math.Round(height*scale)));
        }
        static List<string> SlideFiles(dynamic presentation,ConversionJob job,string stage,string directory,Action cancel) {
            object slides=presentation.Slides,setup=presentation.PageSetup;
            try {
                int count=((dynamic)slides).Count; if(count<1 || count>1000) throw new Exception("スライドは1〜1000枚に対応しています。");
                Size size=SlidePixels((double)((dynamic)setup).SlideWidth,(double)((dynamic)setup).SlideHeight,job.Dpi,job.MaxEdge); List<string> files=new List<string>();
                for(int i=1;i<=count;i++) {
                    cancel(); object slide=((dynamic)slides).Item[i]; string png=Path.Combine(stage,"slide-"+i.ToString("D3")+".png");
                    try { ((dynamic)slide).Export(png,"PNG",(int)size.Width,(int)size.Height); } finally { Release(slide); }
                    if(job.Operation=="slides-jpeg") {
                        string jpeg=Path.ChangeExtension(png,"jpg"); FileConversion.WriteImage(RasterImageDecoder.Read(png,0,directory),"jpeg",job.Quality,jpeg,directory); File.Delete(png); files.Add(jpeg);
                    } else files.Add(png);
                }
                return files;
            } finally { Release(slides); Release(setup); }
        }
        public void Dispose() {
            if(instance==null) return;
            try {
                dynamic app=instance; if(configured) { try { app.AutomationSecurity=security; app.DisplayAlerts=alerts; } catch(COMException) { } }
                if(owned) {
                    object documents=kind==DropKind.Document?app.Documents:kind==DropKind.Spreadsheet?app.Workbooks:app.Presentations;
                    int count; try { count=((dynamic)documents).Count; } finally { Release(documents); }
                    if(count==0) { if(kind==DropKind.Document) app.Quit(OfficeFalse); else app.Quit(); }
                    else if(lease!=null && File.Exists(lease)) File.Delete(lease);
                }
            } catch(COMException) { } finally { Release(instance); instance=null; }
        }
    }
}

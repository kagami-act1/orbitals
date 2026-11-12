using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class DocumentConversionTests {
        static void Check(bool value,string text,List<string> results) { if(!value) throw new Exception(text); results.Add("PASS "+text); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        public static List<string> Run() {
            List<string> results=new List<string>(); string directory=Path.Combine(Path.GetTempPath(),"orbitals-document-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try {
                foreach(string extension in new[]{"doc","DOCX","odt","rtf","xls","xlsx","ods","ppt","pptx","odp","heic","heif","AVIF"}) {
                    string path=Path.Combine(directory,"sample."+extension); File.WriteAllText(path,"fixture"); DropOffer offer=DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{path}));
                    Check(FileConversion.Supported(path) && offer.Kind==(ModernImageCodec.Supported(path)?DropKind.Image:DocumentConversion.Kind(path)),"document and modern image drag classification: "+extension,results);
                }
                Check(DocumentConversion.CommonKind(new[]{"a.doc","b.docx","c.rtf","d.odt"})==DropKind.Document && DocumentConversion.CommonKind(new[]{"a.xlsx","b.ods"})==DropKind.Spreadsheet && DocumentConversion.CommonKind(new[]{"a.pptx","b.ppt"})==DropKind.Presentation,"each office family may contain mixed extensions in the same batch",results);
                Check(DocumentConversion.CommonKind(new[]{"a.docx","b.xlsx"})==DropKind.None && !DropFiles.ImageFile("a.docx") && !DropFiles.ImageFile("a.xlsx") && !DropFiles.ImageFile("a.pptx"),"mixed office families never enter an image decoder",results);
                string a=Path.Combine(directory,"sample.doc"),b=Path.Combine(directory,"sample.xlsx");
                Check(DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{a,b})).Kind==DropKind.Files,"mixed document families retain the ordinary ZIP offer",results);
                ConversionPreferences legacy=Json.Read<ConversionPreferences>("{\"Images\":[],\"Pdf\":[],\"Files\":[],\"Archives\":[],\"ArchiveVersion\":1}"); legacy.Validate();
                Check(legacy.Images.Count==0 && legacy.Pdf.Count==0 && legacy.Files.Count==0 && legacy.Archives.Count==0 && legacy.Documents.Count==4 && legacy.Spreadsheets.Count==4 && legacy.Presentations.Count==4,"older settings gain only the new office categories while all existing conversion lists remain intact",results);
                legacy.Documents[0].Label="提出PDF"; legacy.Documents[1].Enabled=false; legacy.Spreadsheets.Reverse(); legacy.Presentations.Clear(); string saved=Json.Write(legacy); ConversionPreferences restored=Json.Read<ConversionPreferences>(saved); restored.Validate();
                Check(ConversionCatalog.Sources.All(kind=>Json.Write(restored.List(kind))==Json.Write(legacy.List(kind))) && restored.ArchiveVersion==legacy.ArchiveVersion,"all conversion lists retain their complete values and ordering after reopening",results);
                Check(restored.Documents[0].Label=="提出PDF" && !restored.Documents[1].Enabled && restored.Presentations.Count==0,"office conversion names disabled entries and deleted targets persist",results);
                foreach(DropKind kind in new[]{DropKind.Document,DropKind.Spreadsheet,DropKind.Presentation}) Check(ConversionCatalog.Operations(kind).Where(o=>o!="zip").All(o=>DocumentConversion.Source(o)==kind && FileConversion.Operations.Contains(o)) && ConversionCatalog.Operations(kind).Contains("zip"),"office menus contain only operations for their input family: "+kind,results);
                Check(FileConversion.Operations.Length==FileConversion.Names.Length && FileConversion.Operations.Distinct().Count()==FileConversion.Operations.Length,"conversion commands and visible names stay aligned without duplicate operations",results);
                Check(!DocumentConversion.Supports("word-docx",DropKind.Document,new[]{"already.docx"}) && !DocumentConversion.Supports("word-pdf",DropKind.Presentation,new[]{"slides.pptx"}),"runtime targets exclude conversion to the existing DOCX format and incompatible office families",results);
                Check(!FileConversion.ImageSizing("word-pdf") && !FileConversion.ImageSizing("word-docx") && !FileConversion.ImageSizing("sheet-csv") && FileConversion.ImageSizing("slides-png") && FileConversion.ImageSizing("slides-jpeg"),"image-only settings are shown only for the slide raster outputs",results);
                ConversionJob wrong=new ConversionJob { Files=new List<string>{a},Operation="png",Output=directory,Quality=90,Dpi=96,MaxEdge=0,Pages="" };
                Check(Reject(delegate { FileConversion.Validate(wrong); }),"document files cannot be submitted to ordinary image operations",results);
                Check(!AppDomain.CurrentDomain.GetAssemblies().Any(x=>x.GetName().Name.StartsWith("Magick.",StringComparison.Ordinal)),"document registration and settings do not load the image library into the resident process",results);
            } finally { Directory.Delete(directory,true); }
            return results;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbitRing {
    public sealed class ConversionChoice {
        public string Operation { get; set; }
        public string Label { get; set; }
        public string Icon { get; set; }
        public bool Enabled { get; set; }
        public int Quality { get; set; }
        public int MaxEdge { get; set; }
        public int Dpi { get; set; }
        public ConversionChoice() { Enabled=true; Quality=90; Dpi=150; }
        public ActionItem Action() { return new ActionItem(Label,"none","") { Icon=String.IsNullOrEmpty(Icon)?ConversionCatalog.Icon(Operation):Icon }; }
    }
    public sealed class ConversionPreferences {
        public const int MaxChoices=32;
        public List<ConversionChoice> Images { get; set; }
        public List<ConversionChoice> Pdf { get; set; }
        public List<ConversionChoice> Files { get; set; }
        public List<ConversionChoice> Archives { get; set; }
        public List<ConversionChoice> Documents { get; set; }
        public List<ConversionChoice> Spreadsheets { get; set; }
        public List<ConversionChoice> Presentations { get; set; }
        public int ArchiveVersion { get; set; }
        public ConversionPreferences() { Images=ConversionCatalog.Defaults(false); Pdf=ConversionCatalog.Defaults(true); Files=ConversionCatalog.Defaults(DropKind.Files); Archives=ConversionCatalog.Defaults(DropKind.Zip); Documents=ConversionCatalog.Defaults(DropKind.Document); Spreadsheets=ConversionCatalog.Defaults(DropKind.Spreadsheet); Presentations=ConversionCatalog.Defaults(DropKind.Presentation); }
        public void Validate(bool draft=false) {
            if(ArchiveVersion==0) { foreach(List<ConversionChoice> choices in new[]{Images,Pdf}) if(choices!=null && choices.Count>0 && choices.Count<MaxChoices && !choices.Any(c=>c!=null && c.Operation=="zip")) choices.Add(new ConversionChoice { Operation="zip",Label=ConversionCatalog.Label("zip") }); ArchiveVersion=1; }
            if(ArchiveVersion!=1) throw new Exception("圧縮・展開の設定バージョンを確認してください。");
            ValidateList(Images,DropKind.Image,draft); ValidateList(Pdf,DropKind.Pdf,draft); ValidateList(Files,DropKind.Files,draft); ValidateList(Archives,DropKind.Zip,draft);
            ValidateList(Documents,DropKind.Document,draft); ValidateList(Spreadsheets,DropKind.Spreadsheet,draft); ValidateList(Presentations,DropKind.Presentation,draft);
        }
        public List<ConversionChoice> List(DropKind kind) { return kind==DropKind.Document?Documents:kind==DropKind.Spreadsheet?Spreadsheets:kind==DropKind.Presentation?Presentations:kind==DropKind.Pdf?Pdf:kind==DropKind.Files?Files:kind==DropKind.Zip?Archives:Images; }
        public void Reset(DropKind kind) { List<ConversionChoice> choices=List(kind); choices.Clear(); choices.AddRange(ConversionCatalog.Defaults(kind)); }
        static void ValidateList(List<ConversionChoice> choices,DropKind kind,bool draft) {
            if(choices==null || choices.Count>MaxChoices || choices.Any(c=>c==null)) throw new Exception("変換先は入力の種類ごとに32件まで登録できます。");
            foreach(ConversionChoice choice in choices) {
                if(!ConversionCatalog.Operations(kind).Contains(choice.Operation) || choice.Label==null || (!draft && String.IsNullOrWhiteSpace(choice.Label)) || choice.Label.Length>32 || choice.Label.Any(Char.IsControl)) throw new Exception("ファイル変換の名前・処理を確認してください。");
                if(choice.Quality<1 || choice.Quality>100 || choice.MaxEdge<0 || choice.MaxEdge>16000 || choice.Dpi<72 || choice.Dpi>300) throw new Exception("ファイル変換の画質・画像サイズ・解像度を確認してください。");
                ActionIcons.Validate(choice.Icon);
            }
        }
        public List<ConversionChoice> Choices(bool pdf,string initial="") { return (pdf?Pdf:Images).Where(c=>String.IsNullOrEmpty(initial)?c.Enabled:c.Operation==initial).ToList(); }
        public List<ConversionChoice> Choices(DropKind kind,string initial="") { return List(kind).Where(c=>String.IsNullOrEmpty(initial)?c.Enabled:c.Operation==initial).ToList(); }
    }
    static class ConversionCatalog {
        static readonly string[] imageOperations={"image-pdf","png","jpeg","webp","tiff","bmp","gif","ico","ocr-text","ocr-copy","zip"};
        static readonly string[] pdfOperations={"pdf-png","pdf-jpeg","pdf-webp","pdf-tiff","pdf-bmp","pdf-gif","pdf-split","pdf-merge","pdf-ocr-text","pdf-ocr-copy","zip"};
        public static string[] Operations(bool pdf) { return pdf?pdfOperations:imageOperations; }
        public static readonly DropKind[] Sources={DropKind.Image,DropKind.Pdf,DropKind.Document,DropKind.Spreadsheet,DropKind.Presentation,DropKind.Files,DropKind.Zip};
        public static string[] Operations(DropKind kind) { return DocumentConversion.IsDocument(kind)?DocumentConversion.Operations(kind).Concat(new[]{"zip"}).ToArray():kind==DropKind.Files?new[]{"zip"}:kind==DropKind.Zip?new[]{"zip-extract","zip"}:kind==DropKind.Pdf?pdfOperations:imageOperations; }
        public static string SourceName(DropKind kind) { return kind==DropKind.Document?"Word・文書":kind==DropKind.Spreadsheet?"Excel・表計算":kind==DropKind.Presentation?"PowerPoint・スライド":kind==DropKind.Files?"ファイル・フォルダー":kind==DropKind.Zip?"ZIP":kind==DropKind.Pdf?"PDF":"画像"; }
        public static string Label(string operation) {
            if(operation=="zip") return "ZIPに圧縮";
            if(operation=="zip-extract") return "ZIPを展開";
            if(operation=="image-pdf") return "PDF";
            if(DocumentConversion.Operation(operation)) return DocumentConversion.Label(operation);
            if(operation=="pdf-split") return "ページを分割";
            if(operation=="pdf-merge") return "PDFを結合";
            if(operation=="pdf-extract") return "ページを抽出";
            if(FileConversion.Ocr(operation)) return FileConversion.CopiesText(operation)?"文字をコピー":"TXT";
            return operation.Replace("pdf-","").ToUpperInvariant().Replace("WEBP","WebP");
        }
        public static string Icon(string operation) {
            if(operation=="zip") return "ms:folder_zip";
            if(operation=="zip-extract") return "ms:unarchive";
            if(DocumentConversion.Operation(operation)) return operation.EndsWith("-pdf",StringComparison.Ordinal)?"ms:picture_as_pdf":operation=="word-docx"?"ms:docs":operation=="word-text"?"ms:text_snippet":operation.StartsWith("sheet-",StringComparison.Ordinal)?"ms:table_view":operation=="slides-png"?"ms:opacity":"ms:photo_camera";
            if(FileConversion.Ocr(operation)) return FileConversion.CopiesText(operation)?"ms:content_copy":"ms:text_snippet";
            switch(operation.Replace("pdf-","")) { case "image-pdf": return "ms:picture_as_pdf"; case "png": return "ms:opacity"; case "jpeg": return "ms:photo_camera"; case "webp": return "ms:photo_size_select_small"; case "tiff": return "ms:layers"; case "bmp": return "ms:grid_on"; case "gif": return "ms:gif_box"; case "ico": return "ms:apps"; case "merge": return "ms:merge"; case "split": return "ms:call_split"; default: return "ms:image"; }
        }
        public static List<ConversionChoice> Defaults(bool pdf) { return Operations(pdf).Select(o=>new ConversionChoice { Operation=o,Label=Label(o) }).ToList(); }
        public static List<ConversionChoice> Defaults(DropKind kind) { return Operations(kind).Where(o=>kind!=DropKind.Zip || o!="zip").Select(o=>new ConversionChoice { Operation=o,Label=Label(o) }).ToList(); }
        public static bool PdfFile(string path) { return System.IO.Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase); }
        public static bool Supports(string operation,bool pdf,int count) { return Operations(pdf).Contains(operation) && (operation!="pdf-merge" || count>=2); }
        public static bool Supports(string operation,DropKind kind,int count) { return Operations(kind).Contains(operation) && (operation!="pdf-merge" || count>=2); }
    }
}

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
        public ConversionPreferences() { Images=ConversionCatalog.Defaults(false); Pdf=ConversionCatalog.Defaults(true); }
        public void Validate(bool draft=false) { ValidateList(Images,false,draft); ValidateList(Pdf,true,draft); }
        static void ValidateList(List<ConversionChoice> choices,bool pdf,bool draft) {
            if(choices==null || choices.Count>MaxChoices || choices.Any(c=>c==null)) throw new Exception("変換先は画像・PDFごとに32件まで登録できます。");
            foreach(ConversionChoice choice in choices) {
                if(!ConversionCatalog.Operations(pdf).Contains(choice.Operation) || choice.Label==null || (!draft && String.IsNullOrWhiteSpace(choice.Label)) || choice.Label.Length>32 || choice.Label.Any(Char.IsControl)) throw new Exception("ファイル変換の名前・処理を確認してください。");
                if(choice.Quality<1 || choice.Quality>100 || choice.MaxEdge<0 || choice.MaxEdge>16000 || choice.Dpi<72 || choice.Dpi>300) throw new Exception("ファイル変換の画質・画像サイズ・解像度を確認してください。");
                ActionIcons.Validate(choice.Icon);
            }
        }
        public List<ConversionChoice> Choices(bool pdf,string initial="") { return (pdf?Pdf:Images).Where(c=>String.IsNullOrEmpty(initial)?c.Enabled:c.Operation==initial).ToList(); }
    }
    static class ConversionCatalog {
        static readonly string[] imageOperations={"image-pdf","png","jpeg","webp","tiff","bmp","gif","ico","ocr-text","ocr-copy"};
        static readonly string[] pdfOperations={"pdf-png","pdf-jpeg","pdf-webp","pdf-tiff","pdf-bmp","pdf-gif","pdf-split","pdf-merge","pdf-ocr-text","pdf-ocr-copy"};
        public static string[] Operations(bool pdf) { return pdf?pdfOperations:imageOperations; }
        public static string Label(string operation) {
            if(operation=="image-pdf") return "PDF";
            if(operation=="pdf-split") return "ページを分割";
            if(operation=="pdf-merge") return "PDFを結合";
            if(operation=="pdf-extract") return "ページを抽出";
            if(FileConversion.Ocr(operation)) return FileConversion.CopiesText(operation)?"文字をコピー":"TXT";
            return operation.Replace("pdf-","").ToUpperInvariant().Replace("WEBP","WebP");
        }
        public static string Icon(string operation) {
            if(FileConversion.Ocr(operation)) return FileConversion.CopiesText(operation)?"ms:content_copy":"ms:text_snippet";
            switch(operation.Replace("pdf-","")) { case "image-pdf": return "ms:picture_as_pdf"; case "png": return "ms:opacity"; case "jpeg": return "ms:photo_camera"; case "webp": return "ms:photo_size_select_small"; case "tiff": return "ms:layers"; case "bmp": return "ms:grid_on"; case "gif": return "ms:gif_box"; case "ico": return "ms:apps"; case "merge": return "ms:merge"; case "split": return "ms:call_split"; default: return "ms:image"; }
        }
        public static List<ConversionChoice> Defaults(bool pdf) { return Operations(pdf).Select(o=>new ConversionChoice { Operation=o,Label=Label(o) }).ToList(); }
        public static bool PdfFile(string path) { return System.IO.Path.GetExtension(path).Equals(".pdf",StringComparison.OrdinalIgnoreCase); }
        public static bool Supports(string operation,bool pdf,int count) { return Operations(pdf).Contains(operation) && (operation!="pdf-merge" || count>=2); }
    }
}

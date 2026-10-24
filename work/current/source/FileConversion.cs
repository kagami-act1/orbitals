extern alias Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pdf::PdfSharp.Pdf;
using Pdf::PdfSharp.Pdf.IO;
using Pdf::PdfSharp.Drawing;

namespace OrbitRing {
    public sealed class ConversionJob {
        public List<string> Files { get; set; }
        public string Operation { get; set; }
        public string Output { get; set; }
        public int Quality { get; set; }
        public int MaxEdge { get; set; }
        public int Dpi { get; set; }
        public string Pages { get; set; }
    }
    public sealed class ConversionResult {
        public List<string> Files { get; set; }
        public List<string> Errors { get; set; }
        public bool Cancelled { get; set; }
        public string Text { get; set; }
        public ConversionResult() { Files=new List<string>(); Errors=new List<string>(); }
    }
    static class FileConversion {
        public static readonly string[] Operations={"png","jpeg","webp","bmp","tiff","gif","ico","image-pdf","pdf-png","pdf-jpeg","pdf-webp","pdf-tiff","pdf-bmp","pdf-gif","pdf-merge","pdf-split","pdf-extract","ocr-text","ocr-copy","pdf-ocr-text","pdf-ocr-copy"};
        public static readonly string[] Names={"画像 → PNG","画像 → JPEG","画像 → WebP","画像 → BMP","画像 → TIFF","画像 → GIF（静止画）","画像 → ICO","画像 → PDF（1つにまとめる）","PDF → PNG","PDF → JPEG","PDF → WebP","PDF → TIFF","PDF → BMP","PDF → GIF（静止画）","PDFを結合","PDFを1ページずつ分割","PDFからページを抽出","画像の文字をTXTへ","画像の文字をコピー","PDFの文字をTXTへ","PDFの文字をコピー"};
        public const string Filter="画像・SVG・PDF|*.png;*.jpg;*.jpeg;*.jfif;*.webp;*.bmp;*.tif;*.tiff;*.gif;*.ico;*.svg;*.pdf|すべてのファイル|*.*";
        static readonly HashSet<string> extensions=new HashSet<string>(new[]{".png",".jpg",".jpeg",".jfif",".webp",".bmp",".tif",".tiff",".gif",".ico",".svg",".pdf"},StringComparer.OrdinalIgnoreCase);
        public static bool Supported(string path) { return !String.IsNullOrEmpty(path) && extensions.Contains(Path.GetExtension(path)); }
        public static bool PdfImage(string operation) { return operation.StartsWith("pdf-",StringComparison.Ordinal) && new[]{"png","jpeg","webp","tiff","bmp","gif"}.Contains(operation.Substring(4)); }
        public static bool Ocr(string operation) { return operation=="ocr-text" || operation=="ocr-copy" || operation=="pdf-ocr-text" || operation=="pdf-ocr-copy"; }
        public static bool CopiesText(string operation) { return operation=="ocr-copy" || operation=="pdf-ocr-copy"; }
        public static bool PdfRaster(string operation) { return PdfImage(operation) || operation=="pdf-ocr-text" || operation=="pdf-ocr-copy"; }
        public static void Validate(ConversionJob job) {
            if(job==null || job.Files==null || job.Files.Count==0 || job.Files.Count>128 || !Operations.Contains(job.Operation)) throw new Exception("変換するファイルと処理を選んでください（128件まで）。");
            if(job.Quality<1 || job.Quality>100 || job.MaxEdge<0 || job.MaxEdge>16000 || job.Dpi<72 || job.Dpi>300) throw new Exception("画質・画像サイズ・解像度の値を確認してください。");
            if(!CopiesText(job.Operation) && (String.IsNullOrWhiteSpace(job.Output) || !Directory.Exists(job.Output))) throw new Exception("保存先のフォルダーを選んでください。");
            bool pdf=job.Operation.StartsWith("pdf-",StringComparison.Ordinal);
            foreach(string path in job.Files) {
                if(!File.Exists(path) || !Supported(path)) throw new Exception("対応する画像・PDFを選んでください："+Path.GetFileName(path));
                if((Path.GetExtension(path).ToLowerInvariant()==".pdf")!=pdf) throw new Exception(pdf?"この処理にはPDFを選んでください。":"この処理には画像を選んでください。");
                if(new FileInfo(path).Length>512L*1024*1024) throw new Exception("1ファイルは512MBまでです："+Path.GetFileName(path));
            }
            if(job.Operation=="pdf-extract" && String.IsNullOrWhiteSpace(job.Pages)) throw new Exception("抽出するページを指定してください。例：1,3-5");
        }
        public static int[] Pages(string text,int count) {
            if(count<1 || count>1000) throw new Exception("PDFは1〜1000ページに対応しています。");
            if(String.IsNullOrWhiteSpace(text)) return Enumerable.Range(0,count).ToArray();
            if(text.Length>2048) throw new Exception("ページ指定が長すぎます。");
            List<int> pages=new List<int>();
            foreach(string token in text.Replace('，',',').Split(',')) {
                string[] pair=token.Trim().Split('-'); int first,last;
                if(pair.Length>2 || !Int32.TryParse(pair[0],out first)) throw new Exception("ページは1,3-5のように指定してください。");
                last=first; if(pair.Length==2 && !Int32.TryParse(pair[1],out last)) throw new Exception("ページ範囲を確認してください。");
                if(first<1 || last<first || last>count) throw new Exception("指定したページがPDFの範囲外です。");
                for(int i=first;i<=last;i++) if(!pages.Contains(i-1)) pages.Add(i-1);
            }
            return pages.ToArray();
        }
        public static string Quote(string value) {
            StringBuilder result=new StringBuilder("\""); int slashes=0;
            foreach(char c in value) { if(c=='\\') { slashes++; continue; } if(c=='\"') { result.Append('\\',slashes*2+1); result.Append(c); slashes=0; } else { result.Append('\\',slashes); result.Append(c); slashes=0; } }
            result.Append('\\',slashes*2); return result.Append('"').ToString();
        }
        static void Cancel(string directory) { if(!Directory.Exists(directory) || File.Exists(Path.Combine(directory,"cancel"))) throw new OperationCanceledException(); }
        static void Webp(string tool,IEnumerable<string> args,string directory) {
            string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"conversion",tool+".exe");
            using(Process process=Process.Start(new ProcessStartInfo(executable,String.Join(" ",args.Select(Quote))) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true })) {
                var errors=process.StandardError.ReadToEndAsync(); var output=process.StandardOutput.ReadToEndAsync();
                try { while(!process.WaitForExit(100)) Cancel(directory); process.WaitForExit(); if(process.ExitCode!=0) throw new Exception("WebPを処理できませんでした。画像が壊れていないか確認してください。"); }
                catch { if(!process.HasExited) { process.Kill(); process.WaitForExit(); } throw; }
                errors.GetAwaiter().GetResult(); output.GetAwaiter().GetResult();
            }
        }
        static BitmapSource Image(string path,int edge,string directory) {
            string decodedPath=null;
            if(Path.GetExtension(path).Equals(".svg",StringComparison.OrdinalIgnoreCase)) { decodedPath=Path.Combine(directory,"svg-"+Guid.NewGuid().ToString("N")+".png"); SvgRasterizer.Render(path,edge,decodedPath,directory); path=decodedPath; }
            if(Path.GetExtension(path).Equals(".webp",StringComparison.OrdinalIgnoreCase)) { decodedPath=Path.Combine(directory,"decode-"+Guid.NewGuid().ToString("N")+".png"); Webp("dwebp",new[]{path,"-o",decodedPath},directory); path=decodedPath; }
            try {
            BitmapSource source;
            using(Stream input=File.OpenRead(path)) {
                bool icon=Path.GetExtension(path).Equals(".ico",StringComparison.OrdinalIgnoreCase);
                BitmapDecoder decoder=BitmapDecoder.Create(input,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand);
                BitmapFrame header=icon?decoder.Frames.OrderByDescending(f=>(long)f.PixelWidth*f.PixelHeight).First():decoder.Frames[0];
                if(header.PixelWidth<1 || header.PixelHeight<1 || (long)header.PixelWidth*header.PixelHeight>100000000) throw new Exception("画像が大きすぎます（1億画素まで）。");
                input.Position=0;
                if(icon) source=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.OrderByDescending(f=>(long)f.PixelWidth*f.PixelHeight).First();
                else {
                BitmapImage decoded=new BitmapImage(); decoded.BeginInit(); decoded.CacheOption=BitmapCacheOption.OnLoad; decoded.CreateOptions=BitmapCreateOptions.PreservePixelFormat; decoded.StreamSource=input;
                if(edge>0 && Math.Max(header.PixelWidth,header.PixelHeight)>edge) { if(header.PixelWidth>=header.PixelHeight) decoded.DecodePixelWidth=edge; else decoded.DecodePixelHeight=edge; }
                decoded.EndInit(); source=decoded;
                }
            }
            if(edge>0 && Math.Max(source.PixelWidth,source.PixelHeight)>edge) { double scale=(double)edge/Math.Max(source.PixelWidth,source.PixelHeight); source=new TransformedBitmap(source,new ScaleTransform(scale,scale)); }
            source.Freeze(); return source;
            } finally { if(decodedPath!=null && File.Exists(decodedPath)) File.Delete(decodedPath); }
        }
        internal static BitmapSource Opaque(BitmapSource source) {
            DrawingVisual visual=new DrawingVisual(); using(DrawingContext dc=visual.RenderOpen()) { dc.DrawRectangle(Brushes.White,null,new Rect(0,0,source.PixelWidth,source.PixelHeight)); dc.DrawImage(source,new Rect(0,0,source.PixelWidth,source.PixelHeight)); }
            RenderTargetBitmap bitmap=new RenderTargetBitmap(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
        }
        static void Encode(BitmapSource source,string operation,int quality,string path) {
            BitmapEncoder encoder;
            if(operation=="jpeg") { encoder=new JpegBitmapEncoder { QualityLevel=quality }; source=Opaque(source); }
            else if(operation=="bmp") { encoder=new BmpBitmapEncoder(); source=Opaque(source); }
            else if(operation=="tiff") encoder=new TiffBitmapEncoder { Compression=TiffCompressOption.Zip };
            else if(operation=="gif") encoder=new GifBitmapEncoder();
            else encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source)); using(Stream output=File.Create(path)) encoder.Save(output);
        }
        static void Icon(BitmapSource source,string path) {
            int[] sizes={16,32,48,256}; List<byte[]> images=new List<byte[]>();
            foreach(int size in sizes) {
                double scale=Math.Min((double)size/source.PixelWidth,(double)size/source.PixelHeight),w=source.PixelWidth*scale,h=source.PixelHeight*scale;
                DrawingVisual visual=new DrawingVisual(); using(DrawingContext dc=visual.RenderOpen()) dc.DrawImage(source,new Rect((size-w)/2,(size-h)/2,w,h));
                RenderTargetBitmap bitmap=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32); bitmap.Render(visual);
                PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(MemoryStream memory=new MemoryStream()) { encoder.Save(memory); images.Add(memory.ToArray()); }
            }
            using(BinaryWriter writer=new BinaryWriter(File.Create(path))) { writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); int offset=6+16*sizes.Length;
                for(int i=0;i<sizes.Length;i++) { writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(images[i].Length); writer.Write(offset); offset+=images[i].Length; }
                foreach(byte[] bytes in images) writer.Write(bytes);
            }
        }
        static void WriteImage(BitmapSource source,string operation,int quality,string path,string directory) {
            if(operation=="ico") Icon(source,path);
            else if(operation=="webp") { string png=Temporary(directory); try { Encode(source,"png",quality,png); Webp("cwebp",new[]{"-quiet","-q",quality.ToString(),png,"-o",path},directory); } finally { if(File.Exists(png)) File.Delete(png); } }
            else Encode(source,operation,quality,path);
        }
        static string Extension(string operation) { return operation=="jpeg"?"jpg":operation=="tiff"?"tif":operation; }
        static string Commit(string temporary,string directory,string name) {
            string stem=Path.GetFileNameWithoutExtension(name),extension=Path.GetExtension(name);
            string staged=Path.Combine(directory,".orbitals-"+Guid.NewGuid().ToString("N")+".partial");
            try {
                File.Copy(temporary,staged,false);
                for(int i=0;i<10000;i++) { string path=Path.Combine(directory,stem+(i==0?"":" ("+i+")")+extension); try { File.Move(staged,path); File.Delete(temporary); return path; } catch(IOException) { if(!File.Exists(path)) throw; } }
                throw new IOException("同じ名前のファイルが多すぎます。");
            } finally { if(File.Exists(staged)) File.Delete(staged); }
        }
        static string Temporary(string directory) { return Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp"); }
        static void Status(string directory,string text) { try { File.WriteAllText(Path.Combine(directory,"progress"),text,Encoding.UTF8); } catch(IOException) { } }
        public static ConversionResult Run(ConversionJob job,string directory) {
            Validate(job); ConversionResult result=new ConversionResult();
            try {
                if(job.Operation=="image-pdf" || job.Operation=="pdf-merge") {
                    using(PdfDocument target=new PdfDocument()) {
                        foreach(string path in job.Files) {
                            Cancel(directory); Status(directory,Path.GetFileName(path));
                            if(job.Operation=="image-pdf") {
                                string png=Temporary(directory); Encode(Image(path,job.MaxEdge,directory),"png",job.Quality,png);
                                using(XImage image=XImage.FromFile(png)) { PdfPage page=target.AddPage(); page.Width=XUnit.FromPoint(image.PixelWidth*72.0/96); page.Height=XUnit.FromPoint(image.PixelHeight*72.0/96); using(XGraphics graphics=XGraphics.FromPdfPage(page)) graphics.DrawImage(image,0,0,page.Width.Point,page.Height.Point); } File.Delete(png);
                            } else using(PdfDocument source=PdfReader.Open(path,PdfDocumentOpenMode.Import)) { if(source.PageCount+target.PageCount>1000) throw new Exception("結合後のPDFは1000ページまでです。"); for(int i=0;i<source.PageCount;i++) { Cancel(directory); target.AddPage(source.Pages[i]); } }
                        }
                        Cancel(directory); string temp=Temporary(directory); target.Save(temp); Cancel(directory); result.Files.Add(Commit(temp,job.Output,Path.GetFileNameWithoutExtension(job.Files[0])+(job.Operation=="pdf-merge"?"-結合":"")+".pdf"));
                    }
                } else foreach(string path in job.Files) {
                    Cancel(directory); Status(directory,Path.GetFileName(path));
                    try {
                        string stem=Path.GetFileNameWithoutExtension(path);
                        if(Ocr(job.Operation)) {
                            StringBuilder recognized=new StringBuilder();
                            if(job.Operation.StartsWith("pdf-",StringComparison.Ordinal)) {
                                object document=WindowsPdf.Open(path);
                                try { foreach(int page in Pages(job.Pages,WindowsPdf.Count(document))) { Cancel(directory); Status(directory,Path.GetFileName(path)+" · "+(page+1)+"ページの文字を認識"); string png=Temporary(directory); try { WindowsPdf.Render(document,page,png,job.Dpi); string pageText=WindowsOcr.Read(Image(png,0,directory),true); if(!String.IsNullOrWhiteSpace(pageText)) { if(recognized.Length>0) recognized.AppendLine().AppendLine(); recognized.Append(pageText); } if(recognized.Length>1000000) throw new Exception("認識する文字は一度に100万文字までです。PDFを分けてください。"); } finally { if(File.Exists(png)) File.Delete(png); } } }
                                finally { IDisposable disposable=document as IDisposable; if(disposable!=null) disposable.Dispose(); }
                            } else recognized.Append(WindowsOcr.Read(Image(path,0,directory)));
                            Cancel(directory); string text=recognized.ToString();
                            if(String.IsNullOrWhiteSpace(text)) throw new Exception("文字が見つかりませんでした。解像度や文字の向きを確認してください。");
                            if((result.Text==null?0:result.Text.Length)+text.Length>1000000) throw new Exception("認識する文字は一度に100万文字までです。ファイルを分けてください。");
                            result.Text=(String.IsNullOrEmpty(result.Text)?"":result.Text+Environment.NewLine+Environment.NewLine)+text;
                            if(!CopiesText(job.Operation)) { string temp=Temporary(directory); File.WriteAllText(temp,text,new UTF8Encoding(false)); result.Files.Add(Commit(temp,job.Output,stem+".txt")); }
                        } else if(PdfImage(job.Operation)) {
                            object document=WindowsPdf.Open(path); int[] pages=Pages(job.Pages,WindowsPdf.Count(document)); if(result.Files.Count+pages.Length>1000) throw new Exception("一度に出力できるファイルは1000件までです。PDFを分けて変換してください。");
                            try { foreach(int page in pages) { Cancel(directory); Status(directory,Path.GetFileName(path)+" · "+(page+1)+"ページ"); string temp=Temporary(directory); WindowsPdf.Render(document,page,temp,job.Dpi); string format=job.Operation.Substring(4);
                                if(format!="png" || job.MaxEdge>0) { string encoded=Temporary(directory); WriteImage(Image(temp,job.MaxEdge,directory),format,job.Quality,encoded,directory); File.Delete(temp); temp=encoded; }
                                Cancel(directory); result.Files.Add(Commit(temp,job.Output,stem+"-"+(page+1).ToString("D3")+"."+Extension(format)));
                            } } finally { IDisposable disposable=document as IDisposable; if(disposable!=null) disposable.Dispose(); }
                        } else if(job.Operation=="pdf-split" || job.Operation=="pdf-extract") {
                            using(PdfDocument source=PdfReader.Open(path,PdfDocumentOpenMode.Import)) {
                                int[] pages=Pages(job.Pages,source.PageCount); if(job.Operation=="pdf-split" && result.Files.Count+pages.Length>1000) throw new Exception("一度に出力できるファイルは1000件までです。PDFを分けて変換してください。");
                                if(job.Operation=="pdf-extract") { using(PdfDocument target=new PdfDocument()) { foreach(int page in pages) { Cancel(directory); target.AddPage(source.Pages[page]); } string temp=Temporary(directory); target.Save(temp); Cancel(directory); result.Files.Add(Commit(temp,job.Output,stem+"-抽出.pdf")); } }
                                else foreach(int page in pages) { Cancel(directory); using(PdfDocument target=new PdfDocument()) { target.AddPage(source.Pages[page]); string temp=Temporary(directory); target.Save(temp); Cancel(directory); result.Files.Add(Commit(temp,job.Output,stem+"-"+(page+1).ToString("D3")+".pdf")); } }
                            }
                        } else {
                            BitmapSource image=Image(path,job.MaxEdge,directory); string temp=Temporary(directory);
                            WriteImage(image,job.Operation,job.Quality,temp,directory);
                            Cancel(directory); result.Files.Add(Commit(temp,job.Output,stem+"."+Extension(job.Operation)));
                        }
                    } catch(OperationCanceledException) { throw; } catch(Exception ex) { result.Errors.Add(Path.GetFileName(path)+"："+Error(ex)); }
                }
            } catch(OperationCanceledException) { result.Cancelled=true; } catch(Exception ex) { result.Errors.Add(Error(ex)); }
            return result;
        }
        static string Error(Exception ex) { while(ex is System.Reflection.TargetInvocationException && ex.InnerException!=null) ex=ex.InnerException; return ex.Message; }
        public static int Worker(string path) {
            string directory=Path.GetDirectoryName(Path.GetFullPath(path)); ConversionResult result;
            try { result=Run(Json.ReadFile<ConversionJob>(path),directory); } catch(Exception ex) { result=new ConversionResult(); result.Errors.Add(Error(ex)); }
            try { File.WriteAllText(Path.Combine(directory,"result.json"),Json.Write(result),new UTF8Encoding(false)); } catch(IOException) { return 1; } catch(UnauthorizedAccessException) { return 1; }
            return result.Errors.Count==0?0:1;
        }
    }
}

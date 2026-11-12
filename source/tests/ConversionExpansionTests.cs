using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class ConversionExpansionTests {
        static void Check(bool value,string message,List<string> results) { if(!value) throw new Exception(message); results.Add("PASS "+message); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static ConversionResult Run(string operation,string input,string output,string root,int edge=0) {
            string directory=Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            ConversionResult result=FileConversion.Run(new ConversionJob { Files=new List<string>{input},Operation=operation,Output=output,Quality=90,MaxEdge=edge,Dpi=150 },directory);
            if(result.Errors.Count>0) throw new Exception(String.Join("\n",result.Errors)); return result;
        }
        public static List<string> Run() {
            List<string> r=new List<string>(); string root=Path.Combine(Path.GetTempPath(),"orbitals-expanded-conversion-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try {
                string svg=Path.Combine(root,"見本.svg"),output=Path.Combine(root,"output"); Directory.CreateDirectory(output);
                File.WriteAllText(svg,"<svg xmlns='http://www.w3.org/2000/svg' width='80' height='40'><defs><linearGradient id='g'><stop stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient></defs><rect width='80' height='40' fill='url(#g)'/></svg>",new UTF8Encoding(false));
                byte[] original=File.ReadAllBytes(svg);
                ConversionResult png=Run("png",svg,output,root,240); BitmapFrame frame=BitmapFrame.Create(new Uri(png.Files.Single()),BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
                Check(frame.PixelWidth==240 && frame.PixelHeight==120,"SVG rendering honors requested raster dimensions and aspect ratio",r);
                foreach(string operation in new[]{"jpeg","webp","image-pdf"}) Check(Run(operation,svg,output,root,120).Files.Count==1,"SVG inputs use the existing conversion pipeline for "+operation,r);
                Check(File.ReadAllBytes(svg).SequenceEqual(original),"SVG rendering preserves original source bytes",r);
                string bad=Path.Combine(root,"external.svg");
                foreach(string body in new[]{"<image href='file:///C:/secret.png'/>","<image href='https://example.com/a.png'/>","<style>@import 'a.css';</style>","<style>rect{fill:url(file:///C:/a)}</style>","<use href='data:image/svg+xml;base64,PHN2Zy8+'/>","<script>alert(1)</script>"}) { File.WriteAllText(bad,"<svg xmlns='http://www.w3.org/2000/svg'>"+body+"</svg>"); Check(Reject(delegate { SvgRasterizer.Prepare(bad,100); }),"unsafe SVG resource or executable content is rejected before rendering",r); }
                File.WriteAllText(bad,"<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///C:/secret.txt'>]><svg xmlns='http://www.w3.org/2000/svg'>&x;</svg>"); Check(Reject(delegate { SvgRasterizer.Prepare(bad,100); }),"SVG parsing forbids external entities and DTD expansion",r);
                Check(Reject(delegate { SvgRasterizer.Prepare(svg,16000); }),"oversized SVG raster allocation is rejected before launching the renderer",r);
                DrawingVisual visual=new DrawingVisual(); using(DrawingContext dc=visual.RenderOpen()) { dc.DrawRectangle(Brushes.White,null,new Rect(0,0,720,170)); dc.DrawText(new FormattedText("ORBITALS 2468",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Arial"),64,Brushes.Black,1),new Point(24,45)); }
                RenderTargetBitmap bitmap=new RenderTargetBitmap(720,170,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); string image=Path.Combine(root,"文字.png");
                PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(Stream stream=File.Create(image)) encoder.Save(stream);
                ConversionResult text=Run("ocr-text",image,output,root); string compact=new string(text.Text.Where(c=>!Char.IsWhiteSpace(c)).ToArray());
                Check(text.Files.Count==1 && compact.Contains("2468") && compact.IndexOf("ORBITALS",StringComparison.OrdinalIgnoreCase)>=0 && File.ReadAllText(text.Files[0],Encoding.UTF8)==text.Text,"local image OCR saves recognized lines as UTF-8 text",r);
                int count=Directory.GetFiles(output).Length; ConversionResult copy=Run("ocr-copy",image,null,root);
                Check(copy.Files.Count==0 && copy.Text.Contains("2468") && Directory.GetFiles(output).Length==count,"OCR copy produces text without requesting or creating an output file",r);
                string pdf=Run("image-pdf",image,output,root).Files.Single(); ConversionResult pdfText=Run("pdf-ocr-text",pdf,output,root);
                Check(pdfText.Files.Count==1 && pdfText.Text.Contains("2468"),"scanned PDF pages are rendered and recognized locally",r);
                Check(Run("pdf-ocr-copy",pdf,null,root).Text.Contains("2468"),"scanned PDF text can be returned for clipboard copying",r);
                DrawingVisual blankVisual=new DrawingVisual(); using(DrawingContext dc=blankVisual.RenderOpen()) dc.DrawRectangle(Brushes.White,null,new Rect(0,0,120,120)); RenderTargetBitmap blank=new RenderTargetBitmap(120,120,96,96,PixelFormats.Pbgra32); blank.Render(blankVisual);
                Check(String.IsNullOrWhiteSpace(WindowsOcr.Read(blank,true)),"blank PDF pages can be skipped without interrupting text extraction",r);
                ConversionPreferences legacy=new ConversionPreferences(); legacy.Images=legacy.Images.Take(8).Reverse().ToList(); legacy.Pdf=legacy.Pdf.Take(8).ToList(); legacy.Images[0].Label="自分のアイコン"; legacy.Images[0].Enabled=false; legacy.Validate();
                Check(legacy.Images.Count==9 && legacy.Images[8].Operation=="zip" && legacy.Images[0].Operation=="ico" && legacy.Images[0].Label=="自分のアイコン" && !legacy.Images[0].Enabled && legacy.Images.All(c=>!FileConversion.Ocr(c.Operation)),"legacy conversion preferences gain ZIP while keeping removed destinations and customized ordering",r);
                foreach(bool pdfSource in new[]{false,true}) {
                    List<ConversionChoice> choices=ConversionCatalog.Defaults(pdfSource); string[] icons=choices.Select(c=>ConversionCatalog.Icon(c.Operation)).ToArray(); foreach(string icon in icons) ActionIcons.Validate(icon);
                    Check(icons.Distinct().Count()==choices.Count,"each conversion destination has a distinct built-in icon: "+pdfSource,r);
                }
                Canvas drawing=ConversionPlanets.Drawing(ConversionCatalog.Defaults(false).Take(8).ToList(),new AppearancePreferences());
                Check(drawing.Children.OfType<Button>().Count()==16 && Math.Abs(((System.Windows.Shapes.Ellipse)drawing.Children[0]).Width-378)<.001 && drawing.Children.OfType<Button>().Where(b=>b.Width==76).All(b=>b.Template!=null),"conversion uses the same ring proportions and icon button template as planet",r);
            } finally { Directory.Delete(root,true); }
            return r;
        }
    }
}

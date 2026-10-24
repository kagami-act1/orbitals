extern alias Pdf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pdf::PdfSharp.Pdf;
using Pdf::PdfSharp.Pdf.IO;

namespace OrbitRing {
    static class ConversionTests {
        static void Check(bool condition,string name,List<string> results) { if(!condition) throw new Exception(name); results.Add("PASS "+name); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static void Image(string path,int width) {
            byte[] pixels=new byte[width*40*4]; for(int i=0;i<pixels.Length;i+=4) { pixels[i]=100; pixels[i+1]=200; pixels[i+2]=50; pixels[i+3]=(byte)(i<16?0:255); }
            BitmapSource bitmap=BitmapSource.Create(width,40,96,96,PixelFormats.Bgra32,null,pixels,width*4); PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(Stream stream=File.Create(path)) encoder.Save(stream);
        }
        static BitmapSource Read(string path) { using(Stream stream=File.OpenRead(path)) { BitmapSource image=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0]; image.Freeze(); return image; } }
        static ConversionJob Job(string operation,string output,params string[] files) { return new ConversionJob { Files=files.ToList(),Operation=operation,Output=output,Quality=90,Dpi=96 }; }
        static ConversionResult Convert(ConversionJob job,string root) { string directory=Path.Combine(root,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); ConversionResult result=FileConversion.Run(job,directory); if(result.Errors.Count>0) throw new Exception(String.Join("\n",result.Errors)); return result; }
        public static List<string> Run() {
            List<string> r=new List<string>(); string root=Path.Combine(Path.GetTempPath(),"orbitals-conversion-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try {
                string output=Path.Combine(root,"出力"),one=Path.Combine(root,"元画像 ①.png"),two=Path.Combine(root,"2.png"); Directory.CreateDirectory(output); Image(one,80); Image(two,160); byte[] original=File.ReadAllBytes(one);
                Check(FileConversion.Pages("3,1-2,2",3).SequenceEqual(new[]{2,0,1}) && FileConversion.Pages("",2).SequenceEqual(new[]{0,1}),"PDF page selections preserve requested order and remove duplicates",r);
                foreach(string range in new[]{"0","2-1","1-4","a","1,","1-2-3"}) Check(Reject(delegate { FileConversion.Pages(range,3); }),"invalid PDF page range is rejected: "+range,r);
                foreach(string format in new[]{"png","jpeg","bmp","tiff","webp","gif"}) {
                    ConversionJob job=Job(format,output,one); job.MaxEdge=40; ConversionResult result=Convert(job,root); Check(result.Files.Count==1 && File.Exists(result.Files[0]),"image conversion produces "+format,r);
                    if(format=="webp") { ConversionResult decoded=Convert(Job("png",output,result.Files[0]),root); BitmapSource bitmap=Read(decoded.Files[0]); Check(bitmap.PixelWidth==40 && bitmap.PixelHeight==20,"WebP encoding and decoding preserve resized dimensions",r); }
                    else { BitmapSource bitmap=Read(result.Files[0]); Check(bitmap.PixelWidth==40 && bitmap.PixelHeight==20,"image aspect ratio survives "+format+" resize",r); }
                }
                ConversionResult icon=Convert(Job("ico",output,one),root); using(Stream stream=File.OpenRead(icon.Files[0])) { BitmapDecoder decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad); Check(decoder.Frames.Count==4 && decoder.Frames.Any(f=>f.PixelWidth==256) && decoder.Frames.Any(f=>f.PixelWidth==16),"ICO output contains four readable icon sizes",r); }
                ConversionResult iconBack=Convert(Job("png",output,icon.Files[0]),root); Check(iconBack.Files.Count==1 && Read(iconBack.Files[0]).PixelWidth==256,"ICO inputs use their largest available frame",r);
                ConversionResult retained=Convert(Job("png",root,one),root); Check(retained.Files[0]!=one && File.ReadAllBytes(one).SequenceEqual(original),"same-folder conversion allocates a unique filename and never overwrites the source",r);
                ConversionResult transparent=Convert(Job("png",output,one),root); BitmapSource rgba=new FormatConvertedBitmap(Read(transparent.Files[0]),PixelFormats.Bgra32,null,0); byte[] pixel=new byte[4]; rgba.CopyPixels(new System.Windows.Int32Rect(0,0,1,1),pixel,4,0); Check(pixel[3]==0,"PNG conversion preserves transparent pixels",r);
                ConversionResult pdf=Convert(Job("image-pdf",output,one,two),root); using(PdfDocument document=PdfReader.Open(pdf.Files[0],PdfDocumentOpenMode.Import)) Check(document.PageCount==2 && Math.Abs(document.Pages[0].Width.Point-60)<.01 && Math.Abs(document.Pages[1].Width.Point-120)<.01,"image-to-PDF keeps file order and image proportions",r);
                ConversionResult rendered=Convert(Job("pdf-png",output,pdf.Files[0]),root); Check(rendered.Files.Count==2 && Read(rendered.Files[0]).PixelWidth==80 && Read(rendered.Files[1]).PixelWidth==160,"Windows PDF renderer produces one PNG per page at the selected resolution",r);
                ConversionJob jpegJob=Job("pdf-jpeg",output,pdf.Files[0]); jpegJob.Pages="2"; jpegJob.MaxEdge=80; ConversionResult jpeg=Convert(jpegJob,root); Check(jpeg.Files.Count==1 && Read(jpeg.Files[0]).PixelWidth==80,"PDF-to-JPEG honors page selection and maximum image size",r);
                foreach(string format in new[]{"webp","tiff","bmp","gif"}) { ConversionJob job=Job("pdf-"+format,output,pdf.Files[0]); job.Pages="1"; ConversionResult converted=Convert(job,root); Check(converted.Files.Count==1 && File.Exists(converted.Files[0]),"PDF pages convert to "+format,r); if(format=="webp") { ConversionResult decoded=Convert(Job("png",output,converted.Files[0]),root); Check(Read(decoded.Files[0]).PixelWidth==80,"PDF WebP output decodes at the selected resolution",r); } else Check(Read(converted.Files[0]).PixelWidth==80,"PDF raster dimensions survive "+format,r); }
                ConversionResult split=Convert(Job("pdf-split",output,pdf.Files[0]),root); Check(split.Files.Count==2 && split.Files.All(p=>{using(PdfDocument d=PdfReader.Open(p,PdfDocumentOpenMode.Import)) return d.PageCount==1;}),"PDF splitting creates separate readable single-page documents",r);
                ConversionResult merged=Convert(Job("pdf-merge",output,split.Files[1],split.Files[0]),root); using(PdfDocument d=PdfReader.Open(merged.Files[0],PdfDocumentOpenMode.Import)) Check(d.PageCount==2 && d.Pages[0].Width.Point>d.Pages[1].Width.Point,"PDF merging follows the selected file order",r);
                ConversionJob extractJob=Job("pdf-extract",output,pdf.Files[0]); extractJob.Pages="2,1"; ConversionResult extracted=Convert(extractJob,root); using(PdfDocument d=PdfReader.Open(extracted.Files[0],PdfDocumentOpenMode.Import)) Check(d.PageCount==2 && d.Pages[0].Width.Point>d.Pages[1].Width.Point,"PDF extraction keeps explicitly requested page order",r);
                string cancelled=Path.Combine(root,"cancel-test"); Directory.CreateDirectory(cancelled); File.WriteAllText(Path.Combine(cancelled,"cancel"),""); ConversionResult cancel=FileConversion.Run(Job("png",output,one),cancelled); Check(cancel.Cancelled && cancel.Files.Count==0,"cancellation stops conversion before creating final output",r);
                ConversionResult abandoned=FileConversion.Run(Job("png",output,one),Path.Combine(root,"closed-window")); Check(abandoned.Cancelled && abandoned.Files.Count==0,"closing the parent window cannot leave a worker converting after its temporary workspace is removed",r);
                string broken=Path.Combine(root,"壊れた.png"); File.WriteAllText(broken,"invalid"); string failure=Path.Combine(root,"failure"); Directory.CreateDirectory(failure); ConversionResult errors=FileConversion.Run(Job("png",output,broken),failure); Check(errors.Errors.Count==1 && errors.Files.Count==0,"damaged input is reported without a misleading output file",r);
                Check(Reject(delegate { FileConversion.Validate(Job("pdf-merge",output,one)); }) && Reject(delegate { FileConversion.Validate(Job("png",output,pdf.Files[0])); }),"mixed image and PDF operations are rejected before starting a worker",r);
                string worker=Path.Combine(root,"worker"); Directory.CreateDirectory(worker); string jobPath=Path.Combine(worker,"job.json"); File.WriteAllText(jobPath,Json.Write(Job("png",output,one)));
                using(Process process=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--convert-job "+FileConversion.Quote(jobPath)) { UseShellExecute=false,CreateNoWindow=true })) { if(!process.WaitForExit(30000)) { process.Kill(); throw new Exception("conversion worker timed out"); } Check(process.ExitCode==0 && Json.ReadFile<ConversionResult>(Path.Combine(worker,"result.json")).Files.Count==1,"on-demand worker accepts Unicode file paths and exits independently of the resident app",r); }
                Check(File.ReadAllBytes(one).SequenceEqual(original),"all conversion operations leave the original image bytes intact",r);
                string canceledWorker=Path.Combine(root,"cancelled-worker"); Directory.CreateDirectory(canceledWorker); Check(Reject(delegate { ConversionRunner.RunAsync(Job("png",output,one),canceledWorker,false,new System.Threading.CancellationToken(true)).GetAwaiter().GetResult(); }),"a canceled job does not launch an unbounded worker",r);
            } finally { Directory.Delete(root,true); }
            return r;
        }
    }
}

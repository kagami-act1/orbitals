using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class ConversionPlatformTests {
        static void Check(bool value,string message,List<string> result) { if(!value) throw new Exception(message); result.Add("PASS "+message); }
        public static List<string> Run() {
            List<string> result=new List<string>(); string directory=Path.Combine(Path.GetTempPath(),"orbitals-platform-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try {
                string path=Path.Combine(directory,"native.png"); byte[] pixels=new byte[80*40*4];
                for(int i=0;i<pixels.Length;i+=4) { pixels[i+1]=180; pixels[i+3]=255; }
                PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(80,40,96,96,PixelFormats.Bgra32,null,pixels,320)));
                using(Stream stream=File.Create(path)) encoder.Save(stream);
                BitmapSource native;
                Check(WindowsImageDecoder.TryRead(path,40,out native) && native.PixelWidth==40 && native.PixelHeight==20 && native.IsFrozen,"the Windows image API decodes and resizes a local image without a helper process",result);
                byte[] color=new byte[4]; native.CopyPixels(new Int32Rect(10,10,1,1),color,4,0);
                Check(color[1]>=170 && color[3]==255,"Windows native decoding preserves image color and alpha",result);
                bool missing=false; try { WindowsImageDecoder.TryRead(Path.Combine(directory,"missing.png"),0,out native); } catch(FileNotFoundException) { missing=true; }
                Check(missing,"missing images retain their file error instead of triggering a codec fallback",result);
                string text=new string('x',20000)+"end";
                using(StringReader reader=new StringReader(text)) Check(ConversionProcesses.ReadOutput(reader,4096).GetAwaiter().GetResult().Length==4096 && reader.Peek()==-1,"external tool output is fully drained while retained diagnostics stay bounded",result);
                using(StringReader reader=new StringReader(text)) Check(ConversionProcesses.ReadOutput(reader,0).GetAwaiter().GetResult()=="" && reader.Peek()==-1,"unused external tool output is discarded without a growing text buffer",result);
                Size normal=OfficeDocumentConverter.SlidePixels(720,405,96,0),small=OfficeDocumentConverter.SlidePixels(720,405,300,640),large=OfficeDocumentConverter.SlidePixels(7200,4050,300,0);
                Check(normal==new Size(960,540) && small==new Size(640,360),"PowerPoint's image export receives the requested resolution and size without a PDF intermediate",result);
                Check(large.Width*large.Height<40010000 && Math.Abs(large.Width/large.Height-16.0/9)<.001,"oversized slide exports keep their aspect ratio within the image memory limit",result);
                Preferences config=Preferences.Defaults(); string before=Json.Write(config); config.Conversion.Validate();
                Check(Json.Write(config)==before && !AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name.StartsWith("Magick.",StringComparison.Ordinal)),"conversion platform selection changes neither saved options nor resident image-library loading",result);
            } finally { Directory.Delete(directory,true); }
            return result;
        }
    }
}

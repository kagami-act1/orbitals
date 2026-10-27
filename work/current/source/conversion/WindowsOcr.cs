extern alias WinRT;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class WindowsOcr {
        public static string Read(BitmapSource image,bool allowEmpty=false) {
            Type engineType=WindowsRuntimeApi.Get("Windows.Media.Ocr.OcrEngine");
            object engine=engineType.GetMethod("TryCreateFromUserProfileLanguages").Invoke(null,null);
            if(engine==null) throw new Exception("Windowsの表示言語に対応するOCRがありません。Windowsの言語オプションで文字認識機能を追加してください。");
            int maximum=(int)(uint)engineType.GetProperty("MaxImageDimension").GetValue(null,null);
            double scale=Math.Min(1,(double)maximum/Math.Max(image.PixelWidth,image.PixelHeight));
            if(scale<1) image=new TransformedBitmap(image,new ScaleTransform(scale,scale));
            image=FileConversion.Opaque(image); image=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
            int stride=checked(image.PixelWidth*4); byte[] pixels=new byte[checked(stride*image.PixelHeight)]; image.CopyPixels(pixels,stride,0);
            MethodInfo bufferMethod=typeof(WinRT::System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions).GetMethods().Single(m=>m.Name=="AsBuffer" && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==typeof(byte[]));
            object buffer=bufferMethod.Invoke(null,new object[]{pixels});
            Type bitmapType=WindowsRuntimeApi.Get("Windows.Graphics.Imaging.SoftwareBitmap"),formatType=WindowsRuntimeApi.Get("Windows.Graphics.Imaging.BitmapPixelFormat"),alphaType=WindowsRuntimeApi.Get("Windows.Graphics.Imaging.BitmapAlphaMode");
            object bitmap=bitmapType.GetMethods().Single(m=>m.Name=="CreateCopyFromBuffer" && m.GetParameters().Length==5).Invoke(null,new[]{buffer,Enum.Parse(formatType,"Bgra8"),(object)image.PixelWidth,image.PixelHeight,Enum.Parse(alphaType,"Ignore")});
            try {
                object result=WindowsRuntimeApi.Wait(engineType.GetMethod("RecognizeAsync").Invoke(engine,new[]{bitmap}),WindowsRuntimeApi.Get("Windows.Media.Ocr.OcrResult"));
                IEnumerable lines=(IEnumerable)result.GetType().GetProperty("Lines").GetValue(result,null);
                string text=String.Join(Environment.NewLine,lines.Cast<object>().Select(line=>(string)line.GetType().GetProperty("Text").GetValue(line,null)));
                if(String.IsNullOrWhiteSpace(text) && !allowEmpty) throw new Exception("文字が見つかりませんでした。解像度や文字の向きを確認してください。");
                return text;
            } finally { IDisposable disposable=bitmap as IDisposable; if(disposable!=null) disposable.Dispose(); }
        }
    }
}

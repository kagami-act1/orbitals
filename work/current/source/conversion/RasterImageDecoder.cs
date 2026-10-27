using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class RasterImageDecoder {
        public static BitmapSource Read(string path,int edge,string directory) {
            bool modern=ModernImageCodec.Supported(path),webp=Path.GetExtension(path).Equals(".webp",StringComparison.OrdinalIgnoreCase);
            BitmapSource native;
            if((modern || webp) && WindowsImageDecoder.TryRead(path,edge,out native)) return native;
            string converted=null;
            try {
                if(modern || webp || Path.GetExtension(path).Equals(".svg",StringComparison.OrdinalIgnoreCase)) {
                    converted=Path.Combine(directory,"decoded-"+Guid.NewGuid().ToString("N")+".png");
                    if(modern) ModernImageCodec.DecodeFallback(path,converted,edge,directory);
                    else if(webp) ConversionProcesses.Run(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"conversion","dwebp.exe"),new[]{path,"-o",converted},directory,"WebPを読み込めませんでした。");
                    else SvgRasterizer.Render(path,edge,converted,directory);
                    path=converted;
                }
                return ReadBitmap(path,edge);
            } finally { if(converted!=null && File.Exists(converted)) File.Delete(converted); }
        }
        static BitmapSource ReadBitmap(string path,int edge) {
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
        }
    }
}

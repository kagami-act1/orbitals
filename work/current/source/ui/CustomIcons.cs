using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class CustomIcons {
        const string Prefix="image:png;base64,";
        public const int MaxFileBytes=4*1024*1024, MaxDimension=128;
        const int MaxStoredBytes=80*1024, CacheLimit=32;
        sealed class Entry { public string Key; public BitmapSource Image; }
        static readonly Dictionary<string,LinkedListNode<Entry>> cache=new Dictionary<string,LinkedListNode<Entry>>(StringComparer.Ordinal);
        static readonly LinkedList<Entry> recent=new LinkedList<Entry>();
        static readonly object gate=new object();
        public static bool IsImage(string key) { return key!=null && key.StartsWith(Prefix,StringComparison.Ordinal); }
        public static string Share(string key) { if(!IsImage(key)) return key; lock(gate) { LinkedListNode<Entry> entry; return cache.TryGetValue(key,out entry)?entry.Value.Key:key; } }
        public static void ClearCache() { lock(gate) { cache.Clear(); recent.Clear(); } }
        public static void Retain(ISet<string> keys) {
            lock(gate) {
                LinkedListNode<Entry> entry=recent.First;
                while(entry!=null) { LinkedListNode<Entry> next=entry.Next; if(!keys.Contains(entry.Value.Key)) { cache.Remove(entry.Value.Key); recent.Remove(entry); } entry=next; }
            }
        }
        public static int CachedCount { get { lock(gate) return cache.Count; } }
        static bool Png(byte[] data) { return data.Length>=24 && data[0]==137 && data[1]==80 && data[2]==78 && data[3]==71 && data[4]==13 && data[5]==10 && data[6]==26 && data[7]==10 && data[12]==73 && data[13]==72 && data[14]==68 && data[15]==82; }
        static uint Number(byte[] data,int offset) { return (uint)data[offset]<<24 | (uint)data[offset+1]<<16 | (uint)data[offset+2]<<8 | data[offset+3]; }
        static BitmapSource Decode(byte[] data,int maximum) {
            using(MemoryStream stream=new MemoryStream(data,false)) {
                BitmapDecoder decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.DelayCreation,BitmapCacheOption.OnDemand);
                BitmapFrame frame=decoder.Frames[0];
                if(frame.PixelWidth<1 || frame.PixelHeight<1 || frame.PixelWidth>maximum || frame.PixelHeight>maximum) throw new Exception("画像の縦横は4096px以内にしてください。");
                double scale=Math.Min(1,(double)MaxDimension/Math.Max(frame.PixelWidth,frame.PixelHeight));
                int width=Math.Max(1,(int)Math.Round(frame.PixelWidth*scale)),height=Math.Max(1,(int)Math.Round(frame.PixelHeight*scale));
                stream.Position=0;
                BitmapImage decoded=new BitmapImage(); decoded.BeginInit(); decoded.CacheOption=BitmapCacheOption.OnLoad; decoded.CreateOptions=BitmapCreateOptions.PreservePixelFormat;
                decoded.StreamSource=stream; decoded.DecodePixelWidth=width; decoded.DecodePixelHeight=height; decoded.EndInit();
                FormatConvertedBitmap converted=new FormatConvertedBitmap(decoded,PixelFormats.Bgra32,null,0);
                int stride=checked(converted.PixelWidth*4); byte[] pixels=new byte[checked(stride*converted.PixelHeight)]; converted.CopyPixels(pixels,stride,0);
                BitmapSource image=BitmapSource.Create(converted.PixelWidth,converted.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride); image.Freeze(); return image;
            }
        }
        public static string Import(string path) {
            string extension=Path.GetExtension(path).ToLowerInvariant();
            if(!new[]{".png",".jpg",".jpeg",".ico",".bmp",".gif"}.Contains(extension)) throw new Exception("PNG・JPEG・ICO・BMP・GIFの画像を選んでください。");
            byte[] data;
            using(FileStream stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(stream.Length<6 || stream.Length>MaxFileBytes) throw new Exception("画像ファイルは4MB以内にしてください。");
                data=new byte[(int)stream.Length]; int offset=0,read; while(offset<data.Length && (read=stream.Read(data,offset,data.Length-offset))>0) offset+=read;
                if(offset!=data.Length) throw new Exception("画像を最後まで読み込めませんでした。");
            }
            bool supported=Png(data) || (data[0]==255 && data[1]==216 && data[2]==255) || (data[0]==66 && data[1]==77) || (data[0]==71 && data[1]==73 && data[2]==70 && data[3]==56) || (data[0]==0 && data[1]==0 && data[2]==1 && data[3]==0);
            if(!supported) throw new Exception("対応する画像形式ではありません。PNG・JPEG・ICO・BMP・GIFを選んでください。");
            BitmapSource image=Decode(data,4096);
            using(MemoryStream output=new MemoryStream()) {
                PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); encoder.Save(output);
                if(output.Length>MaxStoredBytes) throw new Exception("アイコン画像を保存できませんでした。別の画像を選んでください。");
                string key=Prefix+Convert.ToBase64String(output.ToArray()); Remember(key,image); return Share(key);
            }
        }
        static void Remember(string key,BitmapSource image) {
            lock(gate) {
            LinkedListNode<Entry> existing;
            if(cache.TryGetValue(key,out existing)) { recent.Remove(existing); recent.AddFirst(existing); return; }
            LinkedListNode<Entry> entry=recent.AddFirst(new Entry { Key=key,Image=image }); cache.Add(key,entry);
            while(cache.Count>CacheLimit) { cache.Remove(recent.Last.Value.Key); recent.RemoveLast(); }
            }
        }
        public static BitmapSource Read(string key) {
            if(!IsImage(key) || key.Length>Prefix.Length+((MaxStoredBytes+2)/3)*4) throw new Exception("自作アイコンのデータが不正です。画像を選び直してください。");
            lock(gate) {
            LinkedListNode<Entry> entry;
            if(cache.TryGetValue(key,out entry)) { recent.Remove(entry); recent.AddFirst(entry); return entry.Value.Image; }
            byte[] data;
            try { data=Convert.FromBase64String(key.Substring(Prefix.Length)); } catch(FormatException) { throw new Exception("自作アイコンのデータが不正です。画像を選び直してください。"); }
            if(data.Length>MaxStoredBytes || !Png(data) || Number(data,16)<1 || Number(data,16)>MaxDimension || Number(data,20)<1 || Number(data,20)>MaxDimension) throw new Exception("自作アイコンのサイズが不正です。画像を選び直してください。");
            BitmapSource image=Decode(data,MaxDimension); Remember(key,image); return image;
            }
        }
    }
}

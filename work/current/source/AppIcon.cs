using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class AppIcon {
        static ImageSource image;
        static Stream Open() { return Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitRing.AppIcon"); }
        public static ImageSource Image {
            get {
                if(image==null) using(Stream stream=Open()) {
                    BitmapDecoder decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
                    image=decoder.Frames[decoder.Frames.Count-1]; image.Freeze();
                }
                return image;
            }
        }
        public static System.Drawing.Icon Tray() {
            using(Stream stream=Open()) using(System.Drawing.Icon icon=new System.Drawing.Icon(stream,32,32)) return (System.Drawing.Icon)icon.Clone();
        }
        [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool DestroyIcon(System.IntPtr icon);
        public static System.Drawing.Icon PausedTray() {
            using(System.Drawing.Icon source=Tray()) using(System.Drawing.Bitmap image=source.ToBitmap()) {
                for(int y=0;y<image.Height;y++) for(int x=0;x<image.Width;x++) {
                    System.Drawing.Color c=image.GetPixel(x,y); int gray=(c.R*30+c.G*59+c.B*11)/100;
                    image.SetPixel(x,y,System.Drawing.Color.FromArgb((int)(c.A*.8),gray,gray,gray));
                }
                using(System.Drawing.Graphics drawing=System.Drawing.Graphics.FromImage(image)) {
                    drawing.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using(System.Drawing.Brush badge=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(27,34,43))) drawing.FillEllipse(badge,16,16,16,16);
                    drawing.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.None;
                    using(System.Drawing.Brush bars=new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(237,243,246))) { drawing.FillRectangle(bars,20,20,2,8); drawing.FillRectangle(bars,25,20,2,8); }
                }
                System.IntPtr handle=image.GetHicon();
                try { using(System.Drawing.Icon icon=System.Drawing.Icon.FromHandle(handle)) return (System.Drawing.Icon)icon.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
    }
}

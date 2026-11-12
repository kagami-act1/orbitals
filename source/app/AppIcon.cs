using System;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    enum AppIconState { Running,Stopped,Error }
    static class AppIcon {
        static ImageSource image;
        static Stream Open(AppIconState state) { return Assembly.GetExecutingAssembly().GetManifestResourceStream("OrbitRing.AppIcon"+(state==AppIconState.Running?"":state==AppIconState.Stopped?".Stopped":".Error")); }
        public static ImageSource Image {
            get {
                if(image==null) using(Stream stream=Open(AppIconState.Running)) {
                    BitmapDecoder decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);
                    image=decoder.Frames[decoder.Frames.Count-1]; image.Freeze();
                }
                return image;
            }
        }
        public static int TraySize { get { return Math.Max(16,Math.Min(64,System.Windows.Forms.SystemInformation.SmallIconSize.Width)); } }
        public static System.Drawing.Icon Tray(AppIconState state=AppIconState.Running,int size=32) {
            using(Stream stream=Open(state)) using(System.Drawing.Icon icon=new System.Drawing.Icon(stream,size,size)) return (System.Drawing.Icon)icon.Clone();
        }
        public static AppIconState State(bool paused,bool planet,bool orbit,bool error) { return paused || !planet && !orbit?AppIconState.Stopped:error?AppIconState.Error:AppIconState.Running; }
    }
}

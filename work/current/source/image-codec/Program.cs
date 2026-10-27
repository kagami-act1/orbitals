using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using ImageMagick;

[assembly:DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory|DllImportSearchPath.System32)]

static class Program {
    // Keep the .NET Framework compiler compatible with the codec's newer generic constraints.
    static void Read(MagickImage image,string operation,Stream input,MagickReadSettings settings) {
        MethodInfo method=typeof(MagickImage).GetMethods().Single(m=>m.Name==operation && m.GetParameters().Length==2 && m.GetParameters()[0].ParameterType==typeof(Stream) && m.GetParameters()[1].ParameterType.IsInstanceOfType(settings));
        method.Invoke(image,new object[]{input,settings});
    }
    static int Main(string[] args) {
        try {
            int edge;
            if(args.Length!=3 || !Path.IsPathRooted(args[0]) || !Path.IsPathRooted(args[1]) || !Int32.TryParse(args[2],out edge) || edge<0 || edge>16000) throw new Exception("画像変換の指定を確認してください。");
            string extension=Path.GetExtension(args[0]).ToLowerInvariant();
            if(extension!=".heic" && extension!=".heif" && extension!=".avif") throw new Exception("HEIC・AVIF画像を選んでください。");
            if(new FileInfo(args[0]).Length>512L*1024*1024) throw new Exception("1ファイルは512MBまでです。");
            Environment.SetEnvironmentVariable("MAGICK_CONFIGURE_PATH",AppDomain.CurrentDomain.BaseDirectory);
            MagickNET.SetNativeLibraryDirectory(AppDomain.CurrentDomain.BaseDirectory);
            MagickNET.SetTempDirectory(Path.GetDirectoryName(args[1]));
            ResourceLimits.Memory=256UL*1024*1024; ResourceLimits.Disk=512UL*1024*1024; ResourceLimits.Time=90; ResourceLimits.Thread=2; ResourceLimits.ListLength=16;
            MagickReadSettings settings=new MagickReadSettings { Format=extension==".avif"?MagickFormat.Avif:MagickFormat.Heic,FrameIndex=0,FrameCount=1 };
            using(FileStream input=File.OpenRead(args[0])) using(MagickImage image=new MagickImage()) {
                Read(image,"Ping",input,settings);
                if(image.Width==0 || image.Height==0 || (ulong)image.Width*image.Height>100000000) throw new Exception("画像は1億画素までです。");
                input.Position=0; Read(image,"Read",input,settings); image.AutoOrient();
                if(edge>0 && Math.Max(image.Width,image.Height)>edge) image.Resize(new MagickGeometry((uint)edge,(uint)edge) { Greater=true });
                image.Write(args[1],MagickFormat.Png);
            }
            return 0;
        } catch(Exception ex) { while(ex is TargetInvocationException && ex.InnerException!=null) ex=ex.InnerException; Console.Error.WriteLine(ex.Message); return 1; }
    }
}

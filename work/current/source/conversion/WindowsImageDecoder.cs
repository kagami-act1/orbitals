extern alias WinRT;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class WindowsImageDecoder {
        public static bool TryRead(string path,int edge,out BitmapSource result) {
            try { result=Read(path,edge); return true; }
            catch(Exception ex) {
                while(ex is TargetInvocationException && ex.InnerException!=null) ex=ex.InnerException;
                uint code=(uint)ex.HResult;
                bool codecError=(code&0xFFFFFF00U)==0x88982F00U || code==0xC00D5212U || code==0xC00D36B4U || code==0x80040154U;
                if(!(ex is NotSupportedException || ex is TypeLoadException || codecError)) throw;
                result=null; return false;
            }
        }
        static BitmapSource Read(string path,int edge) {
            object stream=null,bitmap=null;
            using(FileStream input=File.OpenRead(path)) try {
                MethodInfo adapter=typeof(WinRT::System.IO.WindowsRuntimeStreamExtensions).GetMethod("AsRandomAccessStream",new[]{typeof(Stream)});
                stream=adapter.Invoke(null,new object[]{input});
                Type decoderType=WindowsRuntimeApi.Get("Windows.Graphics.Imaging.BitmapDecoder"),bitmapType=WindowsRuntimeApi.Get("Windows.Graphics.Imaging.SoftwareBitmap");
                object decoder=WindowsRuntimeApi.Wait(decoderType.GetMethods().Single(m=>m.Name=="CreateAsync" && m.GetParameters().Length==1).Invoke(null,new[]{stream}),decoderType);
                uint width=(uint)decoderType.GetProperty("PixelWidth").GetValue(decoder,null),height=(uint)decoderType.GetProperty("PixelHeight").GetValue(decoder,null);
                if(width==0 || height==0 || (ulong)width*height>100000000) throw new InvalidDataException("画像が大きすぎます（1億画素まで）。");
                object transform=Activator.CreateInstance(WindowsRuntimeApi.Get("Windows.Graphics.Imaging.BitmapTransform"));
                if(edge>0 && Math.Max(width,height)>edge) {
                    double scale=(double)edge/Math.Max(width,height);
                    transform.GetType().GetProperty("ScaledWidth").SetValue(transform,(uint)Math.Max(1,Math.Round(width*scale)),null);
                    transform.GetType().GetProperty("ScaledHeight").SetValue(transform,(uint)Math.Max(1,Math.Round(height*scale)),null);
                }
                object format=Enum.Parse(WindowsRuntimeApi.Get("Windows.Graphics.Imaging.BitmapPixelFormat"),"Bgra8"),alpha=Enum.Parse(WindowsRuntimeApi.Get("Windows.Graphics.Imaging.BitmapAlphaMode"),"Premultiplied");
                object orientation=Enum.Parse(WindowsRuntimeApi.Get("Windows.Graphics.Imaging.ExifOrientationMode"),"RespectExifOrientation"),color=Enum.Parse(WindowsRuntimeApi.Get("Windows.Graphics.Imaging.ColorManagementMode"),"ColorManageToSRgb");
                MethodInfo read=decoderType.GetMethods().Single(m=>m.Name=="GetSoftwareBitmapAsync" && m.GetParameters().Length==5);
                bitmap=WindowsRuntimeApi.Wait(read.Invoke(decoder,new[]{format,alpha,transform,orientation,color}),bitmapType);
                int w=(int)bitmapType.GetProperty("PixelWidth").GetValue(bitmap,null),h=(int)bitmapType.GetProperty("PixelHeight").GetValue(bitmap,null),stride=checked(w*4);
                byte[] pixels=new byte[checked(stride*h)];
                MethodInfo bufferMethod=typeof(WinRT::System.Runtime.InteropServices.WindowsRuntime.WindowsRuntimeBufferExtensions).GetMethods().Single(m=>m.Name=="AsBuffer" && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==typeof(byte[]));
                bitmapType.GetMethod("CopyToBuffer").Invoke(bitmap,new[]{bufferMethod.Invoke(null,new object[]{pixels})});
                BitmapSource result=BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,pixels,stride); result.Freeze(); return result;
            } finally { IDisposable owned=bitmap as IDisposable; if(owned!=null) owned.Dispose(); owned=stream as IDisposable; if(owned!=null) owned.Dispose(); }
        }
    }
}

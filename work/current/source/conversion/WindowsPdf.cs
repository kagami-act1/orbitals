extern alias WinRT;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace OrbitRing {
    // Project Windows runtime APIs without requiring an installed Windows SDK.
    static class WindowsRuntimeApi {
        public static Type Get(string name) { return Type.GetType(name+", "+name.Substring(0,name.LastIndexOf('.'))+", ContentType=WindowsRuntime",true); }
        public static object Wait(object operation,Type result) {
            MethodInfo method=typeof(WinRT::System.WindowsRuntimeSystemExtensions).GetMethods().Single(m=>m.Name=="AsTask" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length==1 && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType.Name=="IAsyncOperation`1");
            Task task=(Task)method.MakeGenericMethod(result).Invoke(null,new[]{operation}); task.GetAwaiter().GetResult(); return task.GetType().GetProperty("Result").GetValue(task,null);
        }
        public static void WaitAction(object operation) {
            MethodInfo method=typeof(WinRT::System.WindowsRuntimeSystemExtensions).GetMethods().Single(m=>m.Name=="AsTask" && !m.IsGenericMethod && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType.Name=="IAsyncAction");
            ((Task)method.Invoke(null,new[]{operation})).GetAwaiter().GetResult();
        }
    }
    static class WindowsPdf {
        public static object Open(string path) {
            Type fileType=WindowsRuntimeApi.Get("Windows.Storage.StorageFile"),pdfType=WindowsRuntimeApi.Get("Windows.Data.Pdf.PdfDocument");
            object file=WindowsRuntimeApi.Wait(fileType.GetMethod("GetFileFromPathAsync").Invoke(null,new[]{Path.GetFullPath(path)}),fileType);
            MethodInfo load=pdfType.GetMethods().Single(m=>m.Name=="LoadFromFileAsync" && m.GetParameters().Length==1); return WindowsRuntimeApi.Wait(load.Invoke(null,new[]{file}),pdfType);
        }
        public static int Count(object document) { return checked((int)(uint)document.GetType().GetProperty("PageCount").GetValue(document,null)); }
        public static void Render(object document,int index,string path,int dpi) {
            object page=document.GetType().GetMethod("GetPage").Invoke(document,new object[]{(uint)index});
            object stream=Activator.CreateInstance(WindowsRuntimeApi.Get("Windows.Storage.Streams.InMemoryRandomAccessStream"));
            try {
                object size=page.GetType().GetProperty("Size").GetValue(page,null);
                double width=(double)size.GetType().GetProperty("Width").GetValue(size,null),height=(double)size.GetType().GetProperty("Height").GetValue(size,null);
                double scale=dpi/96.0; if(width*height*scale*scale>40000000) scale=Math.Sqrt(40000000/(width*height));
                object options=Activator.CreateInstance(WindowsRuntimeApi.Get("Windows.Data.Pdf.PdfPageRenderOptions"));
                options.GetType().GetProperty("DestinationWidth").SetValue(options,(uint)Math.Max(1,Math.Round(width*scale)),null);
                options.GetType().GetProperty("DestinationHeight").SetValue(options,(uint)Math.Max(1,Math.Round(height*scale)),null);
                MethodInfo render=page.GetType().GetMethods().Single(m=>m.Name=="RenderToStreamAsync" && m.GetParameters().Length==2); WindowsRuntimeApi.WaitAction(render.Invoke(page,new[]{stream,options}));
                stream.GetType().GetMethod("Seek").Invoke(stream,new object[]{0UL});
                MethodInfo read=typeof(WinRT::System.IO.WindowsRuntimeStreamExtensions).GetMethods().First(m=>m.Name=="AsStreamForRead" && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(stream));
                using(Stream input=(Stream)read.Invoke(null,new[]{stream})) using(Stream output=File.Create(path)) input.CopyTo(output);
            } finally { IDisposable disposable=stream as IDisposable; if(disposable!=null) disposable.Dispose(); disposable=page as IDisposable; if(disposable!=null) disposable.Dispose(); }
        }
    }
}

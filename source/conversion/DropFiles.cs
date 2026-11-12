using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using ComData=System.Runtime.InteropServices.ComTypes.IDataObject;

namespace OrbitRing {
    public enum DropKind { None,Image,Pdf,Files,Zip,Document,Spreadsheet,Presentation }
    sealed class DropOffer {
        public DropKind Kind;
        public string[] Paths,Names;
        public string SourceFolder,Reason;
        public int Count { get { return Names==null?1:Names.Length; } }
        public bool Pdf { get { return Kind==DropKind.Pdf; } }
    }
    sealed class DroppedFiles { public readonly List<string> Files=new List<string>(); public string SourceFolder; }
    sealed class DropCapture : IDisposable {
        IntPtr marshaled;
        string[] paths;
        BitmapSource bitmap;
        object png;
        readonly DropOffer offer;
        static Guid dataId=new Guid("0000010e-0000-0000-C000-000000000046");
        [DllImport("ole32.dll")] static extern int CoMarshalInterThreadInterfaceInStream(ref Guid id,[MarshalAs(UnmanagedType.IUnknown)] object value,out IntPtr stream);
        [DllImport("ole32.dll")] static extern int CoGetInterfaceAndReleaseStream(IntPtr stream,ref Guid id,[MarshalAs(UnmanagedType.Interface)] out ComData value);
        [DllImport("ole32.dll")] static extern int CoReleaseMarshalData(IntPtr stream);
        public DropCapture(System.Windows.IDataObject data,DropOffer info) {
            offer=info; if(info.Kind==DropKind.None) throw new Exception(info.Reason);
            if(info.Paths!=null) { paths=(string[])info.Paths.Clone(); return; }
            if(info.Names==null && !data.GetDataPresent("PNG",false) && data.GetDataPresent(DataFormats.Bitmap,false)) {
                BitmapSource source=data.GetData(DataFormats.Bitmap,false) as BitmapSource;
                if(source==null || source.PixelWidth<1 || source.PixelHeight<1 || (long)source.PixelWidth*source.PixelHeight>100000000) throw new Exception("画像のデータが不正、または1億画素を超えています。");
                // A managed WPF bitmap keeps its dispatcher even after COM marshaling.
                // Freeze its existing pixels here; encoding and file IO stay on the capture thread.
                bitmap=(BitmapSource)source.GetAsFrozen(); return;
            }
            ComData native=data as ComData;
            if(native!=null) { Marshal.ThrowExceptionForHR(CoMarshalInterThreadInterfaceInStream(ref dataId,native,out marshaled)); return; }
            if(data.GetDataPresent("PNG",false)) png=data.GetData("PNG",false);
            else if(data.GetDataPresent(DataFormats.Bitmap,false)) { BitmapSource source=data.GetData(DataFormats.Bitmap,false) as BitmapSource; if(source!=null) bitmap=(BitmapSource)source.GetAsFrozen(); }
            if(bitmap==null && png==null) throw new Exception("このアプリからのデータを受け取れません。ファイルを保存してからドラッグしてください。");
        }
        public Task<DroppedFiles> CaptureAsync(string directory,CancellationToken cancel) {
            TaskCompletionSource<DroppedFiles> result=new TaskCompletionSource<DroppedFiles>();
            Thread thread=new Thread(delegate() {
                ComData native=null;
                try {
                    cancel.ThrowIfCancellationRequested(); System.Windows.IDataObject data=null;
                    IntPtr stream=Interlocked.Exchange(ref marshaled,IntPtr.Zero);
                    if(stream!=IntPtr.Zero) { Marshal.ThrowExceptionForHR(CoGetInterfaceAndReleaseStream(stream,ref dataId,out native)); data=new DataObject(native); }
                    else { DataObject local=new DataObject(); if(paths!=null) local.SetData(DataFormats.FileDrop,paths); else if(bitmap!=null) local.SetData(DataFormats.Bitmap,bitmap); else local.SetData("PNG",png); data=local; }
                    result.SetResult(DropFiles.Capture(data,directory,cancel,offer));
                } catch(OperationCanceledException) { result.SetCanceled(); }
                catch(Exception ex) { result.SetException(ex); }
                finally { if(native!=null && Marshal.IsComObject(native)) Marshal.ReleaseComObject(native); paths=null; bitmap=null; png=null; Dispose(); }
            });
            thread.IsBackground=true; thread.Name="orbitals file capture"; thread.SetApartmentState(ApartmentState.STA); thread.Start(); return result.Task;
        }
        public void Dispose() { IntPtr stream=Interlocked.Exchange(ref marshaled,IntPtr.Zero); if(stream!=IntPtr.Zero) { CoReleaseMarshalData(stream); Marshal.Release(stream); } }
    }
    static class DropFiles {
        internal const int MaximumFiles=128;
        internal const long MaximumBytes=512L*1024*1024;
        [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM medium);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr memory);
        public static bool ImageFile(string path) { return FileConversion.Supported(path) && !ConversionCatalog.PdfFile(path) && DocumentConversion.Kind(path)==DropKind.None; }
        public static bool CanCopy(DragDropEffects allowed) { return (allowed&DragDropEffects.Copy)!=0; }
        internal static string SafeName(string name) {
            name=(name??"").Replace('/','\\'); name=name.Substring(name.LastIndexOf('\\')+1);
            char[] invalid=Path.GetInvalidFileNameChars(); name=new string(name.Select(c=>Char.IsControl(c) || invalid.Contains(c)?'_':c).ToArray()).Trim().TrimEnd('.');
            if(name.Length>120) name=name.Substring(0,100)+Path.GetExtension(name);
            string stem=Path.GetFileNameWithoutExtension(name);
            if(String.IsNullOrWhiteSpace(stem)) throw new Exception("ファイル名を確認してください。");
            if(new[]{"CON","PRN","AUX","NUL","COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9","LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"}.Contains(stem.ToUpperInvariant())) name="_"+name;
            return name;
        }
        static string NewFile(string directory,string name) { return ArchiveConversion.Unique(directory,SafeName(name),false); }
        internal static void Copy(Stream input,Stream output,ref long remaining,CancellationToken cancel=default(CancellationToken)) {
            byte[] buffer=new byte[65536]; int read;
            while((read=input.Read(buffer,0,(int)Math.Min(buffer.Length,remaining+1)))>0) { cancel.ThrowIfCancellationRequested(); if(read>remaining) throw new Exception("一度に変換できるファイルは合計512MBまでです。"); output.Write(buffer,0,read); remaining-=read; }
        }
        static byte[] Bytes(object data,int maximum) {
            byte[] bytes=data as byte[]; if(bytes!=null) { if(bytes.Length>maximum) throw new Exception("ドラッグしたファイルの情報が大きすぎます。"); return bytes; }
            Stream stream=data as Stream; if(stream==null) throw new Exception("ファイルのデータを読み込めませんでした。");
            if(stream.CanSeek) stream.Position=0; using(MemoryStream output=new MemoryStream()) { long remaining=maximum; Copy(stream,output,ref remaining); return output.ToArray(); }
        }
        internal static string[] Descriptors(byte[] bytes,bool unicode) {
            int size=unicode?592:332; if(bytes.Length<4) throw new Exception("ドラッグしたファイルの情報が不正です。"); uint count=BitConverter.ToUInt32(bytes,0);
            if(count==0 || count>MaximumFiles || bytes.Length<4+(long)count*size) throw new Exception("ファイルは一度に128件までです。");
            List<string> names=new List<string>();
            for(int i=0;i<count;i++) { int offset=4+i*size; if((BitConverter.ToUInt32(bytes,offset+36)&16)!=0) throw new Exception("フォルダーではなくファイルをドラッグしてください。"); string name=(unicode?Encoding.Unicode:Encoding.Default).GetString(bytes,offset+72,unicode?520:260); int end=name.IndexOf('\0'); names.Add(SafeName(end>=0?name.Substring(0,end):name)); }
            return names.ToArray();
        }
        public static string SourceFolder(IEnumerable<string> paths) {
            string[] folders=paths.Select(p=>Path.GetDirectoryName(Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar))).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); if(folders.Length!=1 || String.IsNullOrEmpty(folders[0])) return null;
            string temporary=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            return folders[0].Equals(temporary,StringComparison.OrdinalIgnoreCase) || folders[0].StartsWith(temporary+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)?null:folders[0];
        }
        public static DropOffer Inspect(System.Windows.IDataObject data) {
            DropOffer offer=new DropOffer { Reason="ファイル・フォルダーをドラッグしてください。" }; if(data==null) return offer;
            try {
                if(data.GetDataPresent(DataFormats.FileDrop,false)) {
                    offer.Paths=data.GetData(DataFormats.FileDrop,false) as string[];
                    if(offer.Paths==null || offer.Paths.Length==0 || offer.Paths.Length>MaximumFiles || offer.Paths.Any(p=>!Path.IsPathRooted(p) || (!File.Exists(p) && !Directory.Exists(p)))) { offer.Reason="ファイル・フォルダーを選んでください（128件まで）。"; return offer; }
                    offer.Names=offer.Paths.Select(p=>Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar))).ToArray(); offer.SourceFolder=SourceFolder(offer.Paths);
                } else if(data.GetDataPresent("FileGroupDescriptorW",false) || data.GetDataPresent("FileGroupDescriptor",false)) { bool unicode=data.GetDataPresent("FileGroupDescriptorW",false); offer.Names=Descriptors(Bytes(data.GetData(unicode?"FileGroupDescriptorW":"FileGroupDescriptor",false),1024*1024),unicode); }
                else if(data.GetDataPresent("PNG",false) || data.GetDataPresent(DataFormats.Bitmap,false)) { offer.Kind=DropKind.Image; return offer; }
                else return offer;
                bool folders=offer.Paths!=null && offer.Paths.Any(Directory.Exists);
                offer.Kind=folders?DropKind.Files:offer.Names.All(ArchiveConversion.ZipFile)?DropKind.Zip:offer.Names.All(ConversionCatalog.PdfFile)?DropKind.Pdf:offer.Names.All(ImageFile)?DropKind.Image:DocumentConversion.CommonKind(offer.Names);
                if(offer.Kind==DropKind.None) offer.Kind=DropKind.Files;
            } catch(Exception ex) { offer.Reason=ex.Message; offer.Kind=DropKind.None; }
            return offer;
        }
        public static bool Offered(System.Windows.IDataObject data) { return Inspect(data).Kind!=DropKind.None; }
        static void VirtualContent(System.Windows.IDataObject data,int index,int count,string path,ref long remaining,CancellationToken cancel) {
            ComData native=data as ComData; if(native==null) throw new Exception("このアプリからのファイルを読み込めません。ファイルを保存してからドラッグしてください。");
            FORMATETC format=new FORMATETC { cfFormat=unchecked((short)DataFormats.GetDataFormat("FileContents").Id),dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=index,tymed=TYMED.TYMED_ISTREAM|TYMED.TYMED_HGLOBAL };
            STGMEDIUM medium=new STGMEDIUM(); bool acquired=false;
            try {
                cancel.ThrowIfCancellationRequested(); native.GetData(ref format,out medium); acquired=true;
                using(Stream output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                    if(medium.tymed==TYMED.TYMED_ISTREAM) {
                        object wrapper=Marshal.GetObjectForIUnknown(medium.unionmember); IntPtr readCount=Marshal.AllocHGlobal(4);
                        try { IStream stream=(IStream)wrapper; byte[] buffer=new byte[65536]; while(true) { cancel.ThrowIfCancellationRequested(); int request=(int)Math.Min(buffer.Length,remaining+1); Marshal.WriteInt32(readCount,0); stream.Read(buffer,request,readCount); int read=Marshal.ReadInt32(readCount); if(read==0) break; if(read<0 || read>request || read>remaining) throw new Exception("ファイルは合計512MBまでです。"); output.Write(buffer,0,read); remaining-=read; } }
                        finally { Marshal.FreeHGlobal(readCount); if(Marshal.IsComObject(wrapper)) Marshal.ReleaseComObject(wrapper); }
                    } else if(medium.tymed==TYMED.TYMED_HGLOBAL) {
                        ulong length=GlobalSize(medium.unionmember).ToUInt64(); if(length==0 || length>(ulong)remaining) throw new Exception("ファイルは合計512MBまでです。"); IntPtr bytes=GlobalLock(medium.unionmember); if(bytes==IntPtr.Zero) throw new Exception("ファイルを読み込めませんでした。");
                        try { byte[] buffer=new byte[65536]; for(int offset=0;offset<(long)length;) { cancel.ThrowIfCancellationRequested(); int read=(int)Math.Min(buffer.Length,(long)length-offset); Marshal.Copy(IntPtr.Add(bytes,offset),buffer,0,read); output.Write(buffer,0,read); offset+=read; remaining-=read; } } finally { GlobalUnlock(medium.unionmember); }
                    } else throw new Exception("この形式のファイルデータには対応していません。");
                }
            } catch(COMException) {
                if(acquired || count!=1) throw; Stream stream=data.GetData("FileContents",false) as Stream; if(stream==null) throw;
                if(stream.CanSeek) stream.Position=0; using(Stream output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)) Copy(stream,output,ref remaining,cancel);
            } finally { if(acquired) ReleaseStgMedium(ref medium); }
        }
        public static DroppedFiles Capture(System.Windows.IDataObject data,string directory,CancellationToken cancel=default(CancellationToken),DropOffer info=null) {
            info=info??Inspect(data); if(info.Kind==DropKind.None) throw new Exception(info.Reason);
            Directory.CreateDirectory(directory); DroppedFiles files=new DroppedFiles { SourceFolder=info.SourceFolder }; long remaining=MaximumBytes;
            if(info.Paths!=null) {
                int count=0; foreach(string source in info.Paths) { cancel.ThrowIfCancellationRequested(); string name=SafeName(Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar))); string destination=ArchiveConversion.Unique(directory,name,Directory.Exists(source)); ArchiveConversion.CopyInput(source,destination,ref remaining,ref count,cancel); files.Files.Add(destination); }
            } else if(info.Names!=null) {
                for(int i=0;i<info.Names.Length;i++) { cancel.ThrowIfCancellationRequested(); string destination=NewFile(directory,info.Names[i]); VirtualContent(data,i,info.Names.Length,destination,ref remaining,cancel); files.Files.Add(destination); }
            } else if(data.GetDataPresent("PNG",false)) {
                string destination=NewFile(directory,"画像.png"); object content=data.GetData("PNG",false); Stream input=content as Stream; byte[] bytes=content as byte[];
                if(input==null && bytes!=null) input=new MemoryStream(bytes,false); if(input==null) throw new Exception("画像データを読み込めませんでした。");
                if(input.CanSeek) input.Position=0; using(Stream output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) Copy(input,output,ref remaining,cancel); files.Files.Add(destination);
            } else if(data.GetDataPresent(DataFormats.Bitmap,false)) {
                BitmapSource bitmap=data.GetData(DataFormats.Bitmap,false) as BitmapSource;
                if(bitmap==null || bitmap.PixelWidth<1 || bitmap.PixelHeight<1 || (long)bitmap.PixelWidth*bitmap.PixelHeight>100000000) throw new Exception("画像のデータが不正、または1億画素を超えています。");
                cancel.ThrowIfCancellationRequested(); string destination=NewFile(directory,"画像.png"); PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap,null,null,null)); using(Stream output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) encoder.Save(output); files.Files.Add(destination);
            }
            cancel.ThrowIfCancellationRequested(); if(files.Files.Count==0 || (info.Kind==DropKind.Image || info.Kind==DropKind.Pdf) && files.Files.Any(p=>new FileInfo(p).Length==0)) throw new Exception("ファイルのデータが空です。"); return files;
        }
    }
}

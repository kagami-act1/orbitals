extern alias Pdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    static class DropConversionTests {
        static void Check(bool value,string message,List<string> results) { if(!value) throw new Exception(message); results.Add("PASS "+message); }
        static bool Reject(Action action) { try { action(); return false; } catch { return true; } }
        static T Await<T>(Task<T> task) { if(!task.IsCompleted) { DispatcherFrame frame=new DispatcherFrame(); DateTime deadline=DateTime.UtcNow.AddSeconds(15); DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(10) }; timer.Tick+=delegate { if(task.IsCompleted || DateTime.UtcNow>deadline) frame.Continue=false; }; timer.Start(); Dispatcher.PushFrame(frame); timer.Stop(); if(!task.IsCompleted) throw new Exception("Capture timed out"); } try { return task.GetAwaiter().GetResult(); } catch(Exception ex) { throw new Exception(ex.ToString(),ex); } }
        static byte[] Image(int width) { byte[] pixels=new byte[width*24*4]; for(int i=0;i<pixels.Length;i+=4) { pixels[i+1]=200; pixels[i+3]=255; } BitmapSource bitmap=BitmapSource.Create(width,24,96,96,PixelFormats.Bgra32,null,pixels,width*4); PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(MemoryStream stream=new MemoryStream()) { encoder.Save(stream); return stream.ToArray(); } }
        static byte[] Descriptor(params string[] names) { byte[] bytes=new byte[4+592*names.Length]; Array.Copy(BitConverter.GetBytes(names.Length),bytes,4); for(int i=0;i<names.Length;i++) { byte[] name=Encoding.Unicode.GetBytes(names[i]+"\0"); Array.Copy(name,0,bytes,4+592*i+72,name.Length); } return bytes; }
        sealed class VirtualImages : System.Windows.IDataObject,System.Runtime.InteropServices.ComTypes.IDataObject {
            readonly DataObject data=new DataObject(); readonly byte[][] contents; public readonly List<int> Requested=new List<int>(); public bool UseStream;
            [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags,UIntPtr bytes);
            [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr memory);
            [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr memory);
            public VirtualImages(string[] names,params byte[][] bytes) { data.SetData("FileGroupDescriptorW",new MemoryStream(Descriptor(names))); data.SetData("FileContents",new MemoryStream(bytes[0])); contents=bytes; }
            public object GetData(string format,bool convert) { return data.GetData(format,convert); }
            public object GetData(string format) { return data.GetData(format); }
            public object GetData(Type format) { return data.GetData(format); }
            public bool GetDataPresent(string format,bool convert) { return data.GetDataPresent(format,convert); }
            public bool GetDataPresent(string format) { return data.GetDataPresent(format); }
            public bool GetDataPresent(Type format) { return data.GetDataPresent(format); }
            public string[] GetFormats(bool convert) { return data.GetFormats(convert); }
            public string[] GetFormats() { return data.GetFormats(); }
            public void SetData(string format,object value,bool convert) { throw new NotSupportedException(); }
            public void SetData(string format,object value) { throw new NotSupportedException(); }
            public void SetData(Type format,object value) { throw new NotSupportedException(); }
            public void SetData(object value) { throw new NotSupportedException(); }
            public void GetData(ref FORMATETC format,out STGMEDIUM medium) {
                if(format.cfFormat!=unchecked((short)DataFormats.GetDataFormat("FileContents").Id) || format.lindex<0 || format.lindex>=contents.Length) throw new COMException("Invalid content index");
                Requested.Add(format.lindex); byte[] bytes=contents[format.lindex]; IntPtr memory=GlobalAlloc(2,(UIntPtr)bytes.Length),pointer=GlobalLock(memory); Marshal.Copy(bytes,0,pointer,bytes.Length); GlobalUnlock(memory); medium=new STGMEDIUM { tymed=TYMED.TYMED_HGLOBAL,unionmember=memory };
                if(UseStream) { ReleaseStgMedium(ref medium); medium=new STGMEDIUM { tymed=TYMED.TYMED_ISTREAM,unionmember=Marshal.GetComInterfaceForObject(new ContentStream(bytes),typeof(IStream)) }; }
            }
            public void GetDataHere(ref FORMATETC f,ref STGMEDIUM s) { throw new NotSupportedException(); }
            public int QueryGetData(ref FORMATETC f) { return 0; }
            public int GetCanonicalFormatEtc(ref FORMATETC f,out FORMATETC o) { o=f; return 1; }
            public void SetData(ref FORMATETC f,ref STGMEDIUM s,bool release) { throw new NotSupportedException(); }
            public IEnumFORMATETC EnumFormatEtc(DATADIR direction) { throw new NotSupportedException(); }
            public int DAdvise(ref FORMATETC f,ADVF flags,IAdviseSink sink,out int connection) { connection=0; return unchecked((int)0x80040003); }
            public void DUnadvise(int connection) { throw new NotSupportedException(); }
            public int EnumDAdvise(out IEnumSTATDATA values) { values=null; return 1; }
            [DllImport("ole32.dll")] static extern void ReleaseStgMedium(ref STGMEDIUM medium);
        }
        sealed class ContentStream : IStream {
            readonly byte[] bytes; int position;
            public ContentStream(byte[] value) { bytes=value; }
            public void Read(byte[] output,int count,IntPtr read) { int length=Math.Min(count,bytes.Length-position); Array.Copy(bytes,position,output,0,length); position+=length; if(read!=IntPtr.Zero) Marshal.WriteInt32(read,length); }
            public void Seek(long offset,int origin,IntPtr target) { position=checked((int)(offset+(origin==0?0:origin==1?position:bytes.Length))); if(target!=IntPtr.Zero) Marshal.WriteInt64(target,position); }
            public void Stat(out System.Runtime.InteropServices.ComTypes.STATSTG stat,int flags) { stat=new System.Runtime.InteropServices.ComTypes.STATSTG { cbSize=bytes.Length,type=2 }; }
            public void Write(byte[] value,int count,IntPtr written) { throw new NotSupportedException(); }
            public void SetSize(long size) { throw new NotSupportedException(); }
            public void CopyTo(IStream destination,long count,IntPtr read,IntPtr written) { throw new NotSupportedException(); }
            public void Commit(int flags) { }
            public void Revert() { throw new NotSupportedException(); }
            public void LockRegion(long offset,long size,int type) { throw new NotSupportedException(); }
            public void UnlockRegion(long offset,long size,int type) { throw new NotSupportedException(); }
            public void Clone(out IStream stream) { stream=new ContentStream(bytes); }
        }
        public static List<string> Run() {
            List<string> results=new List<string>(); string root=Path.Combine(Path.GetTempPath(),"orbitals-drop-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try {
                for(int count=1;count<=10;count++) {
                    var drawing=ConversionPlanets.Drawing(ConversionCatalog.Defaults(false).Take(count).ToList(),new AppearancePreferences()); Rect[] labels=(Rect[])drawing.Tag;
                    for(int i=0;i<count;i++) { Rect label=labels[i]; Check(ConversionPlanets.Hit(ConversionPlanets.Position(i,count),drawing)==i && ConversionPlanets.Hit(new Point(label.X+label.Width/2,label.Y+label.Height/2),drawing)==i,"conversion hit testing matches the visible icon and label: "+count+" / "+i,results); }
                }
                var fullDrawing=ConversionPlanets.Drawing(ConversionCatalog.Defaults(false),new AppearancePreferences());
                Check(ConversionPlanets.Hit(new Point(ConversionPlanets.Center,ConversionPlanets.Center),fullDrawing)==-1 && ConversionPlanets.Hit(new Point(-100,-100),fullDrawing)==-1 && ConversionPlanets.Hit(new Point(ConversionPlanets.Center,5),fullDrawing)==-1,"center and empty space cannot commit a conversion",results);
                foreach(double scale in new[]{.375,.75,1.0,1.5}) {
                    var drawing=ConversionPlanets.Drawing(ConversionCatalog.Defaults(false),new AppearancePreferences(),scale); Rect[] labels=(Rect[])drawing.Tag;
                    bool balanced=true;
                    for(int i=0;i<labels.Length;i++) {
                        Rect label=labels[i]; Point icon=ConversionPlanets.Position(i,labels.Length),at=new Point(label.X+label.Width/2,label.Y+label.Height/2); Vector radial=icon-new Point(ConversionPlanets.Center,ConversionPlanets.Center); radial.Normalize();
                        double gap=Vector.Multiply(at-icon,radial)-Math.Abs(radial.X)*label.Width/2-Math.Abs(radial.Y)*label.Height/2-(ConversionPlanets.RingRadius(labels.Length)-ConversionPlanets.OrbitRadius(labels.Length));
                        balanced &= Math.Abs(gap-12)<.00001 && Math.Abs(Vector.CrossProduct(at-icon,radial))<.00001;
                        balanced &= new Rect(0,0,drawing.Width,drawing.Height).Contains(label);
                        balanced &= ConversionPlanets.Hit(new Point(label.X+.1,label.Y+.1),drawing)==i && ConversionPlanets.Hit(new Point(label.Right-.1,label.Bottom-.1),drawing)==i;
                    }
                    Check(balanced,"measured conversion labels keep the same radial gap and clickable bounds in all eight directions: "+scale,results);
                }
                Check(DropFiles.CanCopy(DragDropEffects.Copy|DragDropEffects.Move) && !DropFiles.CanCopy(DragDropEffects.Move) && !DropFiles.CanCopy(DragDropEffects.Link),"conversion never acknowledges a source move or delete operation",results);
                ActionIcons.Validate("ms:picture_as_pdf"); ActionIcons.Validate("ms:image");
                Check(ConversionCatalog.Operations(false).Distinct().Count()==11 && ConversionCatalog.Operations(true).Distinct().Count()==11,"image and PDF menus include text extraction copying and ZIP compression",results);
                string original=Path.Combine(root,"元画像.png"); byte[] bytes=Image(48); File.WriteAllBytes(original,bytes); DataObject files=new DataObject(DataFormats.FileDrop,new[]{original});
                DroppedFiles local=DropFiles.Capture(files,Path.Combine(root,"local")); Check(local.Files.Count==1 && File.ReadAllBytes(local.Files[0]).SequenceEqual(bytes) && File.ReadAllBytes(original).SequenceEqual(bytes),"file drops snapshot the dragged image and preserve the original bytes",results);
                Check(local.SourceFolder==null,"temporary files have no automatic destination",results);
                DataObject image=new DataObject(DataFormats.Bitmap,BitmapFrame.Create(new Uri(original),BitmapCreateOptions.None,BitmapCacheOption.OnLoad)); DroppedFiles bitmap=DropFiles.Capture(image,Path.Combine(root,"bitmap")); Check(bitmap.Files.Count==1 && bitmap.SourceFolder==null && File.Exists(bitmap.Files[0]),"an in-memory dragged bitmap is captured without using the clipboard",results);
                DataObject png=new DataObject(); png.SetData("PNG",new MemoryStream(bytes)); DroppedFiles raw=DropFiles.Capture(png,Path.Combine(root,"raw")); Check(File.ReadAllBytes(raw.Files[0]).SequenceEqual(bytes),"raw PNG drag data is retained without a network download",results);
                VirtualImages virtualFiles=new VirtualImages(new[]{"ブラウザー.png","もう一枚.png"},bytes,Image(96)); DroppedFiles browser=DropFiles.Capture(virtualFiles,Path.Combine(root,"browser"));
                Check(browser.Files.Count==2 && browser.SourceFolder==null && virtualFiles.Requested.SequenceEqual(new[]{0,1}),"browser-style virtual image drops request each indexed content independently",results);
                Check(DropFiles.Descriptors(Descriptor("..\\画像.png"),true)[0]=="画像.png" && DropFiles.SafeName("C:\\folder\\a.png")=="a.png","virtual filenames cannot escape the owned temporary folder",results);
                Check(Reject(delegate { DropFiles.Descriptors(new byte[3],true); }) && Reject(delegate { DropFiles.Descriptors(Descriptor(""),true); }) && Reject(delegate { DropFiles.Descriptors(BitConverter.GetBytes(129),true); }),"malformed oversized and empty-name virtual file descriptions are rejected",results);
                using(MemoryStream input=new MemoryStream(new byte[4])) using(MemoryStream output=new MemoryStream()) { long budget=3; Check(Reject(delegate { DropFiles.Copy(input,output,ref budget); }),"dragged content cannot exceed the capture byte budget",results); }
                Check(!DropFiles.Offered(new DataObject(DataFormats.Text,"not an image")) && DropFiles.Offered(files) && DropFiles.Offered(virtualFiles),"text-only drags cannot advertise an image conversion target",results);
                Check(DropFiles.SourceFolder(new[]{@"C:\Photos\a.png",@"C:\Photos\b.png"})==@"C:\Photos" && DropFiles.SourceFolder(new[]{@"C:\Photos\a.png",@"D:\Other\b.png"})==null,"files in one persistent folder save beside the source; mixed folders require a destination",results);
                ConversionPreferences preferences=new ConversionPreferences(); preferences.Images.Reverse(); preferences.Images[0].Label="アイコン"; preferences.Images[0].Quality=82; preferences.Images[1].Enabled=false;
                Preferences old=Json.Read<Preferences>("{\"Version\":1,\"Hotkey\":\"F13\",\"Conversion\":{\"UseSourceFolder\":false,\"OutputFolder\":\"C:\\\\old\"},\"Items\":"+Json.Write(Preferences.Defaults().Items)+"}"); old.Validate(); Check(old.Conversion.Images.Count==11 && !Json.Write(old.Conversion).Contains("OutputFolder"),"legacy destination settings migrate without retaining a fixed output folder",results);
                Preferences saved=Preferences.Defaults(); saved.Conversion=preferences; Preferences clone=saved.Clone(); clone.Validate(); Check(clone.Conversion.Images[0].Operation=="zip" && clone.Conversion.Images[0].Label=="アイコン" && clone.Conversion.Images[0].Quality==82 && !clone.Conversion.Images[1].Enabled,"conversion ordering visibility names and quality survive settings backup",results);
                Check(preferences.Choices(false,"gif").Single().Operation=="gif" && preferences.Choices(false).Count==10 && ConversionCatalog.Supports("pdf-webp",true,1) && !ConversionCatalog.Supports("pdf-merge",true,1),"explicit action formats are honored independently of menu visibility and unsupported operations are filtered",results);
                string unsupported=Path.Combine(root,"note.txt"); File.WriteAllText(unsupported,"text"); Check(DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{unsupported})).Kind==DropKind.Files && DropFiles.Inspect(new VirtualImages(new[]{"file.exe"},bytes)).Kind==DropKind.Files,"arbitrary file and virtual-file drops offer ZIP compression without advertising image formats",results);
                string fakePdf=Path.Combine(root,"doc.pdf"); File.WriteAllText(fakePdf,"%PDF-"); Check(DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{fakePdf})).Kind==DropKind.Pdf && DropFiles.Inspect(new DataObject(DataFormats.FileDrop,new[]{fakePdf,original})).Kind==DropKind.Files,"PDF inputs select PDF candidates while mixed files offer ZIP compression",results);
                using(DropCapture pending=new DropCapture(files,DropFiles.Inspect(files))) { DroppedFiles asyncLocal=Await(pending.CaptureAsync(Path.Combine(root,"async-local"),CancellationToken.None)); Check(File.ReadAllBytes(asyncLocal.Files[0]).SequenceEqual(bytes),"file snapshots are captured off the UI thread",results); }
                using(DropCapture pending=new DropCapture(virtualFiles,DropFiles.Inspect(virtualFiles))) { DroppedFiles asyncVirtual=Await(pending.CaptureAsync(Path.Combine(root,"async-virtual"),CancellationToken.None)); Check(asyncVirtual.Files.Count==2,"virtual IDataObject content is marshaled to an STA capture thread",results); }
                virtualFiles.UseStream=true; using(DropCapture pending=new DropCapture(virtualFiles,DropFiles.Inspect(virtualFiles))) { DroppedFiles asyncStream=Await(pending.CaptureAsync(Path.Combine(root,"async-stream"),CancellationToken.None)); Check(asyncStream.Files.Count==2 && File.ReadAllBytes(asyncStream.Files[0]).SequenceEqual(bytes),"marshaled virtual IStream content preserves every byte",results); }
                using(DropCapture pending=new DropCapture(image,DropFiles.Inspect(image))) { DroppedFiles asyncBitmap=Await(pending.CaptureAsync(Path.Combine(root,"async-bitmap"),CancellationToken.None)); Check(asyncBitmap.Files.Count==1,"marshaled WPF bitmap data can be encoded off the UI thread",results); }
                using(DropCapture pending=new DropCapture(png,DropFiles.Inspect(png))) { DroppedFiles asyncPng=Await(pending.CaptureAsync(Path.Combine(root,"async-png"),CancellationToken.None)); Check(File.ReadAllBytes(asyncPng.Files[0]).SequenceEqual(bytes),"marshaled PNG stream data can be copied off the UI thread",results); }
                using(DropCapture pending=new DropCapture(files,DropFiles.Inspect(files))) { Check(Reject(delegate { Await(pending.CaptureAsync(Path.Combine(root,"cancel-capture"),new CancellationToken(true))); }),"canceled captures do not proceed to conversion",results); }
                string session=Path.Combine(Path.GetTempPath(),"orbitals-drop-"+Guid.NewGuid().ToString("N")); DroppedFiles captured=DropFiles.Capture(virtualFiles,Path.Combine(session,"input")); string outputDirectory=Path.Combine(root,"output"); Directory.CreateDirectory(outputDirectory);
                ConversionJob job=new ConversionJob { Files=captured.Files,Operation="image-pdf",Output=outputDirectory,Quality=90,Dpi=150 };
                ConversionResult result=ConversionRunner.RunAsync(job,session,true).GetAwaiter().GetResult();
                using(var pdf=Pdf::PdfSharp.Pdf.IO.PdfReader.Open(result.Files[0],Pdf::PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) Check(result.Errors.Count==0 && result.Files.Count==1 && pdf.PageCount==2 && !Directory.Exists(session),"dropped browser images become a two-page PDF and release their temporary capture after conversion",results);
                Check(Reject(delegate { ConversionRunner.RemoveDropDirectory(Path.GetPathRoot(root)); }),"conversion cleanup rejects directories that it does not own",results);
                string leased=Path.Combine(Path.GetTempPath(),"orbitals-drop-"+Guid.NewGuid().ToString("N")); ConversionRunner.PrepareDropDirectory(leased); Check(!ConversionRunner.CleanupAbandoned(leased) && Directory.Exists(leased),"startup cleanup preserves conversions owned by a living process",results); File.WriteAllText(Path.Combine(leased,"owner.json"),Json.Write(new ConversionLease { ProcessId=Int32.MaxValue,StartedUtc=1 })); Check(ConversionRunner.CleanupAbandoned(leased) && !Directory.Exists(leased),"startup cleanup removes owned captures abandoned by a terminated process",results);
            } finally { Directory.Delete(root,true); }
            return results;
        }
    }
}

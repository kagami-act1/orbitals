using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Xml;

namespace OrbitRing {
    static class SvgRasterizer {
        static readonly Regex urls=new Regex("url\\s*\\(\\s*['\"]?([^)'\"]+)",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        static bool LocalReference(string value) { return value.StartsWith("#",StringComparison.Ordinal) || value.StartsWith("data:image/png;base64,",StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:image/jpeg;base64,",StringComparison.OrdinalIgnoreCase) || value.StartsWith("data:image/webp;base64,",StringComparison.OrdinalIgnoreCase); }
        static void CheckCss(string value) { if(value.IndexOf('\\')>=0 || value.IndexOf("@import",StringComparison.OrdinalIgnoreCase)>=0 || urls.Matches(value).Cast<Match>().Any(m=>!LocalReference(m.Groups[1].Value.Trim()))) throw new Exception("SVGの外部参照には対応していません。画像や書体をSVG内に埋め込んでください。"); }
        static double Length(string value,double fallback) {
            Match match=Regex.Match(value??"","^\\s*([0-9]+(?:\\.[0-9]+)?)(px|pt|pc|in|mm|cm)?\\s*$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant); double number;
            if(!match.Success || !Double.TryParse(match.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out number)) return fallback;
            switch(match.Groups[2].Value.ToLowerInvariant()) { case "pt": number*=96.0/72; break; case "pc": number*=16; break; case "in": number*=96; break; case "mm": number*=96/25.4; break; case "cm": number*=96/2.54; break; } return number;
        }
        internal static XmlDocument Prepare(string path,int edge) {
            if(new FileInfo(path).Length>8*1024*1024) throw new Exception("SVGは8MBまでです。");
            XmlDocument document=new XmlDocument { XmlResolver=null };
            using(XmlReader reader=XmlReader.Create(path,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024 })) document.Load(reader);
            XmlElement root=document.DocumentElement;
            if(root==null || root.LocalName!="svg" || root.NamespaceURI!="http://www.w3.org/2000/svg") throw new Exception("SVGファイルの内容を確認してください。");
            XmlNodeList nodes=document.SelectNodes("//*"); if(nodes.Count>100000) throw new Exception("SVGの要素数が多すぎます。");
            foreach(XmlElement element in nodes) {
                if(element.LocalName=="script" || element.LocalName=="foreignObject") throw new Exception("スクリプトやHTMLを含むSVGには対応していません。");
                if(element.LocalName=="style") CheckCss(element.InnerText);
                foreach(XmlAttribute attribute in element.Attributes) {
                    if(attribute.LocalName=="href" && !LocalReference(attribute.Value.Trim())) throw new Exception("外部ファイルを参照するSVGには対応していません。画像をSVG内に埋め込んでください。");
                    if(attribute.LocalName=="style" || attribute.Value.IndexOf("url",StringComparison.OrdinalIgnoreCase)>=0) CheckCss(attribute.Value);
                    if(attribute.Prefix=="xml" && attribute.LocalName=="base") throw new Exception("外部の基準URLを含むSVGには対応していません。");
                }
            }
            double width=300,height=150; string[] box=root.GetAttribute("viewBox").Split(new[]{' ',','},StringSplitOptions.RemoveEmptyEntries); double w,h;
            if(box.Length==4 && Double.TryParse(box[2],NumberStyles.Float,CultureInfo.InvariantCulture,out w) && Double.TryParse(box[3],NumberStyles.Float,CultureInfo.InvariantCulture,out h)) { width=w; height=h; }
            width=Length(root.GetAttribute("width"),width); height=Length(root.GetAttribute("height"),height);
            if(width<=0 || height<=0 || Double.IsNaN(width) || Double.IsNaN(height) || Double.IsInfinity(width) || Double.IsInfinity(height)) throw new Exception("SVGの画像サイズが不正です。");
            if(!root.HasAttribute("viewBox")) root.SetAttribute("viewBox","0 0 "+width.ToString("R",CultureInfo.InvariantCulture)+" "+height.ToString("R",CultureInfo.InvariantCulture));
            if(edge>0) { double factor=edge/Math.Max(width,height); width*=factor; height*=factor; }
            if(width>16000 || height>16000 || width*height>40000000) throw new Exception("SVGの出力が大きすぎます。長辺のサイズを小さくしてください。");
            root.SetAttribute("width",Math.Max(1,Math.Round(width)).ToString(CultureInfo.InvariantCulture)); root.SetAttribute("height",Math.Max(1,Math.Round(height)).ToString(CultureInfo.InvariantCulture)); return document;
        }
        public static void Render(string input,int edge,string output,string directory) {
            string source=Path.Combine(directory,Guid.NewGuid().ToString("N")+".svg");
            try {
                XmlDocument document=Prepare(input,edge); using(XmlWriter writer=XmlWriter.Create(source,new XmlWriterSettings { Encoding=new UTF8Encoding(false) })) document.Save(writer);
                string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"conversion","resvg.exe");
                using(Process process=Process.Start(new ProcessStartInfo(executable,"--quiet --resources-dir "+FileConversion.Quote(directory)+" "+FileConversion.Quote(source)+" "+FileConversion.Quote(output)) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true })) using(ChildJob job=new ChildJob(process)) {
                    var error=process.StandardError.ReadToEndAsync(); var text=process.StandardOutput.ReadToEndAsync(); DateTime deadline=DateTime.UtcNow.AddSeconds(30);
                    while(!process.WaitForExit(100)) { if(!Directory.Exists(directory) || File.Exists(Path.Combine(directory,"cancel"))) throw new OperationCanceledException(); if(DateTime.UtcNow>deadline) throw new Exception("SVGの処理に時間がかかりすぎたため中止しました。"); }
                    error.GetAwaiter().GetResult(); text.GetAwaiter().GetResult(); if(process.ExitCode!=0 || !File.Exists(output)) throw new Exception("SVGを画像化できませんでした。ファイルの内容を確認してください。");
                }
            } finally { if(File.Exists(source)) File.Delete(source); }
        }
        sealed class ChildJob : IDisposable {
            IntPtr handle;
            [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
            [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job,int information,IntPtr data,uint length);
            [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
            [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
            public ChildJob(Process process) {
                handle=CreateJobObject(IntPtr.Zero,null); IntPtr data=Marshal.AllocHGlobal(144);
                try { for(int i=0;i<144;i++) Marshal.WriteByte(data,i,0); Marshal.WriteInt32(data,16,0x3000); Marshal.WriteInt64(data,120,512L*1024*1024); if(handle==IntPtr.Zero || !SetInformationJobObject(handle,9,data,144) || (!AssignProcessToJobObject(handle,process.Handle) && !process.HasExited)) throw new Exception("SVG変換の実行環境を準備できませんでした。"); }
                catch { if(!process.HasExited) process.Kill(); Dispose(); throw; } finally { Marshal.FreeHGlobal(data); }
            }
            public void Dispose() { if(handle!=IntPtr.Zero) { CloseHandle(handle); handle=IntPtr.Zero; } }
        }
    }
}

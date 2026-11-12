using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace OrbitRing {
    sealed class LibreOfficeConverter : IDocumentConverter {
        static readonly Lazy<string> program=new Lazy<string>(Find);
        public static bool Available { get { return program.Value!=null; } }
        static string Find() {
            foreach(RegistryKey root in new[]{Registry.CurrentUser,Registry.LocalMachine}) using(RegistryKey key=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\soffice.exe")) { string path=key==null?null:key.GetValue(null) as string; if(!String.IsNullOrEmpty(path) && File.Exists(path.Trim('"'))) return path.Trim('"'); }
            foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) { string path=Path.Combine(root,"LibreOffice","program","soffice.exe"); if(File.Exists(path)) return path; } return null;
        }
        static string Filter(string operation) {
            if(operation=="word-text") return "txt:Text (encoded):UTF8";
            if(operation=="word-docx") return "docx:Office Open XML Text";
            if(operation=="sheet-csv" || operation=="sheet-tsv") return (operation=="sheet-csv"?"csv:Text - txt - csv (StarCalc):44":"tsv:Text - txt - csv (StarCalc):9")+",34,76,1,,0,false,true,true,false,false,-1";
            return "pdf";
        }
        public List<string> Convert(string source,ConversionJob job,string stage,string directory,Action cancel) {
            if(!Available) throw new Exception("対応するMicrosoft Officeアプリ、またはLibreOfficeをインストールしてください。");
            string executable=Path.ChangeExtension(program.Value,"com"); if(!File.Exists(executable)) executable=program.Value;
            string profile=Path.Combine(stage,"profile"); Directory.CreateDirectory(profile);
            File.WriteAllText(Path.Combine(profile,"registrymodifications.xcu"),"<?xml version=\"1.0\" encoding=\"UTF-8\"?><oor:items xmlns:oor=\"http://openoffice.org/2001/registry\"><item oor:path=\"/org.openoffice.Office.Common/Security/Scripting\"><prop oor:name=\"MacroSecurityLevel\" oor:op=\"fuse\"><value>3</value></prop></item></oor:items>",new UTF8Encoding(false));
            ConversionProcesses.Run(executable,new[]{"-env:UserInstallation="+new Uri(profile+Path.DirectorySeparatorChar).AbsoluteUri,"--headless","--nologo","--norestore","--convert-to",Filter(job.Operation),"--outdir",stage,source},directory,"文書を変換できませんでした。",180);
            string extension=job.Operation=="word-text"?".txt":job.Operation=="word-docx"?".docx":job.Operation=="sheet-csv"?".csv":job.Operation=="sheet-tsv"?".tsv":".pdf";
            List<string> outputs=Directory.GetFiles(stage,"*"+extension,SearchOption.TopDirectoryOnly).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase).ToList();
            if(outputs.Count==0 || outputs.Any(p=>new FileInfo(p).Length==0)) throw new Exception("変換結果がありません。文書のパスワードや形式を確認してください。");
            if(DocumentConversion.SlideImages(job.Operation)) {
                string images=Path.Combine(stage,"slides"); Directory.CreateDirectory(images);
                ConversionResult rendered=FileConversion.Run(new ConversionJob { Files=outputs,Operation=job.Operation=="slides-png"?"pdf-png":"pdf-jpeg",Output=images,Quality=job.Quality,MaxEdge=job.MaxEdge,Dpi=job.Dpi,Pages="" },directory);
                if(rendered.Cancelled) throw new OperationCanceledException(); if(rendered.Errors.Count>0) throw new Exception(String.Join("\n",rendered.Errors)); return rendered.Files;
            }
            if(outputs.Count>1) {
                string folder=Path.Combine(stage,"sheets"),prefix=Path.GetFileNameWithoutExtension(source)+"-"; Directory.CreateDirectory(folder);
                for(int i=0;i<outputs.Count;i++) {
                    cancel(); string name=Path.GetFileName(outputs[i]); if(name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) name=name.Substring(prefix.Length);
                    string target=ArchiveConversion.Unique(folder,DropFiles.SafeName(name),false); File.Move(outputs[i],target); outputs[i]=target;
                }
            }
            return outputs;
        }
        public void Dispose() { }
    }
}

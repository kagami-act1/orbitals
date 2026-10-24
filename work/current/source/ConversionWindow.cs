using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class ConversionWindow : Window {
        readonly List<string> files=new List<string>();
        readonly string temporary=Path.Combine(Path.GetTempPath(),"orbitals-convert-"+Guid.NewGuid().ToString("N"));
        readonly ListBox list=new ListBox();
        readonly ComboBox operation=new ComboBox(),dpi=new ComboBox();
        readonly TextBox quality=EditorUi.Input("90"),edge=EditorUi.Input("0"),pages=EditorUi.Input(""),output=EditorUi.Input("");
        readonly TextBlock status=EditorUi.Text("ファイルを追加してください。",13,EditorUi.Muted);
        readonly StackPanel inputs=new StackPanel();
        Button convert,cancel;
        FrameworkElement pageField,qualityField,edgeField,dpiField;
        string jobDirectory;
        bool busy,closeWhenDone;
        public ConversionWindow(IntPtr source,string initial) {
            Title="orbitals — ファイル変換"; Icon=AppIcon.Image; Width=850; Height=810; MinWidth=710; MinHeight=680;
            Height=Math.Min(Height,SystemParameters.WorkArea.Height); WindowStartupLocation=WindowStartupLocation.CenterScreen;
            Background=EditorUi.Bg; Foreground=EditorUi.Ink; FontFamily=new FontFamily("Segoe UI, Yu Gothic UI"); EditorUi.Apply(this); Directory.CreateDirectory(temporary);
            DockPanel root=new DockPanel { Margin=new Thickness(28) }; Content=root;
            StackPanel heading=new StackPanel { Margin=new Thickness(0,0,0,20) }; DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading);
            heading.Children.Add(EditorUi.Text("O R B I T A L S",11,EditorUi.Mint)); TextBlock title=EditorUi.Text("ファイル変換",26,EditorUi.Ink); title.Margin=new Thickness(0,6,0,8); heading.Children.Add(title);
            heading.Children.Add(EditorUi.Text("画像とPDFをPC内で変換します。元のファイルは残し、同名の出力には連番を付けます。",13,EditorUi.Muted));
            DockPanel footer=new DockPanel { Margin=new Thickness(0,18,0,0) }; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            StackPanel buttons=new StackPanel { Orientation=Orientation.Horizontal }; DockPanel.SetDock(buttons,Dock.Right); footer.Children.Add(buttons);
            cancel=EditorUi.Button("キャンセル",delegate { RequestCancel(); },false); cancel.Visibility=Visibility.Collapsed; buttons.Children.Add(cancel);
            convert=EditorUi.Button("変換する",delegate { Start(); },true); convert.Margin=new Thickness(0); buttons.Children.Add(convert);
            status.TextWrapping=TextWrapping.Wrap; status.VerticalAlignment=VerticalAlignment.Center; status.Margin=new Thickness(0,0,14,0); footer.Children.Add(status);
            root.Children.Add(new ScrollViewer { Content=inputs,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            WrapPanel add=new EditorButtonRow { Margin=new Thickness(0,0,0,10) }; inputs.Children.Add(add);
            add.Children.Add(EditorUi.Button("＋ ファイル",delegate { var dialog=new Microsoft.Win32.OpenFileDialog { Filter=FileConversion.Filter,Multiselect=true }; if(dialog.ShowDialog(this)==true) AddFiles(dialog.FileNames); },false));
            add.Children.Add(EditorUi.Button("クリップボードから",delegate { FromClipboard(true); },false));
            add.Children.Add(EditorUi.Button("選択を外す",delegate { if(list.SelectedIndex>=0) { files.RemoveAt(list.SelectedIndex); Refresh(); } },false));
            add.Children.Add(EditorUi.Button("↑",delegate { Move(-1); },false)); add.Children.Add(EditorUi.Button("↓",delegate { Move(1); },false));
            list.MinHeight=130; list.MaxHeight=200; list.AllowDrop=true; list.Padding=new Thickness(8); list.Background=EditorUi.Bg; list.Foreground=EditorUi.Ink; list.BorderBrush=EditorUi.Line; list.BorderThickness=new Thickness(1); System.Windows.Automation.AutomationProperties.SetName(list,"変換するファイル（ドラッグして追加）"); inputs.Children.Add(list);
            list.Drop+=delegate(object sender,DragEventArgs e) { if(!busy && e.Data.GetDataPresent(DataFormats.FileDrop)) AddFiles((string[])e.Data.GetData(DataFormats.FileDrop)); e.Handled=true; };
            TextBlock fileHelp=EditorUi.Text("ここへドロップして追加。PDFの結合・作成では、上から順に並べます。GIF・複数ページTIFFは先頭の画像を使います。",12,EditorUi.Muted); fileHelp.TextWrapping=TextWrapping.Wrap; fileHelp.Margin=new Thickness(0,7,0,18); inputs.Children.Add(fileHelp);
            operation.ItemsSource=FileConversion.Names; operation.Padding=new Thickness(8); operation.MinHeight=38; operation.SelectedIndex=Math.Max(0,Array.IndexOf(FileConversion.Operations,initial)); inputs.Children.Add(Field("変換内容",operation));
            WrapPanel options=new WrapPanel { Margin=new Thickness(0,16,0,16) }; inputs.Children.Add(options);
            qualityField=Field("画質（1〜100）",quality); qualityField.Width=230; qualityField.Margin=new Thickness(0,0,12,0); options.Children.Add(qualityField);
            edgeField=Field("長辺の上限（px・0は変更なし）",edge); edgeField.Width=230; edgeField.Margin=new Thickness(0,0,12,0); options.Children.Add(edgeField);
            dpi.ItemsSource=new[]{"96 dpi","150 dpi","300 dpi"}; dpi.SelectedIndex=1; dpi.Padding=new Thickness(8); dpi.MinHeight=38; dpiField=Field("PDFの画像化の解像度",dpi); dpiField.Width=230; options.Children.Add(dpiField);
            pageField=Field("対象ページ（空欄はすべて・例：1,3-5）",pages); pageField.Margin=new Thickness(0,0,0,16); inputs.Children.Add(pageField);
            DockPanel destination=new DockPanel(); Button browse=EditorUi.Button("選ぶ",delegate { using(var dialog=new System.Windows.Forms.FolderBrowserDialog { Description="変換後の保存先",SelectedPath=Directory.Exists(output.Text)?output.Text:"" }) if(dialog.ShowDialog()==System.Windows.Forms.DialogResult.OK) output.Text=dialog.SelectedPath; },false); browse.Margin=new Thickness(12,0,0,0); DockPanel.SetDock(browse,Dock.Right); destination.Children.Add(browse); destination.Children.Add(output); inputs.Children.Add(Field("保存先",destination));
            System.Windows.Automation.AutomationProperties.SetName(output,"保存先");
            Button open=EditorUi.Button("保存先を開く",delegate { if(Directory.Exists(output.Text)) Process.Start(new ProcessStartInfo(output.Text) { UseShellExecute=true }); },false); open.HorizontalAlignment=HorizontalAlignment.Left; open.Margin=new Thickness(0,10,0,0); inputs.Children.Add(open);
            operation.SelectionChanged+=delegate { Options(); }; Options();
            AddFiles(SelectedFiles(source)); if(files.Count==0) FromClipboard(false); if(output.Text=="") output.Text=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) { if(busy) { e.Cancel=true; closeWhenDone=true; RequestCancel(); } };
            Closed+=delegate { Cleanup(temporary); };
        }
        static StackPanel Field(string label,UIElement control) { StackPanel panel=new StackPanel(); TextBlock text=EditorUi.Text(label,12,EditorUi.Muted); text.Margin=new Thickness(0,0,0,7); panel.Children.Add(text); panel.Children.Add(control); if(control is Control) System.Windows.Automation.AutomationProperties.SetName(control,label); return panel; }
        void Options() { string value=FileConversion.Operations[operation.SelectedIndex]; bool pdf=value.StartsWith("pdf-",StringComparison.Ordinal); qualityField.Visibility=value.EndsWith("jpeg",StringComparison.Ordinal) || value.EndsWith("webp",StringComparison.Ordinal)?Visibility.Visible:Visibility.Collapsed; edgeField.Visibility=(!pdf && !FileConversion.Ocr(value)) || FileConversion.PdfImage(value)?Visibility.Visible:Visibility.Collapsed; dpiField.Visibility=FileConversion.PdfRaster(value)?Visibility.Visible:Visibility.Collapsed; pageField.Visibility=pdf && value!="pdf-merge"?Visibility.Visible:Visibility.Collapsed; }
        void AddFiles(IEnumerable<string> paths) {
            int skipped=0; foreach(string path in paths) { if(files.Count>=128 || !File.Exists(path) || !FileConversion.Supported(path)) { skipped++; continue; } if(!files.Contains(path,StringComparer.OrdinalIgnoreCase)) files.Add(path); }
            if(files.Count>0 && String.IsNullOrEmpty(output.Text)) { output.Text=Path.GetDirectoryName(files[0]); if(output.Text.StartsWith(temporary,StringComparison.OrdinalIgnoreCase)) output.Text=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); }
            if(files.Count>0 && files.All(ConversionCatalog.PdfFile) && !FileConversion.Operations[operation.SelectedIndex].StartsWith("pdf-",StringComparison.Ordinal)) operation.SelectedIndex=Array.IndexOf(FileConversion.Operations,"pdf-png");
            Refresh(); if(skipped>0) status.Text+=" 対応外・上限超過のファイルは追加していません。";
        }
        void Refresh() { list.ItemsSource=files.Select(Path.GetFileName).ToArray(); list.ToolTip=String.Join("\n",files); status.Text=files.Count==0?"ファイルを追加してください。":files.Count+"件を選択中"; convert.IsEnabled=files.Count>0; }
        void Move(int direction) { int index=list.SelectedIndex,next=index+direction; if(index<0 || next<0 || next>=files.Count) return; string file=files[index]; files[index]=files[next]; files[next]=file; Refresh(); list.SelectedIndex=next; }
        void FromClipboard(bool message) {
            try {
                if(Clipboard.ContainsFileDropList()) { AddFiles(Clipboard.GetFileDropList().Cast<string>()); return; }
                if(Clipboard.ContainsImage()) { BitmapSource image=Clipboard.GetImage(); if((long)image.PixelWidth*image.PixelHeight>100000000) throw new Exception("画像が大きすぎます。"); string file=Path.Combine(temporary,"クリップボード-"+DateTime.Now.ToString("HHmmssfff")+".png"); PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using(Stream stream=File.Create(file)) encoder.Save(stream); AddFiles(new[]{file}); return; }
                if(message) status.Text="クリップボードに画像・ファイルがありません。";
            } catch(Exception ex) { status.Text="クリップボードを読み込めませんでした。"+ex.Message; }
        }
        internal static IEnumerable<string> SelectedFiles(IntPtr source) {
            List<string> paths=new List<string>(); if(source==IntPtr.Zero) return paths;
            object shell=null,windows=null;
            try {
                Type type=Type.GetTypeFromProgID("Shell.Application"); shell=Activator.CreateInstance(type); windows=type.InvokeMember("Windows",BindingFlags.InvokeMethod,null,shell,null);
                foreach(object window in (System.Collections.IEnumerable)windows) {
                    object document=null,selection=null;
                    try { long hwnd=Convert.ToInt64(window.GetType().InvokeMember("HWND",BindingFlags.GetProperty,null,window,null)); if(hwnd!=source.ToInt64()) continue;
                        document=window.GetType().InvokeMember("Document",BindingFlags.GetProperty,null,window,null); selection=document.GetType().InvokeMember("SelectedItems",BindingFlags.InvokeMethod,null,document,null);
                        foreach(object item in (System.Collections.IEnumerable)selection) try { string path=(string)item.GetType().InvokeMember("Path",BindingFlags.GetProperty,null,item,null); if(File.Exists(path)) paths.Add(path); } finally { if(Marshal.IsComObject(item)) Marshal.ReleaseComObject(item); }
                    } catch(COMException) { } catch(TargetInvocationException) { } finally { if(selection!=null && Marshal.IsComObject(selection)) Marshal.ReleaseComObject(selection); if(document!=null && Marshal.IsComObject(document)) Marshal.ReleaseComObject(document); if(Marshal.IsComObject(window)) Marshal.ReleaseComObject(window); }
                }
            } catch(COMException) { } finally { if(windows!=null && Marshal.IsComObject(windows)) Marshal.ReleaseComObject(windows); if(shell!=null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); }
            return paths;
        }
        async void Start() {
            if(busy) return;
            int q,e; if(!Int32.TryParse(quality.Text,out q) || !Int32.TryParse(edge.Text,out e)) { status.Text="画質・長辺の上限を数値で指定してください。"; return; }
            ConversionJob job=new ConversionJob { Files=files.ToList(),Operation=FileConversion.Operations[operation.SelectedIndex],Quality=q,MaxEdge=e,Dpi=new[]{96,150,300}[dpi.SelectedIndex],Pages=pages.Text.Trim(),Output=output.Text.Trim() };
            try { FileConversion.Validate(job); } catch(Exception ex) { status.Text=ex.Message; return; }
            jobDirectory=Path.Combine(temporary,Guid.NewGuid().ToString("N")); Directory.CreateDirectory(jobDirectory);
            busy=true; inputs.IsEnabled=false; convert.IsEnabled=false; cancel.Visibility=Visibility.Visible; cancel.IsEnabled=true; status.Text="変換しています…";
            DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(250) }; timer.Tick+=delegate { try { string progress=Path.Combine(jobDirectory,"progress"); if(File.Exists(progress)) status.Text="変換中 · "+File.ReadAllText(progress); } catch(IOException) { } }; timer.Start();
            try {
                ConversionResult result=await ConversionRunner.RunAsync(job,jobDirectory);
                if(FileConversion.CopiesText(job.Operation) && !result.Cancelled && result.Errors.Count==0 && !String.IsNullOrEmpty(result.Text)) Clipboard.SetText(result.Text);
                status.Text=result.Files.Count+"件を保存しました。"+(result.Cancelled?" 残りの処理をキャンセルしました。":"")+(result.Errors.Count>0?" "+result.Errors.Count+"件でエラー。":"");
                if(FileConversion.CopiesText(job.Operation) && !result.Cancelled && result.Errors.Count==0) status.Text="認識した文字をコピーしました。";
                if(result.Errors.Count>0 && !closeWhenDone) OrbitDialog.Info(this,"変換できなかったファイル",String.Join("\n",result.Errors));
            } catch(Exception ex) { status.Text=ex.Message; }
            finally { timer.Stop(); busy=false; inputs.IsEnabled=true; convert.IsEnabled=files.Count>0; cancel.Visibility=Visibility.Collapsed; Cleanup(jobDirectory); jobDirectory=null; if(closeWhenDone) Close(); }
        }
        void RequestCancel() { if(!busy || jobDirectory==null) return; File.WriteAllText(Path.Combine(jobDirectory,"cancel"),""); cancel.IsEnabled=false; status.Text="キャンセルしています…"; }
        void Cleanup(string directory) { if(directory!=null && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(temporary),StringComparison.OrdinalIgnoreCase)) try { if(Directory.Exists(directory)) Directory.Delete(directory,true); } catch(IOException) { } catch(UnauthorizedAccessException) { } }
    }
    sealed partial class Controller {
        ConversionWindow conversion;
        public void OpenConversionDetails(IntPtr source,string initial="") { HideRing(); if(conversion!=null) { conversion.Activate(); return; } conversion=new ConversionWindow(source,initial); conversion.Closed+=delegate { conversion=null; }; conversion.Show(); }
    }
}

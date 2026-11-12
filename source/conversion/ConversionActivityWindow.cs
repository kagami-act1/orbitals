using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Interop;

namespace OrbitRing {
    sealed class ConversionActivityWindow : Window {
        readonly IList<ConversionActivity> activities;
        readonly StackPanel rows=new StackPanel();
        readonly DispatcherTimer dismiss=new DispatcherTimer { Interval=TimeSpan.FromSeconds(12) };
        readonly bool automatic;
        string rendered;
        public ConversionActivityWindow(IList<ConversionActivity> jobs,bool transient) {
            activities=jobs; automatic=transient;
            Title="orbitals — 変換の履歴"; Icon=AppIcon.Image; Width=transient?480:600; Height=transient?310:580; MinWidth=450; MinHeight=240; ShowActivated=!transient; Topmost=transient; Background=EditorUi.Bg; Foreground=EditorUi.Ink; FontFamily=new FontFamily("Segoe UI, Yu Gothic UI"); EditorUi.Apply(this);
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            if(transient) { Native.POINT point; if(Native.GetCursorPos(out point)) { var bounds=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(point.X,point.Y)).WorkingArea; uint dx=96,dy=96; try { Native.GetDpiForMonitor(Native.MonitorFromPoint(point,2),0,out dx,out dy); } catch { } double dpi=Math.Max(1,dx/96.0); WindowStartupLocation=WindowStartupLocation.Manual; Left=bounds.Right/dpi-Width-16; Top=bounds.Bottom/dpi-Height-16; } }
            DockPanel root=new DockPanel { Margin=new Thickness(22) }; Content=root;
            DockPanel top=new DockPanel { Margin=new Thickness(0,0,0,14) }; DockPanel.SetDock(top,Dock.Top); root.Children.Add(top);
            Button close=EditorUi.Button("閉じる",delegate { Close(); },false); DockPanel.SetDock(close,Dock.Right); top.Children.Add(close); top.Children.Add(EditorUi.Text("変換の履歴",22,EditorUi.Ink));
            TextBlock note=EditorUi.Text("直近15件を表示します。履歴はアプリを終了すると消えます。",12,EditorUi.Muted); note.Margin=new Thickness(0,12,0,0); DockPanel.SetDock(note,Dock.Bottom); root.Children.Add(note);
            root.Children.Add(new ScrollViewer { Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            dismiss.Tick+=delegate { dismiss.Stop(); if(!IsMouseOver && !IsActive && activities.All(j=>!j.Running)) Close(); }; MouseEnter+=delegate { dismiss.Stop(); }; MouseLeave+=delegate { if(automatic && activities.All(j=>!j.Running)) dismiss.Start(); };
            Closed+=delegate { dismiss.Stop(); rows.Children.Clear(); Content=null; if(Application.Current!=null && Application.Current.MainWindow==this) Application.Current.MainWindow=null; }; Refresh();
        }
        public void Refresh() {
            string stamp=String.Join("|",activities.Select(j=>j.Name+"\n"+j.Status+"\n"+j.Running+"\n"+j.Output)); if(rendered==stamp) return; rendered=stamp; rows.Children.Clear();
            if(activities.Count==0) rows.Children.Add(EditorUi.Text("変換の履歴はありません。",14,EditorUi.Muted));
            foreach(ConversionActivity activity in activities) {
                StackPanel content=new StackPanel { Margin=new Thickness(14) };
                content.Children.Add(EditorUi.Text(activity.Name,14,EditorUi.Ink)); TextBlock status=EditorUi.Text(activity.Status,12,activity.Running?EditorUi.Mint:EditorUi.Muted); status.Margin=new Thickness(0,7,0,8); content.Children.Add(status);
                WrapPanel buttons=new EditorButtonRow(); content.Children.Add(buttons);
                if(activity.Running) buttons.Children.Add(EditorUi.Button("キャンセル",delegate { activity.RequestCancel(); Refresh(); },false));
                if(activity.Result!=null && !String.IsNullOrEmpty(activity.Result.Text)) buttons.Children.Add(EditorUi.Button("文字をコピー",delegate { try { Clipboard.SetText(activity.Result.Text); } catch(Exception ex) { OrbitDialog.Info(this,"コピーできませんでした",ex.Message); } },false));
                if(!String.IsNullOrEmpty(activity.Output)) { TextBlock path=EditorUi.Text(activity.Output,11,EditorUi.Muted); path.TextWrapping=TextWrapping.Wrap; path.Margin=new Thickness(0,0,0,8); content.Children.Insert(content.Children.Count-1,path); buttons.Children.Add(EditorUi.Button("保存先を開く",delegate { try { Process.Start(new ProcessStartInfo(activity.Output) { UseShellExecute=true }); } catch(Exception ex) { OrbitDialog.Info(this,"保存先を開けませんでした",ex.Message); } },false)); }
                if(activity.Result!=null && activity.Result.Errors.Count>0) buttons.Children.Add(EditorUi.Button("エラーの詳細",delegate { OrbitDialog.Info(this,"変換できなかったファイル",String.Join("\n",activity.Result.Errors)); },false));
                rows.Children.Add(new Border { Background=EditorUi.Panel,BorderBrush=EditorUi.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Margin=new Thickness(0,0,0,12),Child=content });
            }
            if(automatic && activities.Count>0 && activities.All(j=>!j.Running)) { dismiss.Stop(); dismiss.Start(); }
        }
    }
    static class ConversionDestination {
        sealed class DialogOwner : System.Windows.Forms.IWin32Window { public IntPtr Handle { get; private set; } public DialogOwner(Window window) { Handle=new WindowInteropHelper(window).Handle; } }
        public static string Choose(Window owner) {
            using(System.Windows.Forms.FolderBrowserDialog dialog=new System.Windows.Forms.FolderBrowserDialog { Description="元ファイルの場所がないため、変換後の保存先を選んでください。",ShowNewFolderButton=true,SelectedPath=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) }) {
                var result=owner==null?dialog.ShowDialog():dialog.ShowDialog(new DialogOwner(owner)); return result==System.Windows.Forms.DialogResult.OK?dialog.SelectedPath:null;
            }
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace OrbitRing {
    sealed class OrbitOverlay : Window {
        readonly Canvas canvas=new Canvas();
        readonly Polyline trail=new Polyline { Stroke=Ui.Mint,StrokeThickness=3,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round };
        readonly TextBlock hint=Ui.Text("",14,Ui.Ink);
        readonly Border label;
        readonly double trailLength;
        double dpi; Native.RECT monitor;
        public readonly int Session;
        public OrbitOverlay(OrbitFrame frame,AppearancePreferences appearance) {
            trail.Stroke=appearance.Trail; trail.StrokeThickness=appearance.TrailWidth; trailLength=appearance.TrailLength;
            Session=frame.Session; Title="orbitals — orbit"; ShowActivated=false; ShowInTaskbar=false; Topmost=true; WindowStyle=WindowStyle.None; AllowsTransparency=true; Background=Brushes.Transparent; IsHitTestVisible=false;
            Native.MONITORINFO info=new Native.MONITORINFO { Size=Marshal.SizeOf(typeof(Native.MONITORINFO)) }; IntPtr m=Native.MonitorFromPoint(frame.Origin,2); Native.GetMonitorInfo(m,ref info); monitor=info.Monitor;
            uint x=96,y=96; try { Native.GetDpiForMonitor(m,0,out x,out y); } catch { } dpi=Math.Max(1,x/96.0);
            Left=monitor.Left/dpi; Top=monitor.Top/dpi; Width=(monitor.Right-monitor.Left)/dpi; Height=(monitor.Bottom-monitor.Top)/dpi;
            label=new Border { Background=Ui.Panel,CornerRadius=new CornerRadius(7),Padding=new Thickness(12,8,12,8),Child=hint }; canvas.Children.Add(trail); canvas.Children.Add(label); Content=canvas;
            IntPtr hwnd=new WindowInteropHelper(this).EnsureHandle(); Native.SetWindowLong(hwnd,-20,Native.GetWindowLong(hwnd,-20)|0x08000000|0x80|0x20);
            Native.SetWindowPos(hwnd,new IntPtr(-1),monitor.Left,monitor.Top,monitor.Right-monitor.Left,monitor.Bottom-monitor.Top,0x10); Update(frame); Show();
        }
        public void Update(OrbitFrame frame) {
            PointCollection p=new PointCollection(); foreach(Point point in AppearancePreferences.VisibleTrail(frame.Points,trailLength)) p.Add(new Point((point.X-monitor.Left)/dpi,(point.Y-monitor.Top)/dpi)); trail.Points=p;
            if(hint.Text!=frame.Hint) hint.Text=frame.Hint;
            double left=Math.Max(4,Math.Min(Width-310,(frame.Origin.X-monitor.Left)/dpi+25)),top=Math.Max(4,Math.Min(Height-60,(frame.Origin.Y-monitor.Top)/dpi+28));
            if(Canvas.GetLeft(label)!=left) Canvas.SetLeft(label,left); if(Canvas.GetTop(label)!=top) Canvas.SetTop(label,top);
        }
        public void Retire() { Close(); Content=null; canvas.Children.Clear(); if(Application.Current!=null && Application.Current.MainWindow==this) Application.Current.MainWindow=null; }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OrbitRing {
    sealed class RingWindow : Window {
        readonly Controller owner;
        RingSurface surface;
        PlanetMenu navigation;
        public bool CanGoBack { get { return navigation!=null && navigation.CanGoBack; } }
        public string StackName { get { return navigation==null?null:navigation.Title; } }
        Preferences presentationConfig;
        Canvas pointerArea;
        Border hoverLabel;
        TextBlock hoverText;
        double displayScale;
        bool preview,holdPressed;
        int presentation;
        bool retired;
        Native.POINT invocationPoint;
        public IntPtr Handle;
        public RingWindow(Controller controller) {
            owner=controller; Icon=AppIcon.Image; Title="orbitals — planet";
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true; Background=Brushes.Transparent;
            ShowInTaskbar=false; ShowActivated=false; Topmost=true;
            SourceInitialized+=delegate {
                Handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(Handle,-20,Native.GetWindowLong(Handle,-20)|0x08000000|0x80);
                HwndSource.FromHwnd(Handle).AddHook(delegate(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled) { if(msg==0x21) { handled=true; return new IntPtr(3); } return IntPtr.Zero; });
            };
        }
        public void Rebuild() {
            PlanetReveal.CancelAll(pointerArea);
            if(navigation==null) navigation=new PlanetMenu(owner.ActiveItems??owner.Config.Items,owner.Config.ButtonSize,owner.Config.Appearance.StackGap,owner.Config.Appearance.EvenSpacing);
            presentationConfig=owner.Presentation(navigation.Targets.Select(t=>t.Item)); surface=new RingSurface(); surface.Bind(presentationConfig,navigation); surface.SetScale(owner.Config.RingScale);
            // Alpha zero pixels of a layered window pass input to the app below.
            // A single alpha step keeps the cancel area practically invisible but interactive.
            pointerArea=new Canvas { Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)) };
            pointerArea.Children.Add(surface); surface.AttachPointerArea(pointerArea); Content=pointerArea;
            pointerArea.PreviewMouseLeftButtonDown+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e) {
                int index=navigation.Hit(e.GetPosition(surface)); PlanetTarget target=navigation.Target(index);
                if(target==null || target.Item.Kind!="hold") return;
                e.Handled=true;
                if(preview) { surface.ShowPreviewSelection(index); return; }
                holdPressed=true;
                if(!pointerArea.CaptureMouse()) { holdPressed=false; return; }
                if(!owner.BeginHold(target.Item)) EndHoldCapture();
            };
            pointerArea.PreviewMouseLeftButtonUp+=delegate(object sender,System.Windows.Input.MouseButtonEventArgs e) {
                if(!holdPressed) return; e.Handled=true; owner.HideRing();
            };
            pointerArea.LostMouseCapture+=delegate { if(holdPressed) owner.HideRing(); };
            hoverText=Ui.Text("",14,Ui.Ink); hoverText.MaxWidth=260;
            hoverLabel=new Border { Child=hoverText,Background=Ui.Panel,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),Padding=new Thickness(11,7,11,7),IsHitTestVisible=false,Visibility=Visibility.Collapsed };
            Canvas.SetZIndex(hoverLabel,30); pointerArea.Children.Add(hoverLabel); surface.IconHovered=ShowHoverLabel;
            surface.Invoked=Run; surface.Cancelled=owner.HideRing; surface.BackRequested=owner.BackStack; surface.StackTitle=owner.InsideStack?owner.StackName:null;
            surface.VolumeRequested=delegate(int delta) { if(!preview) owner.Volume(delta>0?"VolumeUp":"VolumeDown"); };
            surface.SetPlanet(owner.PlanetName,owner.PlanetPosition,owner.CanSwitchPlanet?(Action<int>)owner.SwitchPlanet:null,owner.PlanetPage,owner.PlanetPageCount);
        }
        public void ChangePlanet(int direction=1) {
            navigation=null; RefreshLevel(false); surface.UpdatePointer(new Point(205,205));
            if(owner.Config.Appearance.PlanetSwitchAnimation && SystemParameters.ClientAreaAnimation)
                PlanetReveal.PlaySwitch(pointerArea,new Point(Canvas.GetLeft(surface),Canvas.GetTop(surface)),owner.Config.RingScale,owner.Config.Appearance.Accent,surface,direction);
        }
        void RefreshLevel(bool recenter) { double x=Canvas.GetLeft(surface),y=Canvas.GetTop(surface); Rebuild(); Canvas.SetLeft(surface,x); Canvas.SetTop(surface,y); pointerArea.UpdateLayout(); if(recenter) RestoreInvocationPointer(); surface.UpdatePointer(recenter?new Point(205,205):System.Windows.Input.Mouse.GetPosition(surface)); }
        public void OpenStack(ActionItem item) { bool recenter=PlanetStack.Mode(item)=="level"; if(navigation!=null && navigation.Open(item)) RefreshLevel(recenter); }
        public bool NavigateBack() { if(navigation==null || !navigation.CanGoBack) return false; bool recenter=!navigation.Layers[navigation.Layers.Count-1].Arc; navigation.Back(); RefreshLevel(recenter); return true; }
        void Run(int index) { PlanetTarget target=navigation.Target(index); if(target==null || target.Item.Kind=="none") return; if(target.Item.Kind=="stack") { OpenStack(target.Item); return; } if(preview) surface.ShowPreviewSelection(index); else owner.Execute(target.Item); }
        public void EndHoldCapture() { holdPressed=false; if(pointerArea!=null && pointerArea.IsMouseCaptured) pointerArea.ReleaseMouseCapture(); }
        public void RestoreInvocationPointer() {
            if(!Native.SetCursorPos(invocationPoint.X,invocationPoint.Y))
                throw new Exception("呼び出した位置へカーソルを戻せなかったため、キー保持を中止しました。");
        }
        public void YieldInputForHold() {
            // Release mouse capture and remove the overlay input surface so wheel
            // events reach the foreground app. Controller retains the held keys.
            EndHoldCapture();
            Retire();
        }
        void ShowHoverLabel(int index) {
            if(retired) return;
            if(index<0) { hoverLabel.Visibility=Visibility.Collapsed; return; }
            hoverText.Text=presentationConfig.Items[index].Label;
            hoverLabel.Visibility=Visibility.Visible;
            // A sibling of the scaled ring: text stays at a readable 14 DIP.
            hoverLabel.Measure(new Size(290,Double.PositiveInfinity));
            Point p=surface.TranslatePoint(surface.IconPosition(index),pointerArea);
            double radius=surface.IconSize(index)*displayScale/2;
            double x=p.X-hoverLabel.DesiredSize.Width/2,y=p.Y-radius-hoverLabel.DesiredSize.Height-8;
            if(y<6) y=p.Y+radius+8;
            Canvas.SetLeft(hoverLabel,Math.Max(6,Math.Min(x,Width-hoverLabel.DesiredSize.Width-6)));
            Canvas.SetTop(hoverLabel,Math.Max(6,Math.Min(y,Height-hoverLabel.DesiredSize.Height-6)));
        }
        public void ActivateSlot(int index) { if(IsVisible && index>=0 && index<8 && navigation!=null) Run(navigation.KeyboardTarget(index)); }
        public void PrepareHidden() { presentation++; Opacity=0; }
        public void Retire() {
            if(retired) return; retired=true; EndHoldCapture(); PlanetReveal.CancelAll(pointerArea); PrepareHidden(); Close();
            if(Application.Current!=null && Application.Current.MainWindow==this) Application.Current.MainWindow=null;
            Content=null; pointerArea=null; surface=null; hoverLabel=null; hoverText=null; presentationConfig=null; navigation=null;
        }
        public void Open(bool isPreview,Native.POINT point) {
            PrepareHidden(); int version=presentation;
            preview=isPreview; Rebuild();
            invocationPoint=point;
            IntPtr monitor=Native.MonitorFromPoint(point,2);
            Native.MONITORINFO info=new Native.MONITORINFO { Size=Marshal.SizeOf(typeof(Native.MONITORINFO)) };
            if(!Native.GetMonitorInfo(monitor,ref info)) throw new Exception("画面の情報を取得できませんでした。");
            uint xDpi=96,yDpi=96; try { Native.GetDpiForMonitor(monitor,0,out xDpi,out yDpi); } catch { }
            double dpi=Math.Max(1,xDpi/96.0);
            double scale=owner.Config.RingScale;
            displayScale=scale; surface.SetScale(scale);
            // Use the full monitor, including the taskbar. The ring stays centered
            // on the invocation point even when some of it lies off screen.
            RingPlacement placement=new RingPlacement(point,info.Monitor,dpi,scale);
            Width=placement.WindowSize.Width; Height=placement.WindowSize.Height;
            int w=(int)Math.Round(Width*dpi),h=(int)Math.Round(Height*dpi);
            Canvas.SetLeft(surface,placement.Offset.X); Canvas.SetTop(surface,placement.Offset.Y);
            Left=info.Monitor.Left/dpi; Top=info.Monitor.Top/dpi;
            new WindowInteropHelper(this).EnsureHandle();
            Native.SetWindowPos(Handle,new IntPtr(-1),info.Monitor.Left,info.Monitor.Top,w,h,0x0010); Show();
            pointerArea.UpdateLayout();
            surface.UpdatePointer(new Point(205,205));
            // Let layout/render commit the new pointer position while fully transparent.
            // Versioning prevents an old queued reveal after a quick close/reopen.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,new Action(delegate {
                if(!IsVisible || version!=presentation) return;
                pointerArea.UpdateLayout();
                Opacity=1;
                if(owner.Config.Appearance.RingAnimation && SystemParameters.ClientAreaAnimation) PlanetReveal.Play(pointerArea,placement.Offset,scale,owner.Config.Appearance.Accent,surface);
            }));
        }
        public void Export(string path) {
            Rebuild(); ExportElement(surface,path,410*owner.Config.RingScale,410*owner.Config.RingScale);
        }
        public static void ExportElement(FrameworkElement element,string path,double width,double height) {
            element.Measure(new Size(width,height)); element.Arrange(new Rect(0,0,width,height)); element.UpdateLayout();
            RenderTargetBitmap bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*2),(int)Math.Ceiling(height*2),192,192,PixelFormats.Pbgra32); bitmap.Render(element);
            PngBitmapEncoder encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(FileStream stream=File.Create(path)) encoder.Save(stream);
        }
    }
}

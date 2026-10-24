using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace OrbitRing {
    static class ConversionPlanets {
        public const double Size=750,Center=Size/2;
        public static double OrbitRadius(int count) { return 131; }
        internal static double ButtonSize(int count,double preferred=76) { return Math.Min(preferred,2*OrbitRadius(count)*Math.Sin(Math.PI/Math.Max(3,count))-8); }
        public static double RingRadius(int count) { return OrbitRadius(count)+58; }
        public static Point Position(int index,int count) { double angle=(index*360.0/Math.Max(1,count)-90)*Math.PI/180; return new Point(Center+OrbitRadius(count)*Math.Cos(angle),Center+OrbitRadius(count)*Math.Sin(angle)); }
        internal static Rect LabelBounds(int index,int count,Size size) { Point center=Position(index,count); Vector direction=center-new Point(Center,Center); direction.Normalize(); Point at=center+direction*(RingRadius(count)-OrbitRadius(count)+12+Math.Abs(direction.X)*size.Width/2+Math.Abs(direction.Y)*size.Height/2); return new Rect(at.X-size.Width/2,at.Y-size.Height/2,size.Width,size.Height); }
        public static int Hit(Point point,Canvas drawing) { Rect[] labels=(Rect[])drawing.Tag; for(int i=0;i<labels.Length;i++) if((point-Position(i,labels.Length)).Length<=((FrameworkElement)drawing.Children[i*2+2]).Width/2+4 || drawing.Children[i*2+3].Visibility==Visibility.Visible && labels[i].Contains(point)) return i; return -1; }
        public static Canvas Drawing(IList<ConversionChoice> choices,AppearancePreferences appearance,double scale=1,int selected=-1,bool editing=true,double buttonSize=76) {
            Canvas canvas=new Canvas { Width=Size,Height=Size,Background=Brushes.Transparent };
            if(!appearance.TextOnly) MaterialIcons.Prepare(choices.Select(c=>ActionIcons.EffectiveKey(c.Action())));
            Rect[] labels=new Rect[choices.Count]; canvas.Tag=labels;
            double ring=RingRadius(choices.Count),orbit=OrbitRadius(choices.Count);
            Put(canvas,new Ellipse { Width=ring*2,Height=ring*2,Fill=Ui.Brush("#F2181E26"),Stroke=Ui.Line,StrokeThickness=1 },Center-ring,Center-ring);
            Put(canvas,new Ellipse { Width=orbit*2,Height=orbit*2,Stroke=Ui.Brush("#35404C"),StrokeThickness=1 },Center-orbit,Center-orbit);
            for(int i=0;i<choices.Count;i++) {
                Point p=Position(i,choices.Count); bool active=i==selected;
                double size=ButtonSize(choices.Count,buttonSize); Button bubble=RingSurface.Bubble(choices[i].Action(),i,size,appearance.TextOnly); bubble.Background=active?appearance.Selected:Ui.Panel; bubble.BorderBrush=active?appearance.Accent:Ui.Line; bubble.ToolTip=choices[i].Label; Put(canvas,bubble,p.X-size/2,p.Y-size/2);
                Button label=EditorUi.Button(choices[i].Label,delegate { },false); label.Margin=new Thickness(0); label.MaxWidth=146; label.FontSize=12/scale; label.Foreground=active?appearance.Accent:Ui.Ink; label.Padding=new Thickness(10/scale,7/scale,10/scale,7/scale);
                label.Content=new TextBlock { Text=choices[i].Label,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,MaxWidth=124,FontSize=12/scale };
                label.Measure(new Size(146,Double.PositiveInfinity)); Size measured=new Size(Math.Min(146,Math.Ceiling(label.DesiredSize.Width)+2),Math.Ceiling(label.DesiredSize.Height)+1); Rect bounds=LabelBounds(i,choices.Count,measured); labels[i]=bounds; label.Width=bounds.Width; label.Height=bounds.Height; label.Visibility=!ActionIcons.UsesText(choices[i].Action(),appearance.TextOnly) && (editing || active)?Visibility.Visible:Visibility.Collapsed; Put(canvas,label,bounds.X,bounds.Y);
            }
            return canvas;
        }
        internal static void Put(Canvas canvas,FrameworkElement item,double x,double y) { item.IsHitTestVisible=false; Canvas.SetLeft(item,x); Canvas.SetTop(item,y); canvas.Children.Add(item); }
    }
    sealed class ConversionSession {
        readonly Preferences config;
        readonly string initial;
        readonly Func<DropCapture,DropOffer,ConversionChoice,bool> accept;
        readonly Action<ConversionChoice> pickFiles;
        readonly List<ConversionPlanetWindow> windows=new List<ConversionPlanetWindow>();
        readonly DispatcherTimer timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(50) };
        readonly DateTime started=DateTime.UtcNow;
        readonly Action notDragging;
        List<ConversionChoice> choices;
        DropOffer offer;
        bool seenDrag,cancelled,committed,closed,pressed;
        DateTime? released;
        public event Action Closed;
        public ConversionSession(Preferences preferences,Native.POINT point,bool probe,string operation,Func<DropCapture,DropOffer,ConversionChoice,bool> dropped,Action<ConversionChoice> pick,Action fallback) {
            config=preferences; initial=operation??""; accept=dropped; pickFiles=pick; notDragging=probe?fallback:null; pressed=(Native.GetAsyncKeyState(1)&0x8000)!=0;
            try { foreach(System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens) { System.Drawing.Rectangle bounds=screen.Bounds; windows.Add(new ConversionPlanetWindow(this,config,point,bounds,bounds.Contains(point.X,point.Y))); } }
            catch { Close(); throw; }
            if(!probe) Display(initial.StartsWith("pdf-",StringComparison.Ordinal),null);
            timer.Tick+=Tick; timer.Start();
        }
        void Tick(object sender,EventArgs e) {
            if((Native.GetAsyncKeyState(27)&0x8000)!=0) Cancel();
            bool down=(Native.GetAsyncKeyState(1)&0x8000)!=0;
            if(down) { pressed=true; released=null; }
            else if(pressed && !committed) { if(!released.HasValue) released=DateTime.UtcNow; else if((DateTime.UtcNow-released.Value).TotalMilliseconds>400) { Close(); return; } }
            if(notDragging!=null && !seenDrag && (DateTime.UtcNow-started).TotalMilliseconds>450) { Close(); if(!cancelled) notDragging(); }
        }
        void Display(bool pdf,string error) {
            choices=config.Conversion.Choices(pdf,initial);
            if(offer!=null) choices=choices.Where(c=>ConversionCatalog.Supports(c.Operation,pdf,offer.Count)).ToList();
            foreach(ConversionPlanetWindow window in windows) window.Draw(choices,error??(choices.Count==0?"この形式には変換できません。":null),pdf);
        }
        internal void Over(ConversionPlanetWindow window,DragEventArgs e) {
            e.Handled=true; e.Effects=DragDropEffects.None; seenDrag=true; pressed=true;
            if(cancelled || committed) return;
            if(offer==null) { offer=DropFiles.Inspect(e.Data); Display(offer.Pdf,offer.Kind==DropKind.None?offer.Reason:null); }
            int index=offer.Kind!=DropKind.None && DropFiles.CanCopy(e.AllowedEffects)?window.Hit(e.GetPosition(window.Surface)):-1;
            window.Select(index); if(index>=0) e.Effects=DragDropEffects.Copy;
        }
        internal void Drop(ConversionPlanetWindow window,DragEventArgs e) {
            e.Handled=true; e.Effects=DragDropEffects.None;
            if(cancelled || committed) { Close(); return; }
            int index=window.Hit(e.GetPosition(window.Surface));
            if(index<0 || offer==null || offer.Kind==DropKind.None || !DropFiles.CanCopy(e.AllowedEffects)) { Close(); return; }
            committed=true; DropCapture capture=null;
            try { capture=new DropCapture(e.Data,offer); if(accept(capture,offer,choices[index])) { capture=null; e.Effects=DragDropEffects.Copy; } }
            catch(Exception ex) { OrbitDialog.Info(null,"ファイルを受け取れませんでした",ex.Message); }
            finally { if(capture!=null) capture.Dispose(); Close(); }
        }
        internal void Click(ConversionPlanetWindow window,Point position) {
            if(seenDrag || cancelled || committed) return; int index=window.Hit(position); ConversionChoice choice=index>=0?choices[index]:null; Close(); if(choice!=null) pickFiles(choice);
        }
        public void Cancel() { if(closed) return; cancelled=true; foreach(ConversionPlanetWindow window in windows) window.Conceal(); if(!pressed || (Native.GetAsyncKeyState(1)&0x8000)==0) Close(); }
        public void Close() { if(closed) return; closed=true; timer.Stop(); foreach(ConversionPlanetWindow window in windows) window.Close(); windows.Clear(); offer=null; choices=null; if(Closed!=null) Closed(); }
    }
    sealed class ConversionPlanetWindow : Window {
        readonly ConversionSession session;
        readonly AppearancePreferences appearance;
        readonly double scale,buttonSize;
        readonly Canvas area=new Canvas { Background=new SolidColorBrush(Color.FromArgb(1,0,0,0)),AllowDrop=true };
        readonly bool main;
        readonly Point origin;
        TextBlock heading,detail;
        IList<ConversionChoice> choices;
        int selected=-2;
        bool pdf,invalid;
        public Canvas Surface { get; private set; }
        public ConversionPlanetWindow(ConversionSession owner,Preferences config,Native.POINT point,System.Drawing.Rectangle bounds,bool primary) {
            session=owner; main=primary; appearance=config.Appearance; scale=config.RingScale; buttonSize=config.ButtonSize;
            Title="orbitals — ファイル変換"; Icon=AppIcon.Image; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true; Background=Brushes.Transparent;
            ShowActivated=false; ShowInTaskbar=false; Topmost=true; Content=area;
            IntPtr monitor=Native.MonitorFromPoint(new Native.POINT { X=bounds.X+1,Y=bounds.Y+1 },2);
            uint dx=96,dy=96; try { Native.GetDpiForMonitor(monitor,0,out dx,out dy); } catch { } double dpi=Math.Max(1,dx/96.0);
            Width=bounds.Width/dpi; Height=bounds.Height/dpi; Left=bounds.Left/dpi; Top=bounds.Top/dpi; origin=new Point((point.X-bounds.Left)/dpi-ConversionPlanets.Center*scale,(point.Y-bounds.Top)/dpi-ConversionPlanets.Center*scale);
            Surface=new Canvas { Width=ConversionPlanets.Size,Height=ConversionPlanets.Size };
            area.PreviewDragEnter+=delegate(object s,DragEventArgs e) { session.Over(this,e); }; area.PreviewDragOver+=delegate(object s,DragEventArgs e) { session.Over(this,e); };
            area.PreviewDragLeave+=delegate(object s,DragEventArgs e) { Select(-1); e.Handled=true; }; area.PreviewDrop+=delegate(object s,DragEventArgs e) { session.Drop(this,e); };
            area.MouseLeftButtonUp+=delegate(object s,System.Windows.Input.MouseButtonEventArgs e) { session.Click(this,e.GetPosition(Surface)); }; area.MouseRightButtonUp+=delegate { session.Cancel(); };
            Closed+=delegate { area.Children.Clear(); Content=null; if(Application.Current!=null && Application.Current.MainWindow==this) Application.Current.MainWindow=null; };
            IntPtr handle=new WindowInteropHelper(this).EnsureHandle(); Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x08000000|0x80);
            HwndSource.FromHwnd(handle).AddHook(delegate(IntPtr h,int message,IntPtr w,IntPtr l,ref bool handled) { if(message==0x21) { handled=true; return new IntPtr(3); } return IntPtr.Zero; });
            Native.SetWindowPos(handle,new IntPtr(-1),bounds.X,bounds.Y,bounds.Width,bounds.Height,0x10); Show();
        }
        public int Hit(Point position) { return !main || invalid || choices==null?-1:ConversionPlanets.Hit(position,Surface); }
        public void Draw(IList<ConversionChoice> entries,string error,bool sourcePdf) {
            choices=entries; pdf=sourcePdf; invalid=error!=null; selected=-2; area.Children.Clear(); if(!main) return;
            Surface=ConversionPlanets.Drawing(invalid?new List<ConversionChoice>():entries,appearance,scale,-1,false,buttonSize); Surface.Opacity=appearance.RingOpacity; Surface.LayoutTransform=new ScaleTransform(scale,scale); Canvas.SetLeft(Surface,origin.X); Canvas.SetTop(Surface,origin.Y); area.Children.Add(Surface);
            ConversionPlanets.Put(Surface,RingSurface.CenterBody(appearance,out heading,out detail),ConversionPlanets.Center-73,ConversionPlanets.Center-73);
            Select(-1); if(invalid) { heading.Text="変換できません"; detail.Text=error; }
        }
        public void Select(int index) {
            if(!main || choices==null || invalid || index==selected) return;
            if(selected>=0) { Control previous=Surface.Children[selected*2+2] as Control; if(previous!=null) { previous.Background=Ui.Panel; previous.BorderBrush=Ui.Line; } Surface.Children[selected*2+3].Visibility=Visibility.Collapsed; }
            selected=index;
            if(index>=0) { Control current=Surface.Children[index*2+2] as Control; if(current!=null) { current.Background=appearance.Selected; current.BorderBrush=appearance.Accent; } Surface.Children[index*2+3].Visibility=ActionIcons.UsesText(choices[index].Action(),appearance.TextOnly)?Visibility.Collapsed:Visibility.Visible; }
            heading.Text=index<0?(pdf?"PDFを変換":"画像を変換"):choices[index].Label; detail.Text=index<0?"形式の上で離す":"離すと変換・保存";
        }
        public void Conceal() { area.Children.Clear(); choices=null; }
    }
}

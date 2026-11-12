using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace OrbitRing {
    sealed class GestureDrawing : FrameworkElement {
        readonly List<Point> raw=new List<Point>();
        readonly DispatcherTimer animation;
        readonly Stopwatch elapsed=new Stopwatch();
        Point[] source,projected;
        StreamGeometry geometry;
        Size projectedSize;
        bool projectedEditorView,drawing;
        Window host;
        UIElement hoverTarget;
        AppearancePreferences appearance=new AppearancePreferences();
        public void SetAppearance(AppearancePreferences value) { appearance=value; InvalidateVisual(); }
        public Action<List<GesturePoint>> Recorded;
        public Point[] LastStroke { get; private set; }
        public bool EditorView;
        public bool DrawingEnabled;
        public string Caption;
        public string EmptyHint;
        string specialPattern;
        public string TriggerButton="Right";
        public GestureDrawing() {
            ClipToBounds=true; Focusable=true; SetAppearance(appearance);
            animation=new DispatcherTimer(DispatcherPriority.Render) { Interval=TimeSpan.FromMilliseconds(33) };
            animation.Tick+=delegate { InvalidateVisual(); };
            Loaded+=delegate { host=Window.GetWindow(this); if(host!=null) { host.Activated+=HostChanged; host.Deactivated+=HostChanged; host.StateChanged+=HostChanged; } UpdateAnimation(); };
            Unloaded+=delegate { animation.Stop(); elapsed.Reset(); if(host!=null) { host.Activated-=HostChanged; host.Deactivated-=HostChanged; host.StateChanged-=HostChanged; host=null; } };
            IsVisibleChanged+=delegate { UpdateAnimation(); };
            MouseEnter+=delegate { UpdateAnimation(); }; MouseLeave+=delegate { UpdateAnimation(); };
        }
        public void AnimateOnHover(UIElement target) {
            hoverTarget=target;
            target.MouseEnter+=delegate { UpdateAnimation(); }; target.MouseLeave+=delegate { UpdateAnimation(); };
        }
        void HostChanged(object sender,EventArgs e) { UpdateAnimation(); }
        void UpdateAnimation() {
            bool run=GesturePresentation.Animate(source!=null && source.Length>1,(hoverTarget??this).IsMouseOver,drawing,IsLoaded && IsVisible,host!=null && host.IsActive && host.WindowState!=WindowState.Minimized,SystemParameters.ClientAreaAnimation);
            if(run && !animation.IsEnabled) { elapsed.Restart(); animation.Start(); }
            else if(!run && animation.IsEnabled) { animation.Stop(); elapsed.Reset(); }
            InvalidateVisual();
        }
        void SetShape(List<GesturePoint> shape) {
            source=shape==null?null:shape.Select(p=>new Point(p.X,p.Y)).ToArray();
            projected=null; geometry=null; animation.Stop(); elapsed.Reset(); UpdateAnimation();
        }
        public void Bind(OrbitBinding binding) {
            specialPattern=binding==null?null:binding.Pattern; Caption=GestureInput.Caption(specialPattern,TriggerButton);
            SetShape(binding==null?null:GestureShape.Template(binding));
        }
        static StreamGeometry PathGeometry(IList<Point> path) {
            StreamGeometry result=new StreamGeometry();
            using(StreamGeometryContext context=result.Open()) { context.BeginFigure(path[0],false,false); context.PolyLineTo(path.Skip(1).ToArray(),true,false); }
            result.Freeze(); return result;
        }
        void Project() {
            if(projected!=null && projectedSize==RenderSize && projectedEditorView==EditorView) return;
            projected=GesturePresentation.Project(source,RenderSize,EditorView); projectedSize=RenderSize; projectedEditorView=EditorView;
            geometry=projected.Length>1?PathGeometry(projected):null;
        }
        void DrawArrow(DrawingContext dc,double progress) {
            Point[] points=GesturePresentation.Arrow(projected,progress,GesturePresentation.ArrowSize(appearance.TrailWidth,EditorView));
            StreamGeometry arrow=new StreamGeometry(); using(StreamGeometryContext context=arrow.Open()) { context.BeginFigure(points[0],true,true); context.PolyLineTo(points.Skip(1).ToArray(),true,false); } arrow.Freeze(); dc.DrawGeometry(EditorUi.Ink,null,arrow);
        }
        void DrawMotion(DrawingContext dc) {
            double seconds=elapsed.Elapsed.TotalSeconds,opacity=GesturePresentation.MotionOpacity(seconds,projected,!EditorView);
            if(opacity<=0) return;
            dc.PushOpacity(opacity);
            double progress=GesturePresentation.Progress(seconds),span=GesturePresentation.TrailSpan(seconds,projected,!EditorView);
            Point[] trail=GesturePresentation.Trail(projected,progress,span,GesturePresentation.StrokeWidth(appearance.TrailWidth,EditorView)*1.5);
            GestureTrail.Draw(dc,trail,GesturePresentation.StrokeWidth(appearance.TrailWidth,EditorView),appearance.Trail);
            DrawArrow(dc,progress);
            dc.Pop();
        }
        void DrawMouseOperation(DrawingContext dc) {
            double scale=EditorView?1.6:.75,w=18*scale,h=28*scale;
            FormattedText label=null;
            if(EditorView) { label=new FormattedText(Caption,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Yu Gothic UI"),13,EditorUi.Ink,VisualTreeHelper.GetDpi(this).PixelsPerDip); label.MaxTextWidth=Math.Max(1,ActualWidth-12); label.TextAlignment=TextAlignment.Center; label.LineHeight=19; }
            double total=h+(EditorView?label.Height+38:0),y=Math.Max(2,(ActualHeight-total)/2),x=(ActualWidth-w-(specialPattern=="Rocker"?0:17*scale))/2;
            Rect mouse=new Rect(x,y,w,h); Pen outline=new Pen(EditorUi.Muted,EditorView?1.8:1.3),accent=new Pen(appearance.Trail,EditorView?2:1.5);
            dc.DrawRoundedRectangle(null,outline,mouse,7*scale,7*scale);
            if(specialPattern=="Rocker") {
                dc.PushClip(new RectangleGeometry(mouse,7*scale,7*scale)); dc.DrawRectangle(appearance.Trail,null,new Rect(x,y,w/2,h*.36)); dc.Pop();
                if(TriggerButton=="Right") dc.DrawLine(accent,new Point(x+w*.75,y+3*scale),new Point(x+w*.75,y+8*scale));
                else if(TriggerButton=="X1" || TriggerButton=="X2") dc.DrawLine(accent,new Point(x-3*scale,y+10*scale),new Point(x-3*scale,y+16*scale));
            }
            dc.DrawLine(outline,new Point(x,y+h*.36),new Point(x+w,y+h*.36)); dc.DrawLine(outline,new Point(x+w/2,y+1*scale),new Point(x+w/2,y+h*.36));
            dc.DrawLine(accent,new Point(x+w/2,y+4*scale),new Point(x+w/2,y+8*scale));
            if(specialPattern=="WheelUp" || specialPattern=="WheelDown") {
                double arrowX=x+w+11*scale,tip=specialPattern=="WheelUp"?y+4*scale:y+h-4*scale,direction=specialPattern=="WheelUp"?1:-1;
                dc.DrawLine(accent,new Point(arrowX,tip+direction*15*scale),new Point(arrowX,tip+direction*2*scale));
                StreamGeometry arrow=new StreamGeometry(); using(StreamGeometryContext g=arrow.Open()) { g.BeginFigure(new Point(arrowX,tip),true,true); g.LineTo(new Point(arrowX-4*scale,tip+direction*5*scale),true,false); g.LineTo(new Point(arrowX+4*scale,tip+direction*5*scale),true,false); } dc.DrawGeometry(appearance.Trail,null,arrow);
            }
            if(EditorView) {
                dc.DrawText(label,new Point(6,y+h+10));
                FormattedText help=new FormattedText("設定画面の外で操作します",System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Yu Gothic UI"),11,EditorUi.Muted,VisualTreeHelper.GetDpi(this).PixelsPerDip); dc.DrawText(help,new Point((ActualWidth-help.Width)/2,y+h+label.Height+20));
            }
        }
        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc); dc.DrawRectangle(Brushes.Transparent,null,new Rect(RenderSize));
            IList<Point> path=drawing?(IList<Point>)raw:source;
            if(path!=null && path.Count>1) {
                StreamGeometry trace;
                if(drawing) trace=PathGeometry(path);
                else { Project(); path=projected; trace=geometry; }
                Pen pen=new Pen(appearance.Trail,GesturePresentation.StrokeWidth(appearance.TrailWidth,EditorView)) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
                if(drawing || !animation.IsEnabled) dc.DrawGeometry(null,pen,trace);
                double radius=EditorView?4:2.4; dc.DrawEllipse(appearance.Trail,new Pen(EditorUi.Bg,EditorView?1.5:1),path[0],radius,radius);
                if(!drawing && animation.IsEnabled) DrawMotion(dc);
            } else if(!drawing) {
                if(!String.IsNullOrEmpty(Caption)) { DrawMouseOperation(dc); return; }
                string text=EditorView?(EmptyHint??(DrawingEnabled?"左ドラッグで形を登録":"「編集」を押して形を登録")):"＋";
                FormattedText label=new FormattedText(text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI, Yu Gothic UI"),EditorView?13:11,EditorUi.Muted,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                label.MaxTextWidth=Math.Max(1,ActualWidth-12); dc.DrawText(label,new Point(Math.Max(6,(ActualWidth-label.Width)/2),Math.Max(4,(ActualHeight-label.Height)/2)));
            }
        }
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { if(!EditorView || !DrawingEnabled) return; Focus(); drawing=true; UpdateAnimation(); raw.Clear(); raw.Add(e.GetPosition(this)); CaptureMouse(); InvalidateVisual(); e.Handled=true; }
        protected override void OnMouseMove(MouseEventArgs e) { if(!drawing || e.LeftButton!=MouseButtonState.Pressed) return; GestureShape.Append(raw,e.GetPosition(this)); InvalidateVisual(); }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            if(!drawing) return; GestureShape.Append(raw,e.GetPosition(this)); drawing=false; ReleaseMouseCapture();
            if(GestureShape.Extent(raw)>=20) { LastStroke=raw.ToArray(); List<GesturePoint> shape=GestureShape.Normalize(raw); Caption=""; SetShape(shape); if(shape!=null && Recorded!=null) Recorded(shape); }
            else UpdateAnimation(); InvalidateVisual(); e.Handled=true;
        }
        protected override void OnLostMouseCapture(MouseEventArgs e) { drawing=false; UpdateAnimation(); base.OnLostMouseCapture(e); }
    }
}

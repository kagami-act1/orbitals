using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OrbitRing {
    sealed class PlanetReveal : FrameworkElement {
        public const double DurationMilliseconds=420;
        readonly DrawingGroup trails;
        Canvas host;
        RingSurface surface;
        TimeSpan? firstFrame;
        double progress;
        bool running;
        public bool Running { get { return running; } }
        public double Progress { get { return progress; } }
        public PlanetReveal(Brush color) {
            Width=410; Height=410; IsHitTestVisible=false; Focusable=false;
            SolidColorBrush solid=color as SolidColorBrush; Color tone=solid==null?Color.FromRgb(147,233,193):solid.Color;
            trails=new DrawingGroup();
            using(DrawingContext drawing=trails.Open()) {
                AddTrail(drawing,tone,92,-144,44,1.3,.78);
                AddTrail(drawing,tone,113,8,58,1.5,.85);
                AddTrail(drawing,tone,134,-84,68,1.4,.8);
                AddTrail(drawing,tone,155,128,52,1.5,.9);
                AddTrail(drawing,tone,176,-155,76,1.6,.9);
                AddTrail(drawing,tone,197,12,64,1.5,.86);
                AddTrail(drawing,tone,113,181,22,1.1,.45);
                AddTrail(drawing,tone,155,-16,28,1.1,.48);
                AddTrail(drawing,tone,197,164,32,1.1,.5);
            }
            trails.Freeze(); Unloaded+=delegate { Cancel(); };
        }
        static Point Position(double radius,double angle) { double radians=angle*Math.PI/180; return new Point(205+radius*Math.Cos(radians),205+radius*Math.Sin(radians)); }
        static void AddTrail(DrawingContext drawing,Color color,double radius,double angle,double length,double width,double opacity) {
            Point from=Position(radius,angle),to=Position(radius,angle+length);
            StreamGeometry arc=new StreamGeometry();
            using(StreamGeometryContext path=arc.Open()) { path.BeginFigure(from,false,false); path.ArcTo(to,new Size(radius,radius),0,false,SweepDirection.Clockwise,true,false); } arc.Freeze();
            LinearGradientBrush light=new LinearGradientBrush { MappingMode=BrushMappingMode.Absolute,StartPoint=from,EndPoint=to };
            light.GradientStops.Add(new GradientStop(Color.FromArgb(0,color.R,color.G,color.B),0));
            light.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(75*opacity),color.R,color.G,color.B),.25));
            light.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(210*opacity),color.R,color.G,color.B),.8));
            Color tip=Color.FromArgb((byte)(255*opacity),(byte)(color.R*.8+255*.2),(byte)(color.G*.8+255*.2),(byte)(color.B*.8+255*.2));
            light.GradientStops.Add(new GradientStop(tip,1)); light.Freeze();
            Pen pen=new Pen(light,width) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round }; pen.Freeze(); drawing.DrawGeometry(null,pen,arc);
            SolidColorBrush head=new SolidColorBrush(tip); head.Freeze(); drawing.DrawEllipse(head,null,to,width*.65,width*.65);
        }
        protected override void OnRender(DrawingContext drawing) {
            if(!running) return;
            double opacity=.84*Math.Min(1,progress/.15)*Math.Min(1,(1-progress)/.45);
            drawing.PushOpacity(opacity);
            drawing.PushTransform(new RotateTransform(56*progress,205,205));
            drawing.DrawDrawing(trails);
            drawing.Pop(); drawing.Pop();
        }
        void Frame(object sender,EventArgs e) { Advance(((RenderingEventArgs)e).RenderingTime); }
        internal void Advance(TimeSpan time) {
            if(!running) return;
            // Rendering time starts when the first frame is ready, so opening a
            // cold window cannot consume the animation before it is presented.
            if(!firstFrame.HasValue) firstFrame=time;
            progress=Math.Max(0,(time-firstFrame.Value).TotalMilliseconds/DurationMilliseconds);
            if(surface!=null) surface.SetOpeningProgress(progress);
            if(progress>=1) Cancel(); else InvalidateVisual();
        }
        public void Cancel() {
            if(!running) return; running=false; CompositionTarget.Rendering-=Frame;
            if(surface!=null) { surface.SetOpeningProgress(1); surface=null; }
            Canvas previous=host; host=null; if(previous!=null) previous.Children.Remove(this);
        }
        public static PlanetReveal Play(Canvas parent,Point offset,double scale,Brush accent,RingSurface body=null) {
            foreach(PlanetReveal previous in System.Linq.Enumerable.ToArray(System.Linq.Enumerable.OfType<PlanetReveal>(parent.Children))) previous.Cancel();
            PlanetReveal reveal=new PlanetReveal(accent) { LayoutTransform=new ScaleTransform(scale,scale),host=parent,surface=body,running=true };
            if(body!=null) body.SetOpeningProgress(0);
            Canvas.SetLeft(reveal,offset.X); Canvas.SetTop(reveal,offset.Y); Canvas.SetZIndex(reveal,20); parent.Children.Add(reveal);
            CompositionTarget.Rendering+=reveal.Frame; return reveal;
        }
    }
}

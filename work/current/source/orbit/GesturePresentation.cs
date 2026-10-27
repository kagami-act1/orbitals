using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class GesturePresentation {
        const double StartDelay=.15,TravelTime=1.5,RepeatTime=3.6,DefaultSpan=.5,FadeTime=.25;
        static double Phase(double seconds) { return Math.Max(0,seconds)%RepeatTime; }
        static double Length(IList<Point> path) {
            double length=0; if(path!=null) for(int i=1;i<path.Count;i++) length+=(path[i]-path[i-1]).Length;
            return length;
        }
        public static Point[] Project(IList<Point> source,Size bounds,bool editorView) {
            Point[] path=source==null?new Point[0]:source.ToArray();
            if(path.Length<2) return path;
            double minX=path.Min(p=>p.X),minY=path.Min(p=>p.Y),width=path.Max(p=>p.X)-minX,height=path.Max(p=>p.Y)-minY;
            double padding=editorView?30:8,scale=Math.Max(0,Math.Min(bounds.Width-padding,bounds.Height-padding))/Math.Max(.001,Math.Max(width,height));
            return path.Select(p=>new Point((bounds.Width-width*scale)/2+(p.X-minX)*scale,(bounds.Height-height*scale)/2+(p.Y-minY)*scale)).ToArray();
        }
        public static bool Animate(bool hasPath,bool hovered,bool drawing,bool visible,bool active,bool motionEnabled) { return hasPath && hovered && !drawing && visible && active && motionEnabled; }
        public static double Progress(double seconds) {
            return Math.Max(0,Math.Min(1,(Phase(seconds)-StartDelay)/TravelTime));
        }
        static double Span(double seconds,double limit) {
            return Math.Max(0,limit-Math.Max(0,Phase(seconds)-StartDelay-TravelTime)/TravelTime);
        }
        static double SpanLimit(IList<Point> path,bool compact) {
            return path==null || path.Count<2?DefaultSpan:Math.Min(1,Math.Max(DefaultSpan,(compact?24:48)/Math.Max(.001,Length(path))));
        }
        public static double MotionOpacity(double seconds,IList<Point> path,bool compact) {
            double end=StartDelay+TravelTime*(1+SpanLimit(path,compact));
            return Math.Max(0,Math.Min(1,(end-Phase(seconds))/FadeTime));
        }
        public static double TrailSpan(double seconds,IList<Point> path,bool compact) {
            return Span(seconds,SpanLimit(path,compact));
        }
        public static double StrokeWidth(double width,bool editorView) { return editorView?width:Math.Max(2.2,Math.Min(3.2,width*.6)); }
        public static double ArrowSize(double width,bool editorView) { return editorView?Math.Max(11,width*4):Math.Max(6.5,Math.Min(7,StrokeWidth(width,false)*3)); }
        public static Point[] Arrow(IList<Point> path,double progress,double size) {
            Vector direction; Point tip=Playhead(path,progress,out direction);
            size=Math.Min(Math.Max(0,size),Length(path)*Math.Max(0,Math.Min(1,progress)));
            // Use the outgoing tangent at a corner.
            Vector ahead; Playhead(path,Math.Min(1,progress+.0001),out ahead); if(ahead.LengthSquared>.5) direction=ahead;
            Vector normal=new Vector(-direction.Y,direction.X);
            return new[]{tip,tip-direction*size+normal*size*.4,tip-direction*size-normal*size*.4};
        }
        public static Point Playhead(IList<Point> path,double progress,out Vector direction) {
            direction=new Vector(); if(path==null || path.Count==0) return new Point();
            double length=Length(path);
            double target=length*Math.Max(0,Math.Min(1,progress)),walked=0;
            for(int i=1;i<path.Count;i++) {
                Vector step=path[i]-path[i-1]; double distance=step.Length; if(distance<.000001) continue;
                direction=step/distance;
                if(walked+distance>=target || i==path.Count-1) return path[i-1]+step*Math.Max(0,Math.Min(1,(target-walked)/distance));
                walked+=distance;
            }
            return path[path.Count-1];
        }
        // Clip by arc length, retaining vertices at corners and reversals.
        public static Point[] Trail(IList<Point> path,double progress,double span,double clearance=0) {
            if(path==null || path.Count<2 || span<=0) return new Point[0];
            double end=Math.Max(0,Math.Min(1,progress)),start=Math.Max(0,end-span),total=Length(path);
            if(total>.000001) end=Math.Max(0,end-Math.Max(0,clearance)/total);
            if(total<.000001 || end<=start) return new Point[0];
            Vector unused; List<Point> points=new List<Point> { Playhead(path,start,out unused) }; double walked=0;
            for(int i=1;i<path.Count;i++) { walked+=(path[i]-path[i-1]).Length; if(walked>start*total && walked<end*total) points.Add(path[i]); }
            points.Add(Playhead(path,end,out unused)); return points.ToArray();
        }
    }
}

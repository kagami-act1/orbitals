using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace OrbitRing {
    static class GestureTrail {
        static Color Shade(Color color,double at) { color.A=(byte)Math.Round(color.A*Math.Pow(Math.Max(0,Math.Min(1,at)),1.4)); return color; }
        static void Band(DrawingContext drawing,Point a,Point b,Vector normalA,Vector normalB,double from,double to,double width,Color color) {
            double halfA=width*(.1+.9*from)/2,halfB=width*(.1+.9*to)/2;
            StreamGeometry ribbon=new StreamGeometry();
            using(StreamGeometryContext shape=ribbon.Open()) { shape.BeginFigure(a+normalA*halfA,true,true); shape.LineTo(b+normalB*halfB,true,false); shape.LineTo(b-normalB*halfB,true,false); shape.LineTo(a-normalA*halfA,true,false); }
            LinearGradientBrush gradient=new LinearGradientBrush { MappingMode=BrushMappingMode.Absolute,StartPoint=a,EndPoint=b };
            for(int i=0;i<=4;i++) gradient.GradientStops.Add(new GradientStop(Shade(color,from+(to-from)*i/4.0),i/4.0));
            gradient.Freeze(); ribbon.Freeze(); drawing.DrawGeometry(gradient,null,ribbon);
        }
        public static void Draw(DrawingContext drawing,IList<Point> points,double width,Brush brush) {
            if(points.Count<2) return;
            double total=0; for(int i=1;i<points.Count;i++) total+=(points[i]-points[i-1]).Length; if(total<.001) return;
            Color color=brush is SolidColorBrush?((SolidColorBrush)brush).Color:Colors.Aquamarine;
            Vector direct=points[points.Count-1]-points[0];
            if(direct.Length/total>.9999) { direct.Normalize(); Vector normal=new Vector(-direct.Y,direct.X); Band(drawing,points[0],points[points.Count-1],normal,normal,0,1,width,color); return; }
            Vector[] normals=new Vector[points.Count];
            for(int i=0;i<points.Count;i++) {
                Vector incoming=i==0?points[1]-points[0]:points[i]-points[i-1],outgoing=i==points.Count-1?incoming:points[i+1]-points[i];
                if(incoming.Length>.001) incoming.Normalize(); if(outgoing.Length>.001) outgoing.Normalize();
                Vector tangent=incoming+outgoing; if(tangent.Length<.001) tangent=outgoing; if(tangent.Length>.001) tangent.Normalize(); normals[i]=new Vector(-tangent.Y,tangent.X);
            }
            double walked=0;
            for(int i=1;i<points.Count;i++) { double length=(points[i]-points[i-1]).Length; if(length<.001) continue; Band(drawing,points[i-1],points[i],normals[i-1],normals[i],walked/total,(walked+length)/total,width,color); walked+=length; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    // Correct only recorded templates; retain live input and original drawings.
    static class GestureCleanup {
        const double MaxError=.09;
        static double DistanceToLine(Point p,Point a,Point b) {
            Vector delta=b-a;
            if(delta.LengthSquared<.00000001) return (p-a).Length;
            double t=Math.Max(0,Math.Min(1,Vector.Multiply(p-a,delta)/delta.LengthSquared));
            return (p-(a+delta*t)).Length;
        }
        static void Simplify(Point[] path,int first,int last,List<int> vertices) {
            double farthest=.012; int split=-1;
            for(int i=first+1;i<last;i++) { double distance=DistanceToLine(path[i],path[first],path[last]); if(distance>farthest) { farthest=distance; split=i; } }
            if(split>=0) { Simplify(path,first,split,vertices); Simplify(path,split,last,vertices); }
            else vertices.Add(last);
        }
        // Pivoted least squares. Local coordinates keep shallow arcs stable.
        static double[] Fit(double[] t,double[] values,int degree) {
            int size=degree+1; double[,] matrix=new double[size,size+1];
            for(int i=0;i<t.Length;i++) {
                double[] powers=new double[size*2]; powers[0]=1;
                for(int p=1;p<powers.Length;p++) powers[p]=powers[p-1]*t[i];
                for(int row=0;row<size;row++) { for(int column=0;column<size;column++) matrix[row,column]+=powers[row+column]; matrix[row,size]+=values[i]*powers[row]; }
            }
            return Solve(matrix);
        }
        static double[] Solve(double[,] matrix) {
            int size=matrix.GetLength(0);
            for(int column=0;column<size;column++) {
                int pivot=column;
                for(int row=column+1;row<size;row++) if(Math.Abs(matrix[row,column])>Math.Abs(matrix[pivot,column])) pivot=row;
                if(Math.Abs(matrix[pivot,column])<1e-10) return null;
                for(int j=column;j<=size;j++) { double temp=matrix[column,j]; matrix[column,j]=matrix[pivot,j]; matrix[pivot,j]=temp; }
                double divisor=matrix[column,column]; for(int j=column;j<=size;j++) matrix[column,j]/=divisor;
                for(int row=0;row<size;row++) if(row!=column) { double factor=matrix[row,column]; for(int j=column;j<=size;j++) matrix[row,j]-=factor*matrix[column,j]; }
            }
            double[] result=new double[size]; for(int row=0;row<size;row++) result[row]=matrix[row,size]; return result;
        }
        static Point[] Smooth(Point[] path,int radius) {
            Point[] result=(Point[])path.Clone();
            if(path.Length<5) return result;
            for(int i=1;i<path.Length-1;i++) {
                int count=Math.Min(radius*2+1,path.Length),start=Math.Max(0,Math.Min(i-radius,path.Length-count));
                double[] t=new double[count],x=new double[count],y=new double[count];
                for(int j=0;j<count;j++) { t[j]=(start+j-i)/(double)radius; x[j]=path[start+j].X; y[j]=path[start+j].Y; }
                double[] fx=Fit(t,x,2),fy=Fit(t,y,2);
                if(fx!=null && fy!=null) result[i]=new Point(fx[0],fy[0]);
            }
            return result;
        }
        static bool FitLine(Point[] path,bool align,out Point[] result) {
            result=null; Vector chord=path[path.Length-1]-path[0]; double length=chord.Length;
            if(length<.04) return false;
            Vector axis=chord/length,normal=new Vector(-axis.Y,axis.X);
            double backwards=0; for(int i=1;i<path.Length;i++) backwards+=Math.Max(0,-Vector.Multiply(path[i]-path[i-1],axis));
            if(backwards>length*.15) return false;
            double[] offsets=path.Select(p=>Vector.Multiply(p-path[0],normal)).ToArray();
            if(offsets.Max(v=>Math.Abs(v))>.045) return false;
            // Coherent bows and S bends remain curves, including shallow ones.
            if(path.Length>=6) {
                double[] t=Enumerable.Range(0,path.Length).Select(i=>2.0*i/(path.Length-1)-1).ToArray();
                double[] line=Fit(t,offsets,1),curve=Fit(t,offsets,3); double lineError=0,curveError=0;
                for(int i=0;i<path.Length;i++) {
                    double a=offsets[i]-line[0]-line[1]*t[i],b=offsets[i]-curve[0]-curve[1]*t[i]-curve[2]*t[i]*t[i]-curve[3]*t[i]*t[i]*t[i]; lineError+=a*a; curveError+=b*b;
                }
                if(lineError>path.Length*.003*.003 && curveError<lineError*.4) return false;
            }
            Point aPoint=path[0],bPoint=path[path.Length-1];
            if(align) {
                double angle=Math.Atan2(axis.Y,axis.X),snapped=Math.Round(angle/(Math.PI/4))*(Math.PI/4);
                if(Math.Abs(angle-snapped)<Math.PI/22.5) {
                    Vector direction=new Vector(Math.Cos(snapped),Math.Sin(snapped)); Point center=new Point(path.Average(p=>p.X),path.Average(p=>p.Y));
                    aPoint=center+direction*Vector.Multiply(path[0]-center,direction); bPoint=center+direction*Vector.Multiply(path[path.Length-1]-center,direction);
                }
            }
            result=new[]{aPoint,bPoint}; return true;
        }
        static bool FitArc(Point[] path,out Point[] result) {
            result=null; if(path.Length<8) return false;
            Point mean=new Point(path.Average(p=>p.X),path.Average(p=>p.Y)); double[,] matrix=new double[3,4];
            foreach(Point point in path) {
                double x=point.X-mean.X,y=point.Y-mean.Y; double[] row={x,y,1};
                for(int i=0;i<3;i++) { for(int j=0;j<3;j++) matrix[i,j]+=row[i]*row[j]; matrix[i,3]-=row[i]*(x*x+y*y); }
            }
            double[] fit=Solve(matrix); if(fit==null) return false;
            Point center=new Point(mean.X-fit[0]/2,mean.Y-fit[1]/2); double radiusSquared=(fit[0]*fit[0]+fit[1]*fit[1])/4-fit[2];
            if(radiusSquared<.0025 || radiusSquared>100) return false;
            double radius=Math.Sqrt(radiusSquared),error=0,maximum=0;
            foreach(Point p in path) { double d=Math.Abs((p-center).Length-radius); error+=d*d; maximum=Math.Max(maximum,d); }
            bool nearClosed=(path[0]-path[path.Length-1]).Length<.12;
            if(Math.Sqrt(error/path.Length)>(nearClosed?.025:.012) || maximum>(nearClosed?.05:.035)) return false;
            double first=Math.Atan2(path[0].Y-center.Y,path[0].X-center.X),previous=first,winding=0,absolute=0;
            foreach(Point point in path.Skip(1)) { double angle=Math.Atan2(point.Y-center.Y,point.X-center.X),delta=angle-previous; while(delta>Math.PI) delta-=2*Math.PI; while(delta< -Math.PI) delta+=2*Math.PI; winding+=delta; absolute+=Math.Abs(delta); previous=angle; }
            if(Math.Abs(winding)<.55 || Math.Abs(winding)>6.8 || absolute-Math.Abs(winding)>.15) return false;
            bool closed=(path[0]-path[path.Length-1]).Length<.045 && Math.Abs(winding)>5.8;
            if(closed) winding=Math.Sign(winding)*2*Math.PI;
            int count=Math.Max(32,path.Length*3); result=new Point[count];
            Point start=new Point(center.X+radius*Math.Cos(first),center.Y+radius*Math.Sin(first)),end=new Point(center.X+radius*Math.Cos(first+winding),center.Y+radius*Math.Sin(first+winding));
            Vector from=path[0]-start,to=path[path.Length-1]-end;
            for(int i=0;i<count;i++) {
                double t=i/(double)(count-1),angle=first+winding*t;
                result[i]=new Point(center.X+radius*Math.Cos(angle),center.Y+radius*Math.Sin(angle));
                if(!closed) result[i]+=from*Math.Pow(1-t,3)+to*Math.Pow(t,3);
            }
            return true;
        }
        public static List<GesturePoint> Clean(List<GesturePoint> original) {
            if(!GestureShape.Valid(original)) return original;
            Point[] path=original.Select(p=>new Point(p.X,p.Y)).ToArray(),part;
            if(FitLine(path,true,out part) || FitArc(path,out part)) return Bounded(original,part);
            Point[] guide=Smooth(path,3); List<int> vertices=new List<int> { 0 }; Simplify(guide,0,guide.Length-1,vertices);
            List<int> corners=new List<int> { 0 };
            for(int i=1;i<vertices.Count-1;i++) {
                Vector incoming=guide[vertices[i]]-guide[vertices[i-1]],outgoing=guide[vertices[i+1]]-guide[vertices[i]];
                if(incoming.Length>.035 && outgoing.Length>.035 && Vector.Multiply(incoming,outgoing)/(incoming.Length*outgoing.Length)<.5) corners.Add(vertices[i]);
            }
            corners.Add(path.Length-1); List<Point> corrected=new List<Point>();
            for(int i=1;i<corners.Count;i++) {
                Point[] section=path.Skip(corners[i-1]).Take(corners[i]-corners[i-1]+1).ToArray();
                if(!FitLine(section,false,out part) && !FitArc(section,out part)) part=Smooth(section,5);
                corrected.AddRange(i==1?part:part.Skip(1));
            }
            return Bounded(original,corrected);
        }
        static List<GesturePoint> Bounded(List<GesturePoint> original,IList<Point> path) {
            List<GesturePoint> corrected=GestureShape.Normalize(path);
            return corrected!=null && GestureShape.Distance(original,corrected)<=MaxError?corrected:original.Select(p=>new GesturePoint(new Point(p.X,p.Y))).ToList();
        }
    }
}

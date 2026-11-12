using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace OrbitRing {
    static class GesturePresentationTests {
        static void Check(bool condition,string label,List<string> results) { if(!condition) throw new Exception(label); results.Add("PASS "+label); }
        public static List<string> Run() {
            List<string> results=new List<string>(); Preferences prefs=Preferences.Defaults();
            foreach(string directions in new[]{"UD","DU","LR","RL"}) {
                OrbitBinding binding=new OrbitBinding { Pattern=directions }; List<GesturePoint> template=GestureShape.Template(binding); string saved=Json.Write(template);
                Point[] path=template.Select(q=>new Point(q.X,q.Y)).ToArray();
                foreach(Size size in new[]{new Size(70,30),new Size(800,125)}) {
                    Point[] shown=GesturePresentation.Project(path,size,size.Width>70);
                    bool vertical=directions[0]=='U' || directions[0]=='D';
                    Check(shown.Length==path.Length && shown.All(p=>vertical?Math.Abs(p.X-shown[0].X)<.000001:Math.Abs(p.Y-shown[0].Y)<.000001),"presentation introduces no lateral movement at "+size+": "+directions,results);
                    Vector outward,returning; Point first=GesturePresentation.Playhead(shown,.25,out outward),second=GesturePresentation.Playhead(shown,.75,out returning);
                    Check((first-second).Length<.00001 && Vector.Multiply(outward,returning)<-.99,"animation travels out and back along the identical line: "+directions,results);
                }
                Check(Json.Write(template)==saved && prefs.Orbit.ResolveDrawing("other.exe",directions,path,"")==prefs.Orbit.Resolve("other.exe",directions,""),"animation cannot change saved recognition template: "+directions,results);
            }
            Point[] actual={new Point(300,400),new Point(300,350),new Point(300,280),new Point(300,330),new Point(300,400)};
            DirectionStroke stroke=new DirectionStroke(actual[0],30); foreach(Point p in actual.Skip(1)) stroke.Add(p);
            Check(stroke.Pattern=="UD" && prefs.Orbit.ResolveDrawing("test.exe",stroke.Pattern,actual,"").Action.Value=="Ctrl+R","reload recognizes straight up and down without sideways movement",results);
            Point[] lateral={new Point(0,100),new Point(0,0),new Point(30,0),new Point(30,100)};
            Point[] projected=GesturePresentation.Project(lateral,new Size(300,160),true);
            Check(projected.Length==lateral.Length && Math.Abs(projected.Last().X-projected[0].X)>30,"genuine sideways movement is preserved in the drawing",results);
            Check(GesturePresentation.Progress(0)==0 && GesturePresentation.Progress(2)==1 && GesturePresentation.Progress(3.6)==0,"animation starts at origin and finishes at endpoint before repeating",results);
            Vector up,down; GesturePresentation.Playhead(actual,GesturePresentation.Progress(.55),out up); GesturePresentation.Playhead(actual,GesturePresentation.Progress(1.45),out down);
            Check(up.Y<0 && down.Y>0,"animation demonstrates up before down",results);
            Point[] circle=Enumerable.Range(0,65).Select(i=>new Point(100*Math.Cos(i*Math.PI/32),100*Math.Sin(i*Math.PI/32))).ToArray();
            Point[] projectedCircle=GesturePresentation.Project(circle,new Size(200,200),true);
            Check(projectedCircle.Length==circle.Length && (projectedCircle[16]-projectedCircle[0]).Length>60,"closed curve retains its shape and drawing order",results);
            Point[] corner={new Point(0,0),new Point(0,100),new Point(100,100)};
            Point[] trail=GesturePresentation.Trail(corner,.65,.3);
            Check(trail.Length==3 && trail[1]==corner[1] && (trail[0]-new Point(0,70)).Length<.00001 && Math.Abs(trail[2].X-30)<.00001,"animated tail preserves corners without drawing a diagonal shortcut",results);
            Point[] retrace={new Point(0,100),new Point(0,0),new Point(0,100)};
            trail=GesturePresentation.Trail(retrace,.6,.2);
            Check(trail.Length==3 && trail[1]==retrace[1] && trail.All(p=>p.X==0),"animated tail preserves exact turnaround without sideways artifacts",results);
            foreach(Point[] shape in new[]{corner,retrace,circle,new[]{new Point(0,0),new Point(100,0)}}) {
                Vector direction; Point early=GesturePresentation.Playhead(shape,.1,out direction),late=GesturePresentation.Playhead(shape,.4,out direction);
                Check((late-early).Length>1,"ordinary paths and return paths share the moving presentation",results);
            }
            Check(!GesturePresentation.Animate(true,false,false,true,true,true),"pointer outside preview never starts an animation",results);
            Check(GesturePresentation.Animate(true,true,false,true,true,true),"hover starts an animation for every drawn path",results);
            Check(!GesturePresentation.Animate(true,true,true,true,true,true) && !GesturePresentation.Animate(true,true,false,false,true,true) && !GesturePresentation.Animate(true,true,false,true,false,true) && !GesturePresentation.Animate(true,true,false,true,true,false),"drawing hidden inactive and reduced-motion states stop animation",results);
            Check(GesturePresentation.Trail(null,.5,.2).Length==0 && GesturePresentation.Trail(corner,0,.2).Length==0,"empty and initial trails are safe",results);
            Check(GesturePresentation.TrailSpan(1.65,null,false)>GesturePresentation.TrailSpan(1.683,null,false) && GesturePresentation.TrailSpan(2.1,null,false)>GesturePresentation.TrailSpan(2.3,null,false) && GesturePresentation.TrailSpan(2.3,null,false)>0 && GesturePresentation.TrailSpan(2.41,null,false)==0,"tail continues moving on arrival without a stationary phase",results);
            Check(GesturePresentation.MotionOpacity(2.1,null,false)==1 && GesturePresentation.MotionOpacity(2.3,null,false)>0 && GesturePresentation.MotionOpacity(2.3,null,false)<1 && GesturePresentation.MotionOpacity(2.41,null,false)==0 && GesturePresentation.MotionOpacity(3.6,null,false)==1,"arrow and tail fade together before repeating",results);
            Point[] arrowOut=GesturePresentation.Arrow(retrace,.49,8),arrowBack=GesturePresentation.Arrow(retrace,.51,8);
            Check(arrowOut[0].Y<arrowOut[1].Y && arrowBack[0].Y>arrowBack[1].Y && Math.Abs((arrowBack[1].X+arrowBack[2].X)/2-arrowBack[0].X)<.00001,"arrow changes direction cleanly at turnaround without crossed strokes",results);
            foreach(Vector direction in new[]{new Vector(1,0),new Vector(-1,0),new Vector(0,1),new Vector(0,-1),new Vector(.707106781,.707106781)}) {
                Point[] straight={new Point(10,10),new Point(10,10)+direction*140};
                Point[] initialArrow=GesturePresentation.Arrow(straight,0,12);
                Check(initialArrow.All(point=>point==straight[0]),"first frame has no arrow body behind the start dot: "+direction,results);
                foreach(double progress in new[]{.00001,.002,.02,.05,.5}) {
                    Point[] emerging=GesturePresentation.Arrow(straight,progress,12);
                    Vector tangent; Point tip=GesturePresentation.Playhead(straight,progress,out tangent);
                    Check(emerging[0]==tip && emerging.All(point=>Vector.Multiply(point-straight[0],tangent)>=-.00001),"emerging arrow stays beyond origin with its tip on the path: "+direction+" at "+progress,results);
                }
                Point[] fullArrow=GesturePresentation.Arrow(straight,.5,12);
                Check(Math.Abs((fullArrow[0]-(fullArrow[1]+(fullArrow[2]-fullArrow[1])*.5)).Length-12)<.00001,"arrow reaches normal size after leaving origin: "+direction,results);
                foreach(double progress in new[]{.02,.15,.5,.9,1.0}) {
                    Vector tangent; Point tip=GesturePresentation.Playhead(straight,progress,out tangent);
                    Point[] behind=GesturePresentation.Trail(straight,progress,.2,4.5);
                    Check(behind.All(point=>Vector.Multiply(tip-point,tangent)>=4.49999),"moving trail stays inside arrow point: "+direction+" at "+progress,results);
                }
            }
            Point[] horizontal={new Point(0,0),new Point(1,0)};
            Point[] compactLine=GesturePresentation.Project(horizontal,new Size(70,30),false);
            Check(Math.Abs((compactLine.Last()-compactLine[0]).Length-22)<.00001 && compactLine.All(p=>Math.Abs(p.Y-15)<.00001),"thumbnail gesture retains its original scale instead of stretching the shape",results);
            Point[] compactCorner=GesturePresentation.Project(corner,new Size(70,30),false);
            Check(Math.Abs((compactCorner[1]-compactCorner[0]).Length-(compactCorner[2]-compactCorner[1]).Length)<.00001 && compactCorner.All(p=>p.X>=4 && p.X<=66 && p.Y>=4 && p.Y<=26),"thumbnail curves and corners keep uniform scale within padded bounds",results);
            foreach(string pattern in new[]{"L","R","U","D","UD","DU"}) {
                Point[] small=GesturePresentation.Project(GestureShape.Template(new OrbitBinding { Pattern=pattern }).Select(p=>new Point(p.X,p.Y)).ToArray(),new Size(70,30),false);
                foreach(double width in new[]{1.0,3.0,8.0}) {
                    double progress=GesturePresentation.Progress(1.65),span=GesturePresentation.TrailSpan(1.65,small,true),clearance=GesturePresentation.StrokeWidth(width,false)*1.5;
                    Point[] tail=GesturePresentation.Trail(small,progress,span,clearance); double visible=0;
                    for(int i=1;i<tail.Length;i++) visible+=(tail[i]-tail[i-1]).Length;
                    Check(visible>16,"short thumbnail trail remains visible on arrival beyond arrow clearance: "+pattern+" width "+width,results);
                    tail=GesturePresentation.Trail(small,1,GesturePresentation.TrailSpan(1.8,small,true),clearance); visible=0;
                    for(int i=1;i<tail.Length;i++) visible+=(tail[i]-tail[i-1]).Length;
                    Check(visible>14,"short thumbnail tail stays readable while converging: "+pattern+" width "+width,results);
                }
            }
            Point[] shortEditor={new Point(0,0),new Point(100,0)},longEditor={new Point(0,0),new Point(500,0)};
            Check(Math.Abs(GesturePresentation.TrailSpan(1,shortEditor,false)-.5)<.000001 && Math.Abs(GesturePresentation.TrailSpan(1,longEditor,false)-.5)<.000001,"trail lifetime is independent of gesture length",results);
            Check(GesturePresentation.TrailSpan(3.16,compactLine,true)==0 && GesturePresentation.TrailSpan(3.16,shortEditor,false)==0,"minimum trail length still reaches zero at the end",results);
            Check(GesturePresentation.Progress(1.66)==1 && GesturePresentation.TrailSpan(2.45,compactLine,true)>0 && GesturePresentation.TrailSpan(2.9,compactLine,true)>0,"short fast traces remain visible for more than a second after the arrow stops",results);
            foreach(Point[] shape in new[]{compactLine,shortEditor,longEditor,corner,retrace,circle}) {
                double previous=Double.MaxValue; bool moving=true;
                double until=shape==compactLine?2.9:2.3;
                for(double seconds=1.65;seconds<until;seconds+=.033) {
                    Point[] shrinking=GesturePresentation.Trail(shape,1,GesturePresentation.TrailSpan(seconds,shape,shape==compactLine),3.3);
                    double length=0; for(int i=1;i<shrinking.Length;i++) length+=(shrinking[i]-shrinking[i-1]).Length;
                    if(length<=0 || length>=previous) moving=false;
                    previous=length;
                }
                Check(moving,"tail advances on every frame after arrival for straight corner return and curved paths",results);
                double before=GesturePresentation.Progress(1.6)-GesturePresentation.Progress(1.55);
                double after=GesturePresentation.TrailSpan(1.7,shape,shape==compactLine)-GesturePresentation.TrailSpan(1.75,shape,shape==compactLine);
                double later=GesturePresentation.TrailSpan(2.1,shape,shape==compactLine)-GesturePresentation.TrailSpan(2.15,shape,shape==compactLine);
                Check(Math.Abs(before-after)<.000001 && Math.Abs(before-later)<.000001,"tail retains travel speed throughout convergence for every display size and shape",results);
            }
            GestureDrawing locked=new GestureDrawing { EditorView=true }; bool recorded=false; locked.Recorded=delegate { recorded=true; };
            System.Windows.Input.MouseButtonEventArgs click=new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left) { RoutedEvent=UIElement.MouseLeftButtonDownEvent };
            locked.RaiseEvent(click);
            Check(!locked.DrawingEnabled && !locked.IsMouseCaptured && !click.Handled && !recorded,"large preview ignores drawing input until editing is explicitly enabled",results);
            Check(GesturePresentation.StrokeWidth(1,false)>=2.2 && GesturePresentation.StrokeWidth(8,false)<=3.2 && GesturePresentation.ArrowSize(8,false)<=7 && GesturePresentation.StrokeWidth(1,true)==1,"thumbnail stroke and arrow remain legible without changing editor styling",results);
            return results;
        }
    }
}

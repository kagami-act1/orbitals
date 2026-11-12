using System;
using System.Windows;

namespace OrbitRing {
    sealed class PlanetWheel {
        int remainder;
        public static bool InCenter(Point point) { return (point-new Point(205,205)).Length<RingSurface.InnerRadius; }
        public static int Next(int current,int count,int steps) { if(count<1) return 0; return ((current+steps)%count+count)%count; }
        public int Scroll(int delta) { if(Math.Sign(delta)!=Math.Sign(remainder)) remainder=0; remainder+=delta; int steps=remainder/120; remainder%=120; return -steps; }
        public void Reset() { remainder=0; }
    }
}

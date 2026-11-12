using System.Windows;

namespace OrbitRing {
    struct RingPlacement {
        public readonly Point Offset;
        public readonly Size WindowSize;
        public RingPlacement(Native.POINT invocation,Native.RECT monitor,double dpi,double scale) {
            Offset=new Point((invocation.X-monitor.Left)/dpi-205*scale,(invocation.Y-monitor.Top)/dpi-205*scale);
            WindowSize=new Size((monitor.Right-monitor.Left)/dpi,(monitor.Bottom-monitor.Top)/dpi);
        }
    }
}

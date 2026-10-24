using System;
using System.Windows.Media;

namespace OrbitRing {
    sealed partial class RingSurface {
        static readonly Color PlateColor=Color.FromArgb(242,24,30,38),PanelColor=Color.FromRgb(27,34,43),EdgeColor=Color.FromRgb(48,58,71);
        SolidColorBrush openingPlateFill,openingPlateEdge,openingButtonFill,openingButtonEdge;
        Brush selectedButtonFill;
        Color openingAccent;
        void InitializeOpeningAppearance() {
            openingAccent=(Color)ColorConverter.ConvertFromString(config.Appearance.RingAccent);
            openingPlateFill=new SolidColorBrush(PlateColor); openingPlateEdge=new SolidColorBrush(EdgeColor);
            openingButtonFill=new SolidColorBrush(PanelColor); openingButtonEdge=new SolidColorBrush(EdgeColor);
            selectedButtonFill=config.Appearance.Selected; Opacity=config.Appearance.RingOpacity;
        }
        static Color Tint(Color color,Color accent,double amount) {
            return Color.FromArgb(color.A,(byte)Math.Round(color.R+(accent.R-color.R)*amount),(byte)Math.Round(color.G+(accent.G-color.G)*amount),(byte)Math.Round(color.B+(accent.B-color.B)*amount));
        }
        internal void SetOpeningProgress(double progress) {
            if(config==null) return;
            progress=Math.Max(0,Math.Min(1,progress));
            double settled=1-Math.Pow(1-Math.Min(1,progress/.5),3),light=progress>=1?0:Math.Sin(Math.PI*progress);
            Opacity=config.Appearance.RingOpacity*(.72+.28*settled);
            openingPlateFill.Color=Tint(PlateColor,openingAccent,.06*light);
            openingPlateEdge.Color=Tint(EdgeColor,openingAccent,.32*light);
            openingButtonFill.Color=Tint(PanelColor,openingAccent,.085*light);
            openingButtonEdge.Color=Tint(EdgeColor,openingAccent,.25*light);
        }
    }
}

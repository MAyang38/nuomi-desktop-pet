using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopKitten
{
    /// <summary>
    /// A resolution independent, front facing little marmalade kitten.
    /// Coordinates are in a 240 by 240 logical canvas. The caller owns scaling.
    /// activity is walk intensity in [0,1]; walk phase is derived from time.
    /// </summary>
    public static class CatArt
    {
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, Brush> BrushCache = new Dictionary<string, Brush>();
        private static readonly Dictionary<Brush, Dictionary<double, Pen>> PenCache = new Dictionary<Brush, Dictionary<double, Pen>>();
        private static readonly Dictionary<string, Geometry> GeometryCache = new Dictionary<string, Geometry>();
        private static readonly Brush Ink = B("#583C35");
        private static readonly Brush Fur = B("#FFF0CF");
        private static readonly Brush FurLight = B("#FFF9E9");
        private static readonly Brush FurShade = B("#EBC795");
        private static readonly Brush Orange = B("#F1B36A");
        private static readonly Brush OrangeLight = B("#F8C789");
        private static readonly Brush Stripe = B("#CE8B4C");
        private static readonly Brush EarPink = B("#EEA79E");
        private static readonly Brush EarLight = B("#FFD5C5");
        private static readonly Brush Blush = B("#F4B3A3");
        private static readonly Brush Eye = B("#46362F");
        private static readonly Brush EyeWarm = B("#A57348");
        private static readonly Brush Nose = B("#C57C76");
        private static readonly Brush White = B("#FFFFFF");
        private static readonly Brush HeartPink = B("#EB8D9C");
        private static readonly Brush HeartLight = B("#F7B3B9");
        private static readonly Brush Shadow = B("#26000000");
        private static readonly Pen Outline = P(Ink, 2.8);
        private static readonly Pen Fine = P(Ink, 1.9);
        private static readonly Pen Hair = P(Ink, 1.45);
        private static readonly Pen OrangePen = P(Stripe, 4.4);
        private static readonly Pen ToePen = P(FurShade, 1.4);

        private static readonly Geometry Tail = G("M 153,172 C 166,177 181,182 185,170 C 188,162 176,159 177,151 C 178,144 185,144 190,150 C 207,172 192,194 174,192 C 160,191 151,184 146,179 Z");
        private static readonly Geometry Body = G("M 94,132 C 85,140 81,159 82,176 C 82,194 96,204 120,204 C 144,204 159,194 158,175 C 158,158 154,141 146,132 Z");
        private static readonly Geometry Bib = G("M 103,136 C 108,143 111,145 120,145 C 129,145 133,142 137,136 C 145,151 147,171 139,185 C 134,181 131,188 126,185 C 121,190 116,185 113,185 C 104,188 99,174 99,162 C 99,153 100,144 103,136 Z");
        private static readonly Geometry LeftEar = G("M 64,78 C 61,64 59,46 66,39 C 70,36 87,49 97,61 Z");
        private static readonly Geometry RightEar = G("M 143,61 C 153,49 170,36 174,39 C 181,46 179,64 176,78 Z");
        private static readonly Geometry LeftInner = G("M 67,63 C 66,56 65,45 68,44 C 73,47 81,54 84,59 Z");
        private static readonly Geometry RightInner = G("M 156,59 C 161,54 168,47 172,44 C 175,46 174,57 173,64 Z");
        private static readonly Geometry Head = G("M 120,55 C 145,54 167,62 176,79 C 181,88 181,101 185,110 C 189,123 178,138 161,142 C 145,147 95,147 79,142 C 62,138 51,123 55,110 C 59,101 59,88 64,79 C 73,62 95,54 120,55 Z");
        private static readonly Geometry Crown = G("M 58,59 L 181,59 L 182,88 C 164,86 156,79 148,80 C 136,79 132,87 120,87 C 108,87 105,79 94,80 C 83,80 77,87 58,89 Z");
        private static readonly Geometry ForeheadLeft = G("M 101,58 C 105,57 109,57 112,57 L 110,76 C 109,80 106,80 105,76 Z");
        private static readonly Geometry ForeheadCenter = G("M 116,55 L 124,55 L 123,78 C 122,83 118,83 117,78 Z");
        private static readonly Geometry ForeheadRight = G("M 128,57 C 132,57 136,57 140,58 L 135,76 C 134,80 131,80 130,76 Z");
        private static readonly Geometry NoseShape = G("M 114.5,114 C 115,112 125,112 125.5,114 C 125,117 122,119 120,119 C 118,119 115,117 114.5,114 Z");
        private static readonly Geometry Smile = G("M 120,118 L 120,122 C 117,127 112,126 110,123 M 120,122 C 123,127 128,126 130,123");
        private static readonly Geometry OpenSmile = G("M 112,122 C 116,124 124,124 128,122 C 128,133 113,135 112,122 Z");
        private static readonly Geometry Heart = G("M 0,3 C -10,-4 -9,-12 -3,-12 C 0,-12 2,-10 3,-8 C 5,-12 8,-13 11,-11 C 18,-7 12,1 3,7 Z");

        public static void Draw(DrawingContext dc, double time, string mood, double activity, double petPulse, double scale)
        {
            if (dc == null) return;
            time = Double.IsNaN(time) || Double.IsInfinity(time) ? 0 : time;
            if (mood == "sleep") { CatSleepArt.Draw(dc, time); return; }
            activity = Clamp(activity);
            petPulse = Clamp(petPulse);
            bool walking = mood == "walk";
            bool happy = mood == "happy" || petPulse > 0.05;
            bool dragging = mood == "drag";
            bool jumping = mood == "jump";
            double gait = Math.Sin(time * 8.2);
            double breath = Math.Sin(time * 2.7);
            double bob = walking ? Math.Abs(gait) * -2.0 * (0.4 + 0.6 * activity) : breath * 0.7;
            if (dragging) bob = -1.4;

            dc.DrawEllipse(Shadow, null, new Point(120, 210), 48, 5);
            dc.PushTransform(new TranslateTransform(0, bob));

            // The tail sits behind the body. Its rounded silhouette and clipped stripes
            // remain clean even when rendered at small desktop sizes.
            dc.PushTransform(new RotateTransform(Math.Sin(time * (happy ? 6.0 : 2.8)) * (happy ? 6 : 3.5), 154, 176));
            dc.DrawGeometry(Orange, Outline, Tail);
            dc.PushClip(Tail);
            dc.DrawGeometry(null, OrangePen, G("M 169,183 C 173,180 174,177 174,174 M 181,192 C 181,187 184,184 187,183 M 187,166 C 190,162 194,161 197,162 M 177,151 L 187,149"));
            dc.DrawGeometry(null, P(OrangeLight, 2.0), G("M 155,176 C 166,182 177,186 185,179"));
            dc.Pop();
            dc.Pop();

            dc.DrawGeometry(Fur, Outline, Body);
            dc.DrawEllipse(FurShade, null, new Point(94, 185), 10, 10);
            dc.DrawEllipse(FurShade, null, new Point(146, 185), 10, 10);
            dc.DrawGeometry(FurLight, null, Bib);
            dc.DrawGeometry(null, P(Orange, 4), G("M 84,159 L 92,162 M 83,168 L 91,171 M 156,159 L 148,162 M 157,168 L 149,171"));

            if (walking)
            {
                // Arms swing from the shoulders; each hand counters its same-side foot.
                // Legs have a full stride plus a lifted swing phase, rather than sliding toes.
                double strength = .6 + .4 * activity;
                DrawWalkingArm(dc, 88, 149, 12 + gait * 18 * strength, gait * strength);
                DrawWalkingArm(dc, 152, 149, -12 + gait * 18 * strength, -gait * strength);
                DrawWalkingLeg(dc, 101, 182, gait * strength);
                DrawWalkingLeg(dc, 139, 182, -gait * strength);
            }
            else
            {
                double lift = dragging || jumping ? -7 : 0;
                DrawPaw(dc, 101, 194 + lift, 1);
                DrawPaw(dc, 139, 194 + lift, 1);
            }

            double tilt = dragging ? Math.Sin(time * 3.2) * 3.0 : happy ? Math.Sin(time * 3.5) * 2.0 : walking ? gait * 1.4 : 0;
            dc.PushTransform(new RotateTransform(tilt, 120, 125));
            dc.DrawGeometry(Orange, Outline, LeftEar);
            dc.DrawGeometry(Orange, Outline, RightEar);
            dc.DrawGeometry(EarPink, null, LeftInner);
            dc.DrawGeometry(EarPink, null, RightInner);
            dc.DrawGeometry(null, P(EarLight, 1.8), G("M 69,48 L 70,57 M 171,48 L 170,57"));

            dc.DrawGeometry(Fur, Outline, Head);
            dc.PushClip(Head);
            dc.DrawGeometry(OrangeLight, null, Crown);
            dc.DrawGeometry(Stripe, null, ForeheadLeft);
            dc.DrawGeometry(Stripe, null, ForeheadCenter);
            dc.DrawGeometry(Stripe, null, ForeheadRight);
            dc.DrawGeometry(null, P(Orange, 4.1), G("M 55,103 L 68,107 M 56,113 L 68,116 M 185,103 L 172,107 M 184,113 L 172,116"));
            dc.DrawEllipse(FurLight, null, new Point(102, 124), 25, 17);
            dc.DrawEllipse(FurLight, null, new Point(138, 124), 25, 17);
            dc.Pop();

            dc.DrawEllipse(Blush, null, new Point(79, 116), 10, 5.5);
            dc.DrawEllipse(Blush, null, new Point(161, 116), 10, 5.5);

            double blinkPhase = time % 6.1;
            bool blink = blinkPhase > 5.80 && blinkPhase < 5.96;
            bool content = happy && Math.Sin(time * 3.4) > -0.4;
            if (content || blink)
            {
                dc.DrawGeometry(null, Fine, G("M 85,104 C 90,96 100,96 106,104 M 134,104 C 140,96 150,96 155,104"));
            }
            else
            {
                DrawEye(dc, 96, 101, dragging || jumping);
                DrawEye(dc, 144, 101, dragging || jumping);
            }

            dc.DrawGeometry(Nose, null, NoseShape);
            dc.DrawEllipse(EarLight, null, new Point(118, 114.4), 2.2, 0.8);
            if (happy || jumping)
            {
                dc.DrawGeometry(Ink, Hair, OpenSmile);
                dc.DrawEllipse(EarPink, null, new Point(120, 128.3), 4.6, 2.8);
            }
            else
            {
                dc.DrawGeometry(null, Fine, Smile);
            }
            dc.DrawGeometry(null, Hair, G("M 72,117 L 51,112 M 71,123 L 51,124 M 168,117 L 189,112 M 169,123 L 189,124"));
            dc.DrawEllipse(FurShade, null, new Point(103, 117), 1.1, 1.1);
            dc.DrawEllipse(FurShade, null, new Point(100, 122), 1.1, 1.1);
            dc.DrawEllipse(FurShade, null, new Point(137, 117), 1.1, 1.1);
            dc.DrawEllipse(FurShade, null, new Point(140, 122), 1.1, 1.1);

            // A small neck scarf gives the silhouette a recognizable, friendly detail.
            dc.DrawGeometry(B("#8FBFB4"), P(Ink, 2.0), G("M 97,143 C 111,148 129,148 143,143 L 142,149 C 128,154 111,154 98,149 Z"));
            dc.DrawGeometry(B("#A8D2C7"), P(Ink, 1.8), G("M 134,150 L 141,149 L 146,161 L 139,159 L 136,162 Z"));
            dc.DrawEllipse(B("#F6D182"), P(Ink, 1.6), new Point(120, 151), 4.8, 4.8);
            dc.DrawGeometry(null, P(Stripe, 1.0), G("M 118,152 L 122,152"));
            dc.Pop();
            dc.Pop();

            if (happy)
            {
                double floatOffset = (Math.Sin(time * 2.8) + 1) * 2.8;
                DrawHeart(dc, 48, 78 - floatOffset, 0.57, HeartPink, -15);
                DrawHeart(dc, 191, 62 - floatOffset, 0.69, HeartPink, 13);
                if (petPulse > 0.25) DrawHeart(dc, 181, 91 - floatOffset, 0.39, HeartLight, -8);
            }
        }

        private static void DrawWalkingArm(DrawingContext dc, double x, double y, double angle, double phase)
        {
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(angle));
            dc.PushTransform(new ScaleTransform(1, .88 - .14 * phase));
            dc.DrawGeometry(Fur, Outline, G("M -6,-5 C -11,0 -10,14 -9,23 C -14,34 -8,41 0,41 C 9,41 13,34 9,27 L 8,3 C 8,-3 2,-6 -6,-5 Z"));
            dc.DrawGeometry(null, P(Orange, 3.5), G("M -8,8 L 5,9 M -8,15 L 3,16"));
            dc.DrawEllipse(FurLight, null, new Point(0, 33), 7.2, 5.2);
            dc.DrawGeometry(null, ToePen, G("M -3,35 L -3,38 M 2,35 L 2,38"));
            dc.Pop(); dc.Pop(); dc.Pop();
        }

        private static void DrawWalkingLeg(DrawingContext dc, double x, double y, double phase)
        {
            dc.PushTransform(new TranslateTransform(x, y - Math.Max(0, phase) * 6));
            dc.PushTransform(new RotateTransform(phase * 22));
            dc.DrawGeometry(Fur, Outline, G("M -9,-4 C -11,2 -9,9 -10,14 C -15,17 -15,24 -9,26 C -5,28 8,28 12,24 C 16,20 12,14 8,12 L 9,-4 Z"));
            dc.DrawEllipse(FurLight, null, new Point(0, 21), 9.4, 4.5);
            dc.DrawGeometry(null, ToePen, G("M -4,23 L -4,26 M 3,23 L 3,26"));
            dc.Pop(); dc.Pop();
        }

        private static void DrawEye(DrawingContext dc, double x, double y, bool wide)
        {
            double ry = wide ? 16.4 : 15.0;
            dc.DrawEllipse(FurShade, null, new Point(x, y + 1), 12.1, ry + 0.6);
            dc.DrawEllipse(Eye, null, new Point(x, y), 10.8, ry);
            dc.DrawEllipse(EyeWarm, null, new Point(x, y + 8.0), 7.0, 4.7);
            dc.DrawEllipse(Eye, null, new Point(x, y + 4.0), 7.5, 7.1);
            dc.DrawEllipse(White, null, new Point(x - 3.4, y - 5.7), 4.1, 4.8);
            dc.DrawEllipse(White, null, new Point(x + 4.8, y + 4.8), 1.8, 1.8);
            dc.DrawEllipse(B("#F5D4A0"), null, new Point(x - 3, y + 10), 2.4, 1.2);
        }

        private static void DrawPaw(DrawingContext dc, double x, double y, double squash)
        {
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new ScaleTransform(1, squash));
            dc.DrawGeometry(Fur, Outline, G("M -11,-14 C -12,-7 -14,1 -13,8 C -12,15 12,15 13,8 C 14,1 12,-7 11,-14"));
            dc.DrawEllipse(FurLight, null, new Point(0, 7.2), 9.2, 4.2);
            dc.DrawGeometry(null, ToePen, G("M -4,9 L -4,12 M 3,9 L 3,12"));
            dc.Pop();
            dc.Pop();
        }

        private static void DrawHeart(DrawingContext dc, double x, double y, double size, Brush color, double angle)
        {
            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(angle));
            dc.PushTransform(new ScaleTransform(size, size));
            dc.DrawGeometry(color, null, Heart);
            dc.DrawEllipse(HeartLight, null, new Point(-3.4, -7), 2.3, 1.4);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }

        private static double Clamp(double value)
        {
            if (Double.IsNaN(value)) return 0;
            return Math.Max(0, Math.Min(1, value));
        }

        private static Brush B(string value)
        {
            lock (CacheLock)
            {
                Brush cached;
                if (BrushCache.TryGetValue(value, out cached)) return cached;
                SolidColorBrush brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value);
                brush.Freeze();
                BrushCache[value] = brush;
                return brush;
            }
        }

        private static Pen P(Brush brush, double width)
        {
            lock (CacheLock)
            {
                Dictionary<double, Pen> widths;
                if (!PenCache.TryGetValue(brush, out widths))
                {
                    widths = new Dictionary<double, Pen>();
                    PenCache[brush] = widths;
                }
                Pen cached;
                if (widths.TryGetValue(width, out cached)) return cached;
                Pen pen = new Pen(brush, width);
                pen.StartLineCap = PenLineCap.Round;
                pen.EndLineCap = PenLineCap.Round;
                pen.LineJoin = PenLineJoin.Round;
                pen.Freeze();
                widths[width] = pen;
                return pen;
            }
        }

        private static Geometry G(string value)
        {
            lock (CacheLock)
            {
                Geometry cached;
                if (GeometryCache.TryGetValue(value, out cached)) return cached;
                Geometry geometry = Geometry.Parse(value);
                geometry.Freeze();
                GeometryCache[value] = geometry;
                return geometry;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace DesktopKitten
{
    /// <summary>A separate, genuinely lying-down sleeping pose, on the same 240px canvas.</summary>
    public static class CatSleepArt
    {
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, Brush> Brushes = new Dictionary<string, Brush>();
        private static readonly Dictionary<string, Geometry> Geometries = new Dictionary<string, Geometry>();
        private static readonly Dictionary<Brush, Dictionary<double, Pen>> Pens = new Dictionary<Brush, Dictionary<double, Pen>>();
        private static readonly Brush Ink = B("#583C35");
        private static readonly Brush Fur = B("#FFF0CF");
        private static readonly Brush Light = B("#FFF9E9");
        private static readonly Brush Shade = B("#EBC795");
        private static readonly Brush Orange = B("#F1B36A");
        private static readonly Brush OrangeLight = B("#F8C789");
        private static readonly Brush Stripe = B("#CE8B4C");
        private static readonly Brush Pink = B("#EEA79E");
        private static readonly Pen Outline = P(Ink, 2.8);
        private static readonly Pen Fine = P(Ink, 1.9);

        private static readonly Geometry Body = G("M 102,175 C 119,162 145,158 167,164 C 188,168 201,179 201,192 C 202,204 189,208 168,208 L 107,207 C 91,205 88,187 102,175 Z");
        private static readonly Geometry Haunch = G("M 164,180 C 178,177 190,185 191,196 C 191,204 181,207 168,207 L 147,207 C 143,202 149,198 160,198");
        private static readonly Geometry Tail = G("M 180,191 C 190,195 201,193 200,184 C 200,178 194,177 191,182 C 188,187 189,192 180,194 C 169,196 149,195 144,201 C 137,208 154,212 174,211 C 193,211 207,207 211,197 C 217,185 210,172 200,171 C 190,169 182,177 184,187 Z");
        private static readonly Geometry Head = G("M 80,150 C 102,148 120,157 124,171 C 127,179 128,186 122,192 C 115,200 103,202 82,201 C 60,201 45,195 42,185 C 39,177 43,162 52,156 C 59,152 69,150 80,150 Z");
        private static readonly Geometry LeftEar = G("M 46,169 C 43,159 42,143 47,138 C 51,136 65,146 70,153 Z");
        private static readonly Geometry RightEar = G("M 94,153 C 101,143 110,137 113,141 C 119,146 118,160 116,168 Z");
        private static readonly Geometry LeftInner = G("M 49,155 C 47,149 47,143 49,142 C 53,143 58,148 61,152 Z");
        private static readonly Geometry RightInner = G("M 103,152 C 106,148 110,144 112,145 C 114,149 113,155 112,157 Z");
        private static readonly Geometry PawBack = G("M 79,195 C 90,191 109,191 116,197 C 121,201 119,208 112,209 L 77,209 C 69,208 70,199 79,195 Z");
        private static readonly Geometry PawFront = G("M 50,195 C 61,192 77,194 85,198 C 92,202 89,209 82,210 L 46,210 C 37,209 37,200 43,198 Z");

        public static void Draw(DrawingContext dc, double time)
        {
            if (dc == null) return;
            if (Double.IsNaN(time) || Double.IsInfinity(time)) time = 0;
            double breath = Math.Sin(time * 1.65);

            dc.DrawEllipse(B("#26000000"), null, new Point(126, 211), 88, 4.3);

            // The back rises only a little as she breathes. Paws stay on the floor.
            dc.PushTransform(new ScaleTransform(1, 1 + breath * 0.014, 146, 208));
            dc.DrawGeometry(Fur, Outline, Body);
            dc.PushClip(Body);
            dc.DrawGeometry(OrangeLight, null, G("M 108,172 C 127,158 164,157 184,174 C 173,172 170,184 162,186 C 154,188 151,177 144,177 C 137,177 134,185 127,184 C 118,183 118,173 108,172 Z"));
            dc.DrawGeometry(null, P(Stripe, 4.3), G("M 128,164 C 126,168 127,173 131,176 M 145,162 C 142,168 143,174 147,178 M 161,165 C 157,170 158,176 162,179"));
            dc.DrawEllipse(Light, null, new Point(123, 199), 32, 17);
            dc.Pop();
            dc.DrawGeometry(Fur, P(Ink, 2.3), Haunch);
            dc.DrawGeometry(null, P(Orange, 3.3), G("M 181,184 L 174,188 M 187,191 L 179,194"));
            dc.DrawGeometry(null, P(Shade, 1.5), G("M 161,204 L 166,204 M 171,204 L 176,204"));
            dc.Pop();

            // A curled tail wraps the tucked hindquarters, visibly unlike the upright pose.
            dc.DrawGeometry(Orange, Outline, Tail);
            dc.PushClip(Tail);
            dc.DrawGeometry(null, P(Stripe, 4.0), G("M 152,197 L 154,211 M 169,195 L 171,212 M 188,192 L 193,208 M 198,182 L 211,184 M 194,173 L 195,182"));
            dc.DrawGeometry(null, P(OrangeLight, 2.0), G("M 151,203 C 169,207 197,205 204,196"));
            dc.Pop();

            // Both front legs stretch out beneath the cheek; no standing legs remain.
            dc.DrawGeometry(Fur, Outline, PawBack);
            dc.DrawEllipse(Light, null, new Point(102, 203.7), 12, 3.5);
            dc.DrawGeometry(null, P(Shade, 1.4), G("M 105,205 L 105,208 M 111,204 L 111,207"));
            dc.DrawGeometry(Fur, Outline, PawFront);
            dc.DrawEllipse(Light, null, new Point(57, 204), 13.5, 3.5);
            dc.DrawGeometry(null, P(Shade, 1.4), G("M 49,204 L 49,208 M 56,205 L 56,209"));

            dc.PushTransform(new TranslateTransform(0, breath * -0.45));
            // The scarf is tucked between the low cheek and the shoulder.
            dc.DrawGeometry(B("#8FBFB4"), P(Ink, 2.0), G("M 111,177 C 120,177 125,185 123,196 L 116,200 C 120,190 116,184 109,183 Z"));
            dc.DrawGeometry(B("#A8D2C7"), P(Ink, 1.8), G("M 121,191 L 130,192 L 135,203 L 128,200 L 125,204 Z"));
            dc.DrawEllipse(B("#F6D182"), P(Ink, 1.6), new Point(124, 198), 4.4, 4.4);
            dc.DrawGeometry(null, P(Stripe, 1.0), G("M 122,199 L 126,199"));

            dc.DrawGeometry(Orange, Outline, LeftEar);
            dc.DrawGeometry(Orange, Outline, RightEar);
            dc.DrawGeometry(Pink, null, LeftInner);
            dc.DrawGeometry(Pink, null, RightInner);
            dc.DrawGeometry(null, P(B("#FFD5C5"), 1.6), G("M 49,145 L 50,151 M 111,147 L 110,152"));
            dc.DrawGeometry(Fur, Outline, Head);
            dc.PushClip(Head);
            dc.DrawGeometry(OrangeLight, null, G("M 40,150 L 122,147 L 125,169 C 111,170 107,163 99,165 C 92,168 88,171 81,169 C 72,165 70,164 63,168 C 57,172 49,173 41,171 Z"));
            dc.DrawGeometry(Stripe, null, G("M 69,150 L 76,150 L 77,163 C 76,167 73,167 72,163 Z M 80,150 L 87,150 L 86,164 C 85,168 82,168 81,164 Z M 91,151 L 98,152 L 94,164 C 92,168 90,166 90,163 Z"));
            dc.DrawGeometry(null, P(Orange, 3.0), G("M 42,177 L 51,179 M 43,184 L 51,186 M 124,176 L 115,179 M 124,183 L 116,186"));
            dc.DrawEllipse(Light, null, new Point(67, 188), 17, 10);
            dc.DrawEllipse(Light, null, new Point(95, 188), 17, 10);
            dc.Pop();

            dc.DrawEllipse(B("#F4B3A3"), null, new Point(55, 185), 6.5, 3.7);
            dc.DrawEllipse(B("#F4B3A3"), null, new Point(108, 185), 6.5, 3.7);
            dc.DrawGeometry(null, Fine, G("M 59,176 C 63,181 71,181 76,176 M 88,176 C 93,181 101,181 105,176"));
            dc.DrawGeometry(B("#C57C76"), null, G("M 77.6,185 C 78,183.7 85.5,183.7 86,185 C 85,188 83.3,189 81.8,189 C 80.4,189 78.5,187.5 77.6,185 Z"));
            dc.DrawEllipse(B("#FFD5C5"), null, new Point(80, 185.3), 1.6, 0.6);
            dc.DrawGeometry(null, P(Ink, 1.5), G("M 81.8,189 L 81.8,191 C 79,194 76,193 74,191 M 81.8,191 C 84,194 88,193 90,191"));
            dc.DrawGeometry(null, P(Ink, 1.25), G("M 52,187 L 36,184 M 52,191 L 36,193 M 112,186 L 128,182 M 112,191 L 127,192"));
            dc.DrawEllipse(Shade, null, new Point(70, 186), 0.9, 0.9);
            dc.DrawEllipse(Shade, null, new Point(95, 186), 0.9, 0.9);
            dc.Pop();

            double drift = Math.Sin(time * 1.15) * 2.0;
            dc.PushTransform(new TranslateTransform(drift, -drift));
            dc.DrawGeometry(null, P(B("#AB8F7A"), 2.1), G("M 137,143 L 145,143 L 137,151 L 145,151"));
            dc.DrawGeometry(null, P(B("#AB8F7A"), 1.8), G("M 151,127 L 158,127 L 151,134 L 158,134"));
            dc.Pop();
        }

        private static Brush B(string value)
        {
            lock (CacheLock)
            {
                Brush result;
                if (Brushes.TryGetValue(value, out result)) return result;
                result = (SolidColorBrush)new BrushConverter().ConvertFromString(value);
                result.Freeze();
                Brushes[value] = result;
                return result;
            }
        }

        private static Pen P(Brush brush, double width)
        {
            lock (CacheLock)
            {
                Dictionary<double, Pen> widths;
                if (!Pens.TryGetValue(brush, out widths))
                {
                    widths = new Dictionary<double, Pen>();
                    Pens[brush] = widths;
                }
                Pen result;
                if (widths.TryGetValue(width, out result)) return result;
                result = new Pen(brush, width);
                result.StartLineCap = PenLineCap.Round;
                result.EndLineCap = PenLineCap.Round;
                result.LineJoin = PenLineJoin.Round;
                result.Freeze();
                widths[width] = result;
                return result;
            }
        }

        private static Geometry G(string value)
        {
            lock (CacheLock)
            {
                Geometry result;
                if (Geometries.TryGetValue(value, out result)) return result;
                result = Geometry.Parse(value);
                result.Freeze();
                Geometries[value] = result;
                return result;
            }
        }
    }
}

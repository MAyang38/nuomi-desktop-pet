using System;
using System.Windows;
using System.Windows.Media;

namespace DesktopKitten
{
    /// <summary>
    /// Foreground props for the 240 x 240 logical cat canvas.
    /// Call after CatArt.Draw, under the same canvas scaling transform.
    /// All reusable drawing resources are created once and frozen.
    /// </summary>
    public static class CatInteractionArt
    {
        private static readonly Brush Ink = MakeBrush(88, 60, 53);
        private static readonly Brush Cream = MakeBrush(255, 240, 207);
        private static readonly Brush Orange = MakeBrush(241, 179, 106);
        private static readonly Brush Teal = MakeBrush(116, 183, 173);
        private static readonly Brush TealLight = MakeBrush(169, 217, 203);
        private static readonly Brush TealShade = MakeBrush(82, 143, 137);
        private static readonly Brush Pink = MakeBrush(239, 164, 164);
        private static readonly Brush PinkLight = MakeBrush(255, 209, 196);
        private static readonly Brush Food = MakeBrush(172, 113, 69);
        private static readonly Brush Shadow = MakeBrush(35, 88, 60, 53);
        private static readonly Pen Outline = MakePen(Ink, 2.1);
        private static readonly Pen Fine = MakePen(Ink, 1.25);
        private static readonly Pen WoolLine = MakePen(PinkLight, 1.6);
        private static readonly Pen WoolShade = MakePen(Ink, 1.05);
        private static readonly Pen ThreadPen = MakePen(Pink, 1.5);
        private static readonly Pen SparkPen = MakePen(Orange, 1.4);

        private static readonly Geometry BowlBody = MakeGeometry("M 174,207 L 229,207 L 224,220 Q 222,223 217,223 L 185,223 Q 180,223 179,220 Z");
        private static readonly Geometry BowlRim = MakeGeometry("M 174,207 C 174,202 186,200 201.5,200 C 217,200 229,202 229,207 C 229,212 217,214 201.5,214 C 186,214 174,212 174,207 Z");
        private static readonly Geometry BowlInside = MakeGeometry("M 179,207 C 179,204 190,203 201.5,203 C 213,203 224,204 224,207 C 224,210 213,211 201.5,211 C 190,211 179,210 179,207 Z");
        private static readonly Geometry Fish = MakeGeometry("M 199,217 C 202,213 208,214 211,217 C 208,220 202,220 199,217 L 195,214 L 195,220 Z");
        private static readonly Geometry BowlGlint = MakeGeometry("M 183,214 L 186,219 M 217,214 L 221,213");
        private static readonly Geometry Ball = MakeGeometry("M 0,-13 C 7.5,-13 13,-7.5 13,0 C 13,7.5 7.5,13 0,13 C -7.5,13 -13,7.5 -13,0 C -13,-7.5 -7.5,-13 0,-13 Z");
        private static readonly Geometry BallThreads = MakeGeometry("M -11,-6 C -5,-4 3,3 7,10 M -7,-10 C 0,-7 7,0 11,6 M -1,-12 C 5,-8 10,-3 12,1 M -12,0 C -7,3 -3,8 -1,12 M -10,7 C -3,4 3,-2 7,-10 M -5,12 C 4,7 10,1 12,-4");
        private static readonly Geometry BallSeams = MakeGeometry("M -12,-2 C -6,-1 2,6 4,12 M -4,-12 C 3,-9 10,-2 12,3 M -10,8 C -2,5 4,-2 8,-10");
        private static readonly Geometry LooseThread = MakeGeometry("M 83,216 C 95,213 103,214 103,218 C 103,222 93,222 94,218 C 95,215 104,216 112,221");
        private static readonly Geometry Spark = MakeGeometry("M 0,-3 L 0,3 M -3,0 L 3,0");
        private static readonly Drawing BowlDrawing = MakeBowl();

        private static readonly Point[] FoodCenters = {
            new Point(184,206), new Point(190,204.5), new Point(196,207),
            new Point(203,205), new Point(210,207), new Point(218,205.5)
        };

        /// <summary>
        /// Draws only for mood "eat" or "play". Progress is one interaction,
        /// clamped to [0,1]; time is an animation clock in seconds.
        /// Eat bounds including outlines/shadow: x172..231, y192..226.
        /// Play bounds including outlines/thread: x40..113, y181..223.
        /// </summary>
        public static void Draw(DrawingContext dc, double time, string mood, double progress)
        {
            if (dc == null || (mood != "eat" && mood != "play")) return;
            if (Double.IsNaN(time) || Double.IsInfinity(time)) time = 0;
            if (Double.IsNaN(progress) || Double.IsInfinity(progress)) progress = 0;
            progress = Math.Max(0, Math.Min(1, progress));
            // Keep trigonometric arguments small even for very long uptime.
            time = time % 3600;
            double visibility = Math.Min(1, Math.Min(progress / .10, (1 - progress) / .12));
            if (visibility <= 0) return;

            dc.PushOpacity(visibility);
            if (mood == "eat") DrawFood(dc, time, progress);
            else DrawBall(dc, time, progress);
            dc.Pop();
        }

        private static void DrawFood(DrawingContext dc, double time, double progress)
        {
            dc.DrawDrawing(BowlDrawing);
            int remaining = Math.Max(1, 6 - (int)(progress * 5.5));
            for (int i = 0; i < remaining; i++)
                dc.DrawEllipse(Food, Fine, FoodCenters[i], 2.45, 1.75);

            // A tiny rising crumb gives a gentle eating cue without covering
            // the cat's face. It stays beside the bowl throughout the action.
            if (progress > .15 && progress < .85)
            {
                double crumb = (time * 2.2) % 1;
                dc.PushOpacity(Math.Sin(crumb * Math.PI) * .8);
                dc.DrawEllipse(Orange, null, new Point(221 + Math.Sin(crumb * Math.PI) * 3, 200 - crumb * 7), 1.6, 1.4);
                dc.Pop();
            }
        }

        private static void DrawBall(DrawingContext dc, double time, double progress)
        {
            double phase = time * 4.7;
            double lift = Math.Abs(Math.Sin(phase));
            double x = 64 + Math.Sin(progress * Math.PI * 3) * 9;
            double y = 204 - lift * 8;
            dc.DrawEllipse(Shadow, null, new Point(x, 219), 12 - lift * 2, 2.4);
            dc.DrawGeometry(null, ThreadPen, LooseThread);
            dc.DrawLine(ThreadPen, new Point(x + 8, y + 9), new Point(83, 216));

            dc.PushTransform(new TranslateTransform(x, y));
            dc.PushTransform(new RotateTransform(Math.Sin(progress * Math.PI * 3) * 65 + Math.Sin(phase) * 7));
            dc.DrawGeometry(Pink, Outline, Ball);
            dc.DrawGeometry(null, WoolLine, BallThreads);
            dc.DrawGeometry(null, WoolShade, BallSeams);
            dc.DrawEllipse(PinkLight, null, new Point(-5, -7), 2.2, 1.5);
            dc.Pop();
            dc.Pop();

            if (lift > .76)
            {
                dc.PushOpacity((lift - .76) / .24 * .65);
                dc.PushTransform(new TranslateTransform(x + 19, 187));
                dc.DrawGeometry(null, SparkPen, Spark);
                dc.Pop();
                dc.Pop();
            }
        }

        private static Drawing MakeBowl()
        {
            DrawingGroup drawing = new DrawingGroup();
            using (DrawingContext dc = drawing.Open())
            {
                dc.DrawEllipse(Shadow, null, new Point(201.5, 223), 29, 2.3);
                dc.DrawGeometry(Teal, Outline, BowlBody);
                dc.DrawGeometry(TealLight, Outline, BowlRim);
                dc.DrawGeometry(TealShade, null, BowlInside);
                dc.DrawGeometry(Cream, Fine, Fish);
                dc.DrawEllipse(Ink, null, new Point(207.5, 216.5), .65, .65);
                dc.DrawGeometry(null, MakePen(TealLight, 1.8), BowlGlint);
            }
            drawing.Freeze();
            return drawing;
        }

        private static Brush MakeBrush(byte red, byte green, byte blue)
        { return MakeBrush(255, red, green, blue); }

        private static Brush MakeBrush(byte alpha, byte red, byte green, byte blue)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Pen MakePen(Brush brush, double width)
        {
            Pen pen = new Pen(brush, width);
            pen.StartLineCap = PenLineCap.Round;
            pen.EndLineCap = PenLineCap.Round;
            pen.LineJoin = PenLineJoin.Round;
            pen.Freeze();
            return pen;
        }

        private static Geometry MakeGeometry(string path)
        {
            Geometry geometry = Geometry.Parse(path);
            geometry.Freeze();
            return geometry;
        }
    }
}

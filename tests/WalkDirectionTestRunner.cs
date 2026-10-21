using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopKitten;

internal static class WalkDirectionTestRunner
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static PetWindow pet;
    private static FrameworkElement canvas;
    private static int passed;
    private static int failed;
    private static readonly List<PreviewRow> preview = new List<PreviewRow>();

    private sealed class PreviewRow
    {
        internal double Size;
        internal RenderTargetBitmap Idle, Left, Right;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        Application app = null;
        try
        {
            string root = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);
            Check(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar) == root,
                "The runner must use its isolated build directory.");
            Check(!File.Exists(Path.Combine(root, "settings.txt")), "The test must start without existing settings.");
            app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            pet = new PetWindow();
            pet.Show();
            pet.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            ((DispatcherTimer)Get("timer")).Stop();
            canvas = (FrameworkElement)Get("canvas");
            Set("roaming", false);
            Set("bubbleUntil", -1.0);
            Set("Pulse", 0.0);
            Set("Bounce", 0.0);

            foreach (double size in new double[] { .9, .68 })
            {
                Call("SetSize", size);
                foreach (double time in new double[] { .05, .20, .40, .58 })
                {
                    try
                    {
                        TestSample(size, time);
                        passed++;
                    }
                    catch (Exception error)
                    {
                        failed++;
                        Console.Error.WriteLine("FAIL size={0} time={1}: {2}", size.ToString(CultureInfo.InvariantCulture), time.ToString(CultureInfo.InvariantCulture), error);
                    }
                }
            }
            if (args.Length > 1)
            {
                SavePreview(args[1]);
                Console.WriteLine("Preview: " + args[1]);
            }
        }
        catch (Exception error)
        {
            failed++;
            Console.Error.WriteLine("FAIL: walk test setup - " + error);
        }
        finally
        {
            if (pet != null)
            {
                try { pet.Close(); }
                catch (Exception error) { failed++; Console.Error.WriteLine("FAIL: window cleanup - " + error); }
            }
            if (app != null) app.Shutdown();
        }
        Console.WriteLine("Walk direction rendering tests: {0} passed, {1} failed.", passed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static object Get(string name)
    {
        FieldInfo field = typeof(PetWindow).GetField(name, Fields);
        if (field == null) throw new MissingFieldException(typeof(PetWindow).FullName, name);
        return field.GetValue(pet);
    }

    private static void Set(string name, object value)
    {
        FieldInfo field = typeof(PetWindow).GetField(name, Fields);
        if (field == null) throw new MissingFieldException(typeof(PetWindow).FullName, name);
        field.SetValue(pet, value);
    }

    private static void Call(string name, params object[] values)
    {
        MethodInfo method = typeof(PetWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null) throw new MissingMethodException(typeof(PetWindow).FullName, name);
        try { method.Invoke(pet, values); }
        catch (TargetInvocationException error) { throw error.InnerException ?? error; }
    }

    private static RenderTargetBitmap Render(double size, double time, string mood, double direction)
    {
        Set("Time", time);
        Set("Mood", mood);
        Set("direction", direction);
        canvas.InvalidateVisual();
        canvas.UpdateLayout();
        // 96 DPI produces exactly the pet's normal DIP size, with no enlargement.
        RenderTargetBitmap image = new RenderTargetBitmap((int)Math.Ceiling(240 * size), (int)Math.Ceiling(296 * size), 96, 96, PixelFormats.Pbgra32);
        image.Render(canvas);
        image.Freeze();
        return image;
    }

    private static double NoseCentroid(RenderTargetBitmap image, double size)
    {
        int stride = image.PixelWidth * 4;
        byte[] pixels = new byte[stride * image.PixelHeight];
        image.CopyPixels(pixels, stride, 0);
        // Restrict the RGB(197,124,118) measurement to the head, excluding other art.
        int x0 = Math.Max(0, (int)Math.Floor(25 * size));
        int x1 = Math.Min(image.PixelWidth, (int)Math.Ceiling(215 * size));
        int y0 = Math.Max(0, (int)Math.Floor(140 * size));
        int y1 = Math.Min(image.PixelHeight, (int)Math.Ceiling(184 * size));
        double sum = 0;
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int p = y * stride + x * 4;
                if (pixels[p + 3] >= 250 && Math.Abs(pixels[p + 2] - 197) <= 2
                    && Math.Abs(pixels[p + 1] - 124) <= 2 && Math.Abs(pixels[p] - 118) <= 2)
                {
                    sum += x + .5;
                    count++;
                }
            }
        }
        Check(count >= 3, "Could not find enough nose-color pixels in the actual PetCanvas head region.");
        return sum / count;
    }

    private static void TestSample(double size, double time)
    {
        RenderTargetBitmap idle = Render(size, time, "idle", 1);
        RenderTargetBitmap left = Render(size, time, "walk", -1);
        RenderTargetBitmap right = Render(size, time, "walk", 1);
        if (Math.Abs(time - .20) < .00001)
            preview.Add(new PreviewRow { Size = size, Idle = idle, Left = left, Right = right });
        double center = 120 * size;
        double idleNose = NoseCentroid(idle, size);
        double leftNose = NoseCentroid(left, size);
        double rightNose = NoseCentroid(right, size);
        Console.WriteLine(String.Format(CultureInfo.InvariantCulture,
            "MEASURE size={0:0.00} time={1:0.00} center={2:0.00} nose idle={3:0.00} left={4:0.00} right={5:0.00}",
            size, time, center, idleNose, leftNose, rightNose));
        Check(Math.Abs(idleNose - center) <= 2 * size + 1, "Idle nose must remain centered.");
        Check(rightNose - center >= 25 * size, "Rightward walk must turn its nose to the right side of the head.");
        Check(center - leftNose >= 25 * size, "Leftward walk must turn its nose to the left side of the head.");
        Check(Math.Abs(leftNose + rightNose - 2 * center) <= 2, "Left and right nose positions must be approximately mirror-symmetric.");
        Console.WriteLine("PASS: both walking directions face their movement direction at native size.");
    }

    private static void Label(DrawingContext dc, string text, double x, double y, double fontSize, Brush brush)
    {
        FormattedText formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), fontSize, brush, 1.0);
        dc.DrawText(formatted, new Point(x, y));
    }

    private static void SavePreview(string path)
    {
        Check(preview.Count == 2, "Both native sizes must be available for the comparison preview.");
        const int width = 780, height = 700, cellWidth = 260, rowHeight = 300;
        DrawingVisual sheet = new DrawingVisual();
        using (DrawingContext dc = sheet.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(247, 244, 238)), null, new Rect(0, 0, width, height));
            Label(dc, "Actual PetCanvas | native sizes | four gait phases tested", 20, 12, 18, Brushes.SaddleBrown);
            string[] names = { "Idle: centered", "Walk left", "Walk right" };
            for (int col = 0; col < 3; col++) Label(dc, names[col], col * cellWidth + 30, 46, 17, Brushes.SaddleBrown);
            for (int row = 0; row < preview.Count; row++)
            {
                PreviewRow sample = preview[row];
                RenderTargetBitmap[] images = { sample.Idle, sample.Left, sample.Right };
                double y = 76 + row * rowHeight;
                for (int col = 0; col < images.Length; col++)
                {
                    RenderTargetBitmap image = images[col];
                    double x = col * cellWidth + (cellWidth - image.PixelWidth) / 2.0;
                    // Draw at 1 bitmap pixel per DIP; the test images are not scaled up.
                    dc.DrawImage(image, new Rect(x, y, image.PixelWidth, image.PixelHeight));
                    Label(dc, "size " + sample.Size.ToString("0.00", CultureInfo.InvariantCulture) + " | t=0.20s",
                        col * cellWidth + 30, y + 270, 13, Brushes.SaddleBrown);
                }
            }
        }
        RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(sheet);
        PngBitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream stream = File.Create(path)) encoder.Save(stream);
    }
}

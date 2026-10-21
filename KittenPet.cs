using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopKitten
{
    internal static class Program
    {
        internal const string WindowTitle = "糯米 · 小猫桌宠";
        internal static readonly string Folder = AppDomain.CurrentDomain.BaseDirectory;
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h, int m, IntPtr w, IntPtr l);

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--stop")
                {
                    IntPtr h = FindWindow(null, WindowTitle);
                    if (h != IntPtr.Zero) PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero);
                    return;
                }
                if (args.Length > 1 && args[0] == "--preview")
                {
                    RenderPreview(args[1]);
                    return;
                }
                using (Mutex mutex = new Mutex(false, "Local\\NuomiDesktopKitten.v1"))
                {
                    bool owned;
                    try { owned = mutex.WaitOne(0); } catch (AbandonedMutexException) { owned = true; }
                    if (!owned)
                    {
                        IntPtr h = FindWindow(null, WindowTitle);
                        if (h != IntPtr.Zero) PostMessage(h, 0x8001, IntPtr.Zero, IntPtr.Zero);
                        return;
                    }
                    try
                    {
                        Application app = new Application();
                        app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
                        { Log(e.Exception); e.Handled = true; app.Shutdown(1); };
                        app.Run(new PetWindow());
                    }
                    finally { mutex.ReleaseMutex(); }
                }
            }
            catch (Exception ex)
            {
                Log(ex);
                MessageBox.Show("小猫启动失败。详细信息已保存在 error.log。\n" + ex.Message, "小猫桌宠", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        internal static void Log(Exception ex)
        { try { File.AppendAllText(Path.Combine(Folder, "error.log"), DateTime.Now.ToString("s") + " " + ex + Environment.NewLine); } catch { } }

        private static void RenderPreview(string path)
        {
            DrawingVisual visual = new DrawingVisual();
            string[] moods = { "idle", "walk", "happy", "sleep", "jump", "drag", "eat", "play" };
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(247, 244, 238)), null, new Rect(0, 0, 960, 560));
                for (int i = 0; i < moods.Length; i++)
                {
                    dc.PushTransform(new TranslateTransform((i % 4) * 240, (i / 4) * 280));
                    CatArt.Draw(dc, 1.4 + i * .37, moods[i], .8, moods[i] == "happy" ? 1 : 0, 1);
                    CatInteractionArt.Draw(dc, 1.4 + i * .37, moods[i], .35);
                    FormattedText text = new FormattedText(moods[i], CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.SaddleBrown, 1.0);
                    dc.DrawText(text, new Point(120 - text.Width / 2, 245));
                    dc.Pop();
                }
            }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(960, 560, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            PngBitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }

    public sealed class PetWindow : Window
    {
        private readonly PetCanvas canvas;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Random random = new Random();
        private readonly Forms.NotifyIcon tray;
        private readonly System.Drawing.Icon trayIcon;
        private PetSettings preferences = new PetSettings();
        private double previous, nextAction = 10, walkUntil, petUntil, jumpUntil, bubbleUntil, nextStatus;
        private double interactionStart, interactionUntil;
        private string interaction;
        private double direction = -1, size = .9, dragLeft, dragTop;
        private Point downScreen;
        private bool pressed, dragging, dragged, asleep, roaming = true, menuOpen;
        private bool settingsErrorLogged, statusErrorLogged;
        private string bubble = "喵～我是糯米";
        private Rect area;
        internal string Mood = "idle";
        internal double Time, Pulse, Bounce;

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);

        public PetWindow()
        {
            Title = Program.WindowTitle;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            UseLayoutRounding = true;
            canvas = new PetCanvas(this);
            Content = canvas;
            LoadSettings();
            Width = 240 * size; Height = 296 * size;
            area = SystemParameters.WorkArea;
            PlaceAtHome();
            bubbleUntil = 6;
            SourceInitialized += delegate
            {
                IntPtr h = new WindowInteropHelper(this).Handle;
                SetWindowLong(h, -20, GetWindowLong(h, -20) | 0x08000000 | 0x00000080);
                HwndSource.FromHwnd(h).AddHook(WindowProc);
            };
            MouseLeftButtonDown += Down;
            MouseMove += Move;
            MouseLeftButtonUp += Up;
            LostMouseCapture += delegate { FinishPress(false); };
            MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            { e.Handled = true; ShowMenu(); };
            MouseEnter += delegate { Cursor = Cursors.Hand; };
            MouseLeave += delegate { Cursor = Cursors.Arrow; };
            timer = new DispatcherTimer(DispatcherPriority.Render);
            timer.Interval = TimeSpan.FromMilliseconds(33);
            timer.Tick += Tick;
            trayIcon = MakeIcon();
            tray = new Forms.NotifyIcon();
            tray.Icon = trayIcon;
            tray.Text = "糯米 · 小猫桌宠（双击找回，右键退出）";
            tray.Visible = true;
            tray.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(delegate { Home(); Say("喵，我在这里！", 4); })); };
            Forms.ContextMenuStrip trayMenu = new Forms.ContextMenuStrip();
            trayMenu.Items.Add("找回小猫", null, delegate { Dispatcher.BeginInvoke(new Action(delegate { Home(); Say("喵，我在这里！", 4); })); });
            trayMenu.Items.Add("喂点猫粮", null, delegate { Dispatcher.BeginInvoke(new Action(Feed)); });
            trayMenu.Items.Add("玩毛线球", null, delegate { Dispatcher.BeginInvoke(new Action(Play)); });
            trayMenu.Items.Add("睡觉 / 叫醒", null, delegate { Dispatcher.BeginInvoke(new Action(ToggleSleep)); });
            trayMenu.Items.Add(new Forms.ToolStripSeparator());
            trayMenu.Items.Add("退出小猫", null, delegate { Dispatcher.BeginInvoke(new Action(Close)); });
            tray.ContextMenuStrip = trayMenu;
            trayMenu.Opening += delegate { SetMenuOpen(true); };
            trayMenu.Closed += delegate { SetMenuOpen(false); };
            Closed += delegate
            {
                timer.Stop(); SaveSettings();
                tray.Visible = false; tray.Dispose(); trayMenu.Dispose(); trayIcon.Dispose();
                WriteStatus("closed");
            };
            Loaded += delegate { RestorePosition(); previous = clock.Elapsed.TotalSeconds; timer.Start(); WriteStatus("running"); };
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr w, IntPtr l, ref bool handled)
        {
            if (msg == 0x8001) { Home(); Say("喵，我在这里！", 4); handled = true; }
            // A display, DPI or work-area change can otherwise leave a pet offscreen.
            if (msg == 0x007E || msg == 0x02E0 || (msg == 0x001A && w.ToInt64() == 47))
                Dispatcher.BeginInvoke(new Action(EnsureVisible));
            return IntPtr.Zero;
        }

        private void Home()
        {
            FinishPress(false); CancelActivities(); PlaceAtHome(); SaveSettings();
        }

        private void PlaceAtHome()
        {
            area = SystemParameters.WorkArea;
            Left = area.Right - Width - 30;
            Top = area.Bottom - Height + 14 * size;
            walkUntil = 0;
            Clamp();
        }

        private void RestorePosition()
        {
            if (preferences.Left.HasValue && preferences.Top.HasValue && TryUseArea(preferences.Left.Value, preferences.Top.Value))
            { Left = preferences.Left.Value; Top = preferences.Top.Value; Clamp(); }
            else EnsureVisible();
        }

        private void EnsureVisible()
        {
            if (TryUseArea(Left, Top)) Clamp();
            else PlaceAtHome();
        }

        private bool TryUseArea(double left, double top)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return false;
            Matrix from = source.CompositionTarget.TransformFromDevice;
            Point center = new Point(left + Width / 2, top + 180 * size);
            foreach (Forms.Screen screen in Forms.Screen.AllScreens)
            {
                System.Drawing.Rectangle bounds = screen.WorkingArea;
                Rect candidate = new Rect(from.Transform(new Point(bounds.Left, bounds.Top)), from.Transform(new Point(bounds.Right, bounds.Bottom)));
                if (candidate.Contains(center)) { area = candidate; return true; }
            }
            return false;
        }

        private void UpdateArea()
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return;
            Point center = PointToScreen(new Point(Width / 2, Height / 2));
            Forms.Screen screen = Forms.Screen.FromPoint(new System.Drawing.Point((int)center.X, (int)center.Y));
            System.Drawing.Rectangle bounds = screen.WorkingArea;
            Matrix from = source.CompositionTarget.TransformFromDevice;
            Point tl = from.Transform(new Point(bounds.Left, bounds.Top));
            Point br = from.Transform(new Point(bounds.Right, bounds.Bottom));
            area = new Rect(tl, br);
        }

        private void Clamp()
        {
            Left = Math.Max(area.Left - 20 * size, Math.Min(area.Right - Width + 20 * size, Left));
            Top = Math.Max(area.Top, Math.Min(area.Bottom - Height + 14 * size, Top));
        }

        private void Tick(object sender, EventArgs e)
        {
            Time = clock.Elapsed.TotalSeconds;
            double dt = Math.Min(.08, Time - previous); previous = Time;
            bool walking = false;
            if (Time >= interactionUntil) interaction = null;
            if (!pressed && !asleep && !menuOpen && Time >= petUntil && Time >= jumpUntil && interaction == null && roaming)
            {
                if (Time >= nextAction)
                {
                    UpdateArea(); direction = random.Next(2) == 0 ? -1 : 1;
                    walkUntil = Time + 2.5 + random.NextDouble() * 3;
                    nextAction = walkUntil + 7 + random.NextDouble() * 10;
                }
                if (Time < walkUntil)
                {
                    walking = true;
                    Left += direction * 33 * dt;
                    if (Left <= area.Left - 19 * size) direction = 1;
                    if (Left >= area.Right - Width + 19 * size) direction = -1;
                    Clamp();
                }
            }
            Mood = dragging ? "drag" : asleep ? "sleep" : Time < jumpUntil ? "jump" : interaction != null ? interaction : Time < petUntil ? "happy" : walking ? "walk" : "idle";
            Pulse = Math.Max(0, Math.Min(1, petUntil - Time));
            Bounce = Time < jumpUntil ? Math.Sin(Math.PI * Math.Max(0, Math.Min(1, (1 - (jumpUntil - Time) / .8)))) * 33 : 0;
            canvas.InvalidateVisual();
            if (Time > nextStatus) { WriteStatus("running"); nextStatus = Time + 3; }
        }

        private Point ScreenMouse()
        {
            return PointToScreen(Mouse.GetPosition(this));
        }
        private void Down(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) { Jump(); e.Handled = true; return; }
            downScreen = ScreenMouse(); dragLeft = Left; dragTop = Top;
            pressed = true; dragging = false; dragged = false;
            CaptureMouse(); e.Handled = true;
        }
        private void Move(object sender, MouseEventArgs e)
        {
            if (!pressed) return;
            if (e.LeftButton != MouseButtonState.Pressed) { FinishPress(false); return; }
            Vector delta = ScreenMouse() - downScreen;
            PresentationSource source = PresentationSource.FromVisual(this);
            if (source == null || source.CompositionTarget == null) return;
            delta = source.CompositionTarget.TransformFromDevice.Transform(delta);
            if (!dragged && (Math.Abs(delta.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(delta.Y) >= SystemParameters.MinimumVerticalDragDistance))
            { dragged = true; dragging = true; CancelActivities(); }
            if (dragged) { Left = dragLeft + delta.X; Top = dragTop + delta.Y; }
        }
        private void Up(object sender, MouseButtonEventArgs e)
        {
            if (!pressed) return;
            FinishPress(true);
            e.Handled = true;
        }

        private void FinishPress(bool allowPet)
        {
            if (!pressed) return;
            bool moved = dragged;
            pressed = false; dragging = false; dragged = false;
            if (IsMouseCaptured) ReleaseMouseCapture();
            if (moved)
            { UpdateArea(); Clamp(); Say("换个地方陪你～", 3); nextAction = Time + 12; walkUntil = 0; SaveSettings(); }
            else if (allowPet) Pet();
        }

        private void CancelActivities()
        { petUntil = 0; jumpUntil = 0; walkUntil = 0; interactionUntil = 0; interaction = null; }

        private void Pet()
        {
            CancelActivities(); asleep = false; petUntil = Time + 2.8; nextAction = Time + 10;
            string[] words = { "呼噜呼噜～", "最喜欢摸摸啦", "喵呜，陪着你", "今天也辛苦啦" };
            Say(words[random.Next(words.Length)], 3);
        }
        private void Jump()
        { CancelActivities(); asleep = false; jumpUntil = Time + .8; nextAction = Time + 10; Say("喵！", 1.6); }
        private void ToggleSleep()
        { CancelActivities(); asleep = !asleep; nextAction = Time + 10; Say(asleep ? "眯一会儿…" : "睡醒啦，喵～", 3); }
        private void Feed() { StartInteraction("eat", 5, "开饭啦，啊呜～"); }
        private void Play() { StartInteraction("play", 6, "小球球，别跑～"); }
        private void StartInteraction(string mood, double duration, string words)
        {
            CancelActivities(); asleep = false;
            interaction = mood; interactionStart = Time; interactionUntil = Time + duration;
            nextAction = interactionUntil + 8; Say(words, duration);
        }
        private void Say(string text, double seconds)
        { bubble = text; bubbleUntil = clock.Elapsed.TotalSeconds + seconds; canvas.InvalidateVisual(); }
        private void SetSize(double value)
        {
            double bottom = Top + Height, center = Left + Width / 2;
            size = value; Width = 240 * size; Height = 296 * size;
            Left = center - Width / 2; Top = bottom - Height;
            UpdateArea(); Clamp(); SaveSettings();
        }

        private MenuItem Item(string label, Action action)
        {
            MenuItem item = new MenuItem(); item.Header = label;
            item.Click += delegate { action(); };
            return item;
        }
        private void ShowMenu()
        {
            FinishPress(false);
            ContextMenu menu = new ContextMenu();
            menu.FontFamily = new FontFamily("Microsoft YaHei UI"); menu.FontSize = 13;
            menu.Items.Add(Item("摸摸糯米", Pet));
            menu.Items.Add(Item("跳一下", Jump));
            menu.Items.Add(Item("喂点猫粮", Feed));
            menu.Items.Add(Item("玩毛线球", Play));
            menu.Items.Add(new Separator());
            MenuItem roam = Item("自由走动", delegate { roaming = !roaming; walkUntil = 0; nextAction = Time + 3; SaveSettings(); });
            roam.IsCheckable = true; roam.IsChecked = roaming; menu.Items.Add(roam);
            menu.Items.Add(Item(asleep ? "叫醒小猫" : "睡一会儿", ToggleSleep));
            MenuItem sizes = new MenuItem(); sizes.Header = "小猫大小";
            sizes.Items.Add(SizeItem("小", .68));
            sizes.Items.Add(SizeItem("中", .9));
            sizes.Items.Add(SizeItem("大", 1.15));
            menu.Items.Add(sizes);
            MenuItem top = Item("置顶显示", delegate { Topmost = !Topmost; SaveSettings(); });
            top.IsCheckable = true; top.IsChecked = Topmost; menu.Items.Add(top);
            menu.Items.Add(Item("回到右下角", delegate { Home(); Say("喵，我回来啦", 3); }));
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("退出小猫", Close));
            menu.Opened += delegate { SetMenuOpen(true); };
            menu.Closed += delegate { SetMenuOpen(false); };
            menu.PlacementTarget = canvas;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            canvas.ContextMenu = menu; menu.IsOpen = true;
        }

        private MenuItem SizeItem(string label, double value)
        {
            MenuItem item = Item(label, delegate { SetSize(value); });
            item.IsCheckable = true; item.IsChecked = Math.Abs(size - value) < .01;
            return item;
        }

        private void SetMenuOpen(bool open)
        {
            menuOpen = open; walkUntil = 0; nextAction = Math.Max(nextAction, Time + 7);
            if (open) FinishPress(false);
        }

        private void LoadSettings()
        {
            try
            {
                preferences = PetSettings.Load(Path.Combine(Program.Folder, "settings.txt"));
                size = preferences.Size; roaming = preferences.Roaming; Topmost = preferences.Topmost;
            }
            catch (Exception ex) { Program.Log(ex); }
        }
        private void SaveSettings()
        {
            try
            {
                preferences.Size = size; preferences.Roaming = roaming; preferences.Topmost = Topmost;
                preferences.Left = Left; preferences.Top = Top;
                preferences.Save(Path.Combine(Program.Folder, "settings.txt"));
                settingsErrorLogged = false;
            }
            catch (Exception ex) { if (!settingsErrorLogged) { Program.Log(ex); settingsErrorLogged = true; } }
        }
        private void WriteStatus(string status)
        {
            string path = Path.Combine(Program.Folder, "status.json"), temporary = path + ".tmp";
            try
            {
                string json = "{\"status\":\"" + status + "\",\"pid\":" + Process.GetCurrentProcess().Id + ",\"mood\":\"" + Mood + "\",\"seconds\":" + Time.ToString("F1", CultureInfo.InvariantCulture) + ",\"x\":" + Left.ToString("F1", CultureInfo.InvariantCulture) + ",\"y\":" + Top.ToString("F1", CultureInfo.InvariantCulture) + ",\"roaming\":" + (roaming ? "true" : "false") + ",\"size\":" + size.ToString(CultureInfo.InvariantCulture) + ",\"topmost\":" + (Topmost ? "true" : "false") + ",\"updatedAt\":\"" + DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\"}";
                File.WriteAllText(temporary, json);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                statusErrorLogged = false;
            }
            catch (Exception ex) { if (!statusErrorLogged) { Program.Log(ex); statusErrorLogged = true; } }
            finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
        }
        private System.Drawing.Icon MakeIcon()
        {
            DrawingVisual v = new DrawingVisual();
            using (DrawingContext dc = v.RenderOpen()) { dc.PushTransform(new ScaleTransform(.267, .267)); CatArt.Draw(dc, 1, "idle", 0, 0, 1); dc.Pop(); }
            RenderTargetBitmap bmp = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32); bmp.Render(v);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (MemoryStream stream = new MemoryStream())
            {
                encoder.Save(stream); stream.Position = 0;
                using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(stream))
                {
                    IntPtr handle = bitmap.GetHicon();
                    try { using (System.Drawing.Icon icon = System.Drawing.Icon.FromHandle(handle)) return (System.Drawing.Icon)icon.Clone(); }
                    finally { DestroyIcon(handle); }
                }
            }
        }

        private sealed class PetCanvas : FrameworkElement
        {
            private readonly PetWindow pet;
            public PetCanvas(PetWindow parent) { pet = parent; }
            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                dc.PushTransform(new ScaleTransform(pet.size, pet.size));
                if (pet.Time < pet.bubbleUntil)
                {
                    Brush paper = new SolidColorBrush(Color.FromArgb(248, 255, 253, 246));
                    Pen outline = new Pen(new SolidColorBrush(Color.FromRgb(211, 186, 159)), 1.2);
                    FormattedText text = new FormattedText(pet.bubble, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, new Typeface("Microsoft YaHei UI"), 13, new SolidColorBrush(Color.FromRgb(104, 73, 58)), 1.0);
                    double width = Math.Min(226, Math.Max(82, text.Width + 28));
                    dc.DrawRoundedRectangle(paper, outline, new Rect((240 - width) / 2, 5, width, 35), 15, 15);
                    StreamGeometry tail = new StreamGeometry();
                    using (StreamGeometryContext g = tail.Open()) { g.BeginFigure(new Point(115, 40), true, true); g.LineTo(new Point(120, 46), true, false); g.LineTo(new Point(126, 40), true, false); }
                    dc.DrawGeometry(paper, null, tail);
                    dc.DrawText(text, new Point((240 - text.Width) / 2, 12));
                }
                dc.PushTransform(new TranslateTransform(0, 52 - pet.Bounce));
                bool mirror = pet.Mood == "walk" && pet.direction < 0;
                if (mirror) dc.PushTransform(new ScaleTransform(-1, 1, 120, 120));
                CatArt.Draw(dc, pet.Time, pet.Mood, pet.Mood == "walk" ? 1 : 0, pet.Pulse, 1);
                double progress = pet.interactionUntil > pet.interactionStart
                    ? (pet.Time - pet.interactionStart) / (pet.interactionUntil - pet.interactionStart) : 0;
                CatInteractionArt.Draw(dc, pet.Time, pet.Mood, progress);
                if (mirror) dc.Pop();
                dc.Pop(); dc.Pop();
            }
            protected override HitTestResult HitTestCore(PointHitTestParameters parameters)
            {
                Point p = parameters.HitPoint;
                // Keep empty space around the cat available to the desktop.
                double x = p.X / pet.size, y = p.Y / pet.size;
                if (x >= 30 && x <= (pet.Mood == "eat" ? 233 : 211) && y >= 85 - pet.Bounce && y <= 282) return new PointHitTestResult(this, p);
                return null;
            }
        }
    }
}

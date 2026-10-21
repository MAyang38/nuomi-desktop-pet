using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopKitten;
using Forms = System.Windows.Forms;

internal static class BehaviorTestRunner
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static Application app;
    private static PetWindow pet;
    private static string root;
    private static bool closed;
    private static int passed;
    private static int failed;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            root = Path.GetFullPath(args[0]);
            Check(Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar)
                == root.TrimEnd(Path.DirectorySeparatorChar), "The runner must use its isolated build directory.");
            Check(!File.Exists(SettingsPath), "An isolated run must start without settings.txt.");
            app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            pet = new PetWindow();
            pet.Show();
            pet.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(delegate { }));
            ((DispatcherTimer)Get("timer")).Stop();
            Set("roaming", false);
            Tick();

            Run("interaction changes cancel the previous activity", TestActivities);
            Run("expired interaction returns to idle", TestInteractionExpiry);
            Run("menu open and close preserve the interaction rest interval", TestMenuTiming);
            Run("lost capture finishes a drag and saves its position", TestFinishPress);
            Run("restore and size changes keep the kitten visible", TestPlacement);
            Run("Home saves position and preferences", TestHome);
            Run("status replacement stays complete JSON with no temporary file", TestStatus);
            Run("Close saves settings and closed status", delegate
            {
                pet.Close();
                closed = true;
                CheckSavedSettings();
                CheckStatus("closed");
                CheckNoTemporaryFiles();
            });
        }
        catch (Exception error)
        {
            failed++;
            Console.Error.WriteLine("FAIL: test setup - " + error);
        }
        finally
        {
            if (pet != null && !closed)
            {
                try { pet.Close(); }
                catch (Exception error) { failed++; Console.Error.WriteLine("FAIL: window cleanup - " + error); }
            }
            if (app != null) app.Shutdown();
        }
        Console.WriteLine("Behavior integration tests: {0} passed, {1} failed.", passed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static string SettingsPath { get { return Path.Combine(root, "settings.txt"); } }

    private static void Run(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL: " + name + " - " + error); }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static object Get(string name)
    {
        FieldInfo field = typeof(PetWindow).GetField(name, InstanceFields);
        if (field == null) throw new MissingFieldException(typeof(PetWindow).FullName, name);
        return field.GetValue(pet);
    }

    private static void Set(string name, object value)
    {
        FieldInfo field = typeof(PetWindow).GetField(name, InstanceFields);
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

    private static void Tick() { Call("Tick", null, EventArgs.Empty); }
    private static double Number(string name) { return (double)Get(name); }
    private static void Near(double actual, double expected, string message)
    { Check(Math.Abs(actual - expected) < .000001, message + " Expected " + expected + ", got " + actual + "."); }

    private static void Mood(string expected)
    {
        Tick();
        Check((string)Get("Mood") == expected, "Expected mood " + expected + ", got " + Get("Mood") + ".");
    }

    private static void NoInteraction()
    {
        Check(Get("interaction") == null && Number("interactionUntil") == 0, "The previous interaction was not cleared.");
    }

    private static void TestActivities()
    {
        // Seed every conflicting activity before the sequence starts.
        Set("petUntil", Number("Time") + 99);
        Set("jumpUntil", Number("Time") + 99);
        Set("interaction", "play");
        Set("interactionUntil", Number("Time") + 99);
        Call("Feed");
        Mood("eat");
        Check(Number("petUntil") == 0 && Number("jumpUntil") == 0, "Feed retained pet/jump timers.");
        Check((string)Get("interaction") == "eat" && Number("interactionUntil") > Number("Time"), "Feed did not replace the previous interaction.");
        Call("Play");
        Mood("play");
        Check(Number("petUntil") == 0 && Number("jumpUntil") == 0, "Play retained pet/jump timers.");
        Check((string)Get("interaction") == "play" && Number("interactionUntil") > Number("Time"), "Play retained the eating interaction.");
        Call("ToggleSleep");
        Mood("sleep");
        NoInteraction();
        Check(Number("petUntil") == 0 && Number("jumpUntil") == 0, "Sleep retained pet/jump timers.");
        Call("Pet");
        Mood("happy");
        NoInteraction();
        Check(!(bool)Get("asleep") && Number("jumpUntil") == 0 && Number("petUntil") > Number("Time"), "Pet did not wake the kitten and start a fresh pet timer.");
        Call("Jump");
        Mood("jump");
        NoInteraction();
        Check(!(bool)Get("asleep") && Number("petUntil") == 0 && Number("jumpUntil") > Number("Time"), "Jump retained the previous pet timer.");
    }

    private static void TestInteractionExpiry()
    {
        Call("Feed");
        Set("interactionUntil", -1.0);
        Set("roaming", false);
        Mood("idle");
        Check(Get("interaction") == null, "Expired interaction was retained.");
    }

    private static void TestMenuTiming()
    {
        Call("Play");
        double nextAction = Number("nextAction");
        Call("SetMenuOpen", true);
        Check((bool)Get("menuOpen"), "Opening a menu did not pause roaming.");
        Near(Number("nextAction"), nextAction, "Opening a menu shortened the interaction rest interval.");
        Call("SetMenuOpen", false);
        Check(!(bool)Get("menuOpen"), "Closing a menu did not release the roaming pause.");
        Near(Number("nextAction"), nextAction, "Closing a menu shortened the interaction rest interval.");
        Mood("play");
        Set("interactionUntil", -1.0);
        Mood("idle");
    }

    private static void TestFinishPress()
    {
        Call("Home");
        pet.Left -= 12;
        Set("pressed", true);
        Set("dragged", true);
        Set("dragging", true);
        Call("FinishPress", false);
        Check(!(bool)Get("pressed") && !(bool)Get("dragging") && !(bool)Get("dragged"), "Lost capture left drag flags set.");
        Check(!pet.IsMouseCaptured, "The harness must never capture the global mouse.");
        Mood("idle");
        CheckSavedSettings();
        CheckNoTemporaryFiles();
    }

    private static Rect[] WorkingAreas()
    {
        PresentationSource source = PresentationSource.FromVisual(pet);
        Check(source != null && source.CompositionTarget != null, "The window has no presentation source.");
        Matrix fromDevice = source.CompositionTarget.TransformFromDevice;
        List<Rect> areas = new List<Rect>();
        foreach (Forms.Screen screen in Forms.Screen.AllScreens)
        {
            System.Drawing.Rectangle bounds = screen.WorkingArea;
            areas.Add(new Rect(fromDevice.Transform(new Point(bounds.Left, bounds.Top)), fromDevice.Transform(new Point(bounds.Right, bounds.Bottom))));
        }
        Check(areas.Count != 0, "No monitor work area is available.");
        return areas.ToArray();
    }

    private static double PixelTolerance()
    {
        PresentationSource source = PresentationSource.FromVisual(pet);
        Vector pixel = source.CompositionTarget.TransformFromDevice.Transform(new Vector(1, 1));
        return Math.Max(Math.Abs(pixel.X), Math.Abs(pixel.Y)) + .01;
    }

    private static void CheckVisible()
    {
        double size = Number("size");
        // The window includes transparent margins; test the kitten's visible body.
        Rect body = new Rect(pet.Left + 30 * size, pet.Top + 85 * size, 181 * size, 197 * size);
        foreach (Rect area in WorkingAreas())
        {
            Rect tolerant = area;
            // UseLayoutRounding can round the transparent window by one device pixel.
            tolerant.Inflate(PixelTolerance(), PixelTolerance());
            if (tolerant.Contains(body)) return;
        }
        throw new InvalidOperationException("The kitten's visible body is outside every monitor work area: " + body);
    }

    private static void TestPlacement()
    {
        PetSettings preferences = (PetSettings)Get("preferences");
        Rect workArea = WorkingAreas()[0];
        preferences.Left = workArea.Left + (workArea.Width - pet.Width) / 2;
        preferences.Top = workArea.Top + (workArea.Height - pet.Height) / 2;
        Call("RestorePosition");
        Check(Math.Abs(pet.Left - preferences.Left.Value) <= PixelTolerance(), "A valid saved left position changed beyond native pixel rounding.");
        Check(Math.Abs(pet.Top - preferences.Top.Value) <= PixelTolerance(), "A valid saved top position changed beyond native pixel rounding.");
        CheckVisible();
        preferences.Left = 10000000;
        preferences.Top = -10000000;
        Call("RestorePosition");
        CheckVisible();
        Check(pet.Left != preferences.Left.Value && pet.Top != preferences.Top.Value, "An offscreen saved position was retained.");
        preferences.Left = Double.MaxValue;
        preferences.Top = -Double.MaxValue;
        Call("RestorePosition");
        CheckVisible();
        Check(!Double.IsInfinity(pet.Left) && !Double.IsInfinity(pet.Top), "Extreme finite saved coordinates were assigned to the native window.");
        foreach (double size in new double[] { .68, 1.15 })
        {
            Call("SetSize", size);
            Check(Math.Abs(pet.Width - 240 * size) <= PixelTolerance(), "SetSize did not update width within native pixel rounding.");
            Check(Math.Abs(pet.Height - 296 * size) <= PixelTolerance(), "SetSize did not update height within native pixel rounding.");
            CheckVisible();
            CheckSavedSettings();
        }
    }

    private static void TestHome()
    {
        Set("roaming", false);
        pet.Topmost = false;
        Call("Play");
        Call("Home");
        Mood("idle");
        NoInteraction();
        CheckVisible();
        Rect area = (Rect)Get("area");
        Check(Math.Abs(pet.Left - (area.Right - pet.Width - 30)) <= PixelTolerance(), "Home did not return to the right edge within native pixel rounding.");
        Check(Math.Abs(pet.Top - (area.Bottom - pet.Height + 14 * Number("size"))) <= PixelTolerance(), "Home did not return to the bottom edge within native pixel rounding.");
        CheckSavedSettings();
        CheckNoTemporaryFiles();
    }

    private static void CheckSavedSettings()
    {
        Check(File.Exists(SettingsPath), "Settings were not saved to the isolated build directory.");
        string[] lines = File.ReadAllLines(SettingsPath);
        Check(lines.Length == 5, "Saved settings must have exactly five lines.");
        PetSettings saved = PetSettings.Load(SettingsPath);
        Near(saved.Size, Number("size"), "Saved size changed.");
        Check(saved.Roaming == (bool)Get("roaming") && saved.Topmost == pet.Topmost, "Saved boolean preferences changed.");
        Check(saved.Left.HasValue && saved.Top.HasValue, "Saved settings have no position.");
        Near(saved.Left.Value, pet.Left, "Saved left position changed.");
        Near(saved.Top.Value, pet.Top, "Saved top position changed.");
        Check(!(bool)Get("settingsErrorLogged"), "Saving settings logged an error.");
    }

    private static void TestStatus()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Set("Time", 123.4);
            Call("WriteStatus", "behavior-first");
            CheckStatus("behavior-first");
            Call("WriteStatus", "behavior-second");
            CheckStatus("behavior-second");
        }
        finally { CultureInfo.CurrentCulture = previous; }
        CheckNoTemporaryFiles();
        Check(!File.Exists(Path.Combine(root, "error.log")), "The integration run produced error.log.");
    }

    private static void CheckStatus(string expected)
    {
        string path = Path.Combine(root, "status.json");
        Check(File.Exists(path), "Status JSON was not written.");
        Dictionary<string, object> status = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        string[] fields = { "status", "pid", "mood", "seconds", "x", "y", "roaming", "size", "topmost", "updatedAt" };
        foreach (string field in fields) Check(status.ContainsKey(field), "Status JSON is missing " + field + ".");
        Check((string)status["status"] == expected, "Status replacement retained an older document.");
        Check(Convert.ToInt32(status["pid"], CultureInfo.InvariantCulture) == Process.GetCurrentProcess().Id, "Status has the wrong process ID.");
        Check((string)status["mood"] == (string)Get("Mood"), "Status mood is stale.");
        Check(Convert.ToDouble(status["seconds"], CultureInfo.InvariantCulture) == Number("Time"), "Status elapsed time is stale.");
        Check(Math.Abs(Convert.ToDouble(status["x"], CultureInfo.InvariantCulture) - pet.Left) <= .051, "Status x is stale.");
        Check(Math.Abs(Convert.ToDouble(status["y"], CultureInfo.InvariantCulture) - pet.Top) <= .051, "Status y is stale.");
        Check((bool)status["roaming"] == (bool)Get("roaming") && (bool)status["topmost"] == pet.Topmost, "Status boolean preferences are stale.");
        Near(Convert.ToDouble(status["size"], CultureInfo.InvariantCulture), Number("size"), "Status size is stale.");
        DateTime timestamp;
        Check(DateTime.TryParse((string)status["updatedAt"], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out timestamp), "Status timestamp is invalid.");
        Check(!(bool)Get("statusErrorLogged"), "Writing status logged an error.");
    }

    private static void CheckNoTemporaryFiles()
    {
        Check(Directory.GetFiles(root, "*.tmp").Length == 0, "A temporary settings/status file remains.");
    }
}

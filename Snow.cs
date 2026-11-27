// DesktopSnow - 바탕화면과 창 위에 눈이 내리고 쌓이는 프로그램 (Windows)
// 빌드: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:Snow.exe Snow.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

static class Native
{
    public const int WS_POPUP = unchecked((int)0x80000000);
    public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
                     WS_EX_TOPMOST = 0x8, WS_EX_NOACTIVATE = 0x8000000, WS_EX_APPWINDOW = 0x40000;
    public const int GWL_EXSTYLE = -20;

    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int W, H; public SIZE(int w, int h) { W = w; H = h; } }
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize; public int biWidth, biHeight; public ushort biPlanes, biBitCount;
        public uint biCompression, biSizeImage; public int biXPelsPerMeter, biYPelsPerMeter;
        public uint biClrUsed, biClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO { public int fIcon; public int xHotspot, yHotspot; public IntPtr hbmMask, hbmColor; }

    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsZoomed(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr h, int idx, int val);
    [DllImport("user32.dll")] public static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT r, int size);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int v, int size);
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFOHEADER bmi, uint usage, out IntPtr bits, IntPtr sec, uint off);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT dst, ref SIZE size,
        IntPtr hdcSrc, ref POINT src, int key, ref BLENDFUNCTION blend, int flags);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool GetPhysicalCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] public static extern bool GetMonitorInfo(IntPtr hmon, ref MONITORINFO mi);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr hmon, int type, out uint x, out uint y);
    [StructLayout(LayoutKind.Sequential)] public struct MONITORINFO { public int cbSize; public RECT rcMonitor, rcWork; public uint dwFlags; }
    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern IntPtr SetCapture(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SetCursor(IntPtr c);
    [DllImport("user32.dll")] public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO info);
    [DllImport("user32.dll")] public static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint action, uint param, string pv, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool SystemParametersInfo(uint action, uint param, StringBuilder pv, uint flags);
}

static class U
{
    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Max(0, Math.Min(1, t));
        return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                              (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }
    public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : (v > hi ? hi : v); }
    public static float D2(float x1, float y1, float x2, float y2) { float dx = x1 - x2, dy = y1 - y2; return dx * dx + dy * dy; }
    public static GraphicsPath RoundRect(float x, float y, float w, float h, float r)
    {
        var p = new GraphicsPath();
        r = Math.Max(0.01f, Math.Min(r, Math.Min(w, h) / 2));
        p.AddArc(x, y, r * 2, r * 2, 180, 90);
        p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        p.CloseFigure();
        return p;
    }
}

class Flake
{
    public float X, Y, VX, VY, Fall, R, Phase, Sway;
    public int Alpha;
    public bool Near;   // 앞쪽 눈송이(쌓임) / 뒤쪽 눈송이(배경, 통과)
    public bool Chunk;  // 떨어지거나 부서진 눈 조각 (재생성 안 함)
}

class Ledge
{
    public Rectangle Rect;  // 창의 실제 화면 영역 (윗변 위에 눈이 쌓임)
    public float[] H;       // 각 픽셀 열마다 쌓인 눈 높이
    public bool Ground;     // 작업표시줄/화면 바닥
    public int Z;           // 창 z-순서 (0 = 맨 위)
    public bool Seen;
    public float Max;
    public Ledge(Rectangle r, bool ground, float max)
    {
        Rect = r; Ground = ground; H = new float[Math.Max(1, r.Width)]; Max = max;
    }
}

class Ball
{
    public float X, Y, VX, VY, R, Rot;
    public int State;   // 0 떨어지는 중, 1 놓여 있음, 2 손에 들림
    public bool Thrown, Dead;
}

class Snowman
{
    public float X, Bottom, VX, VY, RB, RH, T, Tilt, Wave, Surprise, BlinkT, DanceT;
    public int Style, State;
    public bool Thrown, Dead;
}

class Sparkle { public float X, Y, Life, Size; }

class Plow
{
    public Ledge L;
    public float X, Load, T;
    public int Dir;
    public bool Dumped;
}

// 모니터 하나를 덮는 투명 창.
// 배율이 큰 모니터(예: 200%)에서는 DPI-unaware 창으로 만들어 낮은 해상도로 그리고 Windows(DWM)가 확대하게 함
// → 그리고 넘기는 픽셀 수가 크게 줄어듦
class Surface : NativeWindow
{
    public Rectangle Phys, Log;
    public float Sx = 1f, Scale = 1f;
    public bool Scaled;
    public IntPtr memDC, hBmp, oldBmp, bits;
    public Bitmap Canvas;
    public Graphics G;
    public readonly List<Rectangle> Dirty = new List<Rectangle>();
    readonly SnowOverlay owner;
    public static bool NoScale;   // 테스트용: 확대 없이 물리 해상도로

    public Surface(SnowOverlay owner, Rectangle phys, float scale)
    {
        this.owner = owner; Phys = phys; Scale = scale; Log = phys;
        IntPtr old = IntPtr.Zero;
        Scaled = scale > 1.01f && !NoScale;
        if (Scaled)
        {
            try
            {
                var c = new Native.POINT(phys.Left + phys.Width / 2, phys.Top + phys.Height / 2);
                IntPtr hmon = Native.MonitorFromPoint(c, 2);
                old = Native.SetThreadDpiAwarenessContext(new IntPtr(-1)); // UNAWARE
                var mi = new Native.MONITORINFO();
                mi.cbSize = Marshal.SizeOf(typeof(Native.MONITORINFO));
                if (Native.GetMonitorInfo(hmon, ref mi))
                    Log = Rectangle.FromLTRB(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.Right, mi.rcMonitor.Bottom);
                if (Log.Width >= phys.Width) Scaled = false;   // 가상화가 안 되면 일반 창
            }
            catch { Scaled = false; }
            if (!Scaled)
            {
                if (old != IntPtr.Zero) { Native.SetThreadDpiAwarenessContext(old); old = IntPtr.Zero; }
                Log = phys;
            }
        }
        var cp = new CreateParams();
        cp.Caption = "DesktopSnow";
        cp.Style = Native.WS_POPUP;
        cp.ExStyle = Native.WS_EX_LAYERED | Native.WS_EX_TRANSPARENT | Native.WS_EX_TOOLWINDOW |
                     Native.WS_EX_TOPMOST | Native.WS_EX_NOACTIVATE;
        cp.X = Log.X; cp.Y = Log.Y; cp.Width = Log.Width; cp.Height = Log.Height;
        CreateHandle(cp);
        if (old != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(old);
        Sx = (float)Log.Width / phys.Width;
        InitBuffer();
    }

    void InitBuffer()
    {
        var bmi = new Native.BITMAPINFOHEADER();
        bmi.biSize = (uint)Marshal.SizeOf(typeof(Native.BITMAPINFOHEADER));
        bmi.biWidth = Log.Width; bmi.biHeight = -Log.Height; bmi.biPlanes = 1; bmi.biBitCount = 32;
        IntPtr screen = Native.GetDC(IntPtr.Zero);
        hBmp = Native.CreateDIBSection(screen, ref bmi, 0, out bits, IntPtr.Zero, 0);
        memDC = Native.CreateCompatibleDC(screen);
        oldBmp = Native.SelectObject(memDC, hBmp);
        Native.ReleaseDC(IntPtr.Zero, screen);
        Canvas = new Bitmap(Log.Width, Log.Height, Log.Width * 4, PixelFormat.Format32bppPArgb, bits);
        G = Graphics.FromImage(Canvas);
        G.SmoothingMode = SmoothingMode.AntiAlias;
        G.ScaleTransform(Sx, Sx);   // 그리기 좌표 = 이 모니터 기준 물리 픽셀
    }

    public void Present()
    {
        G.Flush(FlushIntention.Sync);
        IntPtr old = IntPtr.Zero;
        if (Scaled) old = Native.SetThreadDpiAwarenessContext(new IntPtr(-1));
        var dst = new Native.POINT(Log.Left, Log.Top);
        var size = new Native.SIZE(Log.Width, Log.Height);
        var src = new Native.POINT(0, 0);
        var blend = new Native.BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
        Native.UpdateLayeredWindow(Handle, IntPtr.Zero, ref dst, ref size, memDC, ref src, 0, ref blend, 2);
        if (Scaled) Native.SetThreadDpiAwarenessContext(old);
    }

    public void SetClickThrough(bool on)
    {
        int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
        ex = on ? (ex | Native.WS_EX_TRANSPARENT) : (ex & ~Native.WS_EX_TRANSPARENT);
        Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
    }

    protected override void WndProc(ref Message m)
    {
        if (owner.SurfaceProc(this, ref m)) return;
        base.WndProc(ref m);
    }

    public void Destroy()
    {
        if (G != null) { G.Dispose(); G = null; }
        if (Canvas != null) { Canvas.Dispose(); Canvas = null; }
        if (memDC != IntPtr.Zero) { Native.SelectObject(memDC, oldBmp); Native.DeleteDC(memDC); memDC = IntPtr.Zero; }
        if (hBmp != IntPtr.Zero) { Native.DeleteObject(hBmp); hBmp = IntPtr.Zero; }
        DestroyHandle();
    }
}

class SnowOverlay
{
    readonly Random rnd = new Random();
    readonly List<Flake> flakes = new List<Flake>();
    readonly List<Ball> balls = new List<Ball>();
    readonly List<Snowman> men = new List<Snowman>();
    readonly List<Sparkle> sparkles = new List<Sparkle>();
    readonly List<Plow> plows = new List<Plow>();
    readonly Dictionary<IntPtr, Ledge> winLedges = new Dictionary<IntPtr, Ledge>();
    readonly List<Ledge> active = new List<Ledge>();
    readonly List<Ledge> grounds = new List<Ledge>();
    readonly List<Rectangle> occluders = new List<Rectangle>();
    readonly Native.EnumProc enumProc;
    readonly List<IntPtr> enumResult = new List<IntPtr>();
    readonly Timer timer = new Timer();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly SolidBrush[] flakeBrushes = new SolidBrush[256];
    readonly SolidBrush pileBrush = new SolidBrush(Color.FromArgb(252, 255, 255, 255));
    readonly SolidBrush pileShade = new SolidBrush(Color.FromArgb(70, 170, 190, 220));
    readonly Pen pileEdge = new Pen(Color.FromArgb(120, 150, 170, 200), 1.2f);
    readonly SolidBrush clearBrush = new SolidBrush(Color.Transparent);
    List<Rectangle> dirty;   // 지금 그리는 모니터의 지울 영역
    readonly List<KeyValuePair<long, PointF>> hist = new List<KeyValuePair<long, PointF>>();

    Rectangle vs;
    readonly List<Surface> surfaces = new List<Surface>();
    readonly HashSet<IntPtr> ownHandles = new HashSet<IntPtr>();
    Graphics g;              // 지금 그리는 모니터의 Graphics
    float curSx = 1f;
    bool displayDirty;
    IntPtr mittenCursor;
    float S = 1f;   // DPI 배율

    public int TargetCount = 180;
    public bool Accumulate = true;
    public bool PlayEnabled = true;
    public bool LowPower;            // 저전력 모드 (항상)
    public bool AutoBattery = true;  // 배터리 사용 중엔 자동 저전력
    bool onBattery;
    public bool EffectiveLowPower { get { return LowPower || (AutoBattery && onBattery); } }
    const int MaxObjects = 16;
    bool paused, clickThrough = true;
    float wind, windTarget;
    long lastTick;
    int frame;

    Ball heldBall;
    Snowman heldMan;
    float holdDX, holdDY, heldPrevX;
    PointF downPos;
    bool moved;

    public SnowOverlay()
    {
        enumProc = (h, l) => { enumResult.Add(h); return true; };
        for (int i = 0; i < 256; i++) flakeBrushes[i] = new SolidBrush(Color.FromArgb(i, 255, 255, 255));

        BuildSurfaces();
        float maxScale = 1f;
        foreach (var sf in surfaces) maxScale = Math.Max(maxScale, sf.Scale);
        mittenCursor = MakeMittenCursor(maxScale);
        BuildGrounds();
        for (int i = 0; i < TargetCount; i++) flakes.Add(NewFlake(true));

        timer.Interval = 30;
        timer.Tick += (s, e) => Tick();
        timer.Start();

        SystemEvents.SessionSwitch += (s, e) =>
        {
            if (e.Reason == SessionSwitchReason.SessionLock) timer.Stop();
            else if (e.Reason == SessionSwitchReason.SessionUnlock) { lastTick = 0; timer.Start(); }
        };
    }

    static float MonitorScale(Rectangle b)
    {
        try
        {
            IntPtr hmon = Native.MonitorFromPoint(new Native.POINT(b.Left + b.Width / 2, b.Top + b.Height / 2), 2);
            uint x, y;
            if (Native.GetDpiForMonitor(hmon, 0, out x, out y) == 0 && x > 0) return x / 96f;
        }
        catch { }
        return 1f;
    }

    void BuildSurfaces()
    {
        foreach (var sf in surfaces) sf.Destroy();
        surfaces.Clear();
        ownHandles.Clear();
        vs = SystemInformation.VirtualScreen;
        float primary = 1f;
        foreach (var scr in Screen.AllScreens)
        {
            float sc = MonitorScale(scr.Bounds);
            if (scr.Primary) primary = sc;
            var sf = new Surface(this, scr.Bounds, sc);
            surfaces.Add(sf);
            ownHandles.Add(sf.Handle);
            if (!clickThrough) sf.SetClickThrough(false);
            if (!paused) Native.ShowWindow(sf.Handle, 4); // SW_SHOWNOACTIVATE
        }
        S = primary;
    }

    // 그 위치가 속한 모니터의 배율 (눈송이·눈덩이 크기용)
    float ScaleAt(float x, float y)
    {
        foreach (var sf in surfaces)
            if (x >= sf.Phys.Left && x < sf.Phys.Right && y >= sf.Phys.Top && y < sf.Phys.Bottom) return sf.Scale;
        foreach (var sf in surfaces)
            if (x >= sf.Phys.Left && x < sf.Phys.Right) return sf.Scale;
        return S;
    }

    void BuildGrounds()
    {
        grounds.Clear();
        foreach (var s in Screen.AllScreens)
        {
            var wa = s.WorkingArea;
            grounds.Add(new Ledge(new Rectangle(wa.Left, wa.Bottom, wa.Width, 0), true, 20f * MonitorScale(s.Bounds)));
        }
    }

    public bool SurfaceProc(Surface sf, ref Message m)
    {
        switch (m.Msg)
        {
            case 0x007E: // WM_DISPLAYCHANGE - 다음 프레임에 다시 구성
                displayDirty = true;
                return false;
            case 0x0021: // WM_MOUSEACTIVATE - 클릭해도 다른 창의 포커스를 뺏지 않음
                m.Result = (IntPtr)3;
                return true;
            case 0x0020: // WM_SETCURSOR
                Native.SetCursor(mittenCursor);
                m.Result = (IntPtr)1;
                return true;
            case 0x0201: // WM_LBUTTONDOWN
                OnMouseDown();
                if (heldBall != null || heldMan != null) Native.SetCapture(sf.Handle);
                m.Result = IntPtr.Zero;
                return true;
            case 0x0202: // WM_LBUTTONUP
                OnMouseUp();
                Native.ReleaseCapture();
                m.Result = IntPtr.Zero;
                return true;
            case 0x0215: // WM_CAPTURECHANGED
                OnMouseUp();
                return false;
        }
        return false;
    }

    // ---------- 눈송이 ----------
    Flake NewFlake(bool anywhere)
    {
        var f = new Flake();
        f.X = vs.Left + (float)(rnd.NextDouble() * (vs.Width + 200) - 100);
        f.Y = anywhere ? vs.Top + (float)(rnd.NextDouble() * vs.Height) : vs.Top - (float)(rnd.NextDouble() * 40) - 5;
        float k = ScaleAt(f.X, Math.Max(f.Y, vs.Top));
        f.Near = rnd.NextDouble() < 0.7;
        if (f.Near)
        {
            f.R = (float)(1.6 + rnd.NextDouble() * 2.6) * k;
            f.Fall = (float)(0.9 + f.R / k * 0.35 + rnd.NextDouble() * 0.4) * k;
            f.Alpha = 215 + rnd.Next(40);
        }
        else
        {
            f.R = (float)(0.8 + rnd.NextDouble() * 0.9) * k;
            f.Fall = (float)(0.45 + rnd.NextDouble() * 0.45) * k;
            f.Alpha = 110 + rnd.Next(60);
        }
        f.VY = f.Fall;
        f.Phase = (float)(rnd.NextDouble() * Math.PI * 2);
        f.Sway = (float)(0.25 + rnd.NextDouble() * 0.55) * k;
        return f;
    }

    // ---------- 창 목록 ----------
    static readonly HashSet<string> skipClasses = new HashSet<string> {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd" };

    void RefreshWindows()
    {
        enumResult.Clear();
        Native.EnumWindows(enumProc, IntPtr.Zero);
        occluders.Clear();
        active.Clear();
        foreach (var l in winLedges.Values) l.Seen = false;

        var sb = new StringBuilder(64);
        foreach (var h in enumResult)
        {
            if (ownHandles.Contains(h) || !Native.IsWindowVisible(h) || Native.IsIconic(h)) continue;
            int cloaked;
            if (Native.DwmGetWindowAttribute(h, 14, out cloaked, 4) == 0 && cloaked != 0) continue;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            if ((ex & Native.WS_EX_TOOLWINDOW) != 0 && (ex & Native.WS_EX_APPWINDOW) == 0) continue;
            if ((ex & Native.WS_EX_TRANSPARENT) != 0 && (ex & Native.WS_EX_LAYERED) != 0) continue;
            if (Native.GetWindowTextLength(h) == 0) continue;
            sb.Length = 0;
            Native.GetClassName(h, sb, 64);
            if (skipClasses.Contains(sb.ToString())) continue;

            Native.RECT r;
            if (Native.DwmGetWindowAttribute(h, 9, out r, 16) != 0) Native.GetWindowRect(h, out r);
            var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
            if (rect.Width < 80 || rect.Height < 40) continue;

            int z = occluders.Count;
            occluders.Add(rect);

            // 최대화/전체화면 창(윗변이 모니터 맨 위)은 쌓이지 않음 - 가리기만 함
            bool ledgeOk = !Native.IsZoomed(h);
            if (ledgeOk)
                foreach (var s in Screen.AllScreens)
                    if (s.Bounds.Contains(rect.Left + rect.Width / 2, rect.Top + 1) && rect.Top <= s.Bounds.Top + 2) { ledgeOk = false; break; }

            Ledge led;
            bool known = winLedges.TryGetValue(h, out led);
            if (!ledgeOk)
            {
                if (known) { ShakeOff(led); winLedges.Remove(h); }
                continue;
            }
            if (!known) { led = new Ledge(rect, false, 14f * ScaleAt(rect.Left + rect.Width / 2f, rect.Top)); winLedges[h] = led; }
            else if (led.Rect != rect)
            {
                ShakeOff(led);                    // 창을 움직이면 눈이 떨어짐
                led.Rect = rect;
                led.H = new float[rect.Width];
            }
            led.Z = z;
            led.Seen = true;
            active.Add(led);
        }

        var gone = new List<IntPtr>();
        foreach (var kv in winLedges) if (!kv.Value.Seen) { ShakeOff(kv.Value); gone.Add(kv.Key); }
        foreach (var h in gone) winLedges.Remove(h);
    }

    bool Occluded(int z, float x, float y)
    {
        int ix = (int)x, iy = (int)y;
        for (int k = 0; k < z && k < occluders.Count; k++)
            if (occluders[k].Contains(ix, iy)) return true;
        return false;
    }

    // x 위치에서 fromY 아래쪽으로 가장 가까운 눈 표면
    float SurfaceUnder(float x, float fromY, out Ledge best)
    {
        best = null;
        float bs = float.MaxValue;
        for (int pass = 0; pass < 2; pass++)
        {
            var list = pass == 0 ? grounds : active;
            foreach (var L in list)
            {
                if (x < L.Rect.Left || x >= L.Rect.Right) continue;
                int idx = (int)x - L.Rect.Left;
                if (idx < 0 || idx >= L.H.Length) continue;
                float s = L.Rect.Top - L.H[idx];
                if (s < fromY || s >= bs) continue;
                if (!L.Ground && Occluded(L.Z, x, s - 1)) continue;
                bs = s; best = L;
            }
        }
        return bs;
    }

    // x 위치의 바닥(작업표시줄 위 쌓인 눈 표면). 모니터 밖이면 가장 가까운 바닥
    float FloorAt(float x)
    {
        Ledge best = null;
        float bestDist = float.MaxValue;
        foreach (var G in grounds)
        {
            float d = x < G.Rect.Left ? G.Rect.Left - x : (x >= G.Rect.Right ? x - G.Rect.Right + 1 : 0);
            if (d < bestDist) { bestDist = d; best = G; }
        }
        if (best == null) return vs.Bottom;
        int i = Math.Max(0, Math.Min(best.H.Length - 1, (int)x - best.Rect.Left));
        return best.Rect.Top - best.H[i];
    }

    void ShakeOff(Ledge l)
    {
        // 창 전체 폭에 고르게 나눠서 떨어뜨림 (조각 수가 많으면 일부만 만들고 대신 크게)
        const int cap = 450;
        int total = 0;
        for (int j = 0; j < l.H.Length; j += 3)
            if (l.H[j] >= 0.8f * S) total += (int)(l.H[j] / (4 * S)) + 1;
        if (total == 0) { Array.Clear(l.H, 0, l.H.Length); return; }
        double keep = Math.Min(1.0, (double)cap / total);
        float grow = (float)Math.Min(1.8, Math.Sqrt(1.0 / keep));

        for (int j = 0; j < l.H.Length; j += 3)
        {
            float h = l.H[j];
            if (h < 0.8f * S) continue;
            int n = (int)(h / (4 * S)) + 1;
            for (int k = 0; k < n; k++)
            {
                if (keep < 1.0 && rnd.NextDouble() > keep) continue;
                var f = new Flake();
                f.Near = true; f.Chunk = true;
                f.R = (float)(1.4 + rnd.NextDouble() * 1.8) * S * grow;
                f.X = l.Rect.Left + j + (float)(rnd.NextDouble() * 3);
                f.Y = l.Rect.Top - (float)(rnd.NextDouble() * h);
                f.VX = (float)(rnd.NextDouble() - 0.5) * 1.6f * S;
                f.VY = (float)(rnd.NextDouble() * 0.5) * S;
                f.Fall = (float)(2.2 + rnd.NextDouble() * 1.5) * S;
                f.Alpha = 245;
                f.Sway = 0.15f * S;
                flakes.Add(f);
            }
        }
        Array.Clear(l.H, 0, l.H.Length);
    }

    void Deposit(Ledge l, int i, float r)
    {
        int rad = (int)(r * 1.7f) + 1;
        for (int d = -rad; d <= rad; d++)
        {
            int j = i + d;
            if (j < 0 || j >= l.H.Length) continue;
            float t = (float)d / rad;
            l.H[j] = Math.Min(l.Max, l.H[j] + (1 - t * t) * r * 0.42f);
        }
    }

    // 눈더미가 자연스럽게 무너지며 경사를 이루도록
    static void Settle(Ledge l, bool forward)
    {
        var h = l.H; int n = h.Length;
        const float slope = 1.1f;
        if (n < 2) return;
        if (h[0] > slope) h[0] -= (h[0] - slope) * 0.5f;
        if (h[n - 1] > slope) h[n - 1] -= (h[n - 1] - slope) * 0.5f;
        int start = forward ? 0 : n - 2, end = forward ? n - 1 : -1, step = forward ? 1 : -1;
        for (int j = start; j != end; j += step)
        {
            float d = h[j] - h[j + 1];
            if (d > slope) { float t = (d - slope) * 0.5f; h[j] -= t; h[j + 1] += t; }
            else if (d < -slope) { float t = (-d - slope) * 0.5f; h[j] += t; h[j + 1] -= t; }
        }
    }

    // ---------- 부서짐 / 반짝임 ----------
    void Shatter(float x, float y, float r, float vx, float vy)
    {
        int n = (int)Math.Min(180, Math.Max(14, r * r / (2.5f * S * S)));
        for (int i = 0; i < n; i++)
        {
            var f = new Flake();
            f.Near = true; f.Chunk = true;
            f.R = (float)(1.2 + rnd.NextDouble() * 2.2) * S;
            f.X = x + (float)(rnd.NextDouble() - 0.5) * r * 1.4f;
            f.Y = y + (float)(rnd.NextDouble() - 0.5) * r * 1.4f;
            f.VX = vx * 0.25f + (float)(rnd.NextDouble() - 0.5) * 9f * S;
            f.VY = -(float)rnd.NextDouble() * 8f * S + Math.Min(0, vy * 0.1f);
            f.Fall = (float)(2.5 + rnd.NextDouble() * 1.5) * S;
            f.Alpha = 235 + rnd.Next(20);
            f.Sway = 0.1f * S;
            flakes.Add(f);
        }
        for (int i = 0; i < 5; i++) Spark(x + (float)(rnd.NextDouble() - 0.5) * r * 2, y + (float)(rnd.NextDouble() - 0.5) * r * 2, (4 + (float)rnd.NextDouble() * 4) * S);
    }

    void Spark(float x, float y, float size)
    {
        sparkles.Add(new Sparkle { X = x, Y = y, Life = 1f, Size = size });
    }

    // ---------- 마우스 ----------
    PointF CursorPos()
    {
        // 확대(DPI-unaware) 창의 메시지를 처리하는 중에는 Windows가 좌표를 논리 좌표로 바꿔 주므로,
        // 읽는 동안만 스레드를 Per-Monitor 모드로 바꿔서 항상 실제 픽셀 좌표를 얻음
        Native.POINT p;
        IntPtr old = IntPtr.Zero;
        try { old = Native.SetThreadDpiAwarenessContext(new IntPtr(-4)); } catch { }
        Native.GetCursorPos(out p);
        if (old != IntPtr.Zero) Native.SetThreadDpiAwarenessContext(old);
        return new PointF(p.X, p.Y);
    }

    void SetClickThrough(bool on)
    {
        if (on == clickThrough) return;
        foreach (var sf in surfaces) sf.SetClickThrough(on);
        clickThrough = on;
    }

    object HitObject(PointF p)
    {
        for (int i = men.Count - 1; i >= 0; i--)
        {
            var m = men[i];
            float bcY = m.Bottom - m.RB, hcY = m.Bottom - m.RB * 1.84f - m.RH * 0.85f;
            if (U.D2(p.X, p.Y, m.X, bcY) < m.RB * m.RB || U.D2(p.X, p.Y, m.X, hcY) < m.RH * m.RH * 1.4f) return m;
        }
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            var b = balls[i];
            float r = b.R + 2 * S;
            if (U.D2(p.X, p.Y, b.X, b.Y) < r * r) return b;
        }
        return null;
    }

    bool PileHit(Ledge L, PointF p)
    {
        if (p.X < L.Rect.Left || p.X >= L.Rect.Right) return false;
        int i = (int)p.X - L.Rect.Left;
        if (i < 0 || i >= L.H.Length) return false;
        float h = L.H[i];
        if (h < 3f * S) return false;
        return p.Y >= L.Rect.Top - h - 2f * S && p.Y <= L.Rect.Top + 1f * S;
    }

    Ledge PileAt(PointF p)
    {
        foreach (var L in grounds) if (PileHit(L, p)) return L;
        foreach (var L in active) if (PileHit(L, p) && !Occluded(L.Z, p.X, p.Y)) return L;
        return null;
    }

    void UpdateHover()
    {
        bool through;
        if (!PlayEnabled) through = true;
        else if (heldBall != null || heldMan != null) through = false;
        else { var p = CursorPos(); through = HitObject(p) == null && PileAt(p) == null; }
        SetClickThrough(through);
    }

    void OnMouseDown()
    {
        if (!PlayEnabled || heldBall != null || heldMan != null) return;
        var p = CursorPos();
        downPos = p; moved = false;
        hist.Clear();
        hist.Add(new KeyValuePair<long, PointF>(clock.ElapsedMilliseconds, p));

        var o = HitObject(p);
        var sm = o as Snowman;
        var b = o as Ball;
        if (sm != null)
        {
            heldMan = sm; sm.State = 2; holdDX = sm.X - p.X; holdDY = sm.Bottom - p.Y; heldPrevX = p.X;
            return;
        }
        if (b != null)
        {
            heldBall = b; b.State = 2; holdDX = b.X - p.X; holdDY = b.Y - p.Y;
            return;
        }
        var L = PileAt(p);
        if (L != null && balls.Count + men.Count < MaxObjects)
        {
            // 쌓인 눈을 한 움큼 퍼서 작은 눈덩이 만들기
            float r0 = 7f * ScaleAt(p.X, p.Y);
            int idx = (int)p.X - L.Rect.Left, rad = (int)(r0 * 1.5f);
            for (int d = -rad; d <= rad; d++)
            {
                int j = idx + d;
                if (j < 0 || j >= L.H.Length) continue;
                float t = (float)d / rad;
                L.H[j] -= Math.Min(L.H[j], r0 * 0.7f * (1 - t * t));
            }
            var nb = new Ball { X = p.X, Y = p.Y, R = r0, State = 2 };
            balls.Add(nb);
            heldBall = nb; holdDX = 0; holdDY = 0;
        }
    }

    PointF Velocity()
    {
        if (hist.Count < 2) return PointF.Empty;
        var last = hist[hist.Count - 1];
        var first = hist[0];
        for (int i = hist.Count - 1; i >= 0; i--)
        {
            first = hist[i];
            if (last.Key - hist[i].Key >= 70) break;
        }
        float dtm = Math.Max(1, last.Key - first.Key);
        return new PointF((last.Value.X - first.Value.X) / dtm * 16.67f, (last.Value.Y - first.Value.Y) / dtm * 16.67f);
    }

    void OnMouseUp()
    {
        if (heldBall == null && heldMan == null) return;
        var v = Velocity();
        float sp = (float)Math.Sqrt(v.X * v.X + v.Y * v.Y), cap = 45f * S;
        if (sp > cap) { v = new PointF(v.X * cap / sp, v.Y * cap / sp); sp = cap; }
        if (heldBall != null)
        {
            var b = heldBall; heldBall = null;
            b.State = 0; b.VX = v.X; b.VY = v.Y; b.Thrown = sp > 7f * S;
        }
        else
        {
            var m = heldMan; heldMan = null;
            m.State = 0;
            if (!moved) { m.VX = 0; m.VY = -6f * S; m.Wave = 2.5f; }   // 콕 찌르면 폴짝 뛰며 손 흔들기
            else { m.VX = v.X * 0.8f; m.VY = v.Y * 0.8f; m.Thrown = sp > 14f * S; }
        }
    }

    void UpdateHeld()
    {
        if (heldBall == null && heldMan == null) return;
        var p = CursorPos();
        long now = clock.ElapsedMilliseconds;
        hist.Add(new KeyValuePair<long, PointF>(now, p));
        while (hist.Count > 2 && now - hist[0].Key > 250) hist.RemoveAt(0);
        if (!moved && U.D2(p.X, p.Y, downPos.X, downPos.Y) > 25f * S * S) moved = true;

        if (heldBall != null)
        {
            var b = heldBall;
            float tx = p.X + holdDX, ty = p.Y + holdDY, dx = tx - b.X;
            b.X = tx; b.Y = ty;
            // 작업표시줄(바닥) 아래로는 못 내려가게
            float floor = FloorAt(b.X);
            if (b.Y > floor - b.R * 0.8f) b.Y = floor - b.R * 0.8f;
            Ledge L;
            float s = SurfaceUnder(b.X, b.Y + b.R * 0.2f, out L);
            if (L != null && b.Y + b.R > s - 1.5f * S)
            {
                // 눈 위에서 굴리면 눈을 묻혀 커짐
                b.Y = Math.Min(b.Y, s - b.R * 0.8f);
                float adx = Math.Abs(dx);
                if (adx > 0.3f && b.R < 48f * S)
                {
                    int c0 = (int)(b.X - b.R * 0.6f) - L.Rect.Left, c1 = (int)(b.X + b.R * 0.6f) - L.Rect.Left;
                    float taken = 0, per = adx * 0.05f;
                    for (int c = Math.Max(0, c0); c <= Math.Min(L.H.Length - 1, c1); c++)
                    {
                        float t = Math.Min(L.H[c], per);
                        L.H[c] -= t; taken += t;
                    }
                    b.R = (float)Math.Sqrt(b.R * b.R + taken * 1.1f);
                }
                b.Rot += dx / Math.Max(1f, b.R);
            }
        }
        else if (moved)
        {
            var m = heldMan;
            float dx = p.X - heldPrevX; heldPrevX = p.X;
            m.X = p.X + holdDX; m.Bottom = p.Y + holdDY;
            float floor = FloorAt(m.X) + m.RB * 0.12f;
            if (m.Bottom > floor) m.Bottom = floor;
            m.Tilt = m.Tilt * 0.8f + U.Clamp(dx * 0.03f / S, -0.5f, 0.5f) * 0.2f;
        }
    }

    // ---------- 눈덩이 / 눈사람 ----------
    void MakeSnowman(Ball lower, Ball upper)
    {
        var m = new Snowman();
        float big = Math.Max(lower.R, upper.R), small = Math.Min(lower.R, upper.R);
        m.RB = big;
        m.RH = Math.Max(big * 0.55f, Math.Min(small, big * 0.8f));
        m.X = lower.X; m.Bottom = lower.Y + lower.R * 0.97f;
        m.State = 1; m.Style = rnd.Next(5);
        m.T = (float)rnd.NextDouble() * 10; m.BlinkT = 2 + (float)rnd.NextDouble() * 3;
        m.DanceT = 5 + (float)rnd.NextDouble() * 6; m.Wave = 2f;
        lower.Dead = upper.Dead = true;
        men.Add(m);
        float hy = m.Bottom - m.RB * 1.84f - m.RH * 0.85f;
        for (int i = 0; i < 10; i++)
        {
            double a = rnd.NextDouble() * Math.PI * 2;
            Spark(m.X + (float)Math.Cos(a) * m.RH * 1.8f, hy + (float)Math.Sin(a) * m.RH * 1.8f, (4 + (float)rnd.NextDouble() * 5) * S);
        }
    }

    void StepBalls(float dt)
    {
        foreach (var b in balls)
        {
            if (b.Dead || b.State == 2) continue;
            Ledge L;
            if (b.State == 1)
            {
                float contact = b.Y + b.R * 0.85f;
                float s = SurfaceUnder(b.X, contact - 4f * S, out L);
                if (L == null || s > contact + 3f * S) { b.State = 0; b.VY = 0; }
                else { b.Y = s - b.R * 0.85f; continue; }
            }

            float prevBottom = b.Y + b.R;
            b.VY += 0.55f * S * dt;
            b.VX *= (float)Math.Pow(0.995, dt);
            b.X += b.VX * dt; b.Y += b.VY * dt;
            b.Rot += b.VX * dt / Math.Max(1f, b.R);
            // 화면 벽에 맞으면 반대로 튕김
            if (b.X - b.R < vs.Left) { b.X = vs.Left + b.R; b.VX = Math.Abs(b.VX) * 0.75f; }
            if (b.X + b.R > vs.Right) { b.X = vs.Right - b.R; b.VX = -Math.Abs(b.VX) * 0.75f; }
            if (b.Y - b.R < vs.Top) { b.Y = vs.Top + b.R; b.VY = Math.Abs(b.VY) * 0.5f; }
            float sp = (float)Math.Sqrt(b.VX * b.VX + b.VY * b.VY);

            // 놓여 있는 눈덩이 위에 올리면 눈사람
            foreach (var o in balls)
            {
                if (o == b || o.Dead || o.State != 1) continue;
                float topO = o.Y - o.R * 0.7f;
                if (Math.Abs(b.X - o.X) < o.R * 0.8f && b.VY > 0 && b.Y + b.R >= topO && prevBottom <= topO + 6f * S + b.VY * dt)
                {
                    if (b.Thrown && sp > 9f * S) { Shatter(b.X, b.Y, b.R, b.VX, b.VY); b.Dead = true; }
                    else MakeSnowman(o, b);
                    break;
                }
            }
            if (b.Dead) continue;

            // 던진 눈덩이가 눈사람에 맞으면
            if (b.Thrown && sp > 6f * S)
            {
                foreach (var m in men)
                {
                    if (m.State == 2 || m.Dead) continue;
                    float bcY = m.Bottom - m.RB, hcY = m.Bottom - m.RB * 1.84f - m.RH * 0.85f;
                    if (U.D2(b.X, b.Y, m.X, bcY) < (m.RB + b.R) * (m.RB + b.R) || U.D2(b.X, b.Y, m.X, hcY) < (m.RH + b.R) * (m.RH + b.R))
                    {
                        Shatter(b.X, b.Y, b.R, b.VX, b.VY); b.Dead = true;
                        m.Surprise = 1.5f; m.Wave = 1.5f;
                        if (m.State == 1) { m.State = 0; m.VY = -4f * S; m.VX = b.VX * 0.12f; }
                        break;
                    }
                }
                if (b.Dead) continue;
            }

            float s2 = SurfaceUnder(b.X, Math.Min(prevBottom, b.Y + b.R) - Math.Max(6f * S, b.R * 0.3f), out L);
            if (L != null && b.Y + b.R >= s2)
            {
                if ((b.Thrown && sp > 8f * S) || b.VY > 20f * S) { Shatter(b.X, s2 - b.R * 0.5f, b.R, b.VX, b.VY); b.Dead = true; }
                else { b.Y = s2 - b.R * 0.85f; b.VX = 0; b.VY = 0; b.State = 1; b.Thrown = false; }
            }
            if (b.Y - b.R > vs.Bottom) b.Dead = true;
        }
        balls.RemoveAll(x => x.Dead);
    }

    void StepMen(float dt)
    {
        float sec = dt / 60f;
        foreach (var m in men)
        {
            m.T += sec;
            m.Wave = Math.Max(0, m.Wave - sec);
            m.Surprise = Math.Max(0, m.Surprise - sec);
            m.BlinkT -= sec; if (m.BlinkT < -0.15f) m.BlinkT = 2.5f + (float)rnd.NextDouble() * 3.5f;
            m.DanceT -= sec; if (m.DanceT < -3f) m.DanceT = 7 + (float)rnd.NextDouble() * 10;
            if (m.Style == 4 && rnd.NextDouble() < 0.04 * dt)
                Spark(m.X + (float)(rnd.NextDouble() - 0.5) * m.RH * 2.6f, m.Bottom - m.RB * 1.84f - m.RH * (1.9f + (float)rnd.NextDouble() * 0.8f), (3 + (float)rnd.NextDouble() * 3) * S);
            if (m.State == 2) continue;

            Ledge L;
            if (m.State == 1)
            {
                float contact = m.Bottom - m.RB * 0.12f;
                float s = SurfaceUnder(m.X, contact - 4f * S, out L);
                if (L == null || s > contact + 3f * S) { m.State = 0; m.VY = 0; }
                else { m.Bottom = s + m.RB * 0.12f; m.Tilt *= 0.9f; continue; }
            }

            float prev = m.Bottom - m.RB * 0.12f;
            m.VY += 0.55f * S * dt;
            m.X += m.VX * dt; m.Bottom += m.VY * dt;
            m.Tilt = U.Clamp(m.VX * 0.02f / S, -0.5f, 0.5f);
            if (m.X - m.RB < vs.Left) { m.X = vs.Left + m.RB; m.VX = Math.Abs(m.VX) * 0.6f; }
            if (m.X + m.RB > vs.Right) { m.X = vs.Right - m.RB; m.VX = -Math.Abs(m.VX) * 0.6f; }
            float topY = m.Bottom - m.RB * 2 - m.RH * 2;
            if (topY < vs.Top) { m.Bottom += vs.Top - topY; m.VY = Math.Abs(m.VY) * 0.4f; }

            float s2 = SurfaceUnder(m.X, Math.Min(prev, m.Bottom - m.RB * 0.12f) - Math.Max(6f * S, m.RB * 0.3f), out L);
            if (L != null && m.Bottom - m.RB * 0.12f >= s2)
            {
                float sp = (float)Math.Sqrt(m.VX * m.VX + m.VY * m.VY);
                if ((m.Thrown && sp > 10f * S) || m.VY > 24f * S)
                {
                    Shatter(m.X, s2 - m.RB, m.RB, m.VX, m.VY);
                    Shatter(m.X, s2 - m.RB * 2.2f, m.RH, m.VX, m.VY);
                    m.Dead = true;
                }
                else { m.Bottom = s2 + m.RB * 0.12f; m.VX = 0; m.VY = 0; m.State = 1; m.Thrown = false; }
            }
            if (m.Bottom - m.RB * 3 > vs.Bottom) m.Dead = true;
        }
        men.RemoveAll(x => x.Dead);
    }

    // ---------- 제설차 ----------
    public void CallPlow()
    {
        if (plows.Count > 0) return;
        foreach (var L in grounds)
            plows.Add(new Plow { L = L, Dir = 1, X = L.Rect.Left - 45 * PlowScale(L) });
    }

    float PlowScale(Ledge L) { return ScaleAt(L.Rect.Left + L.Rect.Width / 2f, L.Rect.Top - 1) * 1.1f; }

    static float HeapH(Plow p, float s)
    {
        return Math.Min(20f, (float)Math.Sqrt(p.Load / (s * s)) * 0.55f);
    }

    void StepPlows(float dt)
    {
        for (int i = plows.Count - 1; i >= 0; i--)
        {
            var p = plows[i];
            var L = p.L;
            float s = PlowScale(L);
            float prevFront = p.X + p.Dir * 39 * s;
            p.X += p.Dir * 4.2f * S * dt;
            p.T += dt / 60f;
            float front = p.X + p.Dir * 39 * s;
            float hw = (4 + HeapH(p, s) * 1.6f) * s;

            if (!p.Dumped)
            {
                // 날 앞의 눈을 모두 밀어서 더미로
                float a = Math.Min(prevFront, front + p.Dir * hw), b = Math.Max(prevFront, front + p.Dir * hw);
                int c0 = Math.Max(0, (int)a - L.Rect.Left), c1 = Math.Min(L.H.Length - 1, (int)b - L.Rect.Left);
                for (int c = c0; c <= c1; c++) { p.Load += L.H[c]; L.H[c] = 0; }

                // 길 위의 눈덩이/눈사람은 튕겨냄
                foreach (var bl in balls)
                {
                    if (bl.State != 1 || bl.Y + bl.R < L.Rect.Top - L.Max - 4 * S || bl.Y > L.Rect.Top) continue;
                    float ahead = (bl.X - front) * p.Dir;
                    if (ahead > -bl.R && ahead < hw + bl.R)
                    { bl.State = 0; bl.VX = -p.Dir * (4 + (float)rnd.NextDouble() * 2) * S; bl.VY = -(11 + (float)rnd.NextDouble() * 3) * S; }
                }
                foreach (var m in men)
                {
                    if (m.State != 1 || Math.Abs(m.Bottom - L.Rect.Top) > L.Max + m.RB * 0.3f) continue;
                    float ahead = (m.X - front) * p.Dir;
                    if (ahead > -m.RB && ahead < hw + m.RB)
                    { m.State = 0; m.VX = -p.Dir * (4 + (float)rnd.NextDouble() * 2) * S; m.VY = -(12 + (float)rnd.NextDouble() * 3) * S; m.Surprise = 2f; m.Wave = 2f; }
                }

                bool atEnd = p.Dir > 0 ? front >= L.Rect.Right - 2 : front <= L.Rect.Left + 2;
                if (atEnd)
                {
                    // 끝에 도착하면 모은 눈을 뒤로 휙 뿌림
                    int n = Math.Min(260, (int)(p.Load / (S * S * 6)) + 12);
                    float x = U.Clamp(front, L.Rect.Left + 5, L.Rect.Right - 5);
                    float hh = HeapH(p, s) * s;
                    for (int k = 0; k < n; k++)
                    {
                        var f = new Flake();
                        f.Near = true; f.Chunk = true;
                        f.R = (float)(1.3 + rnd.NextDouble() * 2) * S;
                        f.X = x - p.Dir * (float)rnd.NextDouble() * hw;
                        f.Y = L.Rect.Top - (float)rnd.NextDouble() * hh;
                        f.VX = -p.Dir * (float)rnd.NextDouble() * 6f * S;
                        f.VY = -(4 + (float)rnd.NextDouble() * 9) * S;
                        f.Fall = (float)(2.5 + rnd.NextDouble() * 1.5) * S;
                        f.Alpha = 245; f.Sway = 0.1f * S;
                        flakes.Add(f);
                    }
                    p.Load = 0; p.Dumped = true;
                }
            }
            bool gone = p.Dir > 0 ? p.X - 45 * s > L.Rect.Right : p.X + 45 * s < L.Rect.Left;
            if (gone) plows.RemoveAt(i);
        }
    }

    // ---------- 프레임 ----------
    void Tick()
    {
        if (paused) return;
        long now = clock.ElapsedMilliseconds;
        float dt = lastTick == 0 ? 1f : Math.Min(3f, (now - lastTick) / 16.6667f);
        lastTick = now;
        frame++;

        if (displayDirty)
        {
            displayDirty = false;
            BuildSurfaces();
            BuildGrounds();
            plows.Clear();
        }
        if (frame % 3 == 0) RefreshWindows();
        if (frame % 120 == 0)
            foreach (var sf in surfaces)
                Native.SetWindowPos(sf.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010); // 항상 위 유지

        UpdateHeld();
        UpdateHover();

        // 바람
        if (rnd.NextDouble() < 0.004) windTarget = (float)(rnd.NextDouble() * 2 - 1) * 1.1f;
        wind += (windTarget - wind) * 0.01f * dt;

        int normal = 0;
        foreach (var f in flakes) if (!f.Chunk) normal++;
        for (; normal < TargetCount; normal++) flakes.Add(NewFlake(false));

        for (int i = flakes.Count - 1; i >= 0; i--)
        {
            var f = flakes[i];
            f.Phase += 0.025f * dt;
            f.VY = Math.Min(f.Fall, f.VY + (f.Chunk ? 0.35f : 0.12f) * S * dt);
            f.VX *= (float)Math.Pow(0.97, dt);
            float nx = f.X + (f.VX + wind * S * (f.Near ? 1f : 0.55f) + (float)Math.Sin(f.Phase) * f.Sway) * dt;
            float ny = f.Y + f.VY * dt;

            bool landed = false;
            if (f.Near && Accumulate) landed = TryLand(grounds, f, nx, ny) || TryLand(active, f, nx, ny);
            f.X = nx; f.Y = ny;

            bool outOf = f.Y > vs.Bottom + 10 || f.X < vs.Left - 150 || f.X > vs.Right + 150;
            if (landed || outOf)
            {
                if (f.Chunk || normal > TargetCount) { flakes.RemoveAt(i); if (!f.Chunk) normal--; }
                else flakes[i] = NewFlake(false);
            }
        }

        StepBalls(dt);
        StepMen(dt);
        StepPlows(dt);
        for (int i = sparkles.Count - 1; i >= 0; i--)
        {
            var sp = sparkles[i];
            sp.Life -= dt / 60f * 1.4f; sp.Y -= 0.25f * S * dt;
            if (sp.Life <= 0) sparkles.RemoveAt(i);
        }

        bool fwd = (frame & 1) == 0;
        foreach (var l in grounds) Settle(l, fwd);
        foreach (var l in active) Settle(l, fwd);

        // 움직이는 게 있으면 부드럽게(약 64fps), 평소엔 약 32fps
        bool busy = heldBall != null || heldMan != null || plows.Count > 0;
        if (!busy) foreach (var b in balls) if (b.State == 0) { busy = true; break; }
        if (!busy) foreach (var m in men) if (m.State == 0) { busy = true; break; }
        if (frame % 150 == 1)
        {
            try { onBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline; }
            catch { onBattery = false; }
        }
        // 보통: 평소 약 32fps / 움직일 때 약 64fps,  저전력: 평소 약 21fps / 움직일 때 약 32fps
        bool low = EffectiveLowPower;
        int want = busy ? (low ? 30 : 15) : (low ? 45 : 30);
        if (timer.Interval != want) timer.Interval = want;

        Render();
    }

    bool TryLand(List<Ledge> ledges, Flake f, float nx, float ny)
    {
        foreach (var l in ledges)
        {
            if (nx < l.Rect.Left || nx >= l.Rect.Right) continue;
            int idx = (int)nx - l.Rect.Left;
            if (idx < 0 || idx >= l.H.Length) continue;
            float surface = l.Rect.Top - l.H[idx];
            if (f.Y <= surface + 0.5f && ny >= surface)
            {
                if (!l.Ground && Occluded(l.Z, nx, surface)) continue; // 다른 창에 가려진 곳
                Deposit(l, idx, f.R);
                return true;
            }
        }
        return false;
    }

    // ---------- 그리기 ----------
    static bool Hits(Surface sf, float x, float y, float r)
    {
        return x + r >= sf.Phys.Left && x - r <= sf.Phys.Right && y + r >= sf.Phys.Top && y - r <= sf.Phys.Bottom;
    }

    void Render()
    {
        foreach (var sf in surfaces) RenderSurface(sf);
    }

    void RenderSurface(Surface sf)
    {
        g = sf.G; dirty = sf.Dirty; curSx = sf.Sx;
        g.ResetClip();
        int ox = -sf.Phys.Left, oy = -sf.Phys.Top;

        // 전체를 지우지 않고 지난 프레임에 그린 부분만 지움
        g.SmoothingMode = SmoothingMode.None;
        g.CompositingMode = CompositingMode.SourceCopy;
        int pad = (int)Math.Ceiling(2 / sf.Sx);
        foreach (var r in dirty) { var rr = r; rr.Inflate(pad, pad); g.FillRectangle(clearBrush, rr); }
        g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        dirty.Clear();

        foreach (var l in grounds)
            if (Hits(sf, l.Rect.Left + l.Rect.Width / 2f, l.Rect.Top, l.Rect.Width / 2f + l.Max)) DrawPile(l, ox, oy);
        foreach (var l in active)
        {
            if (!Hits(sf, l.Rect.Left + l.Rect.Width / 2f, l.Rect.Top, l.Rect.Width / 2f + l.Max)) continue;
            using (var rg = new Region(new Rectangle(l.Rect.Left + ox, l.Rect.Top - (int)l.Max - 2 + oy, l.Rect.Width, (int)l.Max + 3)))
            {
                for (int k = 0; k < l.Z && k < occluders.Count; k++)
                {
                    var o = occluders[k]; o.Offset(ox, oy); rg.Exclude(o);
                }
                g.Clip = rg;
                DrawPile(l, ox, oy);
                g.ResetClip();
            }
        }

        foreach (var p in plows) if (Hits(sf, p.X, p.L.Rect.Top, 200 * PlowScale(p.L))) DrawPlow(p, ox, oy);
        foreach (var b in balls) if (b.State != 2 && Hits(sf, b.X, b.Y, b.R + 4)) DrawBall(b, ox, oy);
        foreach (var m in men) if (Hits(sf, m.X, m.Bottom - m.RB * 2, m.RB * 3 + m.RH * 2)) DrawSnowman(m, ox, oy);
        foreach (var b in balls) if (b.State == 2 && Hits(sf, b.X, b.Y, b.R + 4)) DrawBall(b, ox, oy);

        foreach (var f in flakes)
        {
            if (!Hits(sf, f.X, f.Y, f.R + 2)) continue;
            float d = f.R * 2;
            float fx = f.X - f.R + ox, fy = f.Y - f.R + oy;
            g.FillEllipse(flakeBrushes[f.Alpha], fx, fy, d, d);
            dirty.Add(new Rectangle((int)fx - 2, (int)fy - 2, (int)d + 5, (int)d + 5));
        }

        foreach (var sp in sparkles) if (Hits(sf, sp.X, sp.Y, sp.Size + 2)) DrawSparkle(sp, ox, oy);

        sf.Present();
    }

    void DrawPile(Ledge l, int ox, int oy)
    {
        var h = l.H; int n = h.Length;
        float max = 0;
        for (int j = 0; j < n; j++) if (h[j] > max) max = h[j];
        if (max < 0.6f) return;

        float baseY = l.Rect.Top + oy + 1;
        float left = l.Rect.Left + ox;
        dirty.Add(new Rectangle((int)left - 2, (int)(baseY - max) - 3, n + 4, (int)max + 6));
        // 눈이 있는 구간만 골라서, 그라데이션 한 번으로 채우고 윗면 윤곽선만 그림
        int stepPx = Math.Max(3, (int)Math.Round(2.5f / curSx));
        using (var grad = new LinearGradientBrush(new RectangleF(left, baseY - max - 1, Math.Max(1, n), max + 2),
                                                  Color.FromArgb(252, 255, 255, 255), Color.FromArgb(252, 218, 230, 248), 90f))
        {
            var top = new List<PointF>(64);
            int j0 = -1;
            for (int j = 0; j <= n; j += stepPx)
            {
                int jj = Math.Min(j, n - 1);
                bool on = j < n && h[jj] > 0.3f;
                if (on)
                {
                    if (j0 < 0) { j0 = jj; top.Clear(); }
                    top.Add(new PointF(left + jj, baseY - h[jj]));
                }
                if ((!on || j + stepPx > n) && j0 >= 0)
                {
                    float xEnd = top[top.Count - 1].X;
                    var poly = new PointF[top.Count + 2];
                    poly[0] = new PointF(left + j0 - stepPx * 0.5f, baseY);
                    top.CopyTo(poly, 1);
                    poly[poly.Length - 1] = new PointF(xEnd + stepPx * 0.5f, baseY);
                    g.FillPolygon(grad, poly);
                    if (top.Count >= 2) g.DrawLines(pileEdge, top.ToArray());
                    j0 = -1;
                }
            }
        }
    }

    void FillSnowBall(RectangleF r)
    {
        if (r.Width < 1 || r.Height < 1) return;
        using (var br = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y - 1, r.Width + 2, r.Height + 2),
                                                Color.White, Color.FromArgb(205, 218, 238), 55f))
            g.FillEllipse(br, r);
        using (var pen = new Pen(Color.FromArgb(80, 120, 140, 175), Math.Max(1f, r.Width * 0.012f)))
            g.DrawEllipse(pen, r);
    }

    void DrawBall(Ball b, int ox, int oy)
    {
        float cx = b.X + ox, cy = b.Y + oy;
        FillSnowBall(new RectangleF(cx - b.R, cy - b.R, b.R * 2, b.R * 2));
        using (var dot = new SolidBrush(Color.FromArgb(45, 120, 140, 180)))
        {
            for (int k = 0; k < 3; k++)
            {
                double a = b.Rot + k * 2.1;
                float px = cx + (float)Math.Cos(a) * b.R * 0.55f, py = cy + (float)Math.Sin(a) * b.R * 0.35f;
                if (Math.Cos(a * 0.5) < -0.2) continue;
                float rr = b.R * 0.12f;
                g.FillEllipse(dot, px - rr, py - rr, rr * 2, rr * 2);
            }
        }
        dirty.Add(new Rectangle((int)(cx - b.R) - 3, (int)(cy - b.R) - 3, (int)(b.R * 2) + 7, (int)(b.R * 2) + 7));
    }

    void DrawSparkle(Sparkle sp, int ox, int oy)
    {
        int a = (int)(255 * U.Clamp(sp.Life, 0, 1));
        if (a <= 0) return;
        float x = sp.X + ox, y = sp.Y + oy, s = sp.Size * (0.6f + sp.Life * 0.4f);
        using (var br = new SolidBrush(Color.FromArgb(a, 255, 250, 215)))
        {
            g.FillEllipse(br, x - s, y - s * 0.16f, s * 2, s * 0.32f);
            g.FillEllipse(br, x - s * 0.16f, y - s, s * 0.32f, s * 2);
        }
        dirty.Add(new Rectangle((int)(x - s) - 2, (int)(y - s) - 2, (int)(s * 2) + 5, (int)(s * 2) + 5));
    }

    static readonly Color[] scarfColors = {
        Color.FromArgb(250, 200, 40), Color.FromArgb(40, 170, 80), Color.FromArgb(210, 40, 50), Color.FromArgb(60, 120, 230), Color.Empty };

    void DrawSnowman(Snowman m, int ox, int oy)
    {
        float t = m.T, RB = m.RB, RH = m.RH;
        bool dancing = m.DanceT < 0 && m.State == 1;
        float breath = 1f + (float)Math.Sin(t * 2.2) * 0.025f;
        float dance = dancing ? (float)Math.Sin(t * 7) : 0f;
        float hop = dancing ? Math.Abs((float)Math.Sin(t * 7)) * RB * 0.12f : 0f;
        float tiltDeg = m.Tilt * 57.3f + dance * 7f;

        var st = g.Save();
        g.TranslateTransform(m.X + ox, m.Bottom + oy - hop);
        g.RotateTransform(tiltDeg);

        float bh = RB * 2 * breath;
        float headCY = -RB * 1.84f * breath - RH * 0.85f + (float)Math.Sin(t * 2.2 + 0.6) * RB * 0.02f;
        float headTop = headCY - RH;

        // 팔 (몸 뒤쪽)
        float shY = -RB * 1.45f * breath;
        double baseL = Math.PI + 0.45, baseR = -0.45;
        double aL, aR;
        if (m.Wave > 0) { aL = baseL - 0.1; aR = -1.0 + Math.Sin(t * 12) * 0.5; }
        else if (dancing) { aL = baseL + Math.Sin(t * 7) * 0.45; aR = baseR + Math.Sin(t * 7) * 0.45; }
        else { aL = baseL + Math.Sin(t * 1.6) * 0.07; aR = baseR - Math.Sin(t * 1.6) * 0.07; }
        if (m.Surprise > 0) { aL = Math.PI + 0.9; aR = -0.9; }
        Color mitten = m.Style == 1 ? Color.FromArgb(40, 170, 80) : (m.Style == 3 ? Color.FromArgb(60, 120, 230) : Color.Empty);
        DrawArm(-RB * 0.78f, shY, aL, RB * 1.05f, RB, mitten);
        DrawArm(RB * 0.78f, shY, aR, RB * 1.05f, RB, mitten);

        // 몸통
        FillSnowBall(new RectangleF(-RB, -bh, RB * 2, bh));
        if (m.Style == 0 || m.Style == 2 || m.Style == 4)
            using (var bb = new SolidBrush(Color.FromArgb(45, 45, 55)))
            {
                float br = RB * 0.075f;
                g.FillEllipse(bb, -br, -RB * 1.3f * breath - br, br * 2, br * 2);
                g.FillEllipse(bb, -br, -RB * 0.95f * breath - br, br * 2, br * 2);
                g.FillEllipse(bb, -br, -RB * 0.6f * breath - br, br * 2, br * 2);
            }

        // 머리
        FillSnowBall(new RectangleF(-RH, headCY - RH, RH * 2, RH * 2));

        // 목도리
        Color sc = scarfColors[m.Style];
        if (!sc.IsEmpty)
        {
            float neckY = headCY + RH * 0.86f;
            Color dark = U.Lerp(sc, Color.Black, 0.25f);
            using (var sb = new SolidBrush(sc))
            using (var db = new SolidBrush(dark))
            {
                g.FillPolygon(db, new[] {
                    new PointF(RH * 0.2f, neckY), new PointF(RH * 0.62f, neckY),
                    new PointF(RH * 0.72f + (float)Math.Sin(t * 3) * RH * 0.05f, neckY + RH * 0.95f),
                    new PointF(RH * 0.34f + (float)Math.Sin(t * 3) * RH * 0.05f, neckY + RH * 1.0f) });
                using (var path = U.RoundRect(-RH * 0.95f, neckY - RH * 0.17f, RH * 1.9f, RH * 0.36f, RH * 0.16f))
                    g.FillPath(sb, path);
                using (var pen = new Pen(dark, Math.Max(1f, RH * 0.06f)))
                {
                    g.DrawLine(pen, -RH * 0.45f, neckY - RH * 0.15f, -RH * 0.45f, neckY + RH * 0.16f);
                    g.DrawLine(pen, RH * 0.05f, neckY - RH * 0.15f, RH * 0.05f, neckY + RH * 0.16f);
                }
            }
        }

        // 얼굴
        bool surprised = m.Style == 4 || m.Surprise > 0;
        using (var eye = new SolidBrush(Color.FromArgb(35, 35, 45)))
        {
            float er = RH * (surprised ? 0.12f : 0.1f), ey = headCY - RH * 0.12f;
            if (m.BlinkT < 0 && !surprised)
                using (var pen = new Pen(Color.FromArgb(35, 35, 45), Math.Max(1f, RH * 0.06f)))
                {
                    g.DrawLine(pen, -RH * 0.32f - er, ey, -RH * 0.32f + er, ey);
                    g.DrawLine(pen, RH * 0.32f - er, ey, RH * 0.32f + er, ey);
                }
            else
            {
                g.FillEllipse(eye, -RH * 0.32f - er, ey - er, er * 2, er * 2);
                g.FillEllipse(eye, RH * 0.32f - er, ey - er, er * 2, er * 2);
                using (var hl = new SolidBrush(Color.White))
                {
                    float hr = er * 0.35f;
                    g.FillEllipse(hl, -RH * 0.32f - er * 0.2f, ey - er * 0.6f, hr * 2, hr * 2);
                    g.FillEllipse(hl, RH * 0.32f - er * 0.2f, ey - er * 0.6f, hr * 2, hr * 2);
                }
            }
            if (surprised)
                g.FillEllipse(eye, -RH * 0.1f, headCY + RH * 0.36f, RH * 0.2f, RH * 0.26f);
            else
                for (int k = -2; k <= 2; k++)
                {
                    float dr = RH * 0.05f, ax = k * RH * 0.15f;
                    float ay = headCY + RH * 0.4f + (1 - (k / 2f) * (k / 2f)) * RH * 0.1f;
                    g.FillEllipse(eye, ax - dr, ay - dr, dr * 2, dr * 2);
                }
        }
        if (m.Style == 0 || m.Style == 1 || m.Style == 4)
            using (var blush = new SolidBrush(Color.FromArgb(110, 255, 130, 150)))
            {
                g.FillEllipse(blush, -RH * 0.68f, headCY + RH * 0.14f, RH * 0.28f, RH * 0.17f);
                g.FillEllipse(blush, RH * 0.4f, headCY + RH * 0.14f, RH * 0.28f, RH * 0.17f);
            }
        using (var carrot = new SolidBrush(Color.FromArgb(245, 130, 35)))
            g.FillPolygon(carrot, new[] {
                new PointF(-RH * 0.04f, headCY + RH * 0.05f), new PointF(RH * 0.62f, headCY + RH * 0.17f), new PointF(-RH * 0.04f, headCY + RH * 0.26f) });

        // 모자
        DrawHat(m.Style, headTop, RH);

        g.Restore(st);
        float wHalf = RB * 2.3f;
        float top = m.Bottom - hop - (RB * 2 + RH * 3.2f) * 1.1f;
        dirty.Add(new Rectangle((int)(m.X + ox - wHalf) - 4, (int)(top + oy) - 4, (int)(wHalf * 2) + 8, (int)(m.Bottom - top + RB * 0.5f) + 8));
    }

    void DrawArm(float x0, float y0, double ang, float len, float RB, Color mitten)
    {
        using (var pen = new Pen(Color.FromArgb(115, 72, 40), Math.Max(1.5f, RB * 0.075f)))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
            float c = (float)Math.Cos(ang), s = (float)Math.Sin(ang);
            float x1 = x0 + c * len, y1 = y0 + s * len;
            g.DrawLine(pen, x0, y0, x1, y1);
            float mx = x0 + c * len * 0.62f, my = y0 + s * len * 0.62f;
            double side = c < 0 ? 0.65 : -0.65;
            g.DrawLine(pen, mx, my, mx + (float)Math.Cos(ang + side) * len * 0.3f, my + (float)Math.Sin(ang + side) * len * 0.3f);
            if (!mitten.IsEmpty)
                using (var mb = new SolidBrush(mitten))
                {
                    float r = RB * 0.14f;
                    g.FillEllipse(mb, x1 - r, y1 - r, r * 2, r * 2);
                }
            else
            {
                g.DrawLine(pen, x1, y1, x1 + (float)Math.Cos(ang + 0.5) * len * 0.2f, y1 + (float)Math.Sin(ang + 0.5) * len * 0.2f);
                g.DrawLine(pen, x1, y1, x1 + (float)Math.Cos(ang - 0.5) * len * 0.2f, y1 + (float)Math.Sin(ang - 0.5) * len * 0.2f);
            }
        }
    }

    void DrawHat(int style, float headTop, float RH)
    {
        if (style == 0 || style == 1)
        {
            // 산타 모자
            using (var red = new SolidBrush(Color.FromArgb(215, 35, 45)))
            using (var white = new SolidBrush(Color.White))
            {
                g.FillPolygon(red, new[] {
                    new PointF(-RH * 0.85f, headTop + RH * 0.28f), new PointF(-RH * 0.3f, headTop - RH * 0.6f),
                    new PointF(RH * 0.2f, headTop - RH * 0.82f), new PointF(RH * 0.75f, headTop - RH * 0.45f),
                    new PointF(RH * 0.85f, headTop + RH * 0.28f) });
                using (var brim = U.RoundRect(-RH * 0.98f, headTop + RH * 0.08f, RH * 1.96f, RH * 0.36f, RH * 0.17f))
                    g.FillPath(white, brim);
                float pr = RH * 0.21f;
                g.FillEllipse(white, RH * 0.75f - pr, headTop - RH * 0.45f - pr, pr * 2, pr * 2);
            }
        }
        else if (style == 2)
        {
            // 중절모(실크햇)
            using (var black = new SolidBrush(Color.FromArgb(28, 28, 34)))
            using (var band = new SolidBrush(Color.FromArgb(200, 35, 45)))
            {
                g.FillRectangle(black, -RH * 0.62f, headTop - RH * 0.95f, RH * 1.24f, RH * 1.15f);
                g.FillRectangle(band, -RH * 0.62f, headTop - RH * 0.08f, RH * 1.24f, RH * 0.2f);
                g.FillEllipse(black, -RH * 1.08f, headTop + RH * 0.05f, RH * 2.16f, RH * 0.34f);
            }
        }
        else if (style == 3)
        {
            // 털모자(비니)
            using (var blue = new SolidBrush(Color.FromArgb(60, 120, 230)))
            using (var cuff = new SolidBrush(Color.FromArgb(40, 90, 190)))
            using (var white = new SolidBrush(Color.White))
            using (var path = new GraphicsPath())
            {
                path.AddArc(new RectangleF(-RH * 0.98f, headTop - RH * 0.2f, RH * 1.96f, RH * 1.5f), 180, 180);
                path.CloseFigure();
                g.FillPath(blue, path);
                using (var c = U.RoundRect(-RH * 1.02f, headTop + RH * 0.4f, RH * 2.04f, RH * 0.32f, RH * 0.14f))
                    g.FillPath(cuff, c);
                float pr = RH * 0.24f;
                g.FillEllipse(white, -pr, headTop - RH * 0.2f - pr * 1.3f, pr * 2, pr * 2);
            }
        }
        else
        {
            // 양동이
            var st = g.Save();
            g.TranslateTransform(0, headTop + RH * 0.2f);
            g.RotateTransform(-12);
            var pts = new[] { new PointF(-RH * 0.58f, -RH * 0.95f), new PointF(RH * 0.58f, -RH * 0.95f),
                              new PointF(RH * 0.8f, RH * 0.05f), new PointF(-RH * 0.8f, RH * 0.05f) };
            using (var br = new LinearGradientBrush(new RectangleF(-RH * 0.85f, -RH, RH * 1.7f, RH * 1.1f),
                                                    Color.FromArgb(200, 208, 220), Color.FromArgb(125, 135, 152), 0f))
                g.FillPolygon(br, pts);
            using (var rim = new SolidBrush(Color.FromArgb(215, 222, 232)))
                g.FillRectangle(rim, -RH * 0.86f, -RH * 0.08f, RH * 1.72f, RH * 0.17f);
            using (var pen = new Pen(Color.FromArgb(110, 120, 138), Math.Max(1f, RH * 0.04f)))
                g.DrawPolygon(pen, pts);
            g.Restore(st);
        }
    }

    void DrawPlow(Plow p, int ox, int oy)
    {
        var L = p.L;
        float s = PlowScale(L);
        float hh = HeapH(p, s), hw = 4 + hh * 1.6f;
        var st = g.Save();
        g.TranslateTransform(p.X + ox, L.Rect.Top + oy + 1);
        g.ScaleTransform(p.Dir * s, s);
        g.TranslateTransform(0, (float)Math.Sin(p.T * 25) * 0.5f);

        using (var outline = new Pen(Color.FromArgb(90, 60, 40, 30), 0.7f))
        {
            // 밀고 있는 눈더미
            if (hh > 0.5f && !p.Dumped)
                using (var path = new GraphicsPath())
                {
                    path.AddArc(new RectangleF(36 - hw * 0.15f, -hh, hw * 1.3f, hh * 2), 180, 180);
                    path.CloseFigure();
                    g.FillPath(pileBrush, path);
                    using (var e = new Pen(Color.FromArgb(120, 150, 170, 200), 0.7f)) g.DrawPath(e, path);
                }

            // 짐칸 + 실은 눈
            using (var orange = new SolidBrush(Color.FromArgb(240, 135, 30)))
            using (var bed = U.RoundRect(-34, -22, 34, 13, 2))
            { g.FillPath(orange, bed); g.DrawPath(outline, bed); }
            using (var load = new SolidBrush(Color.White))
                g.FillEllipse(load, -33, -30, 32, 13);
            // 운전석
            using (var cab = U.RoundRect(0, -31, 25, 22, 4))
            using (var orange2 = new SolidBrush(Color.FromArgb(252, 155, 40)))
            { g.FillPath(orange2, cab); g.DrawPath(outline, cab); }
            using (var win = U.RoundRect(10, -28, 12, 9, 2))
            using (var glass = new SolidBrush(Color.FromArgb(175, 225, 255)))
                g.FillPath(glass, win);
            using (var glint = new Pen(Color.FromArgb(200, 255, 255, 255), 0.8f))
                g.DrawLine(glint, 13, -26.5f, 15.5f, -21);
            // 차대
            using (var chassis = new SolidBrush(Color.FromArgb(70, 70, 82)))
                g.FillRectangle(chassis, -35, -11, 63, 4);
            // 바퀴
            using (var tire = new SolidBrush(Color.FromArgb(38, 38, 46)))
            using (var hub = new SolidBrush(Color.FromArgb(190, 190, 200)))
            {
                foreach (float wx in new[] { -22f, 14f })
                {
                    g.FillEllipse(tire, wx - 5.5f, -10.5f, 11, 11);
                    g.FillEllipse(hub, wx - 2.2f, -7.2f, 4.4f, 4.4f);
                }
            }
            // 제설 날 (빨강/흰 줄무늬)
            using (var arm = new Pen(Color.FromArgb(90, 90, 100), 1.6f)) g.DrawLine(arm, 24, -12, 31, -12);
            using (var blade = new GraphicsPath())
            {
                blade.AddPolygon(new[] { new PointF(28, -21), new PointF(32, -25), new PointF(40, -1), new PointF(30, 0) });
                using (var wb = new SolidBrush(Color.White)) g.FillPath(wb, blade);
                var st2 = g.Save();
                g.SetClip(blade, CombineMode.Intersect);
                using (var red = new Pen(Color.FromArgb(220, 40, 45), 2.6f))
                    for (float k = -30; k < 10; k += 6) g.DrawLine(red, 26, k, 44, k + 14);
                g.Restore(st2);
                g.DrawPath(outline, blade);
            }
            // 경광등
            bool on = ((int)(p.T * 6)) % 2 == 0;
            using (var lamp = new SolidBrush(on ? Color.FromArgb(255, 205, 0) : Color.FromArgb(255, 110, 0)))
                g.FillRectangle(lamp, 12, -35, 6, 4);
            if (on)
                using (var glow = new SolidBrush(Color.FromArgb(70, 255, 200, 0)))
                    g.FillEllipse(glow, 9, -38, 12, 10);
        }
        g.Restore(st);
        float ext = (45 + hw) * s;
        dirty.Add(new Rectangle((int)(p.X + ox - ext) - 4, (int)(L.Rect.Top + oy - 42 * s) - 4, (int)(ext * 2) + 8, (int)(46 * s) + 8));
    }

    static IntPtr MakeMittenCursor(float S)
    {
        int size = Math.Min(96, Math.Max(32, (int)(32 * S)));
        using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.SmoothingMode = SmoothingMode.AntiAlias;
                float k = size / 32f;
                gr.ScaleTransform(k, k);
                using (var green = new SolidBrush(Color.FromArgb(60, 175, 95)))
                using (var dark = new Pen(Color.FromArgb(30, 110, 60), 1.3f))
                using (var cuff = new SolidBrush(Color.White))
                using (var cuffPen = new Pen(Color.FromArgb(150, 170, 190), 1f))
                {
                    var st = gr.Save();
                    gr.TranslateTransform(5, 15); gr.RotateTransform(-30);
                    gr.FillEllipse(green, -3, -4, 7, 10); gr.DrawEllipse(dark, -3, -4, 7, 10);
                    gr.Restore(st);
                    gr.FillEllipse(green, 8, 3, 16, 21); gr.DrawEllipse(dark, 8, 3, 16, 21);
                    gr.DrawLine(dark, 13, 7, 13, 13); gr.DrawLine(dark, 18, 6, 18, 12);
                    using (var c = U.RoundRect(8.5f, 21, 15, 7, 2)) { gr.FillPath(cuff, c); gr.DrawPath(cuffPen, c); }
                }
            }
            IntPtr hIcon = bmp.GetHicon();
            Native.ICONINFO ii;
            Native.GetIconInfo(hIcon, out ii);
            ii.fIcon = 0; ii.xHotspot = size / 2; ii.yHotspot = size / 3;
            IntPtr cur = Native.CreateIconIndirect(ref ii);
            Native.DeleteObject(ii.hbmMask); Native.DeleteObject(ii.hbmColor);
            Native.DestroyIcon(hIcon);
            return cur;
        }
    }

    // ---------- 메뉴 동작 ----------
    public void ClearSnow()
    {
        foreach (var l in grounds) Array.Clear(l.H, 0, l.H.Length);
        foreach (var l in winLedges.Values) Array.Clear(l.H, 0, l.H.Length);
    }

    public void ShakeAll()
    {
        foreach (var l in winLedges.Values) ShakeOff(l);
    }

    public void ClearObjects()
    {
        heldBall = null; heldMan = null;
        foreach (var b in balls) Shatter(b.X, b.Y, b.R, 0, 0);
        foreach (var m in men) { Shatter(m.X, m.Bottom - m.RB, m.RB, 0, 0); Shatter(m.X, m.Bottom - m.RB * 2.2f, m.RH, 0, 0); }
        balls.Clear(); men.Clear();
    }

    // 테스트용: 눈사람 5종을 세우고 눈을 쌓아 둔 뒤 제설차 출발
    public void Demo()
    {
        foreach (var L in grounds) for (int i = 0; i < L.H.Length; i++) L.H[i] = L.Max * 0.7f;
        var G = grounds[0];
        for (int st = 0; st < 5; st++)
        {
            var m = new Snowman { RB = 26 * S, RH = 18 * S, X = G.Rect.Left + G.Rect.Width * (0.3f + st * 0.12f), Bottom = G.Rect.Top - G.Max - 40 * S, State = 0, Style = st, BlinkT = 3, DanceT = st == 2 ? -1 : 9, Wave = st == 1 ? 30 : 0 };
            men.Add(m);
        }
        balls.Add(new Ball { X = G.Rect.Left + G.Rect.Width * 0.18f, Y = G.Rect.Top - 60 * S, R = 15 * S });
        var t = new Timer { Interval = 4000 };
        t.Tick += (s, e) => { t.Stop(); CallPlow(); };
        t.Start();
    }

    public void SetPaused(bool p)
    {
        paused = p;
        if (p) { SetClickThrough(true); foreach (var sf in surfaces) Native.ShowWindow(sf.Handle, 0); }
        else { lastTick = 0; foreach (var sf in surfaces) Native.ShowWindow(sf.Handle, 4); }
    }

    public void Shutdown()
    {
        timer.Stop();
        foreach (var sf in surfaces) sf.Destroy();
        surfaces.Clear();
    }
}

// ---------- 겨울 배경화면 (직접 그려서 생성) ----------
static class Wallpapers
{
    public static readonly string[] Names = {
        "고요한 겨울밤", "오로라", "분홍빛 새벽", "겨울 노을", "눈보라",
        "보름달", "보랏빛 밤", "크리스마스 마을", "한낮의 설원", "은하수" };

    class Preset
    {
        public Color Top, Mid, Hor, M1, M2, M3, Tree, SnowHi, SnowLo;
        public int Moon, Sun, Stars, Flakes, Cabins;
        public bool Aurora, Milky, Lights;
        public float Fog;
    }

    static Color C(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }

    static Preset P(int top, int mid, int hor, int m1, int m2, int m3, int tree, int hi, int lo)
    {
        return new Preset { Top = C(top), Mid = C(mid), Hor = C(hor), M1 = C(m1), M2 = C(m2), M3 = C(m3), Tree = C(tree), SnowHi = C(hi), SnowLo = C(lo) };
    }

    static Preset Get(int i)
    {
        Preset p;
        switch (i)
        {
            case 0: p = P(0x080E28, 0x16285C, 0x465FA0, 0x3C5087, 0x283A69, 0x1C2A50, 0x0E1E34, 0xEBF0FF, 0x96AAD7); p.Moon = 1; p.Stars = 400; p.Cabins = 1; p.Flakes = 250; break;
            case 1: p = P(0x020A16, 0x062030, 0x144655, 0x28465A, 0x193244, 0x0F2332, 0x08181E, 0xDCF5F5, 0x78A0AF); p.Aurora = true; p.Stars = 500; p.Flakes = 80; break;
            case 2: p = P(0x3C3C82, 0xBE82AA, 0xFFC3B4, 0xAA8CB4, 0x826EA0, 0x5F5587, 0x323255, 0xFFF0F5, 0xBEAFD2); p.Stars = 60; p.Flakes = 120; break;
            case 3: p = P(0x281E5A, 0xC85A5A, 0xFFAA5A, 0x965A78, 0x694164, 0x462D50, 0x231932, 0xFFEBDC, 0xB496B4); p.Sun = 1; p.Stars = 30; p.Flakes = 60; p.Cabins = 1; break;
            case 4: p = P(0x788291, 0xA5AFBC, 0xCDD4DE, 0xAAB2BE, 0x96A0AE, 0x808A9B, 0x465262, 0xF5F7FA, 0xC3CBD7); p.Flakes = 1400; p.Fog = 0.6f; break;
            case 5: p = P(0x050A1E, 0x0F1E46, 0x2D467D, 0x324B82, 0x223664, 0x16264A, 0x0A1628, 0xE6EEFF, 0x8CA0CD); p.Moon = 2; p.Stars = 250; p.Flakes = 150; break;
            case 6: p = P(0x140828, 0x3C1E64, 0x8250AA, 0x644696, 0x4B3278, 0x34235A, 0x1C1232, 0xF0E6FF, 0xA08CC8); p.Moon = 1; p.Stars = 400; p.Flakes = 200; break;
            case 7: p = P(0x0A1030, 0x192350, 0x415A96, 0x3C5087, 0x283A69, 0x1C2A50, 0x0E2226, 0xEBF0FF, 0x96AAD7); p.Stars = 300; p.Cabins = 5; p.Lights = true; p.Flakes = 300; break;
            case 8: p = P(0x3C82DC, 0x82B9F0, 0xDCEEFC, 0xC8D7EB, 0xA5B9D7, 0x8296B9, 0x1E4646, 0xFFFFFF, 0xC8D7EB); p.Sun = 2; break;
            default: p = P(0x02040F, 0x0A122D, 0x23325F, 0x23345A, 0x172440, 0x0E182E, 0x060E1A, 0xDCE6FF, 0x7888B4); p.Milky = true; p.Stars = 1200; p.Cabins = 1; break;
        }
        return p;
    }

    static string Folder
    {
        get
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopSnow");
            Directory.CreateDirectory(d);
            return d;
        }
    }

    public static void Apply(int idx)
    {
        var b = Screen.PrimaryScreen.Bounds;
        string file = Path.Combine(Folder, string.Format("winter_{0}_{1}x{2}.jpg", idx + 1, b.Width, b.Height));
        if (!File.Exists(file))
            using (var bmp = Render(idx, b.Width, b.Height)) SaveJpeg(bmp, file);
        SaveOriginal();
        using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
        {
            k.SetValue("WallpaperStyle", "10");
            k.SetValue("TileWallpaper", "0");
        }
        Native.SystemParametersInfo(20, 0, file, 3);
    }

    static void SaveOriginal()
    {
        string f = Path.Combine(Folder, "original.txt");
        if (File.Exists(f)) return;
        var sb = new StringBuilder(1024);
        Native.SystemParametersInfo(0x73, 1024, sb, 0);
        string style = "10", tile = "0";
        using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop"))
        {
            if (k != null)
            {
                object s = k.GetValue("WallpaperStyle"), t = k.GetValue("TileWallpaper");
                if (s != null) style = s.ToString();
                if (t != null) tile = t.ToString();
            }
        }
        File.WriteAllLines(f, new[] { sb.ToString(), style, tile }, Encoding.UTF8);
    }

    public static bool Restore()
    {
        string f = Path.Combine(Folder, "original.txt");
        if (!File.Exists(f)) return false;
        var lines = File.ReadAllLines(f, Encoding.UTF8);
        if (lines.Length < 3) return false;
        using (var k = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", true))
        {
            k.SetValue("WallpaperStyle", lines[1]);
            k.SetValue("TileWallpaper", lines[2]);
        }
        Native.SystemParametersInfo(20, 0, lines[0], 3);
        File.Delete(f);
        return true;
    }

    static void SaveJpeg(Bitmap bmp, string file)
    {
        foreach (var codec in ImageCodecInfo.GetImageEncoders())
            if (codec.MimeType == "image/jpeg")
            {
                var ep = new EncoderParameters(1);
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 94L);
                bmp.Save(file, codec, ep);
                return;
            }
        bmp.Save(file, ImageFormat.Jpeg);
    }

    static float[] Ridge(Random r, int n, float rough)
    {
        var a = new float[n];
        a[0] = (float)r.NextDouble(); a[n - 1] = (float)r.NextDouble();
        float range = 1f;
        for (int step = n - 1; step > 1; step /= 2)
        {
            for (int i = 0; i + step < n; i += step)
            {
                int m = i + step / 2;
                a[m] = (a[i] + a[i + step]) / 2 + (float)(r.NextDouble() - 0.5) * range;
            }
            range *= rough;
        }
        float mn = float.MaxValue, mx = float.MinValue;
        foreach (var v in a) { mn = Math.Min(mn, v); mx = Math.Max(mx, v); }
        for (int i = 0; i < n; i++) a[i] = (a[i] - mn) / Math.Max(0.0001f, mx - mn);
        return a;
    }

    static double Gauss(Random r)
    {
        double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    public static Bitmap Render(int idx, int w, int h)
    {
        var p = Get(idx);
        var rnd = new Random(1234 + idx * 77);
        var bmp = new Bitmap(w, h, PixelFormat.Format24bppRgb);
        float sc = h / 1080f;
        float gY = h * 0.68f;
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 하늘
            using (var br = new LinearGradientBrush(new RectangleF(0, -1, w, gY + 3), p.Top, p.Hor, 90f))
            {
                var cb = new ColorBlend(3);
                cb.Colors = new[] { p.Top, p.Mid, p.Hor };
                cb.Positions = new[] { 0f, 0.6f, 1f };
                br.InterpolationColors = cb;
                g.FillRectangle(br, 0, 0, w, gY + 1);
            }
            using (var hb = new SolidBrush(p.Hor)) g.FillRectangle(hb, 0, gY, w, h - gY);

            // 은하수
            if (p.Milky)
            {
                float x0 = w * 0.05f, y0 = -h * 0.05f, x1 = w * 0.95f, y1 = h * 0.6f;
                float dx = x1 - x0, dy = y1 - y0, len = (float)Math.Sqrt(dx * dx + dy * dy);
                float nx = -dy / len, ny = dx / len;
                using (var glow = new SolidBrush(Color.FromArgb(7, 200, 190, 255)))
                    for (int i = 0; i < 60; i++)
                    {
                        float t = (float)rnd.NextDouble(), off = (float)Gauss(rnd) * h * 0.04f, rr = h * (0.06f + (float)rnd.NextDouble() * 0.08f);
                        float cx = x0 + dx * t + nx * off, cy = y0 + dy * t + ny * off;
                        g.FillEllipse(glow, cx - rr, cy - rr * 0.6f, rr * 2, rr * 1.2f);
                    }
                for (int i = 0; i < 5000; i++)
                {
                    float t = (float)rnd.NextDouble(), off = (float)Gauss(rnd) * h * 0.05f;
                    float cx = x0 + dx * t + nx * off, cy = y0 + dy * t + ny * off;
                    if (cy > gY) continue;
                    float rr = (0.4f + (float)rnd.NextDouble() * 1.0f) * sc;
                    using (var sb = new SolidBrush(Color.FromArgb(30 + rnd.Next(140), 235, 230, 255)))
                        g.FillEllipse(sb, cx - rr, cy - rr, rr * 2, rr * 2);
                }
            }

            // 별
            for (int i = 0; i < p.Stars; i++)
            {
                float x = (float)rnd.NextDouble() * w, y = (float)Math.Pow(rnd.NextDouble(), 1.3) * gY * 0.92f;
                float rr = (0.5f + (float)Math.Pow(rnd.NextDouble(), 3) * 1.8f) * sc;
                int a = 70 + rnd.Next(185);
                using (var sb = new SolidBrush(Color.FromArgb(a, 255, 255, 255)))
                    g.FillEllipse(sb, x - rr, y - rr, rr * 2, rr * 2);
                if (rnd.NextDouble() < 0.025)
                {
                    using (var gb = new SolidBrush(Color.FromArgb(40, 220, 230, 255)))
                        g.FillEllipse(gb, x - rr * 4, y - rr * 4, rr * 8, rr * 8);
                    using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), Math.Max(1, 0.8f * sc)))
                    { g.DrawLine(pen, x - rr * 6, y, x + rr * 6, y); g.DrawLine(pen, x, y - rr * 6, x, y + rr * 6); }
                }
            }

            // 오로라
            if (p.Aurora)
            {
                Color[] cols = { Color.FromArgb(90, 255, 170), Color.FromArgb(60, 220, 200), Color.FromArgb(170, 110, 255) };
                for (int band = 0; band < 3; band++)
                {
                    double ph = rnd.NextDouble() * 10, ph2 = rnd.NextDouble() * 10, ph3 = rnd.NextDouble() * 10;
                    float step = Math.Max(2f, 2.5f * sc);
                    for (float x = 0; x < w; x += step)
                    {
                        float yTop = h * (0.08f + band * 0.06f) + (float)Math.Sin(x / w * 5 + ph) * h * 0.06f + (float)Math.Sin(x / w * 13 + ph2) * h * 0.02f;
                        float len = h * (0.16f + 0.08f * (float)Math.Sin(x / w * 7 + ph3));
                        float inten = 0.55f + 0.45f * (float)Math.Sin(x / w * 9 + ph2 * 2);
                        int a = (int)((band == 2 ? 45 : 85) * inten);
                        if (a < 3 || len < 2) continue;
                        using (var br = new LinearGradientBrush(new RectangleF(x, yTop - 1, step + 1, len + 2), Color.FromArgb(0, cols[band]), Color.FromArgb(0, cols[band]), 90f))
                        {
                            var cb = new ColorBlend(3);
                            cb.Colors = new[] { Color.FromArgb(0, cols[band]), Color.FromArgb(a, cols[band]), Color.FromArgb(0, cols[band]) };
                            cb.Positions = new[] { 0f, 0.75f, 1f };
                            br.InterpolationColors = cb;
                            g.FillRectangle(br, x, yTop, step + 1, len);
                        }
                    }
                }
            }

            // 달 / 해
            if (p.Moon > 0)
            {
                float mx = w * 0.78f, my = h * 0.17f, r = (p.Moon == 2 ? 105 : 52) * sc;
                for (int i = 8; i >= 1; i--)
                    using (var gb = new SolidBrush(Color.FromArgb(9, 220, 230, 255)))
                    { float rr = r * (1 + i * 0.4f); g.FillEllipse(gb, mx - rr, my - rr, rr * 2, rr * 2); }
                using (var mb = new SolidBrush(Color.FromArgb(250, 248, 228))) g.FillEllipse(mb, mx - r, my - r, r * 2, r * 2);
                using (var cr = new SolidBrush(Color.FromArgb(70, 200, 198, 180)))
                {
                    g.FillEllipse(cr, mx - r * 0.45f, my - r * 0.3f, r * 0.4f, r * 0.35f);
                    g.FillEllipse(cr, mx + r * 0.1f, my + r * 0.15f, r * 0.3f, r * 0.28f);
                    g.FillEllipse(cr, mx - r * 0.1f, my - r * 0.6f, r * 0.22f, r * 0.2f);
                }
            }
            if (p.Sun > 0)
            {
                bool low = p.Sun == 1;
                float sx = low ? w * 0.3f : w * 0.72f, sy = low ? gY - h * 0.1f : h * 0.14f, r = (low ? 75 : 55) * sc;
                Color gc = low ? Color.FromArgb(255, 190, 120) : Color.FromArgb(255, 250, 225);
                for (int i = 12; i >= 1; i--)
                    using (var gb = new SolidBrush(Color.FromArgb(10, gc)))
                    { float rr = r * (1 + i * 0.5f); g.FillEllipse(gb, sx - rr, sy - rr, rr * 2, rr * 2); }
                using (var sb = new SolidBrush(low ? Color.FromArgb(255, 225, 170) : Color.FromArgb(255, 254, 242)))
                    g.FillEllipse(sb, sx - r, sy - r, r * 2, r * 2);
            }

            // 산 3겹
            for (int k = 0; k < 3; k++)
            {
                Color mc = k == 0 ? p.M1 : (k == 1 ? p.M2 : p.M3);
                float baseY = h * (0.50f + 0.06f * k), amp = h * (0.22f - 0.05f * k);
                var ridge = Ridge(rnd, 129, 0.52f);
                var pts = new List<PointF>();
                pts.Add(new PointF(0, h));
                for (int i = 0; i < ridge.Length; i++) pts.Add(new PointF(i * w / 128f, baseY - ridge[i] * amp));
                pts.Add(new PointF(w, h));
                float topY = baseY - amp;
                using (var br = new LinearGradientBrush(new RectangleF(0, topY - 2, w, h - topY + 4), U.Lerp(p.SnowHi, mc, 0.2f + k * 0.18f), mc, 90f))
                {
                    var cb = new ColorBlend(3);
                    cb.Colors = new[] { U.Lerp(p.SnowHi, mc, 0.2f + k * 0.18f), mc, mc };
                    cb.Positions = new[] { 0f, (baseY - topY) / (h - topY + 4) * 0.9f, 1f };
                    br.InterpolationColors = cb;
                    g.FillPolygon(br, pts.ToArray());
                }
                int fa = (int)(50 + p.Fog * 170);
                using (var fb = new LinearGradientBrush(new RectangleF(0, baseY - h * 0.09f - 1, w, h * 0.11f + 2), Color.FromArgb(0, p.Hor), Color.FromArgb(fa, p.Hor), 90f))
                    g.FillRectangle(fb, 0, baseY - h * 0.09f, w, h * 0.11f);
            }

            // 설원
            FillHill(g, rnd, w, h, gY, p.SnowHi, p.SnowLo, 0.012f);

            // 나무 + 집
            float[] haze = { 0.62f, 0.42f, 0.2f, 0f };
            for (int row = 0; row < 4; row++)
            {
                if (row == 2)
                {
                    float hillY = gY + h * 0.13f;
                    FillHill(g, rnd, w, h, hillY, U.Lerp(p.SnowHi, Color.White, 0.3f), p.SnowLo, 0.01f);
                    for (int c = 0; c < p.Cabins; c++)
                    {
                        float cx = p.Cabins == 1 ? w * 0.42f : w * (0.12f + 0.76f * (c + 0.5f) / p.Cabins) + (float)(rnd.NextDouble() - 0.5) * w * 0.04f;
                        DrawCabin(g, rnd, cx, hillY + h * 0.015f, h * (0.06f + (float)rnd.NextDouble() * 0.015f));
                    }
                }
                float baseRow = row < 2 ? gY + h * (0.01f + 0.06f * row) : gY + h * (0.17f + 0.12f * (row - 2));
                float rowH = h * (0.05f + 0.06f * row);
                Color tc = U.Lerp(p.Tree, p.Hor, haze[row]), snow = U.Lerp(p.SnowHi, p.Hor, haze[row] * 0.5f);
                float x = -rowH * (float)rnd.NextDouble();
                while (x < w + rowH)
                {
                    float th = rowH * (0.75f + (float)rnd.NextDouble() * 0.5f);
                    bool keep;
                    if (row == 3) keep = x < w * 0.2f || x > w * 0.82f;
                    else if (row == 2) keep = rnd.NextDouble() > 0.45 && (x < w * 0.3f || x > w * 0.62f);
                    else keep = rnd.NextDouble() > 0.2 && Math.Sin(x / w * 7 + row * 3) > -0.4;
                    if (keep) DrawTree(g, rnd, x, baseRow + (float)rnd.NextDouble() * h * 0.015f, th, tc, snow, p.Lights && row >= 2);
                    x += th * (0.28f + (float)rnd.NextDouble() * 0.45f);
                }
            }

            // 내리는 눈
            for (int i = 0; i < p.Flakes; i++)
            {
                float x = (float)rnd.NextDouble() * w, y = (float)rnd.NextDouble() * h;
                float rr = (0.8f + (float)Math.Pow(rnd.NextDouble(), 2) * 3.5f) * sc;
                using (var sb = new SolidBrush(Color.FromArgb(110 + rnd.Next(120), 255, 255, 255)))
                    g.FillEllipse(sb, x - rr, y - rr, rr * 2, rr * 2);
            }
            if (p.Fog > 0)
                using (var fog = new SolidBrush(Color.FromArgb((int)(p.Fog * 60), 230, 235, 242))) g.FillRectangle(fog, 0, 0, w, h);

            // 비네팅
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(-w * 0.25f, -h * 0.25f, w * 1.5f, h * 1.5f);
                using (var vb = new PathGradientBrush(path))
                {
                    vb.CenterColor = Color.FromArgb(0, 0, 0, 0);
                    vb.SurroundColors = new[] { Color.FromArgb(p.Sun == 2 ? 50 : 120, 0, 0, 10) };
                    var cb = new ColorBlend(3);
                    cb.Colors = new[] { Color.FromArgb(p.Sun == 2 ? 50 : 120, 0, 0, 10), Color.FromArgb(0, 0, 0, 0), Color.FromArgb(0, 0, 0, 0) };
                    cb.Positions = new[] { 0f, 0.45f, 1f };
                    vb.InterpolationColors = cb;
                    g.FillPath(vb, path);
                }
            }
        }
        return bmp;
    }

    static void FillHill(Graphics g, Random rnd, int w, int h, float y0, Color hi, Color lo, float amp)
    {
        double ph = rnd.NextDouble() * 10, ph2 = rnd.NextDouble() * 10;
        var pts = new List<PointF>();
        pts.Add(new PointF(0, h));
        for (int i = 0; i <= 96; i++)
        {
            float x = i * w / 96f;
            pts.Add(new PointF(x, y0 + (float)(Math.Sin(x / w * 6 + ph) * h * amp + Math.Sin(x / w * 15 + ph2) * h * amp * 0.4)));
        }
        pts.Add(new PointF(w, h));
        using (var br = new LinearGradientBrush(new RectangleF(0, y0 - h * amp * 2 - 2, w, h - y0 + h * amp * 2 + 4), hi, lo, 90f))
            g.FillPolygon(br, pts.ToArray());
    }

    static void DrawTree(Graphics g, Random rnd, float x, float by, float th, Color c, Color snow, bool lights)
    {
        using (var tb = new SolidBrush(U.Lerp(c, Color.Black, 0.35f)))
            g.FillRectangle(tb, x - th * 0.035f, by - th * 0.1f, th * 0.07f, th * 0.1f);
        using (var cb = new SolidBrush(c))
        using (var sb = new SolidBrush(snow))
        {
            for (int i = 0; i < 4; i++)
            {
                float bot = by - th * 0.08f - i * th * 0.2f, top = bot - th * 0.36f, hw = th * 0.34f * (1 - i * 0.2f);
                g.FillPolygon(cb, new[] { new PointF(x - hw, bot), new PointF(x, top), new PointF(x + hw, bot) });
                float sy = top + (bot - top) * 0.5f, sw = hw * 0.5f;
                g.FillPolygon(sb, new[] {
                    new PointF(x, top), new PointF(x + sw, sy), new PointF(x + sw * 0.5f, sy - th * 0.025f),
                    new PointF(x + sw * 0.1f, sy + th * 0.012f), new PointF(x - sw * 0.35f, sy - th * 0.02f), new PointF(x - sw, sy) });
                if (lights)
                {
                    Color[] lc = { Color.FromArgb(255, 80, 80), Color.FromArgb(255, 210, 80), Color.FromArgb(90, 170, 255), Color.FromArgb(110, 230, 120) };
                    for (int k = 0; k < 3; k++)
                    {
                        float t = (float)rnd.NextDouble();
                        float lx = x + (t * 2 - 1) * hw * 0.85f, ly = bot - th * 0.03f - (float)rnd.NextDouble() * th * 0.05f;
                        var col = lc[rnd.Next(lc.Length)];
                        float lr = Math.Max(1.5f, th * 0.018f);
                        using (var gl = new SolidBrush(Color.FromArgb(70, col))) g.FillEllipse(gl, lx - lr * 3, ly - lr * 3, lr * 6, lr * 6);
                        using (var lb = new SolidBrush(col)) g.FillEllipse(lb, lx - lr, ly - lr, lr * 2, lr * 2);
                    }
                }
            }
        }
    }

    static void DrawCabin(Graphics g, Random rnd, float x, float by, float sz)
    {
        float w = sz * 1.3f, hh = sz * 0.62f, left = x - w / 2, top = by - hh;
        using (var spill = new SolidBrush(Color.FromArgb(45, 255, 190, 110)))
            g.FillEllipse(spill, x - sz * 1.3f, by - sz * 0.2f, sz * 2.6f, sz * 0.6f);
        using (var wall = new SolidBrush(Color.FromArgb(105, 62, 38))) g.FillRectangle(wall, left, top, w, hh);
        using (var logp = new Pen(Color.FromArgb(78, 44, 26), Math.Max(1, sz * 0.02f)))
            for (int i = 1; i < 5; i++) g.DrawLine(logp, left, top + hh * i / 5f, left + w, top + hh * i / 5f);
        foreach (float wx in new[] { left + w * 0.13f, left + w * 0.65f })
        {
            float ww = w * 0.22f, wh = hh * 0.36f, wy = top + hh * 0.28f;
            using (var gl = new SolidBrush(Color.FromArgb(55, 255, 200, 100))) g.FillEllipse(gl, wx - ww * 0.8f, wy - wh * 0.9f, ww * 2.6f, wh * 2.8f);
            using (var wb = new SolidBrush(Color.FromArgb(255, 212, 115))) g.FillRectangle(wb, wx, wy, ww, wh);
            using (var bar = new Pen(Color.FromArgb(90, 50, 30), Math.Max(1, sz * 0.02f)))
            { g.DrawLine(bar, wx + ww / 2, wy, wx + ww / 2, wy + wh); g.DrawLine(bar, wx, wy + wh / 2, wx + ww, wy + wh / 2); }
        }
        using (var door = new SolidBrush(Color.FromArgb(70, 40, 25))) g.FillRectangle(door, x - w * 0.07f, top + hh * 0.38f, w * 0.14f, hh * 0.62f);
        using (var roof = new SolidBrush(Color.FromArgb(70, 40, 35)))
            g.FillPolygon(roof, new[] { new PointF(left - w * 0.1f, top + 1), new PointF(x, top - sz * 0.55f), new PointF(left + w * 1.1f, top + 1) });
        using (var snow = new SolidBrush(Color.FromArgb(245, 248, 255)))
        {
            g.FillPolygon(snow, new[] {
                new PointF(left - w * 0.13f, top - sz * 0.01f), new PointF(x, top - sz * 0.63f), new PointF(left + w * 1.13f, top - sz * 0.01f),
                new PointF(left + w * 0.95f, top + sz * 0.05f), new PointF(left + w * 0.7f, top - sz * 0.02f),
                new PointF(left + w * 0.45f, top + sz * 0.06f), new PointF(left + w * 0.2f, top - sz * 0.01f), new PointF(left, top + sz * 0.05f) });
            using (var ch = new SolidBrush(Color.FromArgb(95, 62, 52))) g.FillRectangle(ch, left + w * 0.68f, top - sz * 0.5f, w * 0.1f, sz * 0.32f);
            g.FillEllipse(snow, left + w * 0.66f, top - sz * 0.55f, w * 0.14f, sz * 0.09f);
        }
        for (int i = 0; i < 6; i++)
        {
            float r = sz * (0.06f + i * 0.03f);
            using (var smoke = new SolidBrush(Color.FromArgb(40 - i * 5, 210, 215, 225)))
                g.FillEllipse(smoke, left + w * 0.73f + i * sz * 0.09f - r, top - sz * 0.6f - i * sz * 0.16f - r, r * 2, r * 2);
        }
    }
}

class App : ApplicationContext
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    readonly SnowOverlay overlay;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly SynchronizationContext ui;

    public App(bool demo, bool lowPower)
    {
        Surface.NoScale = Environment.GetCommandLineArgs().Length > 1 && Array.IndexOf(Environment.GetCommandLineArgs(), "--noscale") >= 0;
        overlay = new SnowOverlay();
        overlay.LowPower = lowPower;
        if (demo) overlay.Demo();

        var menu = new ContextMenuStrip();
        ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        var amountMenu = new ToolStripMenuItem("눈 양");
        var amounts = new[] { new KeyValuePair<string, int>("조금", 70), new KeyValuePair<string, int>("보통", 180),
                              new KeyValuePair<string, int>("많이", 400), new KeyValuePair<string, int>("펑펑 (폭설)", 900) };
        var amountItems = new List<ToolStripMenuItem>();
        foreach (var a in amounts)
        {
            var item = new ToolStripMenuItem(a.Key) { Checked = a.Value == 180 };
            int count = a.Value;
            item.Click += (s, e) =>
            {
                overlay.TargetCount = count;
                foreach (var x in amountItems) x.Checked = x == s;
            };
            amountItems.Add(item);
            amountMenu.DropDownItems.Add(item);
        }
        menu.Items.Add(amountMenu);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add("🚜 제설차 부르기", null, (s, e) => overlay.CallPlow());
        var play = new ToolStripMenuItem("마우스로 눈 놀이 (눈덩이·눈사람)") { Checked = true, CheckOnClick = true };
        play.CheckedChanged += (s, e) => overlay.PlayEnabled = play.Checked;
        menu.Items.Add(play);
        var acc = new ToolStripMenuItem("눈 쌓이기") { Checked = true, CheckOnClick = true };
        acc.CheckedChanged += (s, e) => overlay.Accumulate = acc.Checked;
        menu.Items.Add(acc);
        menu.Items.Add("창 위의 눈 털기", null, (s, e) => overlay.ShakeAll());
        menu.Items.Add("쌓인 눈 모두 치우기", null, (s, e) => overlay.ClearSnow());
        menu.Items.Add("눈덩이·눈사람 모두 부수기", null, (s, e) => overlay.ClearObjects());
        menu.Items.Add(new ToolStripSeparator());

        var wpMenu = new ToolStripMenuItem("겨울 배경화면");
        for (int i = 0; i < Wallpapers.Names.Length; i++)
        {
            int idx = i;
            wpMenu.DropDownItems.Add(string.Format("{0}. {1}", i + 1, Wallpapers.Names[i]), null, (s, e) => ApplyWallpaper(idx));
        }
        wpMenu.DropDownItems.Add(new ToolStripSeparator());
        wpMenu.DropDownItems.Add("원래 배경화면으로 되돌리기", null, (s, e) =>
        {
            bool ok = false;
            try { ok = Wallpapers.Restore(); } catch { }
            tray.ShowBalloonTip(2000, "DesktopSnow", ok ? "원래 배경화면으로 되돌렸어요." : "되돌릴 배경화면 기록이 없어요.", ToolTipIcon.None);
        });
        menu.Items.Add(wpMenu);
        menu.Items.Add(new ToolStripSeparator());

        var power = new ToolStripMenuItem("절전");
        var lowItem = new ToolStripMenuItem("저전력 모드 (항상)") { CheckOnClick = true, Checked = overlay.LowPower };
        lowItem.CheckedChanged += (s, e) => overlay.LowPower = lowItem.Checked;
        var battItem = new ToolStripMenuItem("배터리 사용 중엔 자동으로 저전력") { CheckOnClick = true, Checked = true };
        battItem.CheckedChanged += (s, e) => overlay.AutoBattery = battItem.Checked;
        power.DropDownItems.Add(lowItem);
        power.DropDownItems.Add(battItem);
        menu.Items.Add(power);
        var pause = new ToolStripMenuItem("일시정지") { CheckOnClick = true };
        pause.CheckedChanged += (s, e) => overlay.SetPaused(pause.Checked);
        menu.Items.Add(pause);
        var startup = new ToolStripMenuItem("Windows 시작 시 자동 실행") { CheckOnClick = true, Checked = IsStartup() };
        startup.CheckedChanged += (s, e) => SetStartup(startup.Checked);
        menu.Items.Add(startup);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("사용법", null, (s, e) => MessageBox.Show(
            "❄ 눈이 모든 창 위와 작업표시줄에 쌓여요.\n" +
            "❄ 창을 움직이거나 닫으면 쌓인 눈이 우수수 떨어져요.\n\n" +
            "🧤 쌓인 눈 위에 마우스를 올리면 장갑 커서로 바뀌어요.\n" +
            "   • 클릭해서 집으면 눈덩이가 생겨요\n" +
            "   • 눈 위에서 좌우로 굴리면 점점 커져요\n" +
            "   • 놓인 눈덩이 위에 다른 눈덩이를 올리면 눈사람 완성!\n" +
            "   • 눈사람을 콕 누르면 폴짝 뛰며 손을 흔들어요\n" +
            "   • 휙 던지면 벽에 맞고 튕기다가 와장창 부서져요\n\n" +
            "🚜 메뉴의 '제설차 부르기'로 작업표시줄 위 눈을 싹 밀어요.\n" +
            "🖼 '겨울 배경화면'에서 10가지 겨울 풍경을 고를 수 있어요.\n\n" +
            "트레이 아이콘 더블클릭 = 일시정지/재개",
            "DesktopSnow 사용법", MessageBoxButtons.OK, MessageBoxIcon.Information));
        menu.Items.Add("종료", null, (s, e) => ExitThread());

        tray.Icon = MakeIcon();
        tray.Text = "DesktopSnow ❄ (우클릭: 메뉴)";
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (s, e) => pause.Checked = !pause.Checked;
        tray.Visible = true;
        tray.ShowBalloonTip(2500, "DesktopSnow", "눈이 내리기 시작합니다 ❄  쌓인 눈을 마우스로 집어 보세요. 트레이 아이콘 우클릭 = 메뉴", ToolTipIcon.None);
    }

    void ApplyWallpaper(int idx)
    {
        tray.ShowBalloonTip(1500, "DesktopSnow", "'" + Wallpapers.Names[idx] + "' 배경화면을 만드는 중…", ToolTipIcon.None);
        ThreadPool.QueueUserWorkItem(_ =>
        {
            string msg;
            try { Wallpapers.Apply(idx); msg = "바탕화면을 '" + Wallpapers.Names[idx] + "'(으)로 바꿨어요."; }
            catch (Exception ex) { msg = "배경화면을 바꾸지 못했어요: " + ex.Message; }
            ui.Post(o => tray.ShowBalloonTip(2000, "DesktopSnow", (string)o, ToolTipIcon.None), msg);
        });
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        tray.Dispose();
        overlay.Shutdown();
        base.ExitThreadCore();
    }

    static bool IsStartup()
    {
        try { using (var k = Registry.CurrentUser.OpenSubKey(RunKey)) return k != null && k.GetValue("DesktopSnow") != null; }
        catch { return false; }
    }

    static void SetStartup(bool on)
    {
        try
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (on) k.SetValue("DesktopSnow", "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue("DesktopSnow", false);
            }
        }
        catch { }
    }

    static Icon MakeIcon()
    {
        var bmp = new Bitmap(32, 32);
        using (var gr = Graphics.FromImage(bmp))
        using (var p = new Pen(Color.FromArgb(90, 190, 255), 3f))
        {
            gr.SmoothingMode = SmoothingMode.AntiAlias;
            p.StartCap = p.EndCap = LineCap.Round;
            for (int i = 0; i < 3; i++)
            {
                double a = Math.PI / 3 * i + Math.PI / 2;
                float dx = (float)Math.Cos(a) * 13, dy = (float)Math.Sin(a) * 13;
                gr.DrawLine(p, 16 - dx, 16 - dy, 16 + dx, 16 + dy);
            }
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    static void LogError(Exception ex)
    {
        try
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopSnow");
            Directory.CreateDirectory(d);
            File.AppendAllText(Path.Combine(d, "error.log"), DateTime.Now + Environment.NewLine + ex + Environment.NewLine + Environment.NewLine);
        }
        catch { }
    }

    [STAThread]
    static void Main(string[] args)
    {
        // 배경화면 미리보기용: Snow.exe --render-wallpapers <폴더>
        if (args.Length == 2 && args[0] == "--render-wallpapers")
        {
            for (int i = 0; i < Wallpapers.Names.Length; i++)
                using (var bmp = Wallpapers.Render(i, 1280, 720))
                    bmp.Save(Path.Combine(args[1], string.Format("wp_{0}.png", i + 1)), ImageFormat.Png);
            return;
        }

        bool created;
        using (var m = new Mutex(true, "DesktopSnow_SingleInstance", out created))
        {
            if (!created) return;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => LogError(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogError(e.ExceptionObject as Exception);
            try { if (!Native.SetProcessDpiAwarenessContext(new IntPtr(-4))) Native.SetProcessDPIAware(); }
            catch { try { Native.SetProcessDPIAware(); } catch { } }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new App(Array.IndexOf(args, "--demo") >= 0, Array.IndexOf(args, "--lowpower") >= 0));
        }
    }
}

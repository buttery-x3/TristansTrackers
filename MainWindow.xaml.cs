using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace TristansTrackers
{
    /// <summary>
    /// Native Win32 interop used to keep the window out of Alt+Tab and at the
    /// top of the desktop z-order without activating it.
    /// </summary>
    internal static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_NOOWNERZORDER = 0x0200;
        public static readonly IntPtr HWND_TOPMOST = new(-1);

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);
    }

    public partial class MainWindow : Window
    {
        private const double DurationSeconds = 1.0;
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TristansTrackers");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "timebar_config.json");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly DispatcherTimer _topmostTimer;
        private TimeBarConfig _config = new();
        private TimeSpan _lastFrameTime = TimeSpan.Zero;
        private double _progress;
        private bool _isLocked;

        public MainWindow()
        {
            InitializeComponent();

            _topmostTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _topmostTimer.Tick += (_, _) => EnsureTopmost();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(
                hwnd,
                NativeMethods.GWL_EXSTYLE,
                exStyle | NativeMethods.WS_EX_TOOLWINDOW);

            CompositionTarget.Rendering += OnRenderFrame;
            LoadConfig();

            if (_config.XPos < 0 || _config.YPos < 0)
            {
                var screen = SystemParameters.WorkArea;
                _config.XPos = (screen.Width - _config.Width) / 2;
                _config.YPos = (screen.Height - _config.Height) / 2;
                SaveConfig();
            }

            Width = _config.Width;
            Height = _config.Height;
            Left = _config.XPos;
            Top = _config.YPos;

            EnsureTopmost();
            _topmostTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            _topmostTimer.Stop();
            CompositionTarget.Rendering -= OnRenderFrame;
            base.OnClosed(e);
        }

        private void EnsureTopmost()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.SetWindowPos(
                hwnd,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE |
                NativeMethods.SWP_NOSIZE |
                NativeMethods.SWP_NOACTIVATE |
                NativeMethods.SWP_NOOWNERZORDER);
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (e is not RenderingEventArgs renderArgs)
            {
                return;
            }

            TimeSpan now = renderArgs.RenderingTime;
            if (_lastFrameTime == TimeSpan.Zero)
            {
                _lastFrameTime = now;
                return;
            }

            _progress += (now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;

            if (_progress >= DurationSeconds)
            {
                _progress %= DurationSeconds;
                TriggerCompletionPulse();
            }

            FillBar.Width = MainBorder.ActualWidth * (_progress / DurationSeconds);
        }

        private void TriggerCompletionPulse()
        {
            static DoubleAnimationUsingKeyFrames PulseAnimation(double peakValue)
            {
                var animation = new DoubleAnimationUsingKeyFrames
                {
                    Duration = TimeSpan.FromMilliseconds(330),
                    FillBehavior = FillBehavior.Stop
                };

                animation.KeyFrames.Add(new SplineDoubleKeyFrame(peakValue, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(45))));
                animation.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(330))));
                return animation;
            }

            CompletionPulseOverlay.BeginAnimation(OpacityProperty, PulseAnimation(1.0), HandoffBehavior.SnapshotAndReplace);
            CompletionGlow.BeginAnimation(DropShadowEffect.OpacityProperty, PulseAnimation(1.0), HandoffBehavior.SnapshotAndReplace);
            CompletionGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, PulseAnimation(18.0), HandoffBehavior.SnapshotAndReplace);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            DragMove();
            _config.XPos = Left;
            _config.YPos = Top;
            SaveConfig();
        }

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            LockIcon.Text = _isLocked ? "🔒" : "🔓";
        }

        private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            LockButton.Visibility = Visibility.Visible;
        }

        private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isLocked)
            {
                LockButton.Visibility = Visibility.Collapsed;
            }
        }

        private void LoadConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);

                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    _config = JsonSerializer.Deserialize<TimeBarConfig>(json) ?? new TimeBarConfig();
                }
                else
                {
                    SaveConfig();
                }
            }
            catch
            {
                _config = new TimeBarConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(ConfigDirectory);
                string json = JsonSerializer.Serialize(_config, JsonOptions);
                File.WriteAllText(ConfigPath, json);
            }
            catch
            {
                // Configuration persistence should never prevent the timer from running.
            }
        }
    }
}

using System.IO;
using System.Media;
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
        private static readonly int[] AlarmDurationMinutes =
        [
            1, 2, 5, 10, 15, 20, 30, 45, 60, 90, 120
        ];
        private static readonly string ConfigDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TristansTrackers");
        private static readonly string ConfigPath = Path.Combine(ConfigDirectory, "timebar_config.json");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        private readonly DispatcherTimer _topmostTimer;
        private readonly HudMenuManager _menuManager;
        private TimeBarConfig _config = new();
        private TimeSpan _lastFrameTime = TimeSpan.Zero;
        private TimeSpan _alarmDuration = TimeSpan.Zero;
        private DateTimeOffset? _alarmEndsAtUtc;
        private double _progress;
        private double _upperContentHeight;
        private bool _isLocked;
        private bool _isPointerOver;
        private AlarmState _alarmState;

        private enum AlarmState
        {
            None,
            Running,
            Completed
        }

        public MainWindow()
        {
            InitializeComponent();

            _menuManager = new HudMenuManager();

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
            _menuManager.Close();
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

            // Always raise the independent menu after the tracker so it stays
            // above the bars within the topmost z-order band.
            _menuManager.EnsureTopmost();
        }

        private void OnRenderFrame(object? sender, EventArgs e)
        {
            if (e is not RenderingEventArgs renderArgs)
            {
                return;
            }

            UpdateAlarm(DateTimeOffset.UtcNow);

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
            CompletionPulseOverlay.BeginAnimation(OpacityProperty, PulseAnimation(1.0), HandoffBehavior.SnapshotAndReplace);
            CompletionGlow.BeginAnimation(DropShadowEffect.OpacityProperty, PulseAnimation(1.0), HandoffBehavior.SnapshotAndReplace);
            CompletionGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, PulseAnimation(18.0), HandoffBehavior.SnapshotAndReplace);
        }

        private static DoubleAnimationUsingKeyFrames PulseAnimation(double peakValue)
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

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isLocked || e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _menuManager.Close();
            DragMove();
            _config.XPos = Left;
            _config.YPos = _alarmState == AlarmState.None ? Top : Top + _upperContentHeight;
            SaveConfig();
        }

        private void AlarmButton_Click(object sender, RoutedEventArgs e)
        {
            if (_alarmState == AlarmState.Completed)
            {
                CancelAlarm();
                return;
            }

            var items = new List<HudMenuItem>();
            foreach (int minutes in AlarmDurationMinutes)
            {
                int selectedMinutes = minutes;
                items.Add(new HudMenuItem(
                    FormatDurationMenuLabel(selectedMinutes),
                    () => StartAlarm(selectedMinutes)));
            }

            if (_alarmState == AlarmState.Running)
            {
                items.Add(new HudMenuItem("Cancel alarm", CancelAlarm, SeparatorBefore: true));
            }

            _menuManager.Show(items, HudMenuAnchor.ForElement(GetElementScreenBounds(AlarmButton)));
            EnsureTopmost();
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _menuManager.Show(
                [new HudMenuItem("Exit", () => Application.Current.Shutdown(), IsDestructive: true)],
                HudMenuAnchor.AtCursor(ToScreenDips(e.GetPosition(this))));
            EnsureTopmost();
        }

        private void StartAlarm(int minutes)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            _alarmDuration = TimeSpan.FromMinutes(minutes);
            _alarmEndsAtUtc = now + _alarmDuration;
            _alarmState = AlarmState.Running;

            CompletedAlarmPulseOverlay.BeginAnimation(OpacityProperty, null);
            CompletedAlarmGlow.BeginAnimation(DropShadowEffect.OpacityProperty, null);
            CompletedAlarmGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, null);
            AlarmButton.ToolTip = "Alarm options";

            ApplyAlarmStateLayout();
            UpdateAlarm(now);
        }

        private void UpdateAlarm(DateTimeOffset now)
        {
            if (_alarmEndsAtUtc is not DateTimeOffset alarmEndsAtUtc)
            {
                return;
            }

            TimeSpan remaining = alarmEndsAtUtc - now;
            if (remaining <= TimeSpan.Zero)
            {
                CompleteAlarm();
                return;
            }

            double remainingRatio = Math.Clamp(
                remaining.TotalSeconds / _alarmDuration.TotalSeconds,
                0,
                1);
            double elapsedRatio = 1 - remainingRatio;
            AlarmFillBar.Width = AlarmGrid.ActualWidth * elapsedRatio;

            if (_isPointerOver)
            {
                AlarmRemainingText.Text = FormatRemainingMinutes(remaining);
                AlarmRemainingText.Visibility = Visibility.Visible;
            }
        }

        private void CompleteAlarm()
        {
            _alarmEndsAtUtc = null;
            _alarmState = AlarmState.Completed;
            AlarmFillBar.Width = 0;
            AlarmRemainingText.Visibility = Visibility.Collapsed;
            AlarmButton.ToolTip = "Dismiss alarm";
            ApplyAlarmStateLayout();

            CompletedAlarmPulseOverlay.BeginAnimation(OpacityProperty, PulseAnimation(1.0), HandoffBehavior.SnapshotAndReplace);
            CompletedAlarmGlow.BeginAnimation(DropShadowEffect.OpacityProperty, PulseAnimation(1.0), HandoffBehavior.SnapshotAndReplace);
            CompletedAlarmGlow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, PulseAnimation(18.0), HandoffBehavior.SnapshotAndReplace);
            SystemSounds.Exclamation.Play();
        }

        private void CancelAlarm()
        {
            _alarmEndsAtUtc = null;
            _alarmDuration = TimeSpan.Zero;
            _alarmState = AlarmState.None;
            AlarmButton.ToolTip = "Set alarm";
            ApplyAlarmStateLayout();
        }

        private void ApplyAlarmStateLayout()
        {
            _menuManager.Close();

            switch (_alarmState)
            {
                case AlarmState.Running:
                    _upperContentHeight = _config.Height;
                    AlarmBorder.Visibility = Visibility.Visible;
                    CompletedAlarmPanel.Visibility = Visibility.Collapsed;
                    break;

                case AlarmState.Completed:
                    double completedAlarmSize = Math.Max(1, _config.Width);
                    double completedIconSize = Math.Max(1, completedAlarmSize - 12);
                    _upperContentHeight = completedAlarmSize;
                    CompletedAlarmPanel.Width = completedAlarmSize;
                    CompletedAlarmPanel.Height = completedAlarmSize;
                    CompletedAlarmButton.Width = completedAlarmSize;
                    CompletedAlarmButton.Height = completedAlarmSize;
                    CompletedAlarmIconViewbox.Width = completedIconSize;
                    CompletedAlarmIconViewbox.Height = completedIconSize;
                    CompletedAlarmPulseOverlay.Width = completedAlarmSize;
                    CompletedAlarmPulseOverlay.Height = completedAlarmSize;
                    AlarmBorder.Visibility = Visibility.Collapsed;
                    CompletedAlarmPanel.Visibility = Visibility.Visible;
                    break;

                default:
                    _upperContentHeight = 0;
                    AlarmRemainingText.Visibility = Visibility.Collapsed;
                    AlarmFillBar.Width = 0;
                    AlarmBorder.Visibility = Visibility.Collapsed;
                    CompletedAlarmPanel.Visibility = Visibility.Collapsed;
                    break;
            }

            AlarmRow.Height = new GridLength(_upperContentHeight);
            Height = _config.Height + _upperContentHeight;
            Top = _upperContentHeight == 0
                ? _config.YPos
                : Math.Max(SystemParameters.WorkArea.Top, _config.YPos - _upperContentHeight);

            EnsureTopmost();
        }

        private static string FormatRemainingMinutes(TimeSpan remaining)
        {
            int minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
            return $"{minutes} min";
        }

        private static string FormatDurationMenuLabel(int minutes)
        {
            return minutes switch
            {
                1 => "1 minute",
                120 => "2 hours",
                _ => $"{minutes} minutes"
            };
        }

        private void CompletedAlarmButton_Click(object sender, RoutedEventArgs e)
        {
            CancelAlarm();
        }

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            LockIcon.Text = _isLocked ? "🔒" : "🔓";
        }

        private Point ToScreenDips(Point pointInWindow)
        {
            Point screenPixels = PointToScreen(pointInWindow);
            PresentationSource? source = PresentationSource.FromVisual(this);
            return source?.CompositionTarget?.TransformFromDevice.Transform(screenPixels)
                ?? screenPixels;
        }

        private Rect GetElementScreenBounds(FrameworkElement element)
        {
            Point topLeft = element.TranslatePoint(new Point(0, 0), this);
            Point bottomRight = element.TranslatePoint(
                new Point(element.ActualWidth, element.ActualHeight),
                this);
            return new Rect(ToScreenDips(topLeft), ToScreenDips(bottomRight));
        }

        private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            _isPointerOver = true;
            AlarmButton.Visibility = Visibility.Visible;
            LockButton.Visibility = Visibility.Visible;

            if (_alarmState == AlarmState.Running)
            {
                if (_alarmEndsAtUtc is DateTimeOffset alarmEndsAtUtc)
                {
                    AlarmRemainingText.Text = FormatRemainingMinutes(alarmEndsAtUtc - DateTimeOffset.UtcNow);
                }

                AlarmRemainingText.Visibility = Visibility.Visible;
            }
        }

        private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            _isPointerOver = false;
            AlarmButton.Visibility = Visibility.Collapsed;

            AlarmRemainingText.Visibility = Visibility.Collapsed;

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

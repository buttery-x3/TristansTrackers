using Newtonsoft.Json;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;


namespace TristansTrackers
{
    /// <summary>
    /// Native Win32 flags for forcing TopMost / ToolWindow behavior.
    /// Makes the window behave more like a floating HUD widget instead of a normal app window.
    /// </summary>
    internal static class NativeMethods
    {
        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;   // Hides from Alt+Tab
        public const int WS_EX_TOPMOST = 0x00000008;      // Stay above normal windows
        public const int WS_EX_NOACTIVATE = 0x08000000;   // Optional: don't steal focus

        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }

    public partial class MainWindow : Window
    {
        // ---- Smooth Progress Bar Timing State ----

        private double _progress = 0.0;
        private const double DurationSeconds = 1.0; // How long a full bar fill takes
        private TimeSpan _lastFrameTime = TimeSpan.Zero;  // Used for deltaTime calculation

        // ---- Locking / Dragging ----

        private bool _isLocked = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        /// <summary>
        /// Runs when the window finishes loading.
        /// Sets native window flags and hooks up the render loop.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // --- Apply special window flags (TopMost, ToolWindow, etc.) ---
            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            exStyle |= NativeMethods.WS_EX_TOPMOST;
            exStyle |= NativeMethods.WS_EX_TOOLWINDOW;
            // exStyle |= NativeMethods.WS_EX_NOACTIVATE; // Enable if you want "click-through" behavior
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle);

            // --- Hook the composition render loop for buttery-smooth animation ---
            CompositionTarget.Rendering += OnRenderFrame;

            // --- Load saved config (position, etc.) ---
            LoadConfig();

            // If first launch (positions -1), center on primary monitor
            if (_config.XPos < 0 || _config.YPos < 0)
            {
                var screen = System.Windows.SystemParameters.WorkArea;

                _config.XPos = (screen.Width - _config.Width) / 2;
                _config.YPos = (screen.Height - _config.Height) / 2;

                SaveConfig(); // save initial position
            }

            // Apply saved values
            this.Width = _config.Width;
            this.Height = _config.Height;

            this.Left = _config.XPos;
            this.Top = _config.YPos;
        }

        /// <summary>
        /// High-fidelity animation callback (fires once per rendered frame, synced to monitor refresh).
        /// Computes deltaTime and updates the bar smoothly.
        /// </summary>
        private void OnRenderFrame(object sender, EventArgs e)
        {
            if (e is RenderingEventArgs renderArgs)
            {
                TimeSpan now = renderArgs.RenderingTime;

                // First frame — initialize and exit
                if (_lastFrameTime == TimeSpan.Zero)
                {
                    _lastFrameTime = now;
                    return;
                }

                // Compute deltaTime between frames
                double deltaSeconds = (now - _lastFrameTime).TotalSeconds;
                _lastFrameTime = now;

                // Advance progress loop
                _progress += deltaSeconds;

                if (_progress >= DurationSeconds)
                    _progress = 0;

                double pct = _progress / DurationSeconds;

                // Update bar width (safe even before layout finishes)
                FillBar.Width = MainBorder.ActualWidth * pct;
            }
        }

        // -------------------------------------------------------------
        //                       Window Dragging
        // -------------------------------------------------------------

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // If locked, the window cannot be moved.
            if (_isLocked) return;

            if (e.ChangedButton == MouseButton.Left)
                DragMove();

            // Save new position AFTER the move finishes:
            _config.XPos = this.Left;
            _config.YPos = this.Top;
            SaveConfig();
        }

        // -------------------------------------------------------------
        //                       Lock Button
        // -------------------------------------------------------------

        private void LockButton_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = !_isLocked;
            LockIcon.Text = _isLocked ? "🔒" : "🔓";
        }

        // -------------------------------------------------------------
        //                  Hover Show/Hide Lock Button
        // -------------------------------------------------------------

        private void RootGrid_MouseEnter(object sender, MouseEventArgs e)
        {
            // Only show lock button when not locked
            LockButton.Visibility = Visibility.Visible;
        }

        private void RootGrid_MouseLeave(object sender, MouseEventArgs e)
        {
            // Hide if locked; unlocked = stays visible
            if (_isLocked)
                LockButton.Visibility = Visibility.Collapsed;
        }

        // -------------------------------------------------------------
        //                  Configuration Load/Save
        // -------------------------------------------------------------

        private readonly string _configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "TristansTrackers", "timebar_config.json");

        private TimeBarConfig _config = new TimeBarConfig();

        private void LoadConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    _config = JsonConvert.DeserializeObject<TimeBarConfig>(json)
                              ?? new TimeBarConfig();
                }
                else
                {
                    SaveConfig(); // generate fresh one
                }
            }
            catch
            {
                // If anything explodes, use defaults
                _config = new TimeBarConfig();
            }
        }

        private void SaveConfig()
        {
            try
            {
                var dir = Path.GetDirectoryName(_configPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(_config, Formatting.Indented);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
                // ignore failures; app still works
            }
        }
    }
}

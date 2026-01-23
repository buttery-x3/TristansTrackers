using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TristansTrackers
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 



    public partial class MainWindow : Window
    {


        private DispatcherTimer _timer;
        private int _secondsElapsed = 0;

        public MainWindow()
        {
            InitializeComponent();

            this.Loaded += MainWindow_Loaded;
        }
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (TimerText == null) return;

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();

            TimerText.Text = "00:00"; // safe now
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            _secondsElapsed++;

            int minutes = _secondsElapsed / 60;
            int seconds = _secondsElapsed % 60;

            TimerText.Text = $"{minutes:00}:{seconds:00}";
        }

        private void ActivitySelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TimerText == null) return;
            _secondsElapsed = 0;
            TimerText.Text = "00:00";
        }

        private bool _isExpanded = false;

        private void ExpandButton_Click(object sender, RoutedEventArgs e)
        {
            RotateTransform rt = ExpandIcon.RenderTransform as RotateTransform;

            if (_isExpanded)
            {
                // Collapse
                this.Width = 300;
                this.Height = 100;

                MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xAA, 0x1E, 0x1E, 0x1E));
                rt.Angle = 0; // arrow points down
            }
            else
            {
                // Expand
                this.Width = 300;
                this.Height = 300;

                MainBorder.Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E));
                rt.Angle = 180; // arrow points up
            }

            _isExpanded = !_isExpanded;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
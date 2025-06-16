using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace WindowsSipPhone.Controls
{
    public partial class AudioLevelMeter : System.Windows.Controls.UserControl
    {
        private DispatcherTimer _updateTimer;
        private double _inputLevel = 0.0;
        private double _outputLevel = 0.0;
        
        public static readonly DependencyProperty InputLevelProperty =
            DependencyProperty.Register("InputLevel", typeof(double), typeof(AudioLevelMeter),
                new PropertyMetadata(0.0, OnInputLevelChanged));

        public static readonly DependencyProperty OutputLevelProperty =
            DependencyProperty.Register("OutputLevel", typeof(double), typeof(AudioLevelMeter),
                new PropertyMetadata(0.0, OnOutputLevelChanged));

        public static readonly DependencyProperty ShowLabelsProperty =
            DependencyProperty.Register("ShowLabels", typeof(bool), typeof(AudioLevelMeter),
                new PropertyMetadata(true));        public static readonly DependencyProperty OrientationProperty =
            DependencyProperty.Register("Orientation", typeof(System.Windows.Controls.Orientation), typeof(AudioLevelMeter),
                new PropertyMetadata(System.Windows.Controls.Orientation.Horizontal));

        public double InputLevel
        {
            get => (double)GetValue(InputLevelProperty);
            set => SetValue(InputLevelProperty, Math.Max(0, Math.Min(1, value)));
        }

        public double OutputLevel
        {
            get => (double)GetValue(OutputLevelProperty);
            set => SetValue(OutputLevelProperty, Math.Max(0, Math.Min(1, value)));
        }

        public bool ShowLabels
        {
            get => (bool)GetValue(ShowLabelsProperty);
            set => SetValue(ShowLabelsProperty, value);
        }        public System.Windows.Controls.Orientation Orientation
        {
            get => (System.Windows.Controls.Orientation)GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public AudioLevelMeter()
        {
            InitializeComponent();
            
            // Update timer for smooth animation
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 20 FPS
            };
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();
        }

        private static void OnInputLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioLevelMeter meter)
            {
                meter._inputLevel = (double)e.NewValue;
                meter.UpdateMeterDisplay();
            }
        }

        private static void OnOutputLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioLevelMeter meter)
            {
                meter._outputLevel = (double)e.NewValue;
                meter.UpdateMeterDisplay();
            }
        }

        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            // Add some decay to the levels for more natural looking meters
            _inputLevel = Math.Max(0, _inputLevel - 0.05);
            _outputLevel = Math.Max(0, _outputLevel - 0.05);
            
            UpdateMeterDisplay();
        }

        private void UpdateMeterDisplay()
        {
            try
            {
                // Update input meter
                if (InputMeterFill != null)
                {
                    var inputWidth = Math.Max(0, Math.Min(1, _inputLevel)) * InputMeterBackground.ActualWidth;
                    InputMeterFill.Width = inputWidth;
                    
                    // Change color based on level
                    InputMeterFill.Fill = GetLevelBrush(_inputLevel);
                }

                // Update output meter
                if (OutputMeterFill != null)
                {
                    var outputWidth = Math.Max(0, Math.Min(1, _outputLevel)) * OutputMeterBackground.ActualWidth;
                    OutputMeterFill.Width = outputWidth;
                    
                    // Change color based on level
                    OutputMeterFill.Fill = GetLevelBrush(_outputLevel);
                }

                // Update level text
                if (InputLevelText != null && ShowLabels)
                {
                    InputLevelText.Text = $"{(_inputLevel * 100):F0}%";
                }

                if (OutputLevelText != null && ShowLabels)
                {
                    OutputLevelText.Text = $"{(_outputLevel * 100):F0}%";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUDIO METER DEBUG] Error updating display: {ex.Message}");
            }
        }        private System.Windows.Media.Brush GetLevelBrush(double level)
        {
            // Green for low levels, yellow for medium, red for high
            if (level < 0.6)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green
            else if (level < 0.8)
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 191, 36)); // Yellow
            else
                return new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red
        }

        // Method to simulate audio levels for testing
        public void SimulateAudioLevels()
        {
            var random = new Random();
            InputLevel = random.NextDouble() * 0.8; // Max 80% for input
            OutputLevel = random.NextDouble() * 0.9; // Max 90% for output
        }

        // Clean up timer
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _updateTimer?.Stop();
        }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // Simulate some audio activity for demonstration
            SimulateAudioLevels();
            Console.WriteLine($"[AUDIO METER DEBUG] Simulated levels - Input: {InputLevel:F2}, Output: {OutputLevel:F2}");
        }
    }
}

using System;
using System.Windows;
using System.Windows.Controls;

namespace WindowsSipPhone.Controls
{
    public partial class AudioQualityControl : System.Windows.Controls.UserControl
    {
        public enum QualityPreset
        {
            Battery,
            Balanced,
            High,
            Studio
        }

        public static readonly DependencyProperty CurrentPresetProperty =
            DependencyProperty.Register("CurrentPreset", typeof(QualityPreset), typeof(AudioQualityControl),
                new PropertyMetadata(QualityPreset.Balanced, OnCurrentPresetChanged));

        public static readonly DependencyProperty EchoCancellationEnabledProperty =
            DependencyProperty.Register("EchoCancellationEnabled", typeof(bool), typeof(AudioQualityControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty NoiseReductionEnabledProperty =
            DependencyProperty.Register("NoiseReductionEnabled", typeof(bool), typeof(AudioQualityControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty VoiceActivationEnabledProperty =
            DependencyProperty.Register("VoiceActivationEnabled", typeof(bool), typeof(AudioQualityControl),
                new PropertyMetadata(false));

        public QualityPreset CurrentPreset
        {
            get => (QualityPreset)GetValue(CurrentPresetProperty);
            set => SetValue(CurrentPresetProperty, value);
        }

        public bool EchoCancellationEnabled
        {
            get => (bool)GetValue(EchoCancellationEnabledProperty);
            set => SetValue(EchoCancellationEnabledProperty, value);
        }

        public bool NoiseReductionEnabled
        {
            get => (bool)GetValue(NoiseReductionEnabledProperty);
            set => SetValue(NoiseReductionEnabledProperty, value);
        }

        public bool VoiceActivationEnabled
        {
            get => (bool)GetValue(VoiceActivationEnabledProperty);
            set => SetValue(VoiceActivationEnabledProperty, value);
        }

        public event EventHandler<QualityPreset>? PresetChanged;
        public event EventHandler<bool>? EchoCancellationChanged;
        public event EventHandler<bool>? NoiseReductionChanged;
        public event EventHandler<bool>? VoiceActivationChanged;

        public AudioQualityControl()
        {
            InitializeComponent();
            UpdateQualityDetails();
        }

        private static void OnCurrentPresetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AudioQualityControl control)
            {
                control.UpdateQualityDetails();
                control.PresetChanged?.Invoke(control, (QualityPreset)e.NewValue);
            }
        }

        private void QualityPresetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QualityPresetsComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var presetTag = selectedItem.Tag.ToString();
                if (Enum.TryParse<QualityPreset>(presetTag, out var preset))
                {
                    Console.WriteLine($"[AUDIO QUALITY DEBUG] User selected preset: {preset}");
                    CurrentPreset = preset;
                    ApplyPresetSettings(preset);
                }
            }
        }

        private void ApplyPresetSettings(QualityPreset preset)
        {
            switch (preset)
            {
                case QualityPreset.Battery:
                    EchoCancellationEnabled = false;
                    NoiseReductionEnabled = false;
                    VoiceActivationEnabled = true;
                    break;
                case QualityPreset.Balanced:
                    EchoCancellationEnabled = true;
                    NoiseReductionEnabled = true;
                    VoiceActivationEnabled = false;
                    break;
                case QualityPreset.High:
                    EchoCancellationEnabled = true;
                    NoiseReductionEnabled = true;
                    VoiceActivationEnabled = false;
                    break;
                case QualityPreset.Studio:
                    EchoCancellationEnabled = true;
                    NoiseReductionEnabled = true;
                    VoiceActivationEnabled = false;
                    break;
            }

            // Update UI checkboxes
            EchoCancellationCheckBox.IsChecked = EchoCancellationEnabled;
            NoiseReductionCheckBox.IsChecked = NoiseReductionEnabled;
            VoiceActivationCheckBox.IsChecked = VoiceActivationEnabled;

            Console.WriteLine($"[AUDIO QUALITY DEBUG] Applied settings for {preset}: Echo={EchoCancellationEnabled}, Noise={NoiseReductionEnabled}, VAD={VoiceActivationEnabled}");
        }

        private void UpdateQualityDetails()
        {
            var (description, sampleRate, bitRate) = CurrentPreset switch
            {
                QualityPreset.Battery => ("Optimized for battery life with basic quality", "8 kHz", "32 kbps"),
                QualityPreset.Balanced => ("Balanced quality with moderate CPU usage", "16 kHz", "64 kbps"),
                QualityPreset.High => ("High quality audio with increased processing", "22 kHz", "128 kbps"),
                QualityPreset.Studio => ("Studio-grade quality with maximum processing", "48 kHz", "320 kbps"),
                _ => ("Unknown preset", "16 kHz", "64 kbps")
            };

            if (QualityDescriptionText != null)
                QualityDescriptionText.Text = description;
            if (SampleRateText != null)
                SampleRateText.Text = sampleRate;
            if (BitRateText != null)
                BitRateText.Text = bitRate;
        }

        private void EchoCancellationCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            EchoCancellationEnabled = EchoCancellationCheckBox.IsChecked == true;
            EchoCancellationChanged?.Invoke(this, EchoCancellationEnabled);
            Console.WriteLine($"[AUDIO QUALITY DEBUG] Echo cancellation: {EchoCancellationEnabled}");
        }

        private void NoiseReductionCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            NoiseReductionEnabled = NoiseReductionCheckBox.IsChecked == true;
            NoiseReductionChanged?.Invoke(this, NoiseReductionEnabled);
            Console.WriteLine($"[AUDIO QUALITY DEBUG] Noise reduction: {NoiseReductionEnabled}");
        }

        private void VoiceActivationCheckBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
            VoiceActivationEnabled = VoiceActivationCheckBox.IsChecked == true;
            VoiceActivationChanged?.Invoke(this, VoiceActivationEnabled);
            Console.WriteLine($"[AUDIO QUALITY DEBUG] Voice activation: {VoiceActivationEnabled}");
        }

        // Method to get current audio settings summary
        public string GetSettingsSummary()
        {
            return $"Preset: {CurrentPreset}, Echo: {EchoCancellationEnabled}, Noise: {NoiseReductionEnabled}, VAD: {VoiceActivationEnabled}";
        }
    }
}

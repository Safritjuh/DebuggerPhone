using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace WindowsSipPhone
{
    public class EnhancedRingtoneService : IRingtoneService
    {
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioFileReader;
        private string _selectedRingtone = "Traditional Ring";
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _ringtoneTask;
        private bool _isPlaying = false;
          // Base paths for ringtone files
        private readonly string _builtInRingtonesPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Infrastructure", "Resources", "Audio", "Ringtones");
            
        private readonly string _customRingtonesPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Services", "Audio", "Ringtones");
        
        // Built-in ringtone names mapping to WAV files
        private readonly Dictionary<string, string> _builtInRingtones = new()
        {
            { "Traditional Ring", "traditional-ring.wav" },
            { "Classic Bell", "classic-bell.wav" },
            { "European Ring", "european-ring.wav" },
            { "Old Telephone", "old-telephone.wav" },
            { "Modern Tone", "modern-tone.wav" }
        };
        
        public string[] AvailableRingtones 
        {
            get
            {
                var ringtones = new List<string>();
                
                // Add built-in ringtones
                ringtones.AddRange(_builtInRingtones.Keys);
                
                // Add custom ringtones from Services/Audio/Ringtones
                ringtones.AddRange(GetCustomRingtones());
                
                return ringtones.ToArray();
            }
        }
          
        public string SelectedRingtone 
        { 
            get => _selectedRingtone; 
            set => _selectedRingtone = value ?? "Traditional Ring"; 
        }
        
        public bool IsPlaying => _isPlaying;
        
        public void PlayRingtone(string? ringtoneName = null)
        {
            try
            {
                var ringtone = ringtoneName ?? _selectedRingtone;
                
                // Stop any currently playing sound
                StopRingtone();
                
                // Start new ringtone in a loop
                _cancellationTokenSource = new CancellationTokenSource();
                _ringtoneTask = PlayRingtoneLoop(ringtone, _cancellationTokenSource.Token);
                
                Console.WriteLine($"[RINGTONE DEBUG] Started playing ringtone: {ringtone}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing ringtone: {ex.Message}");
            }
        }
        
        private async Task PlayRingtoneLoop(string ringtone, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Play the WAV file
                    await PlaySingleRingtoneWav(ringtone, cancellationToken);
                    
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    
                    // Wait before repeating (1 second pause between rings)
                    await Task.Delay(1000, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                Console.WriteLine("[RINGTONE DEBUG] Ringtone playback cancelled");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RINGTONE DEBUG] Error in ringtone loop: {ex.Message}");
            }
        }
        
        private async Task PlaySingleRingtoneWav(string ringtone, CancellationToken cancellationToken)
        {
            try
            {                // Get the audio file path for the selected ringtone
                var audioFilePath = GetAudioFilePath(ringtone);
                
                if (!File.Exists(audioFilePath))
                {
                    Console.WriteLine($"[RINGTONE DEBUG] Audio file not found: {audioFilePath}");
                    return;
                }
                
                // Dispose previous resources
                _audioFileReader?.Dispose();
                _waveOut?.Dispose();
                  // Load the audio file
                _audioFileReader = new AudioFileReader(audioFilePath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_audioFileReader);
                
                _isPlaying = true;
                _waveOut.Play();
                
                Console.WriteLine($"[RINGTONE DEBUG] Playing audio file: {Path.GetFileName(audioFilePath)}");
                
                // Wait for playback to complete or cancellation
                while (_waveOut.PlaybackState == PlaybackState.Playing && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(100, cancellationToken);
                }
                
                _waveOut?.Stop();
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RINGTONE DEBUG] Error playing WAV file: {ex.Message}");
            }        }
        
        private List<string> GetCustomRingtones()
        {
            var customRingtones = new List<string>();
            
            try
            {
                // Ensure custom ringtones directory exists
                if (!Directory.Exists(_customRingtonesPath))
                {
                    Directory.CreateDirectory(_customRingtonesPath);
                    return customRingtones;
                }
                
                // Get all supported audio files
                var supportedExtensions = new[] { "*.wav", "*.mp3" };
                var audioFiles = new List<string>();
                
                foreach (var extension in supportedExtensions)
                {
                    audioFiles.AddRange(Directory.GetFiles(_customRingtonesPath, extension, SearchOption.TopDirectoryOnly));
                }
                
                // Convert file paths to display names (filename without extension)
                foreach (var filePath in audioFiles)
                {
                    var displayName = Path.GetFileNameWithoutExtension(filePath);
                    customRingtones.Add(displayName);
                }
                
                Console.WriteLine($"[RINGTONE DEBUG] Found {customRingtones.Count} custom ringtones");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RINGTONE DEBUG] Error scanning custom ringtones: {ex.Message}");
            }
            
            return customRingtones;
        }
        
        private string GetAudioFilePath(string ringtone)
        {
            // Check if it's a built-in ringtone
            if (_builtInRingtones.ContainsKey(ringtone))
            {
                var fileName = _builtInRingtones[ringtone];
                var builtInPath = Path.Combine(_builtInRingtonesPath, fileName);
                
                if (File.Exists(builtInPath))
                {
                    return builtInPath;
                }
            }
            
            // Check for custom ringtones (try both WAV and MP3)
            var customWavPath = Path.Combine(_customRingtonesPath, $"{ringtone}.wav");
            var customMp3Path = Path.Combine(_customRingtonesPath, $"{ringtone}.mp3");
            
            if (File.Exists(customWavPath))
            {
                return customWavPath;
            }
            
            if (File.Exists(customMp3Path))
            {
                return customMp3Path;
            }
            
            // Fallback to default built-in ringtone
            Console.WriteLine($"[RINGTONE DEBUG] Ringtone '{ringtone}' not found, using default");
            return Path.Combine(_builtInRingtonesPath, "traditional-ring.wav");
        }
          
        public void StopRingtone()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;
                
                _audioFileReader?.Dispose();
                _audioFileReader = null;
                
                _isPlaying = false;
                
                Console.WriteLine("[RINGTONE DEBUG] Stopped ringtone playback");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping ringtone: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            StopRingtone();
        }
    }
}

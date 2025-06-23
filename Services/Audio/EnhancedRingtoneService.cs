using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WindowsSipPhone
{
    public class EnhancedRingtoneService : IRingtoneService
    {
        private WaveOutEvent? _waveOut;
        private string _selectedRingtone = "Traditional Ring";
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _ringtoneTask;
        private bool _isPlaying = false;
        
        public string[] AvailableRingtones => new[]
        {
            "Traditional Ring",
            "Classic Bell", 
            "European Ring",
            "Old Telephone",
            "Modern Tone"
        };
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
                    // Generate and play the ringtone sound using NAudio
                    PlaySingleRingtone(ringtone);
                    
                    // Wait before repeating (2 seconds pause between rings)
                    await Task.Delay(2000, cancellationToken);
                    
                    // Stop current audio before next iteration
                    _waveOut?.Stop();
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
        
        private void PlaySingleRingtone(string ringtone)
        {
            try
            {
                // Dispose previous wave out if exists
                _waveOut?.Dispose();
                
                // Create the ringtone audio based on selected type
                var sampleProvider = CreateRingtoneSampleProvider(ringtone);
                
                // Initialize NAudio for playback
                _waveOut = new WaveOutEvent();
                _waveOut.Init(sampleProvider);
                _waveOut.Play();
                
                _isPlaying = true;
                Console.WriteLine($"[RINGTONE DEBUG] Playing single ringtone: {ringtone}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RINGTONE DEBUG] Error playing single ringtone: {ex.Message}");
            }
        }
        
        private ISampleProvider CreateRingtoneSampleProvider(string ringtone)
        {
            const int sampleRate = 44100;
            
            switch (ringtone)
            {
                case "Traditional Ring":
                    return CreateTraditionalRing(sampleRate);
                    
                case "Classic Bell":
                    return CreateClassicBell(sampleRate);
                    
                case "European Ring":
                    return CreateEuropeanRing(sampleRate);
                    
                case "Old Telephone":
                    return CreateOldTelephone(sampleRate);
                    
                case "Modern Tone":
                    return CreateModernTone(sampleRate);
                    
                default:
                    return CreateTraditionalRing(sampleRate);
            }
        }
        
        private ISampleProvider CreateTraditionalRing(int sampleRate)
        {
            // Traditional US phone ring: Two-tone (440Hz + 480Hz) for 2 seconds
            var tone1 = new SignalGenerator(sampleRate, 1) { Frequency = 440, Type = SignalGeneratorType.Sin, Gain = 0.3 };
            var tone2 = new SignalGenerator(sampleRate, 1) { Frequency = 480, Type = SignalGeneratorType.Sin, Gain = 0.3 };
            var mixed = new MixingSampleProvider(new[] { tone1, tone2 });
            return mixed.Take(TimeSpan.FromSeconds(2));
        }        private ISampleProvider CreateClassicBell(int sampleRate)
        {
            // Classic bell sound: 800Hz for 1.5 seconds
            var generator = new SignalGenerator(sampleRate, 1) { Frequency = 800, Type = SignalGeneratorType.Sin, Gain = 0.4 };
            
            // Take the duration we want
            var timedSignal = generator.Take(TimeSpan.FromSeconds(1.5));
            
            // Apply fade out to simulate bell decay
            var fadedSignal = new FadeInOutSampleProvider(timedSignal);
            fadedSignal.BeginFadeOut(1000); // 1 second fade out
            
            return fadedSignal;
        }
        
        private ISampleProvider CreateEuropeanRing(int sampleRate)
        {
            // European style: Single 425Hz tone for 1 second
            var generator = new SignalGenerator(sampleRate, 1) { Frequency = 425, Type = SignalGeneratorType.Sin, Gain = 0.35 };
            return generator.Take(TimeSpan.FromSeconds(1));
        }
        
        private ISampleProvider CreateOldTelephone(int sampleRate)
        {
            // Old telephone: Lower frequency bell-like sound with harmonics
            var fundamental = new SignalGenerator(sampleRate, 1) { Frequency = 300, Type = SignalGeneratorType.Sin, Gain = 0.3 };
            var harmonic = new SignalGenerator(sampleRate, 1) { Frequency = 600, Type = SignalGeneratorType.Sin, Gain = 0.15 };
            var mixed = new MixingSampleProvider(new[] { fundamental, harmonic });
            return mixed.Take(TimeSpan.FromSeconds(2.5));
        }
        
        private ISampleProvider CreateModernTone(int sampleRate)
        {
            // Modern tone: Clean 1000Hz sine wave for 1.2 seconds
            var generator = new SignalGenerator(sampleRate, 1) { Frequency = 1000, Type = SignalGeneratorType.Sin, Gain = 0.25 };
            return generator.Take(TimeSpan.FromSeconds(1.2));
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

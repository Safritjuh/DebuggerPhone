using System;
using System.IO;
using System.Media;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsSipPhone
{
    public class EnhancedRingtoneService : IRingtoneService
    {
        private SoundPlayer? _soundPlayer;
        private string _selectedRingtone = "Default Ring";
        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _ringtoneTask;
        
        public string[] AvailableRingtones => new[]
        {
            "Default Ring",
            "Classic Phone", 
            "Modern Chime",
            "Old School Bell",
            "Notification Sound"
        };
        
        public string SelectedRingtone 
        { 
            get => _selectedRingtone; 
            set => _selectedRingtone = value ?? "Default Ring"; 
        }
        
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
                    // Play the ringtone sound
                    await PlaySingleRingtoneAsync(ringtone);
                    
                    // Wait before repeating (3 seconds)
                    await Task.Delay(3000, cancellationToken);
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
        
        private async Task PlaySingleRingtoneAsync(string ringtone)
        {
            try
            {
                // Create a more audible ringtone using multiple system sounds
                switch (ringtone)
                {
                    case "Default Ring":
                        SystemSounds.Exclamation.Play();
                        await Task.Delay(200);
                        SystemSounds.Exclamation.Play();
                        break;
                        
                    case "Classic Phone":
                        SystemSounds.Question.Play();
                        await Task.Delay(300);
                        SystemSounds.Question.Play();
                        await Task.Delay(300);
                        SystemSounds.Question.Play();
                        break;
                        
                    case "Modern Chime":
                        SystemSounds.Asterisk.Play();
                        await Task.Delay(150);
                        SystemSounds.Asterisk.Play();
                        await Task.Delay(150);
                        SystemSounds.Asterisk.Play();
                        break;
                        
                    case "Old School Bell":
                        for (int i = 0; i < 5; i++)
                        {
                            SystemSounds.Beep.Play();
                            await Task.Delay(100);
                        }
                        break;
                        
                    case "Notification Sound":
                        SystemSounds.Hand.Play();
                        await Task.Delay(400);
                        SystemSounds.Hand.Play();
                        break;
                        
                    default:
                        SystemSounds.Exclamation.Play();
                        await Task.Delay(200);
                        SystemSounds.Exclamation.Play();
                        break;
                }
                
                Console.WriteLine($"[RINGTONE DEBUG] Played single ringtone: {ringtone}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RINGTONE DEBUG] Error playing single ringtone: {ex.Message}");
            }
        }
        
        public void StopRingtone()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
                
                _soundPlayer?.Stop();
                _soundPlayer?.Dispose();
                _soundPlayer = null;
                
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

using System;

namespace WindowsSipPhone
{
    public interface IRingtoneService
    {
        string[] AvailableRingtones { get; }
        string SelectedRingtone { get; set; }
        void PlayRingtone(string? ringtoneName = null);
        void StopRingtone();
        void Dispose();
    }
}

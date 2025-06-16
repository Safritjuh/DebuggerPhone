//
// Enhanced NAudio-based RtpAudioManager with device conflict resolution
// This implementation fixes the "Already recording" issues while maintaining
// the stable, well-tested NAudio API that works reliably on Windows 10+
//

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using NAudio.Wave;

namespace WindowsSipPhone
{
    /// <summary>
    /// Enhanced RtpAudioManager with proper device conflict resolution
    /// Uses NAudio with robust state management to eliminate "Already recording" errors
    /// Optimized for Windows 10+ audio device management
    /// </summary>
    public class RtpAudioManager : IDisposable
    {
        private UdpClient? _rtpSocket;
        private IPEndPoint? _remoteRtpEndpoint;
        private WaveInEvent? _audioInput;
        private WaveOutEvent? _audioOutput;
        private BufferedWaveProvider? _audioBuffer;
        private bool _isActive = false;
        private CancellationTokenSource? _cancellationTokenSource;
        
        // Enhanced device conflict management for Windows 10+
        private readonly object _deviceLock = new object();
        private readonly object _microphoneLock = new object();
        private bool _isRecording = false;
        private WaveInEvent? _testMicrophone = null;
          // RTP Configuration
        private const int SAMPLE_RATE = 8000;
        private const int CHANNELS = 1;
        private const int BITS_PER_SAMPLE = 16;
        private int _localRtpPort = 0;
        private bool _isMuted = false;
        private double _volume = 0.8;
        private string _audioCodec = "PCMA";
        private int _payloadType = 8;
        
        // Audio device selection
        private int _inputDeviceId = -1; // -1 means system default
        private int _outputDeviceId = -1; // -1 means system default
        private double _inputVolume = 0.8;
        private double _outputVolume = 0.8;
        private bool _inputMuted = false;
        private bool _outputMuted = false;
        
        // Remote endpoint tracking for debug logging
        private string _remoteIp = "";
        private int _remoteRtpPort = 0;

        // DTMF Configuration (RFC 2833)
        private const int DTMF_PAYLOAD_TYPE = 101;
        private const int DTMF_SAMPLE_RATE = 8000;
        private static readonly Dictionary<char, byte> DtmfEventMap = new Dictionary<char, byte>
        {
            {'0', 0}, {'1', 1}, {'2', 2}, {'3', 3}, {'4', 4},
            {'5', 5}, {'6', 6}, {'7', 7}, {'8', 8}, {'9', 9},
            {'*', 10}, {'#', 11}
        };
        
        // DTMF state management
        private bool _isDtmfActive = false;
        private char _currentDtmfDigit;
        private uint _dtmfStartTimestamp;
        private ushort _dtmfSequenceNumber;
        private const int DTMF_DURATION_MS = 100; // Standard DTMF tone duration
        private const int DTMF_VOLUME = 10; // Volume level (0-63)

        // RTP packet sequencing
        private static ushort _sequenceNumber = 0;
        private static uint _timestamp = 0;
        private static uint _ssrc = (uint)new Random().Next();        
        // Audio processing state variables
        private static bool _gateOpen = false;
        private static short[] _hpfDelayLine = new short[4];
        private static short[] _noiseHistory = new short[8];
        private static short _previousError = 0;
        
        // Advanced noise reduction state
        private static short[] _noiseProfile = new short[16];  // Background noise profile
        private static int _noiseProfileIndex = 0;
        private static bool _noiseProfileReady = false;
        private static short[] _smoothingBuffer = new short[32]; // For advanced smoothing
        private static int _smoothingIndex = 0;
        private static short[] _adaptiveFilter = new short[8];   // Adaptive filter coefficients
        private static double _noiseFloor = 0;
        private static int _silenceCounter = 0;

        public event Action<bool>? AudioStatusChanged;
        public event Action<string>? AudioError;
        
        public int LocalRtpPort => _localRtpPort;
        public bool IsRunning => _isActive;
        
        /// <summary>
        /// Check if we have an active RTP socket that can be resumed
        /// </summary>
        public bool HasActiveSocket() => _rtpSocket != null && _localRtpPort > 0;
        
        // DTMF Public Methods
        /// <summary>
        /// Send DTMF digit according to RFC 2833
        /// </summary>
        public void SendDtmfDigit(char digit)
        {
            try
            {
                if (!_isActive || _rtpSocket == null || _remoteRtpEndpoint == null)
                {
                    Console.WriteLine($"[DTMF] ❌ Cannot send DTMF '{digit}' - RTP session not active");
                    return;
                }

                if (!DtmfEventMap.ContainsKey(digit))
                {
                    Console.WriteLine($"[DTMF] ❌ Invalid DTMF digit: '{digit}'");
                    return;
                }

                Console.WriteLine($"[DTMF] 📞 Sending DTMF digit: '{digit}'");
                
                // Start DTMF transmission
                _isDtmfActive = true;
                _currentDtmfDigit = digit;
                _dtmfStartTimestamp = _timestamp;
                _dtmfSequenceNumber = _sequenceNumber;
                
                // Send DTMF start and duration packets
                Task.Run(async () => await SendDtmfSequenceAsync(digit));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DTMF] ❌ Error sending DTMF digit '{digit}': {ex.Message}");
            }
        }

        /// <summary>
        /// Generate local DTMF tone for audio feedback (dual-tone multi-frequency)
        /// </summary>
        public void PlayDtmfTone(char digit)
        {
            try
            {
                if (!DtmfEventMap.ContainsKey(digit))
                    return;

                // DTMF frequency pairs according to ITU-T Q.23
                var dtmfFrequencies = new Dictionary<char, (int low, int high)>
                {
                    {'1', (697, 1209)}, {'2', (697, 1336)}, {'3', (697, 1477)},
                    {'4', (770, 1209)}, {'5', (770, 1336)}, {'6', (770, 1477)},
                    {'7', (852, 1209)}, {'8', (852, 1336)}, {'9', (852, 1477)},
                    {'*', (941, 1209)}, {'0', (941, 1336)}, {'#', (941, 1477)}
                };

                if (dtmfFrequencies.TryGetValue(digit, out var frequencies))
                {
                    Task.Run(() => GenerateAndPlayTone(frequencies.low, frequencies.high, DTMF_DURATION_MS));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DTMF] ⚠️ Error playing local DTMF tone for '{digit}': {ex.Message}");
            }
        }

        /// <summary>
        /// Generate and play dual-tone DTMF audio locally
        /// </summary>
        private void GenerateAndPlayTone(int lowFreq, int highFreq, int durationMs)
        {
            try
            {
                const int sampleRate = 8000;
                const double amplitude = 0.3; // Gentle volume for local feedback
                
                int sampleCount = (sampleRate * durationMs) / 1000;
                var samples = new short[sampleCount];
                
                for (int i = 0; i < sampleCount; i++)
                {
                    double t = (double)i / sampleRate;
                    double lowTone = Math.Sin(2 * Math.PI * lowFreq * t);
                    double highTone = Math.Sin(2 * Math.PI * highFreq * t);
                    double combinedTone = (lowTone + highTone) * amplitude;
                    
                    samples[i] = (short)(combinedTone * 32767);
                }
                
                // Convert to byte array for audio playback
                var audioData = new byte[samples.Length * 2];
                Buffer.BlockCopy(samples, 0, audioData, 0, audioData.Length);
                
                // Play through existing audio output if available
                if (_audioBuffer != null && _audioOutput != null)
                {
                    _audioBuffer.AddSamples(audioData, 0, audioData.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DTMF] ⚠️ Error generating DTMF tone: {ex.Message}");
            }
        }

        public RtpAudioManager()
        {
            Console.WriteLine("[RTPAUDIO] Enhanced NAudio RtpAudioManager - Device conflict resolution enabled for Windows 10+");
            InitializeAudioDevices();
        }

        /// <summary>
        /// Initialize audio devices with Windows 10+ specific configuration
        /// </summary>
        private void InitializeAudioDevices()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] Initializing audio devices for Windows 10+...");
                
                // Log available input devices
                Console.WriteLine($"[RTPAUDIO] Available WaveIn devices: {WaveIn.DeviceCount}");
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    Console.WriteLine($"[RTPAUDIO] Device {i}: {caps.ProductName} (Channels: {caps.Channels})");
                }
                
                // Log available output devices
                Console.WriteLine($"[RTPAUDIO] Available WaveOut devices: {WaveOut.DeviceCount}");
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    Console.WriteLine($"[RTPAUDIO] Device {i}: {caps.ProductName}");
                }
                
                Console.WriteLine("[RTPAUDIO] ✅ Audio device initialization complete");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ⚠️ Error initializing audio devices: {ex.Message}");
                AudioError?.Invoke($"Audio device initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Prepares the RTP socket and allocates a local port for SDP offer
        /// Call this before sending INVITE to get a valid port number
        /// </summary>
        public bool PrepareRtpSocket()
        {
            try
            {
                lock (_deviceLock)
                {
                    // If socket already exists, return current port
                    if (_rtpSocket != null && _localRtpPort > 0)
                    {
                        Console.WriteLine($"[RTPAUDIO] ✅ RTP socket already prepared on port: {_localRtpPort}");
                        return true;
                    }
                    
                    // Create RTP socket with Windows 10+ optimizations
                    _rtpSocket = new UdpClient();
                    _rtpSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    
                    // Explicitly bind to any available port to ensure LocalEndPoint is set
                    _rtpSocket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    _localRtpPort = ((IPEndPoint)_rtpSocket.Client.LocalEndPoint!).Port;
                    Console.WriteLine($"[RTPAUDIO] ✅ RTP socket prepared and bound to local port: {_localRtpPort}");
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Failed to prepare RTP socket: {ex.Message}");
                AudioError?.Invoke($"Failed to prepare RTP socket: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Start RTP audio session with enhanced device conflict resolution for Windows 10+
        /// </summary>
        public async Task<bool> StartRtpSession(string remoteIp, int remoteRtpPort, string audioCodec = "PCMA", int payloadType = 8)
        {
            try
            {                Console.WriteLine($"[RTPAUDIO] Starting RTP session to {remoteIp}:{remoteRtpPort} (Codec: {audioCodec}, PayloadType: {payloadType})");
                
                // Store remote RTP endpoint info
                _remoteIp = remoteIp;
                _remoteRtpPort = remoteRtpPort;
                
                // Store codec settings
                _audioCodec = audioCodec;
                _payloadType = payloadType;
                
                lock (_deviceLock)
                {
                    // Enhanced conflict resolution - stop any existing session first
                    if (_isActive)
                    {
                        Console.WriteLine("[RTPAUDIO] ⚠️ Existing session detected, stopping gracefully...");
                        StopRtpSession();
                        Thread.Sleep(100); // Allow Windows 10+ to release audio devices
                    }
                    
                    // Test microphone availability before starting
                    if (!TestMicrophoneAvailability())
                    {
                        Console.WriteLine("[RTPAUDIO] ❌ Microphone not available - device conflict detected");
                        AudioError?.Invoke("Microphone is in use by another application");
                        return false;
                    }
                }

                // Validate remote endpoint
                if (!IPAddress.TryParse(remoteIp, out var ipAddress))
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Invalid IP address: {remoteIp}");
                    AudioError?.Invoke($"Invalid IP address: {remoteIp}");
                    return false;
                }
                
                _remoteRtpEndpoint = new IPEndPoint(ipAddress, remoteRtpPort);
                _remoteIp = remoteIp;
                _remoteRtpPort = remoteRtpPort;
                Console.WriteLine($"[RTPAUDIO] ✅ Remote RTP endpoint created: {_remoteRtpEndpoint}");
                
                // Create or reuse RTP socket with Windows 10+ optimizations
                if (_rtpSocket == null || _localRtpPort == 0)
                {
                    _rtpSocket = new UdpClient();
                    _rtpSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                    
                    // Explicitly bind to any available port to ensure LocalEndPoint is set
                    _rtpSocket.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                    _localRtpPort = ((IPEndPoint)_rtpSocket.Client.LocalEndPoint!).Port;
                    Console.WriteLine($"[RTPAUDIO] ✅ RTP socket bound to local port: {_localRtpPort}");
                }
                else
                {
                    Console.WriteLine($"[RTPAUDIO] ✅ Reusing prepared RTP socket on port: {_localRtpPort}");
                }
                  // Initialize enhanced audio input with optimal settings for voice quality
                _audioInput = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
                    DeviceNumber = _inputDeviceId >= 0 ? _inputDeviceId : 0, // Use selected device or default
                    BufferMilliseconds = 40 // Optimized buffer size for quality vs latency balance
                };

                _audioInput.DataAvailable += OnAudioDataAvailable;
                _audioInput.RecordingStopped += OnRecordingStopped;

                // Enhanced recording start with pre-check
                try
                {
                    // Ensure no existing recording state
                    if (_isRecording)
                    {
                        Console.WriteLine("[RTPAUDIO] ⚠️ Recording state conflict detected, clearing...");
                        try { _audioInput.StopRecording(); } catch { }
                        await Task.Delay(50);
                        _isRecording = false;
                    }

                    _audioInput.StartRecording();
                    _isRecording = true;
                    Console.WriteLine("[RTPAUDIO] ✅ Audio input recording started successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Failed to start recording: {ex.Message}");
                    AudioError?.Invoke($"Failed to start audio recording: {ex.Message}");
                    return false;
                }

                // Initialize audio output for Windows 10+
                _audioBuffer = new BufferedWaveProvider(new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS))
                {
                    BufferDuration = TimeSpan.FromMilliseconds(500),
                    DiscardOnBufferOverflow = true
                };
                  _audioOutput = new WaveOutEvent();
                if (_outputDeviceId >= 0)
                {
                    _audioOutput.DeviceNumber = _outputDeviceId; // Use selected output device
                }
                _audioOutput.Init(_audioBuffer);
                _audioOutput.Volume = (float)_outputVolume; // Apply volume setting
                _audioOutput.Play();

                // Create cancellation token before starting listener
                _cancellationTokenSource = new CancellationTokenSource();
                _isActive = true;

                // Start incoming RTP packet listener for audio playback
                StartIncomingRtpListener();

                Console.WriteLine("[RTPAUDIO] ✅ RTP session started successfully");
                AudioStatusChanged?.Invoke(true);
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error starting RTP session: {ex.Message}");
                AudioError?.Invoke($"Failed to start RTP session: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop RTP audio session with enhanced cleanup for Windows 10+
        /// </summary>
        public void StopRtpSession()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] Stopping RTP session...");
                
                lock (_deviceLock)
                {
                    _isActive = false;
                    _cancellationTokenSource?.Cancel();
                    
                    Console.WriteLine("[RTPAUDIO] 🎧 Stopping incoming RTP listener...");

                    // Enhanced audio input cleanup
                    if (_audioInput != null)
                    {
                        try
                        {
                            if (_isRecording)
                            {
                                _audioInput.StopRecording();
                                _isRecording = false;
                                Console.WriteLine("[RTPAUDIO] ✅ Audio recording stopped");
                            }
                            
                            _audioInput.DataAvailable -= OnAudioDataAvailable;
                            _audioInput.RecordingStopped -= OnRecordingStopped;
                            _audioInput.Dispose();
                            _audioInput = null;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error stopping audio input: {ex.Message}");
                        }
                    }

                    // Enhanced audio output cleanup
                    if (_audioOutput != null)
                    {
                        try
                        {
                            _audioOutput.Stop();
                            _audioOutput.Dispose();
                            _audioOutput = null;
                            Console.WriteLine("[RTPAUDIO] ✅ Audio output stopped");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error stopping audio output: {ex.Message}");
                        }
                    }

                    // Clean up RTP socket
                    if (_rtpSocket != null)
                    {
                        try
                        {
                            _rtpSocket.Close();
                            _rtpSocket.Dispose();
                            _rtpSocket = null;
                            Console.WriteLine("[RTPAUDIO] ✅ RTP socket closed");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error closing RTP socket: {ex.Message}");
                        }
                    }

                    _audioBuffer = null;
                    _remoteRtpEndpoint = null;
                    _localRtpPort = 0;
                    
                    // Allow Windows 10+ to fully release audio devices
                    Thread.Sleep(100);
                }

                Console.WriteLine("[RTPAUDIO] ✅ RTP session stopped successfully");
                AudioStatusChanged?.Invoke(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error stopping RTP session: {ex.Message}");
                AudioError?.Invoke($"Error stopping RTP session: {ex.Message}");
            }
        }

        /// <summary>
        /// Pause RTP audio streams without tearing down the socket (for hold operations)
        /// Keeps the RTP socket and port intact for proper resume functionality
        /// </summary>
        public void PauseRtpStreams()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] Pausing RTP streams for hold operation...");
                
                lock (_deviceLock)
                {
                    // Stop audio recording but keep socket alive
                    if (_audioInput != null && _isRecording)
                    {
                        try
                        {
                            _audioInput.StopRecording();
                            _isRecording = false;
                            Console.WriteLine("[RTPAUDIO] ✅ Audio recording paused");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error pausing audio input: {ex.Message}");
                        }
                    }

                    // Stop audio output but keep socket alive
                    if (_audioOutput != null)
                    {
                        try
                        {
                            _audioOutput.Stop();
                            Console.WriteLine("[RTPAUDIO] ✅ Audio output paused");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error pausing audio output: {ex.Message}");
                        }
                    }

                    // NOTE: We deliberately keep _rtpSocket, _localRtpPort, and _remoteRtpEndpoint
                    // so that resume can restart using the same ports
                    
                    _isActive = false;
                    Console.WriteLine($"[RTPAUDIO] ✅ RTP streams paused - socket kept alive on port {_localRtpPort}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error pausing RTP streams: {ex.Message}");
                AudioError?.Invoke($"Error pausing RTP streams: {ex.Message}");
            }
        }

        /// <summary>        /// Resume RTP audio streams after hold operation
        /// Restarts audio recording/playback using existing socket and endpoints
        /// </summary>
        public Task<bool> ResumeRtpStreams()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] Resuming RTP streams after hold operation...");
                  if (_rtpSocket == null || _localRtpPort == 0 || _remoteRtpEndpoint == null)
                {
                    Console.WriteLine("[RTPAUDIO] ❌ Cannot resume - RTP session not properly initialized");
                    return Task.FromResult(false);
                }

                lock (_deviceLock)
                {
                    // Restart audio input if not already running
                    if (_audioInput != null && !_isRecording)
                    {
                        try
                        {
                            _audioInput.StartRecording();
                            _isRecording = true;
                            Console.WriteLine("[RTPAUDIO] ✅ Audio recording resumed");
                        }                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ❌ Failed to resume recording: {ex.Message}");
                            return Task.FromResult(false);
                        }
                    }

                    // Restart audio output if not already running
                    if (_audioOutput != null)
                    {
                        try
                        {
                            if (_audioOutput.PlaybackState != PlaybackState.Playing)
                            {
                                _audioOutput.Play();
                                Console.WriteLine("[RTPAUDIO] ✅ Audio output resumed");
                            }
                        }
                        catch (Exception ex)                        {
                            Console.WriteLine($"[RTPAUDIO] ❌ Failed to resume playback: {ex.Message}");
                            return Task.FromResult(false);
                        }
                    }                    _isActive = true;
                    
                    // CRITICAL FIX: Restart the incoming RTP listener after resume
                    // The listener was stopped when _isActive was set to false during pause
                    if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        StartIncomingRtpListener();
                        Console.WriteLine("[RTPAUDIO] ✅ Incoming RTP listener restarted after resume");
                    }                    
                    Console.WriteLine($"[RTPAUDIO] ✅ RTP streams resumed successfully on port {_localRtpPort}");
                    AudioStatusChanged?.Invoke(true);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error resuming RTP streams: {ex.Message}");
                AudioError?.Invoke($"Error resuming RTP streams: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        /// <summary>
        /// Update the remote RTP endpoint for an existing session (used during resume)
        /// </summary>
        public bool UpdateRemoteEndpoint(string remoteIp, int remoteRtpPort)
        {
            try
            {
                if (!IPAddress.TryParse(remoteIp, out var ipAddress))
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Invalid IP address for endpoint update: {remoteIp}");
                    return false;
                }

                lock (_deviceLock)
                {
                    _remoteRtpEndpoint = new IPEndPoint(ipAddress, remoteRtpPort);
                    _remoteIp = remoteIp;
                    _remoteRtpPort = remoteRtpPort;
                    Console.WriteLine($"[RTPAUDIO] ✅ Remote RTP endpoint updated to: {_remoteRtpEndpoint}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error updating remote endpoint: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns the remote RTP endpoint as a string for debugging.
        /// </summary>
        public string GetRemoteEndpoint()
        {
            return _remoteRtpEndpoint?.ToString() ?? "(none)";
        }

        /// <summary>
        /// Enhanced audio data handler with detailed RTP packet transmission logging
        /// Optimized for Windows 10+ audio pipeline
        /// </summary>
        private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {            try
            {
                if (_rtpSocket == null || _remoteRtpEndpoint == null || _isMuted || _inputMuted)
                    return;
                
                // Enhanced audio level check for debugging with processing pipeline info
                if (_sequenceNumber % 200 == 0) // Check every 200th packet
                {
                    double rawAudioLevel = CalculateAudioLevel(e.Buffer, e.BytesRecorded);
                    Console.WriteLine($"[RTPAUDIO] Raw audio: {rawAudioLevel:F1}% | Gate: {(_gateOpen ? "OPEN" : "CLOSED")} | Seq: {_sequenceNumber}");
                }

                // Create RTP packet
                var rtpPacket = CreateRtpPacket(e.Buffer, e.BytesRecorded);
                
                // Send RTP packet
                _rtpSocket.Send(rtpPacket, rtpPacket.Length, _remoteRtpEndpoint);
                
                // Detailed transmission logging with advanced audio processing info
                if (_sequenceNumber % 100 == 0) // Log every 100th packet to avoid spam
                {
                    string noiseStatus = _noiseProfileReady ? 
                        $"Adaptive NR: ON (Floor: {_noiseFloor:F0})" : 
                        $"Adaptive NR: Learning ({_silenceCounter}/50)";
                    
                    Console.WriteLine($"[RTPAUDIO] RTP packet #{_sequenceNumber} sent to {_remoteRtpEndpoint} " +
                                      $"({e.BytesRecorded} bytes PCM -> {rtpPacket.Length - 12} bytes G.711 A-law + 12 bytes RTP header) " +
                                      $"| {noiseStatus}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error sending RTP packet: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate audio level for debugging purposes
        /// </summary>
        private static double CalculateAudioLevel(byte[] buffer, int bytesRecorded)
        {
            if (bytesRecorded < 2) return 0;
            
            double sum = 0;
            int samples = bytesRecorded / 2;
            
            for (int i = 0; i < bytesRecorded; i += 2)
            {
                if (i + 1 < bytesRecorded)
                {
                    short sample = (short)((buffer[i + 1] << 8) | buffer[i]);
                    sum += Math.Abs(sample);
                }
            }
            
            double avgLevel = sum / samples;
            return (avgLevel / 32768.0) * 100; // Convert to percentage
        }

        /// <summary>
        /// Create RTP packet with proper header and enhanced G.711 A-law encoded audio for Windows 10+
        /// </summary>
        private byte[] CreateRtpPacket(byte[] audioData, int audioLength)
        {
            // Apply comprehensive audio processing pipeline
            byte[] processedAudio = ApplyAudioProcessingPipeline(audioData, audioLength);
            
            // Convert 16-bit PCM to G.711 A-law (each 16-bit sample becomes 1 byte)
            int encodedLength = audioLength / 2; // 16-bit PCM -> 8-bit A-law
            byte[] encodedAudio = new byte[encodedLength];
            
            // Convert processed PCM to G.711 A-law encoding
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    // Extract 16-bit PCM sample (little-endian)
                    short pcmSample = (short)((processedAudio[i + 1] << 8) | processedAudio[i]);
                    
                    // Encode to G.711 A-law
                    encodedAudio[i / 2] = LinearToALawSample(pcmSample);
                }
            }
            
            var rtpPacket = new byte[12 + encodedLength]; // RTP header (12 bytes) + encoded audio data
            
            // RTP Header
            rtpPacket[0] = 0x80; // Version 2, no padding, no extension, no CSRC
            rtpPacket[1] = (byte)_payloadType; // Payload type (PCMA = 8)
            
            // Sequence number (big-endian)
            rtpPacket[2] = (byte)(_sequenceNumber >> 8);
            rtpPacket[3] = (byte)(_sequenceNumber & 0xFF);
            _sequenceNumber++;
            
            // Timestamp (big-endian) - for G.711 A-law, timestamp increments by sample count
            rtpPacket[4] = (byte)(_timestamp >> 24);
            rtpPacket[5] = (byte)((_timestamp >> 16) & 0xFF);
            rtpPacket[6] = (byte)((_timestamp >> 8) & 0xFF);
            rtpPacket[7] = (byte)(_timestamp & 0xFF);
            _timestamp += (uint)encodedLength; // Increment by encoded sample count
            
            // SSRC (big-endian)
            rtpPacket[8] = (byte)(_ssrc >> 24);
            rtpPacket[9] = (byte)((_ssrc >> 16) & 0xFF);
            rtpPacket[10] = (byte)(_ssrc >> 8);
            rtpPacket[11] = (byte)(_ssrc & 0xFF);
            
            // Copy G.711 A-law encoded audio data
            Array.Copy(encodedAudio, 0, rtpPacket, 12, encodedLength);
            
            return rtpPacket;
        }

        /// <summary>
        /// Apply comprehensive audio processing pipeline for superior voice quality with advanced noise reduction
        /// </summary>
        private static byte[] ApplyAudioProcessingPipeline(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            // Step 1: Build noise profile during silence periods
            processed = BuildNoiseProfile(processed, audioLength);
            
            // Step 2: Pre-filter DC bias removal
            processed = RemoveDCBias(processed, audioLength);
            
            // Step 3: Enhanced high-pass filter to remove low-frequency noise (< 300Hz)
            processed = ApplyEnhancedHighPassFilter(processed, audioLength);
            
            // Step 4: Advanced adaptive noise reduction
            processed = ApplyAdaptiveNoiseReduction(processed, audioLength);
            
            // Step 5: Spectral noise reduction with improved algorithm
            processed = ApplyAdvancedSpectralNoiseReduction(processed, audioLength);
            
            // Step 6: Advanced noise gate with hysteresis
            processed = ApplyAdvancedNoiseGate(processed, audioLength);
            
            // Step 7: Automatic gain control for consistent volume
            processed = ApplyAutomaticGainControl(processed, audioLength);
            
            // Step 8: Light compression to reduce dynamic range
            processed = ApplyLightCompression(processed, audioLength);
            
            // Step 9: Final multi-stage noise shaping filter
            processed = ApplyAdvancedNoiseShaping(processed, audioLength);
            
            return processed;
        }

        /// <summary>
        /// Build noise profile during silence periods for adaptive noise reduction
        /// </summary>
        private static byte[] BuildNoiseProfile(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            // Calculate signal level
            double signalLevel = 0;
            int samples = audioLength / 2;
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    signalLevel += Math.Abs(sample);
                }
            }
            signalLevel = signalLevel / samples;
            
            // If signal is quiet (likely background noise), update noise profile
            if (signalLevel < 800) // Threshold for detecting silence
            {
                _silenceCounter++;
                
                // Collect noise samples
                for (int i = 0; i < audioLength; i += 2)
                {
                    if (i + 1 < audioLength)
                    {
                        short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                        
                        _noiseProfile[_noiseProfileIndex] = sample;
                        _noiseProfileIndex = (_noiseProfileIndex + 1) % _noiseProfile.Length;
                        
                        if (_silenceCounter > 50) // After enough silence samples
                        {
                            _noiseProfileReady = true;
                        }
                    }
                }
                
                // Update noise floor estimate
                if (_noiseProfileReady)
                {
                    double sum = 0;
                    for (int j = 0; j < _noiseProfile.Length; j++)
                    {
                        sum += Math.Abs(_noiseProfile[j]);
                    }
                    _noiseFloor = sum / _noiseProfile.Length;
                }
            }
            else
            {
                _silenceCounter = Math.Max(0, _silenceCounter - 1); // Decay silence counter
            }
            
            return processed;
        }

        /// <summary>
        /// Apply adaptive noise reduction based on learned noise profile - VOICE OPTIMIZED
        /// </summary>
        private static byte[] ApplyAdaptiveNoiseReduction(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            if (!_noiseProfileReady) return processed; // Can't reduce noise without profile
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    short filteredSample = sample;
                    
                    // Calculate correlation with noise profile
                    double correlation = 0;
                    for (int j = 0; j < _noiseProfile.Length; j++)
                    {
                        correlation += Math.Abs(sample - _noiseProfile[j]);
                    }
                    correlation = correlation / _noiseProfile.Length;
                    
                    // Much gentler noise reduction - preserve voice at all costs
                    short absSample = (short)Math.Abs(sample);
                    if (correlation < _noiseFloor * 1.8 && absSample < _noiseFloor * 3) // Only reduce if clearly noise
                    {
                        // Very gentle reduction - never go below 70% of original
                        double reductionFactor = Math.Max(0.7, 1.0 - (correlation / (_noiseFloor * 4.0)));
                        filteredSample = (short)(sample * reductionFactor);
                    }
                    
                    processed[i] = (byte)(filteredSample & 0xFF);
                    processed[i + 1] = (byte)((filteredSample >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Apply advanced spectral noise reduction with voice preservation - VOICE OPTIMIZED
        /// </summary>
        private static byte[] ApplyAdvancedSpectralNoiseReduction(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    
                    // Store in circular buffer for advanced smoothing
                    _smoothingBuffer[_smoothingIndex] = sample;
                    _smoothingIndex = (_smoothingIndex + 1) % _smoothingBuffer.Length;
                    
                    // Much gentler smoothing - preserve voice transients
                    short shortTermAvg = 0;
                    
                    // Only use short-term average (4 samples) to preserve voice clarity
                    for (int j = 0; j < 4; j++)
                    {
                        int idx = (_smoothingIndex - 1 - j + _smoothingBuffer.Length) % _smoothingBuffer.Length;
                        shortTermAvg += _smoothingBuffer[idx];
                    }
                    shortTermAvg /= 4;
                    
                    // Voice-friendly blending - much less aggressive
                    short absSample = (short)Math.Abs(sample);
                    double voiceFactor = Math.Min(1.0, absSample / 1500.0); // Lower threshold for voice detection
                    
                    // Gentle blend - preserve most of original signal
                    short result = (short)(
                        sample * (0.85 + voiceFactor * 0.15) +     // Keep 85-100% of original
                        shortTermAvg * (0.15 - voiceFactor * 0.15) // Very light smoothing
                    );
                    
                    processed[i] = (byte)(result & 0xFF);
                    processed[i + 1] = (byte)((result >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Apply gentle noise shaping filter - VOICE OPTIMIZED for clarity without clipping
        /// </summary>
        private static byte[] ApplyAdvancedNoiseShaping(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    
                    // Very gentle error feedback - no aggressive shaping
                    short error = (short)(sample - _previousError * 0.3); // Much less feedback
                    short shaped = (short)(error * 0.98 + _previousError * 0.02); // Minimal shaping
                    
                    // No high-frequency emphasis - it was causing clipping
                    // Just use the lightly shaped signal
                    
                    // Gentle soft limiting to prevent any clipping - much higher threshold
                    if (shaped > 32000) shaped = 32000;      // Near max but not clipping
                    if (shaped < -32000) shaped = -32000;
                    
                    _previousError = (short)(sample - shaped); // Store actual error
                    
                    processed[i] = (byte)(shaped & 0xFF);
                    processed[i + 1] = (byte)((shaped >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Remove DC bias (offset) from audio signal
        /// </summary>
        private static byte[] RemoveDCBias(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            // Calculate DC offset
            long sum = 0;
            int samples = audioLength / 2;
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    sum += sample;
                }
            }
            
            short dcOffset = (short)(sum / samples);
            
            // Remove DC offset
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    sample = (short)(sample - dcOffset);
                    
                    processed[i] = (byte)(sample & 0xFF);
                    processed[i + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Apply enhanced high-pass filter with better noise rejection
        /// </summary>
        private static byte[] ApplyEnhancedHighPassFilter(byte[] audioData, int audioLength)
        {
            byte[] filtered = new byte[audioLength];
            Array.Copy(audioData, filtered, audioLength);
            
            // Enhanced high-pass filter with steeper roll-off
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short input = (short)((audioData[i + 1] << 8) | audioData[i]);
                    
                    // 4-pole high-pass filter (Butterworth, fc = 350Hz)
                    short output = (short)(
                        0.8 * input
                        - 0.7 * _hpfDelayLine[0]
                        + 0.4 * _hpfDelayLine[1]
                        - 0.2 * _hpfDelayLine[2]
                        + 0.1 * _hpfDelayLine[3]
                    );
                    
                    // Update delay line
                    _hpfDelayLine[3] = _hpfDelayLine[2];
                    _hpfDelayLine[2] = _hpfDelayLine[1];
                    _hpfDelayLine[1] = _hpfDelayLine[0];
                    _hpfDelayLine[0] = input;
                    
                    // Write filtered sample
                    filtered[i] = (byte)(output & 0xFF);
                    filtered[i + 1] = (byte)((output >> 8) & 0xFF);
                }
            }
            
            return filtered;
        }

        /// <summary>
        /// Apply advanced noise gate with hysteresis - VOICE OPTIMIZED
        /// </summary>
        private static byte[] ApplyAdvancedNoiseGate(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            // Much more voice-friendly thresholds
            const short openThreshold = 800;   // Lower threshold to catch quiet speech
            const short closeThreshold = 400;  // Much lower to avoid cutting off speech
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    short absSample = (short)Math.Abs(sample);
                    
                    // Hysteresis logic
                    if (!_gateOpen && absSample > openThreshold)
                    {
                        _gateOpen = true;
                    }
                    else if (_gateOpen && absSample < closeThreshold)
                    {
                        _gateOpen = false;
                    }
                    
                    // Much gentler gate - only reduce by 50% instead of 95%
                    if (!_gateOpen)
                    {
                        sample = (short)(sample * 0.5); // Gentle reduction instead of aggressive cut
                    }
                    
                    // Write back processed sample
                    processed[i] = (byte)(sample & 0xFF);
                    processed[i + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Apply gentle automatic gain control - VOICE OPTIMIZED
        /// </summary>
        private static byte[] ApplyAutomaticGainControl(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            // Calculate RMS level for this buffer
            double rmsLevel = 0;
            int samples = audioLength / 2;
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    rmsLevel += sample * sample;
                }
            }
            
            rmsLevel = Math.Sqrt(rmsLevel / samples);
            
            // More conservative target level - don't over-amplify
            const double targetRms = 6000.0; // Lower target to avoid clipping
            
            // Calculate gain factor (with much tighter limits)
            double gainFactor = 1.0;
            if (rmsLevel > 200) // Only apply AGC if there's significant signal
            {
                gainFactor = targetRms / rmsLevel;
                gainFactor = Math.Max(0.8, Math.Min(1.5, gainFactor)); // Much more conservative gain range
            }
            
            // Apply gain with clipping protection
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    int amplified = (int)(sample * gainFactor);
                    
                    // Conservative clipping protection
                    sample = (short)Math.Max(-30000, Math.Min(30000, amplified));
                    
                    processed[i] = (byte)(sample & 0xFF);
                    processed[i + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Apply very gentle compression - VOICE OPTIMIZED to prevent clipping
        /// </summary>
        private static byte[] ApplyLightCompression(byte[] audioData, int audioLength)
        {
            byte[] processed = new byte[audioLength];
            Array.Copy(audioData, processed, audioLength);
            
            const short threshold = 20000; // Much higher threshold - only compress very loud signals
            const double ratio = 2.0;      // Gentle 2:1 compression ratio instead of 3:1
            
            for (int i = 0; i < audioLength; i += 2)
            {
                if (i + 1 < audioLength)
                {
                    short sample = (short)((audioData[i + 1] << 8) | audioData[i]);
                    short absSample = (short)Math.Abs(sample);
                    
                    if (absSample > threshold)
                    {
                        // Apply very gentle compression above threshold
                        short excess = (short)(absSample - threshold);
                        short compressedExcess = (short)(excess / ratio);
                        short compressedSample = (short)(threshold + compressedExcess);
                        
                        // Maintain sign
                        sample = (short)(sample >= 0 ? compressedSample : -compressedSample);
                    }
                    
                    processed[i] = (byte)(sample & 0xFF);
                    processed[i + 1] = (byte)((sample >> 8) & 0xFF);
                }
            }
            
            return processed;
        }

        /// <summary>
        /// Send DTMF sequence according to RFC 2833 standards
        /// </summary>
        private async Task SendDtmfSequenceAsync(char digit)
        {
            try
            {
                if (!DtmfEventMap.TryGetValue(digit, out byte eventCode))
                    return;

                var startTimestamp = _dtmfStartTimestamp;
                var sequenceBase = _dtmfSequenceNumber;
                
                // Send DTMF events according to RFC 2833
                // Send multiple packets during tone duration for reliability
                const int packetsPerSecond = 50; // 20ms intervals
                const int totalPackets = (DTMF_DURATION_MS * packetsPerSecond) / 1000;
                const int packetInterval = 20; // 20ms
                
                for (int i = 0; i < totalPackets; i++)
                {
                    if (!_isDtmfActive || _currentDtmfDigit != digit)
                        break;
                        
                    bool isEnd = (i == totalPackets - 1);
                    uint duration = (uint)(i * (DTMF_SAMPLE_RATE * packetInterval / 1000));
                    
                    var dtmfPacket = CreateDtmfPacket(eventCode, duration, isEnd, startTimestamp, (ushort)(sequenceBase + i));
                    
                    if (_rtpSocket != null && _remoteRtpEndpoint != null)
                    {
                        _rtpSocket.Send(dtmfPacket, dtmfPacket.Length, _remoteRtpEndpoint);
                    }
                    
                    if (!isEnd)
                        await Task.Delay(packetInterval);
                }
                
                // Send final end packet (RFC 2833 recommendation)
                if (_isDtmfActive && _currentDtmfDigit == digit)
                {
                    uint finalDuration = (uint)(DTMF_DURATION_MS * DTMF_SAMPLE_RATE / 1000);
                    var endPacket = CreateDtmfPacket(eventCode, finalDuration, true, startTimestamp, (ushort)(sequenceBase + totalPackets));
                    
                    if (_rtpSocket != null && _remoteRtpEndpoint != null)
                    {
                        _rtpSocket.Send(endPacket, endPacket.Length, _remoteRtpEndpoint);
                        await Task.Delay(10);
                        _rtpSocket.Send(endPacket, endPacket.Length, _remoteRtpEndpoint); // Send duplicate for reliability
                    }
                }
                
                _isDtmfActive = false;
                Console.WriteLine($"[DTMF] ✅ DTMF digit '{digit}' transmission completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DTMF] ❌ Error in DTMF sequence for '{digit}': {ex.Message}");
                _isDtmfActive = false;
            }
        }

        /// <summary>
        /// Create RFC 2833 DTMF event packet
        /// </summary>
        private byte[] CreateDtmfPacket(byte eventCode, uint duration, bool isEnd, uint startTimestamp, ushort sequenceNumber)
        {
            var dtmfPacket = new byte[16]; // RTP header (12 bytes) + DTMF event (4 bytes)
            
            // RTP Header for DTMF
            dtmfPacket[0] = 0x80; // Version 2, no padding, no extension, no CSRC
            dtmfPacket[1] = (byte)DTMF_PAYLOAD_TYPE; // Payload type for telephone-event (101)
            
            // Sequence number (big-endian)
            dtmfPacket[2] = (byte)(sequenceNumber >> 8);
            dtmfPacket[3] = (byte)(sequenceNumber & 0xFF);
            
            // Timestamp (big-endian) - use DTMF start timestamp
            dtmfPacket[4] = (byte)(startTimestamp >> 24);
            dtmfPacket[5] = (byte)((startTimestamp >> 16) & 0xFF);
            dtmfPacket[6] = (byte)((startTimestamp >> 8) & 0xFF);
            dtmfPacket[7] = (byte)(startTimestamp & 0xFF);
            
            // SSRC (big-endian)
            dtmfPacket[8] = (byte)(_ssrc >> 24);
            dtmfPacket[9] = (byte)((_ssrc >> 16) & 0xFF);
            dtmfPacket[10] = (byte)((_ssrc >> 8) & 0xFF);
            dtmfPacket[11] = (byte)(_ssrc & 0xFF);
            
            // DTMF Event Data (RFC 2833 format)
            dtmfPacket[12] = eventCode; // Event code (0-11 for digits 0-9, *, #)
            dtmfPacket[13] = (byte)((isEnd ? 0x80 : 0x00) | DTMF_VOLUME); // E=end flag, R=0, Volume
            
            // Duration (big-endian) - duration in timestamp units
            dtmfPacket[14] = (byte)(duration >> 8);
            dtmfPacket[15] = (byte)(duration & 0xFF);
            
            return dtmfPacket;
        }

        /// <summary>
        /// Convert 16-bit linear PCM sample to G.711 A-law encoded byte (ITU-T G.711 standard)
        /// </summary>
        private static byte LinearToALawSample(short pcmSample)
        {
            // ITU-T G.711 A-law encoding table for improved quality
            int sign = 0;
            int absValue = pcmSample;
            
            // Handle sign
            if (pcmSample < 0)
            {
                sign = 0x80;
                absValue = -pcmSample;
            }
            
            // Clamp to maximum value
            if (absValue > 32635) absValue = 32635;
            
            byte alaw;
            if (absValue >= 256)
            {
                int exponent = 7;
                int expMask = 0x4000;
                
                // Find the exponent
                for (int i = 0; i < 7; i++)
                {
                    if ((absValue & expMask) != 0)
                        break;
                    exponent--;
                    expMask >>= 1;
                }
                
                int mantissa = (absValue >> (exponent + 3)) & 0x0F;
                alaw = (byte)((exponent << 4) | mantissa);
            }
            else
            {
                alaw = (byte)(absValue >> 4);
            }
            
            // Apply sign and A-law inversion
            return (byte)(alaw ^ sign ^ 0x55);
        }

        /// <summary>
        /// Handle recording stopped event
        /// </summary>
        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isRecording = false;
            Console.WriteLine($"[RTPAUDIO] Recording stopped: {e.Exception?.Message ?? "Normal stop"}");
            
            if (e.Exception != null)
            {
                AudioError?.Invoke($"Recording error: {e.Exception.Message}");
            }
        }

        /// <summary>
        /// Test microphone availability to detect device conflicts
        /// Critical for Windows 10+ where multiple apps can conflict
        /// </summary>
        public bool TestMicrophoneAvailability()
        {
            lock (_microphoneLock)
            {
                try
                {
                    Console.WriteLine("[RTPAUDIO] Testing microphone availability...");
                    
                    if (_testMicrophone != null)
                    {
                        try { _testMicrophone.Dispose(); } catch { }
                        _testMicrophone = null;
                    }

                    _testMicrophone = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
                        DeviceNumber = 0
                    };

                    _testMicrophone.StartRecording();
                    Thread.Sleep(50); // Brief test
                    _testMicrophone.StopRecording();
                    _testMicrophone.Dispose();
                    _testMicrophone = null;

                    Console.WriteLine("[RTPAUDIO] ✅ Microphone availability test passed");
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Microphone availability test failed: {ex.Message}");
                    
                    if (_testMicrophone != null)
                    {
                        try { _testMicrophone.Dispose(); } catch { }
                        _testMicrophone = null;
                    }
                    
                    return false;
                }
            }
        }

        /// <summary>
        /// Test audio capture with level monitoring for diagnostics
        /// Enhanced version with Windows 10+ specific monitoring
        /// </summary>
        public async Task TestAudioCapture()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] Starting enhanced audio capture test for Windows 10+...");
                
                using var testInput = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
                    DeviceNumber = 0,
                    BufferMilliseconds = 100
                };

                bool audioDetected = false;
                double maxLevel = 0;
                int sampleCount = 0;

                testInput.DataAvailable += (sender, e) =>
                {
                    var samples = new short[e.BytesRecorded / 2];
                    Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
                    
                    double level = 0;
                    foreach (var sample in samples)
                    {
                        level += Math.Abs(sample);
                    }
                    level = (level / samples.Length) / 32768.0 * 100; // Convert to percentage
                    
                    if (level > maxLevel) maxLevel = level;
                    if (level > 1.0) audioDetected = true;
                    
                    sampleCount++;
                    if (sampleCount % 10 == 0) // Log every 10th sample
                    {
                        Console.WriteLine($"[RTPAUDIO] Audio level: {level:F1}% (Max: {maxLevel:F1}%)");
                    }
                };

                testInput.StartRecording();
                Console.WriteLine("[RTPAUDIO] Recording for 3 seconds... please speak into microphone");
                
                await Task.Delay(3000);
                
                testInput.StopRecording();

                Console.WriteLine($"[RTPAUDIO] Test completed - Audio detected: {audioDetected}, Max level: {maxLevel:F1}%");
                
                if (!audioDetected)
                {
                    AudioError?.Invoke("No audio detected. Check microphone permissions and device selection.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Audio capture test failed: {ex.Message}");
                AudioError?.Invoke($"Audio test failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Start microphone level monitoring for real-time feedback
        /// Enhanced for Windows 10+ with centralized device tracking
        /// </summary>
        public void StartMicrophoneLevelMonitoring()
        {
            lock (_microphoneLock)
            {
                try
                {
                    Console.WriteLine("[RTPAUDIO] Starting microphone level monitoring for Windows 10+...");
                    
                    if (_testMicrophone != null)
                    {
                        Console.WriteLine("[RTPAUDIO] ⚠️ Existing monitoring session found, stopping first");
                        try { _testMicrophone.Dispose(); } catch { }
                        _testMicrophone = null;
                    }

                    _testMicrophone = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
                        DeviceNumber = 0,
                        BufferMilliseconds = 50 // High refresh rate for real-time monitoring
                    };

                    _testMicrophone.DataAvailable += (sender, e) =>
                    {
                        var samples = new short[e.BytesRecorded / 2];
                        Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);
                        
                        double level = 0;
                        foreach (var sample in samples)
                        {
                            level += Math.Abs(sample);
                        }
                        level = (level / samples.Length) / 32768.0 * 100;
                        
                        // Visual level bar for console
                        var bars = Math.Min(20, (int)(level / 5));
                        var levelBar = new string('█', bars).PadRight(20, '░');
                        Console.WriteLine($"[RTPAUDIO] Level: [{levelBar}] {level:F1}%");
                    };

                    _testMicrophone.StartRecording();
                    Console.WriteLine("[RTPAUDIO] ✅ Microphone monitoring started - speak to see levels");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Failed to start microphone monitoring: {ex.Message}");
                    AudioError?.Invoke($"Microphone monitoring failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Set audio volume for Windows 10+ compatibility
        /// </summary>
        public void SetVolume(double volume)
        {
            _volume = Math.Max(0.0, Math.Min(1.0, volume));
            Console.WriteLine($"[RTPAUDIO] Volume set to: {_volume:P0}");
            
            if (_audioOutput != null)
            {
                _audioOutput.Volume = (float)_volume;
            }
        }        /// <summary>
        /// Set mute state for Windows 10+ compatibility
        /// </summary>
        public void SetMuted(bool muted)
        {
            _isMuted = muted;
            _inputMuted = muted; // Also update input muted state
            Console.WriteLine($"[RTPAUDIO] Mute set to: {_isMuted}");
        }

        /// <summary>
        /// Set input volume (0.0 to 1.0)
        /// </summary>
        public void SetInputVolume(double volume)
        {
            _inputVolume = Math.Max(0.0, Math.Min(1.0, volume));
            Console.WriteLine($"[RTPAUDIO] Input volume set to: {_inputVolume:F2}");
        }

        /// <summary>
        /// Set output volume (0.0 to 1.0)
        /// </summary>
        public void SetOutputVolume(double volume)
        {
            _outputVolume = Math.Max(0.0, Math.Min(1.0, volume));
            if (_audioOutput != null)
            {
                _audioOutput.Volume = (float)_outputVolume;
            }
            Console.WriteLine($"[RTPAUDIO] Output volume set to: {_outputVolume:F2}");
        }

        /// <summary>
        /// Get current volume level
        /// </summary>
        public double GetVolume() => _volume;

        /// <summary>
        /// Get current mute state
        /// </summary>
        public bool IsMuted() => _isMuted;        /// <summary>
        /// Device management methods for Windows 10+ compatibility
        /// </summary>
        public void SetInputDevice(int deviceId)
        {
            _inputDeviceId = deviceId;
            Console.WriteLine($"[RTPAUDIO] Setting input device to: {deviceId} ({GetInputDeviceName(deviceId)})");
            
            // If currently active, need to restart audio input with new device
            if (_isActive && _audioInput != null)
            {
                Console.WriteLine("[RTPAUDIO] ⚠️ Restarting audio input with new device...");
                try
                {
                    // Stop current input
                    if (_isRecording)
                    {
                        _audioInput.StopRecording();
                        _isRecording = false;
                    }
                    _audioInput.Dispose();
                    
                    // Create new input with selected device
                    _audioInput = new WaveInEvent
                    {
                        WaveFormat = new WaveFormat(SAMPLE_RATE, BITS_PER_SAMPLE, CHANNELS),
                        DeviceNumber = deviceId >= 0 ? deviceId : 0,
                        BufferMilliseconds = 40
                    };
                    
                    _audioInput.DataAvailable += OnAudioDataAvailable;
                    _audioInput.RecordingStopped += OnRecordingStopped;
                    _audioInput.StartRecording();
                    _isRecording = true;
                    
                    Console.WriteLine("[RTPAUDIO] ✅ Audio input device changed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Error changing input device: {ex.Message}");
                }
            }
        }

        public void SetOutputDevice(int deviceId)
        {
            _outputDeviceId = deviceId;
            Console.WriteLine($"[RTPAUDIO] Setting output device to: {deviceId} ({GetOutputDeviceName(deviceId)})");
            
            // If currently active, need to restart audio output with new device
            if (_isActive && _audioOutput != null)
            {
                Console.WriteLine("[RTPAUDIO] ⚠️ Restarting audio output with new device...");
                try
                {
                    // Stop current output
                    _audioOutput.Stop();
                    _audioOutput.Dispose();
                    
                    // Create new output with selected device
                    _audioOutput = new WaveOutEvent();
                    if (deviceId >= 0)
                    {
                        _audioOutput.DeviceNumber = deviceId;
                    }
                    _audioOutput.Init(_audioBuffer);
                    _audioOutput.Volume = (float)_outputVolume;
                    _audioOutput.Play();
                    
                    Console.WriteLine("[RTPAUDIO] ✅ Audio output device changed successfully");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ Error changing output device: {ex.Message}");
                }
            }
        }

        public void ApplySettings(object settings)
        {
            Console.WriteLine("[RTPAUDIO] Applying audio settings...");
            
            if (settings is not Pages.AudioSettings audioSettings)
            {
                Console.WriteLine("[RTPAUDIO] ⚠️ Invalid settings object type");
                return;
            }
            
            try
            {
                // Apply device selections
                if (audioSettings.InputDevice != null)
                {
                    SetInputDevice(audioSettings.InputDevice.Id);
                }
                
                if (audioSettings.OutputDevice != null)
                {
                    SetOutputDevice(audioSettings.OutputDevice.Id);
                }
                
                // Apply volume settings
                SetInputVolume(audioSettings.InputVolume);
                SetOutputVolume(audioSettings.OutputVolume);
                
                // Apply mute settings
                _inputMuted = audioSettings.InputMuted;
                _outputMuted = audioSettings.OutputMuted;
                _isMuted = audioSettings.InputMuted; // Legacy compatibility
                
                Console.WriteLine($"[RTPAUDIO] ✅ Audio settings applied successfully:");
                Console.WriteLine($"[RTPAUDIO]   - Input Device: {audioSettings.InputDevice?.Name ?? "Default"}");
                Console.WriteLine($"[RTPAUDIO]   - Output Device: {audioSettings.OutputDevice?.Name ?? "Default"}");
                Console.WriteLine($"[RTPAUDIO]   - Input Volume: {audioSettings.InputVolume:F2}");
                Console.WriteLine($"[RTPAUDIO]   - Output Volume: {audioSettings.OutputVolume:F2}");
                Console.WriteLine($"[RTPAUDIO]   - Input Muted: {audioSettings.InputMuted}");
                Console.WriteLine($"[RTPAUDIO]   - Output Muted: {audioSettings.OutputMuted}");
                Console.WriteLine($"[RTPAUDIO]   - Echo Cancellation: {audioSettings.EchoCancellation}");
                Console.WriteLine($"[RTPAUDIO]   - Noise Suppression: {audioSettings.NoiseSuppression}");
                Console.WriteLine($"[RTPAUDIO]   - Auto Gain Control: {audioSettings.AutoGainControl}");
                Console.WriteLine($"[RTPAUDIO]   - Sample Rate: {audioSettings.SampleRate} Hz");
                Console.WriteLine($"[RTPAUDIO]   - Buffer Size: {audioSettings.BufferSize} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error applying settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Log detailed audio device information for Windows 10+ troubleshooting
        /// </summary>
        public void LogAudioDeviceInfo()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] =================================");
                Console.WriteLine("[RTPAUDIO] Windows 10+ Audio Device Information");
                Console.WriteLine("[RTPAUDIO] =================================");
                Console.WriteLine($"[RTPAUDIO] Input Devices ({WaveIn.DeviceCount}):");
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    Console.WriteLine($"[RTPAUDIO] [{i}] {caps.ProductName}");
                    Console.WriteLine($"[RTPAUDIO]     Channels: {caps.Channels}");
                }
                
                Console.WriteLine($"[RTPAUDIO] Output Devices ({WaveOut.DeviceCount}):");
                for (int i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    Console.WriteLine($"[RTPAUDIO] [{i}] {caps.ProductName}");
                    Console.WriteLine($"[RTPAUDIO]     Channels: {caps.Channels}");
                }
                
                Console.WriteLine("[RTPAUDIO] =================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error logging device info: {ex.Message}");
            }
        }

        /// <summary>
        /// Enhanced debugging method to check socket state
        /// </summary>
        private void LogSocketDebugInfo(string context)
        {
            try
            {
                Console.WriteLine($"[RTPAUDIO DEBUG] === Socket Debug Info ({context}) ===");
                Console.WriteLine($"[RTPAUDIO DEBUG] _rtpSocket is null: {_rtpSocket == null}");
                if (_rtpSocket != null)
                {
                    Console.WriteLine($"[RTPAUDIO DEBUG] _rtpSocket.Client is null: {_rtpSocket.Client == null}");
                    if (_rtpSocket.Client != null)
                    {
                        Console.WriteLine($"[RTPAUDIO DEBUG] LocalEndPoint is null: {_rtpSocket.Client.LocalEndPoint == null}");
                        if (_rtpSocket.Client.LocalEndPoint != null)
                        {
                            Console.WriteLine($"[RTPAUDIO DEBUG] LocalEndPoint: {_rtpSocket.Client.LocalEndPoint}");
                        }
                        Console.WriteLine($"[RTPAUDIO DEBUG] Socket Connected: {_rtpSocket.Client.Connected}");
                        Console.WriteLine($"[RTPAUDIO DEBUG] Socket Bound: {_rtpSocket.Client.IsBound}");
                    }
                }
                Console.WriteLine($"[RTPAUDIO DEBUG] _localRtpPort: {_localRtpPort}");
                Console.WriteLine($"[RTPAUDIO DEBUG] === End Socket Debug Info ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO DEBUG] Error logging socket info: {ex.Message}");
            }
        }

        /// <summary>
        /// Enhanced dispose method with proper Windows 10+ cleanup
        /// </summary>
        public void Dispose()
        {
            try
            {
                Console.WriteLine("[RTPAUDIO] Disposing RtpAudioManager...");
                
                StopRtpSession();
                
                lock (_microphoneLock)
                {
                    if (_testMicrophone != null)
                    {
                        try
                        {
                            _testMicrophone.StopRecording();
                            _testMicrophone.Dispose();
                            _testMicrophone = null;
                            Console.WriteLine("[RTPAUDIO] ✅ Test microphone disposed");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error disposing test microphone: {ex.Message}");
                        }
                    }
                }
                
                _cancellationTokenSource?.Dispose();
                
                Console.WriteLine("[RTPAUDIO] ✅ RtpAudioManager disposed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error during disposal: {ex.Message}");
            }
        }

        /// <summary>
        /// Start incoming RTP packet listener for audio playback
        /// </summary>
        private void StartIncomingRtpListener()
        {
            if (_rtpSocket == null || _cancellationTokenSource == null)
            {
                Console.WriteLine("[RTPAUDIO] ❌ Cannot start RTP listener: socket or cancellation token not available");
                return;
            }

            Console.WriteLine("[RTPAUDIO] 🎧 Starting incoming RTP packet listener for audio playback...");
            
            // Start background task to listen for incoming RTP packets
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[1024]; // Buffer for incoming RTP packets
                    
                    while (!_cancellationTokenSource.Token.IsCancellationRequested && _isActive)
                    {
                        try
                        {
                            // Receive incoming RTP packet
                            var result = await _rtpSocket.ReceiveAsync();
                            
                            if (result.Buffer.Length > 12) // Must have RTP header (12 bytes) + audio data
                            {
                                ProcessIncomingRtpPacket(result.Buffer);
                            }
                        }
                        catch (ObjectDisposedException)
                        {
                            // Socket was disposed, exit gracefully
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[RTPAUDIO] ⚠️ Error receiving RTP packet: {ex.Message}");
                            await Task.Delay(10); // Small delay to prevent rapid error loops
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RTPAUDIO] ❌ RTP listener stopped with error: {ex.Message}");
                }
                
                Console.WriteLine("[RTPAUDIO] 🎧 RTP listener stopped");
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Process incoming RTP packet and decode G.711 A-law audio for playback
        /// </summary>
        private void ProcessIncomingRtpPacket(byte[] rtpPacket)
        {
            try
            {
                if (rtpPacket.Length < 12)
                {
                    Console.WriteLine("[RTPAUDIO] ⚠️ Received RTP packet too small (< 12 bytes)");
                    return;
                }

                // Parse RTP header
                byte version = (byte)((rtpPacket[0] & 0xC0) >> 6);
                byte payloadType = (byte)(rtpPacket[1] & 0x7F);
                ushort sequenceNumber = (ushort)((rtpPacket[2] << 8) | rtpPacket[3]);
                uint timestamp = (uint)((rtpPacket[4] << 24) | (rtpPacket[5] << 16) | (rtpPacket[6] << 8) | rtpPacket[7]);
                
                // Validate RTP packet
                if (version != 2)
                {
                    Console.WriteLine($"[RTPAUDIO] ⚠️ Invalid RTP version: {version}");
                    return;
                }

                // Extract audio payload (skip 12-byte RTP header)
                int audioDataLength = rtpPacket.Length - 12;
                byte[] encodedAudio = new byte[audioDataLength];
                Array.Copy(rtpPacket, 12, encodedAudio, 0, audioDataLength);

                // Decode G.711 A-law to 16-bit PCM
                byte[] decodedPcm = DecodeALawToPcm(encodedAudio);

                // Add decoded audio to playback buffer
                if (_audioBuffer != null && decodedPcm.Length > 0)
                {
                    _audioBuffer.AddSamples(decodedPcm, 0, decodedPcm.Length);
                    
                                       
                    // Log every 100th packet to avoid spam
                    if (sequenceNumber % 100 == 0)
                    {
                        Console.WriteLine($"[RTPAUDIO] 🎧 RTP packet #{sequenceNumber} received " +
                                          $"({audioDataLength} bytes G.711 A-law -> {decodedPcm.Length} bytes PCM) " +
                                          $"| Payload Type: {payloadType} | Buffer: {_audioBuffer.BufferedBytes} bytes");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RTPAUDIO] ❌ Error processing incoming RTP packet: {ex.Message}");
            }
        }

        /// <summary>
        /// Decode G.711 A-law encoded audio data to 16-bit PCM for playback
        /// </summary>
        private static byte[] DecodeALawToPcm(byte[] alawData)
        {
            byte[] pcmData = new byte[alawData.Length * 2]; // Each A-law byte becomes 2 PCM bytes (16-bit)
            
            for (int i = 0; i < alawData.Length; i++)
            {
                short pcmSample = ALawToLinearSample(alawData[i]);
                
                // Convert to little-endian 16-bit PCM
                pcmData[i * 2] = (byte)(pcmSample & 0xFF);
                pcmData[i * 2 + 1] = (byte)((pcmSample >> 8) & 0xFF);
            }
            
            return pcmData;
        }        /// <summary>
        /// Convert G.711 A-law encoded byte to 16-bit linear PCM sample (ITU-T G.711 standard)
        /// </summary>
        private static short ALawToLinearSample(byte alawByte)
        {
            // Apply A-law inversion
            alawByte = (byte)(alawByte ^ 0x55);
            
            int sign = alawByte & 0x80;
            int exponent = (alawByte & 0x70) >> 4;
            int mantissa = alawByte & 0x0F;
            
            int sample;
            if (exponent == 0)
            {
                sample = (mantissa << 4) + 8;
            }
            else
            {
                sample = ((mantissa + 16) << (exponent + 3));
            }
            
            // Apply sign
            return (short)(sign != 0 ? -sample : sample);
        }
        
        /// <summary>
        /// Get input device name by ID for logging
        /// </summary>
        private string GetInputDeviceName(int deviceId)
        {
            try
            {
                if (deviceId < 0) return "System Default";
                if (deviceId >= WaveIn.DeviceCount) return "Unknown Device";
                
                var caps = WaveIn.GetCapabilities(deviceId);
                return caps.ProductName;
            }
            catch
            {
                return "Unknown Device";
            }
        }
        
        /// <summary>
        /// Get output device name by ID for logging
        /// </summary>
        private string GetOutputDeviceName(int deviceId)
        {
            try
            {
                if (deviceId < 0) return "System Default";
                if (deviceId >= WaveOut.DeviceCount) return "Unknown Device";
                
                var caps = WaveOut.GetCapabilities(deviceId);
                return caps.ProductName;
            }
            catch
            {
                return "Unknown Device";
            }
        }
    }
}

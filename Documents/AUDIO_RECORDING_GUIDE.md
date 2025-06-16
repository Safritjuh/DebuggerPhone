# Audio Test Recording and Quality Assurance

## Overview

This document describes the comprehensive audio recording and quality analysis infrastructure implemented for the SIP Phone application testing suite. This addresses the requirement to create recording artifacts of test phone calls and enable audio quality regression detection.

## Features Implemented

### 🎤 Primary Goal: Call Recording Artifacts

The testing infrastructure now captures audio recordings during integration tests to verify software functionality:

- **WAV File Generation**: All test calls are recorded as standard WAV files (16-bit PCM, 8kHz mono)
- **Timestamped Artifacts**: Each recording includes unique identifiers and timestamps
- **CI/CD Integration**: Audio artifacts are automatically uploaded as GitHub Actions artifacts
- **30-Day Retention**: Recording artifacts are retained for 30 days for historical analysis

### 📊 Secondary Goal: Audio Quality Analysis

Advanced audio quality metrics and regression detection capabilities:

- **Quality Scoring**: Automated 0-100 quality scores based on multiple audio metrics
- **Signal Analysis**: SNR, dynamic range, peak/average levels, and frequency distribution
- **Baseline Comparison**: Compare current recordings against established baselines
- **Regression Detection**: Automatic failure if audio quality degrades beyond thresholds
- **JSON Analysis Reports**: Detailed quality metrics saved alongside audio files

## Test Categories

### CallRecording Integration Tests

Four specialized test categories have been added:

1. **`RecordTestCall_WithSipServer_ShouldCreateAudioArtifacts`**
   - Simulates a complete phone call with the Asterisk SIP server
   - Records both incoming and outgoing audio
   - Validates artifact creation and file integrity

2. **`RecordTestTones_ShouldCreateQualityBaseline`**
   - Generates standardized test tones at telephony frequencies (300, 440, 1000, 1400, 2000 Hz)
   - Creates baseline recordings for quality comparison
   - Validates audio quality metrics

3. **`RecordSipRegistrationCall_ShouldCaptureProtocolAudio`**
   - Records audio during SIP registration and protocol exchange
   - Captures dial tones, confirmation tones, and error signals
   - Validates SIP protocol audio behavior

4. **`AudioQualityComparison_WithBaseline_ShouldDetectDegradation`**
   - Demonstrates baseline comparison functionality
   - Detects audio quality regressions automatically
   - Fails tests if quality drops below acceptable thresholds

## Audio Quality Metrics

### Core Quality Indicators

- **Signal-to-Noise Ratio (SNR)**: Measures audio clarity (>20dB is good, >10dB acceptable)
- **Dynamic Range**: Difference between peak and noise floor levels
- **Average Level**: Overall signal strength (10-90% of max range optimal)
- **Peak Level**: Maximum signal amplitude (should not exceed 90% to avoid clipping)

### Frequency Analysis

- **Low Frequency Energy**: 0-33% of spectrum (bass/fundamental frequencies)
- **Mid Frequency Energy**: 33-66% of spectrum (voice clarity range)
- **High Frequency Energy**: 66-100% of spectrum (consonants/detail)

### Quality Scoring Algorithm

```csharp
Quality Score = 100 (base)
- Penalty for low signal levels (up to -30 points)
- Penalty for poor SNR (up to -40 points)  
- Penalty for limited dynamic range (up to -20 points)
= Final Score (0-100)
```

**Quality Ranges:**
- 80-100: Excellent audio quality
- 60-79: Good audio quality
- 40-59: Fair audio quality (may have issues)
- 0-39: Poor audio quality (significant issues)

## CI/CD Integration

### Pipeline Enhancements

The CI/CD pipeline has been enhanced with audio recording capabilities:

```yaml
- name: Run call recording tests
  run: dotnet test --filter "Category=CallRecording"
  
- name: Upload audio test artifacts
  uses: actions/upload-artifact@v4
  if: always()
  with:
    name: audio-test-artifacts
    path: /tmp/audio-test-artifacts/
    retention-days: 30
```

### Artifact Structure

Audio artifacts are organized as follows:

```
/tmp/audio-test-artifacts/
├── call-recording-{test-id}-{timestamp}.wav
├── audio-analysis-{test-id}-{timestamp}.json
├── baseline-tones-{test-id}-{timestamp}.wav
└── quality-comparison-results.json
```

## Usage Examples

### Manual Test Execution

Run only call recording tests:
```bash
dotnet test --filter "Category=CallRecording"
```

Run all tests including audio recording:
```bash
dotnet test
```

### Viewing Audio Artifacts

After test execution, audio artifacts are available in `/tmp/audio-test-artifacts/`:

```bash
# List all recordings
ls -la /tmp/audio-test-artifacts/*.wav

# View quality analysis
cat /tmp/audio-test-artifacts/audio-analysis-*.json

# Play audio (if audio tools available)
aplay /tmp/audio-test-artifacts/call-recording-*.wav
```

### Quality Analysis Example

Sample audio analysis output:
```json
{
  "RecordingId": "baseline-tones-20250612-233146",
  "Timestamp": "2025-06-12T23:31:59.2707536Z",
  "TotalSamples": 5,
  "Duration": 10.0008253,
  "AverageLevel": 24.09150390625,
  "PeakLevel": 39.996337890625,
  "SignalToNoiseRatio": 37.31766685969818,
  "DynamicRange": 39.910330636160715,
  "QualityScore": 90,
  "FrequencyAnalysis": {
    "LowFrequency": 33.25033371252952,
    "MidFrequency": 33.132784066319196,
    "HighFrequency": 33.616882221151286
  },
  "Notes": "Excellent audio quality"
}
```

## Implementation Details

### TestAudioRecorder Class

The core recording functionality is implemented in `TestAudioRecorder.cs`:

- **Thread-safe recording**: Uses locks to ensure safe concurrent access
- **Multiple audio formats**: Supports various sample rates and bit depths
- **Test tone generation**: Built-in sine wave generator for standardized testing
- **WAV file creation**: Proper WAV header generation with PCM encoding
- **Quality analysis**: Real-time audio quality metrics calculation

### Audio Sample Structure

```csharp
public class AudioSample
{
    public DateTime Timestamp { get; set; }
    public byte[] Data { get; set; }
    public AudioDirection Direction { get; set; } // Incoming/Outgoing
    public int SampleRate { get; set; }
    public int Channels { get; set; }
}
```

### Integration with Existing Tests

The audio recording functionality integrates seamlessly with existing SIP integration tests:

- **Non-intrusive**: Existing tests continue to work without modification
- **Optional recording**: Recording only activates when `TestAudioRecorder` is instantiated
- **Separate test category**: New tests use `[Trait("Category", "CallRecording")]`
- **Docker compatibility**: Works with the existing Asterisk SIP server setup

## Quality Assurance Benefits

### For Developers

- **Audio regression detection**: Automatically catch audio quality degradation
- **Debugging support**: Playable recordings help diagnose audio issues
- **Performance validation**: Ensure audio processing meets SLA requirements
- **Protocol verification**: Confirm SIP audio exchange works correctly

### For QA Teams

- **Artifact preservation**: 30-day retention for manual verification
- **Quality metrics**: Objective measurements replace subjective assessments
- **Trend analysis**: Historical quality data helps identify patterns
- **Compliance verification**: Ensure telephony standards are met

### For CI/CD Pipeline

- **Automated quality gates**: Fail builds on significant audio degradation
- **Artifact collection**: Centralized storage of test recordings
- **Regression prevention**: Catch issues before they reach production
- **Quality reporting**: Generate quality trends and reports

## Future Enhancements

### Potential Improvements

1. **PESQ Implementation**: Add industry-standard PESQ (Perceptual Evaluation of Speech Quality) scoring
2. **Codec Testing**: Test various audio codecs (G.729, GSM, etc.) beyond G.711
3. **Network Simulation**: Test audio quality under different network conditions
4. **Voice Activity Detection**: Analyze speech patterns and silence detection
5. **Echo Analysis**: Measure and test echo cancellation effectiveness

### Advanced Quality Metrics

1. **STOI (Short-Time Objective Intelligibility)**: Speech intelligibility measurement
2. **THD (Total Harmonic Distortion)**: Audio fidelity analysis
3. **Jitter Analysis**: Network timing variation measurement
4. **Packet Loss Simulation**: Test audio quality under network stress

## Conclusion

The audio recording and quality analysis infrastructure provides comprehensive verification of the SIP Phone application's audio functionality. This addresses both the primary requirement for recording artifacts and the secondary goal of quality regression detection, ensuring high-quality voice communication in production environments.

The implementation provides:
- ✅ **Recording artifacts** for manual verification and compliance
- ✅ **Automated quality analysis** for regression detection  
- ✅ **CI/CD integration** for continuous quality assurance
- ✅ **Comprehensive metrics** for objective audio assessment
- ✅ **Future-ready architecture** for advanced telephony testing

This foundation enables confident continuous deployment while maintaining the audio quality standards expected in professional telephony applications.
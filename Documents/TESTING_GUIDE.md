# Testing Guide for SIP Phone Application

This guide provides comprehensive instructions for testing the SIP Phone application across different environments and scenarios.

## Quick Start Testing

### Prerequisites

1. **.NET 8.0 SDK** installed
2. **Git** for version control
3. **Docker** (for integration tests)

### Running Tests

```bash
# Run all unit tests
dotnet test WindowsSipPhone.Tests --filter "Category!=Integration"

# Run integration tests (requires Docker)
docker run -d --name asterisk -p 5060:5060/udp andrius/asterisk:latest
dotnet test WindowsSipPhone.Tests --filter "Category=Integration"

# Run with coverage
dotnet test WindowsSipPhone.Tests --collect:"XPlat Code Coverage"
```

## Test Structure

### Unit Tests
Located in `WindowsSipPhone.Tests/`

- **SdpManagerTests.cs** - SDP creation and parsing
- **SipDigestAuthTests.cs** - Authentication algorithms
- **PerformanceTests.cs** - Performance and memory tests

### Integration Tests
Located in `WindowsSipPhone.Tests/SipIntegrationTests.cs`

- SIP server connectivity
- End-to-end message flows
- Protocol compliance validation

## Test Categories

### 1. Core SIP Functionality Tests

**SDP (Session Description Protocol)**
```csharp
[Fact]
public void CreateSdpOffer_WithValidParameters_ReturnsValidSdp()
```
- Validates SDP structure and content
- Tests codec preferences (G.711 A-law/μ-law)
- Verifies DTMF support configuration

**Digest Authentication**
```csharp
[Fact]
public void CalculateResponse_WithValidParameters_ReturnsCorrectResponse()
```
- Tests MD5 hash calculation
- Validates WWW-Authenticate header parsing
- Verifies Authorization header generation

### 2. Performance Tests

**Memory Usage**
```csharp
[Theory]
[InlineData(100), InlineData(1000), InlineData(5000)]
public void MemoryUsageShouldBeStable(int iterations)
```
- Monitors memory allocation patterns
- Detects memory leaks
- Validates garbage collection efficiency

**Response Time**
```csharp
[Fact]
public void ShouldExecuteWithinPerformanceBounds()
```
- Measures operation execution time
- Validates performance requirements
- Identifies performance regressions

### 3. Integration Tests

**SIP Server Connectivity**
```csharp
[Fact]
public async Task SipServer_ShouldBeReachable()
```
- Tests network connectivity to SIP server
- Validates TCP/UDP socket creation
- Confirms server responsiveness

**SIP Registration Flow**
```csharp
[Fact]
public async Task SipRegistration_WithValidCredentials_ShouldSucceed()
```
- Tests complete registration workflow
- Validates authentication challenges
- Confirms proper message formatting

## Test Data and Configuration

### Default Test Configuration

```csharp
private readonly string _sipServerHost = "localhost";
private readonly int _sipServerPort = 5060;
```

### Test Credentials

```csharp
var username = "testuser";
var password = "testpass";
var domain = "test.domain.local";
```

### Network Configuration

```csharp
// Client SIP port
var clientPort = 5062;

// RTP port range
var rtpPortRange = 5004..5010;
```

## Manual Testing Scenarios

### 1. Registration Testing

**Happy Path:**
1. Start the application
2. Configure SIP settings (server, credentials)
3. Attempt registration
4. Verify successful registration status

**Error Scenarios:**
1. Invalid credentials → Expect authentication failure
2. Unreachable server → Expect timeout error
3. Malformed configuration → Expect validation error

### 2. Call Testing

**Outbound Call:**
1. Register with SIP server
2. Enter target number
3. Initiate call
4. Verify call establishment
5. Test audio bidirectionally
6. Terminate call

**Inbound Call:**
1. Register with SIP server
2. Receive incoming call
3. Accept call
4. Verify audio functionality
5. End call

### 3. Audio Testing

**Codec Testing:**
- Test G.711 A-law codec
- Test G.711 μ-law codec
- Verify DTMF tone generation
- Test volume controls
- Test mute/unmute functionality

## Automated Testing with GitHub Actions

### CI/CD Pipeline

The testing pipeline runs automatically on:
- Pull requests to `main`
- Pushes to `main` and `develop`

### Pipeline Stages

1. **Unit Tests** (Ubuntu)
   - Build and restore dependencies
   - Run unit and performance tests
   - Generate code coverage report
   - Upload coverage to Codecov

2. **Integration Tests** (Ubuntu + Docker)
   - Start Asterisk SIP server
   - Wait for server readiness
   - Execute integration test suite
   - Validate SIP protocol compliance

3. **Windows Build** (Windows)
   - Build WPF application
   - Create executable package
   - Store build artifacts

4. **Release** (main branch only)
   - Generate version number
   - Create GitHub release
   - Attach executable files

## Test Environment Setup

### Local Development

```bash
# Clone repository
git clone https://github.com/Safritjuh/Sip-Phone.git
cd Sip-Phone

# Restore dependencies
dotnet restore WindowsSipPhone.Tests/

# Run tests
dotnet test WindowsSipPhone.Tests/
```

### Docker SIP Server

```bash
# Start Asterisk SIP server
docker run -d \
  --name test-asterisk \
  -p 5060:5060/udp \
  -p 5060:5060/tcp \
  andrius/asterisk:latest

# Verify server is running
docker logs test-asterisk
```

### Alternative SIP Servers

**FreeSWITCH:**
```bash
docker run -d \
  --name test-freeswitch \
  -p 5060:5060/udp \
  safarov/freeswitch
```

**OpenSIPS:**
```bash
docker run -d \
  --name test-opensips \
  -p 5060:5060/udp \
  opensips/opensips
```

## Troubleshooting Test Issues

### Common Issues

**Test Server Unreachable:**
- Check Docker container status
- Verify port mappings
- Test network connectivity

**Authentication Failures:**
- Verify test credentials
- Check server configuration
- Validate digest calculation

**Performance Test Failures:**
- Run on isolated environment
- Check system resources
- Verify test thresholds

### Debug Commands

```bash
# Check Docker containers
docker ps -a

# View container logs
docker logs test-asterisk

# Test network connectivity
telnet localhost 5060

# Check UDP port
nc -u localhost 5060
```

## Test Reporting

### Coverage Reports

Generated automatically by CI pipeline:
- Line coverage percentage
- Branch coverage analysis
- Uncovered code identification

### Performance Metrics

Tracked in test execution:
- Execution time benchmarks
- Memory usage patterns
- Resource utilization

### Quality Gates

**Pull Request Requirements:**
- All tests must pass
- Coverage must be ≥ 80%
- No performance regressions
- Integration tests successful

## Best Practices

### Writing Tests

1. **Naming Convention:** `Method_Scenario_ExpectedOutcome`
2. **Test Structure:** Arrange-Act-Assert pattern
3. **Independence:** Tests should not depend on each other
4. **Determinism:** Tests should produce consistent results

### Test Maintenance

1. **Regular Updates:** Keep tests aligned with code changes
2. **Performance Monitoring:** Track test execution time
3. **Cleanup:** Remove obsolete tests
4. **Documentation:** Update test documentation

### Continuous Improvement

1. **Code Review:** Review test code like production code
2. **Refactoring:** Improve test quality regularly
3. **Metrics Analysis:** Monitor test effectiveness
4. **Feedback Loop:** Learn from test failures

---

*For questions or issues with testing, please refer to the [QA Standards](QA_STANDARDS.md) document or create an issue in the repository.*
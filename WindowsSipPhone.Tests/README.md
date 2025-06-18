# WindowsSipPhone.Tests

This directory contains automated tests for the Windows SIP Phone application, implementing comprehensive unit and integration testing as specified in issue #50.

## Test Structure

### Unit Tests (`Category=Unit`)
- **SipDigestAuth Tests**: Validation of SIP digest authentication header generation
- **SdpManager Tests**: Session Description Protocol (SDP) creation and validation
- **Performance Tests**: Benchmarking of core SIP operations

### Integration Tests (`Category=Integration`)
- **SIP Server Connectivity**: Validates network connectivity to SIP server
- **SIP Registration**: End-to-end registration testing with valid/invalid credentials
- **SIP Message Flow**: Validates proper SIP protocol message exchange

### Performance Tests (`Category=Performance`)
- **SDP Creation Performance**: Measures SDP generation performance
- **Auth Header Performance**: Measures digest authentication performance

## Test Configuration

### SIP Server Settings (for CI/CD)
- **Host**: `localhost` (Docker container in CI)
- **Port**: `5060`
- **Protocol**: TCP
- **Username**: `103`
- **Password**: `274104`

### Running Tests

```bash
# Run all unit tests (excludes integration tests that require SIP server)
dotnet test --filter "Category!=Integration"

# Run only integration tests (requires SIP server)
dotnet test --filter "Category=Integration"

# Run all tests
dotnet test
```

## CI/CD Integration

The tests are integrated with the existing GitHub Actions workflow in `.github/workflows/ci-cd.yml`:

1. **Unit Tests**: Run on Ubuntu without external dependencies
2. **Integration Tests**: Run with Docker-based Asterisk SIP server
3. **Test Coverage**: Results uploaded to Codecov

## Cross-Platform Compatibility

The test project targets `.NET 8.0` (not Windows-specific) and includes:
- Custom `TestSipClient` for cross-platform SIP testing
- Only cross-platform SIP core components
- No Windows WPF dependencies

## Implementation Notes

- Tests use xUnit framework with proper categorization
- Integration tests validate SIP RFC 3261 compliance
- Performance tests ensure operations complete within reasonable bounds
- Tests provide detailed output for debugging and validation
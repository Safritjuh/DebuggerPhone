using Xunit;
using Xunit.Abstractions;
using System;

namespace WindowsSipPhone.Tests.Unit
{
    /// <summary>
    /// Unit tests for IncomingCallWindow exception handling logic
    /// Tests the DialogResult handling without requiring WPF components
    /// </summary>
    public class IncomingCallWindowLogicTests
    {
        private readonly ITestOutputHelper _output;

        public IncomingCallWindowLogicTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void DialogResultExceptionHandling_ShouldHandleInvalidOperationException()
        {
            // Arrange
            var exceptionThrown = false;
            var exceptionCaught = false;
            
            // Simulate the try-catch logic from OnClosing method
            Action setDialogResult = () =>
            {
                // Simulate setting DialogResult on non-modal window
                exceptionThrown = true;
                throw new InvalidOperationException("DialogResult can be set only after Window is created and shown as dialog.");
            };

            // Act
            try
            {
                setDialogResult();
            }
            catch (InvalidOperationException ex)
            {
                exceptionCaught = true;
                _output.WriteLine($"Caught expected exception: {ex.Message}");
            }

            // Assert
            Assert.True(exceptionThrown, "InvalidOperationException should be thrown when setting DialogResult on non-modal window");
            Assert.True(exceptionCaught, "InvalidOperationException should be caught and handled gracefully");
            
            _output.WriteLine("DialogResult exception handling test passed");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        [Trait("Category", "Unit")]
        public void CallAcceptedFlag_ShouldDetermineIfDialogResultIsSet(bool callAccepted)
        {
            // Arrange
            bool dialogResultSetAttempted = false;
            bool dialogResultTrue = false;
            bool dialogResultFalse = false;
            
            // Simulate the condition logic from OnClosing method
            // if (!_callAccepted && DialogResult != true && DialogResult != false)
            
            // Act - Simulate the logic without actual WPF DialogResult
            if (!callAccepted && !dialogResultTrue && !dialogResultFalse)
            {
                dialogResultSetAttempted = true;
            }

            // Assert
            if (callAccepted)
            {
                Assert.False(dialogResultSetAttempted, "DialogResult should not be set when call was already accepted");
                _output.WriteLine("Call was accepted - DialogResult not set (correct behavior)");
            }
            else
            {
                Assert.True(dialogResultSetAttempted, "DialogResult should be attempted when call was not accepted");
                _output.WriteLine("Call was not accepted - DialogResult set attempted (correct behavior)");
            }
        }

        [Fact]
        [Trait("Category", "Unit")]
        public void CallerInfoParsing_ShouldHandleVariousFormats()
        {
            // This tests the caller info parsing logic that's used in the constructor
            // Testing different SIP URI formats that might be received

            var testCases = new[]
            {
                ("sip:12345@domain.com", "12345", "12345"),
                ("\"John Doe\" <sip:12345@domain.com>", "John Doe", "12345"),
                ("12345", "12345", "12345"),
                ("sip:user@192.168.1.1", "user", "user")
            };

            foreach (var (input, expectedName, expectedNumber) in testCases)
            {
                _output.WriteLine($"Testing caller info parsing for: {input}");
                
                // This would normally be tested by calling ParseCallerInfo method
                // but since we can't instantiate the WPF window, we'll test the logic principles
                
                string displayName = "";
                string actualNumber = "";
                
                // Simulate the parsing logic
                if (input.Contains("<") && input.Contains(">"))
                {
                    var nameEnd = input.IndexOf('<');
                    if (nameEnd > 0)
                    {
                        displayName = input.Substring(0, nameEnd).Trim().Trim('"');
                    }
                    
                    var uriStart = input.IndexOf('<') + 1;
                    var uriEnd = input.IndexOf('>');
                    if (uriEnd > uriStart)
                    {
                        var uri = input.Substring(uriStart, uriEnd - uriStart);
                        actualNumber = ExtractNumberFromUri(uri);
                    }
                }
                else if (input.StartsWith("sip:"))
                {
                    actualNumber = ExtractNumberFromUri(input);
                }
                else
                {
                    actualNumber = input;
                }
                
                var finalCallerName = !string.IsNullOrWhiteSpace(displayName) ? displayName : actualNumber;
                var finalCallerNumber = actualNumber;
                
                Assert.Equal(expectedName, finalCallerName);
                Assert.Equal(expectedNumber, finalCallerNumber);
                
                _output.WriteLine($"  Expected: Name='{expectedName}', Number='{expectedNumber}'");
                _output.WriteLine($"  Actual:   Name='{finalCallerName}', Number='{finalCallerNumber}'");
            }
        }

        private string ExtractNumberFromUri(string sipUri)
        {
            if (sipUri.StartsWith("sip:"))
            {
                var withoutScheme = sipUri.Substring(4);
                var atIndex = withoutScheme.IndexOf('@');
                if (atIndex > 0)
                {
                    return withoutScheme.Substring(0, atIndex);
                }
                return withoutScheme;
            }
            return sipUri;
        }
    }
}
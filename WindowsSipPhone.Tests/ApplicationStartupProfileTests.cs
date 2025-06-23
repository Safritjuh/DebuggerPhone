using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Tests
{
    /// <summary>
    /// Unit tests to verify profiles are present during application startup
    /// </summary>
    public class ApplicationStartupProfileTests
    {
        [Fact]
        public void ApplicationStartup_ShouldHaveProfilesAvailable()
        {
            // Act
            var profiles = SipProfile.GetPredefinedProfiles();
            
            // Assert
            Assert.NotNull(profiles);
            Assert.NotEmpty(profiles);
            Assert.True(profiles.Count >= 5, $"Expected at least 5 profiles, got {profiles.Count}");
        }

        [Fact]
        public void ApplicationStartup_ShouldHaveRequiredProfiles()
        {
            // Arrange
            var expectedProfiles = new[] { "Generic", "Avaya IP Office", "Cloud Generic", "FreeSWITCH", "Cisco" };
            
            // Act
            var profiles = SipProfile.GetPredefinedProfiles();
            var profileNames = profiles.Select(p => p.Name).ToList();
            
            // Assert
            foreach (var expectedProfile in expectedProfiles)
            {
                Assert.Contains(expectedProfile, profileNames);
            }
        }

        [Fact]
        public void ApplicationStartup_ShouldHaveValidProfileConfiguration()
        {
            // Act
            var profiles = SipProfile.GetPredefinedProfiles();
            
            // Assert
            foreach (var profile in profiles)
            {
                Assert.False(string.IsNullOrWhiteSpace(profile.Name), "Profile name cannot be null or empty");
                Assert.False(string.IsNullOrWhiteSpace(profile.Description), "Profile description cannot be null or empty");
                Assert.True(profile.RegistrationExpiry > 0, "Registration expiry must be positive");
                Assert.False(string.IsNullOrWhiteSpace(profile.Transport), "Transport cannot be null or empty");
                Assert.True(profile.DefaultPort > 0, "Default port must be positive");
            }
        }

        [Fact]
        public void ApplicationStartup_ShouldHaveDefaultProfile()
        {
            // Act
            var defaultProfile = SipProfile.GetDefaultProfile();
            
            // Assert
            Assert.NotNull(defaultProfile);
            Assert.Equal("Generic", defaultProfile.Name);
            Assert.False(string.IsNullOrWhiteSpace(defaultProfile.Description));
        }

        [Fact]
        public void ApplicationStartup_ShouldCreateDefaultProfilesIfNeeded()
        {
            // Act & Assert - Should not throw
            try
            {
                SipProfile.CreateDefaultProfilesIfNeeded();
                // If we get here without an exception, the test passes
                Assert.True(true);
            }
            catch (Exception ex)
            {
                Assert.Fail($"CreateDefaultProfilesIfNeeded should not throw, but threw: {ex.Message}");
            }
        }

        [Fact]
        public void ApplicationStartup_ShouldFindSpecificProfileByName()
        {
            // Act
            var genericProfile = SipProfile.GetPredefinedProfile("Generic");
            var avayaProfile = SipProfile.GetPredefinedProfile("Avaya IP Office");
            
            // Assert
            Assert.NotNull(genericProfile);
            Assert.Equal("Generic", genericProfile.Name);
            
            Assert.NotNull(avayaProfile);
            Assert.Equal("Avaya IP Office", avayaProfile.Name);
        }

        [Fact]
        public void ApplicationStartup_ShouldHandleNonExistentProfile()
        {
            // Act
            var nonExistentProfile = SipProfile.GetPredefinedProfile("NonExistentProfile");
            
            // Assert
            Assert.Null(nonExistentProfile);
        }

        [Fact]
        public void ApplicationStartup_ProfilesShouldHaveValidTransport()
        {
            // Arrange
            var validTransports = new[] { "TCP", "UDP", "TLS" };
            
            // Act
            var profiles = SipProfile.GetPredefinedProfiles();
            
            // Assert
            foreach (var profile in profiles)
            {
                Assert.Contains(profile.Transport, validTransports);
            }
        }

        [Fact]
        public void ApplicationStartup_ProfilesShouldHaveUserAgentString()
        {
            // Act
            var profiles = SipProfile.GetPredefinedProfiles();
            
            // Assert
            foreach (var profile in profiles)
            {
                Assert.False(string.IsNullOrWhiteSpace(profile.UserAgentString), 
                    $"Profile '{profile.Name}' should have a User Agent string");
            }
        }
    }
}
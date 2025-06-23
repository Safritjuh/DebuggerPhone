using System;
using System.IO;
using System.Linq;
using Xunit;
using WindowsSipPhone.Core.Models;
using WindowsSipPhone.Core.Utilities;
using WindowsSipPhone.Services.Data;

namespace WindowsSipPhone.Tests
{
    /// <summary>
    /// Unit tests to verify SIP Profile system functionality
    /// </summary>
    public class SipProfileSystemTests
    {
        [Fact]
        public void SipProfile_ShouldHaveRequiredPredefinedProfiles()
        {
            // Act
            var profiles = SipProfile.GetPredefinedProfiles();
            
            // Assert
            Assert.NotNull(profiles);
            Assert.Equal(5, profiles.Count);
            
            var expectedProfiles = new[] { "Generic", "Avaya IP Office", "Cloud Generic", "FreeSWITCH", "Cisco" };
            
            foreach (var expectedName in expectedProfiles)
            {
                var profile = profiles.FirstOrDefault(p => p.Name == expectedName);
                Assert.NotNull(profile);
                Assert.False(string.IsNullOrWhiteSpace(profile.Description));
                Assert.True(profile.RegistrationExpiry > 0);
                Assert.False(string.IsNullOrWhiteSpace(profile.Transport));
                Assert.False(string.IsNullOrWhiteSpace(profile.UserAgentString));
            }
        }

        [Fact]
        public void SipProfile_ShouldHaveValidGenericProfile()
        {
            // Act
            var profile = SipProfile.GetPredefinedProfile("Generic");
            
            // Assert
            Assert.NotNull(profile);
            Assert.Equal("Generic", profile.Name);
            Assert.Equal("Default generic SIP settings compatible with most platforms", profile.Description);
            Assert.False(profile.IsCustom);
            Assert.Equal(300, profile.RegistrationExpiry);
            Assert.False(profile.RequireKeepAlive);
            Assert.Equal("TCP", profile.Transport);
            Assert.Equal("Windows-SIP-Phone/2.0", profile.UserAgentString);
            Assert.False(profile.UseShortHeaders);
            Assert.Equal(5060, profile.DefaultPort);
        }

        [Fact]
        public void SipProfile_ShouldHaveValidAvayaProfile()
        {
            // Act
            var profile = SipProfile.GetPredefinedProfile("Avaya IP Office");
            
            // Assert
            Assert.NotNull(profile);
            Assert.Equal("Avaya IP Office", profile.Name);
            Assert.False(profile.IsCustom);
            Assert.Equal(3600, profile.RegistrationExpiry);
            Assert.False(profile.RequireKeepAlive);
            Assert.Equal("TCP", profile.Transport);
            Assert.Contains("Avaya", profile.UserAgentString);
        }

        [Fact]
        public void SipProfile_IniFileHandling_ShouldWorkCorrectly()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_profile_{Guid.NewGuid()}.ini");
            
            var testProfile = new SipProfile
            {
                Name = "Test Profile",
                Description = "Test description",
                IsCustom = true,
                RegistrationExpiry = 600,
                RequireKeepAlive = true,
                Transport = "UDP",
                UserAgentString = "Test-Agent/1.0",
                UseShortHeaders = true,
                DefaultPort = 5061
            };
            testProfile.PreferredCodecs.AddRange(new[] { "G722", "PCMU" });
            testProfile.CustomHeaders.Add("X-Test", "TestValue");

            try
            {
                // Act - Save and load profile
                ProfileManager.ExportProfileToIni(testProfile, tempFile);
                var loadedProfile = ProfileManager.ImportProfileFromIni(tempFile);

                // Assert
                Assert.NotNull(loadedProfile);
                Assert.Equal(testProfile.Name, loadedProfile.Name);
                Assert.Equal(testProfile.Description, loadedProfile.Description);
                Assert.Equal(testProfile.IsCustom, loadedProfile.IsCustom);
                Assert.Equal(testProfile.RegistrationExpiry, loadedProfile.RegistrationExpiry);
                Assert.Equal(testProfile.RequireKeepAlive, loadedProfile.RequireKeepAlive);
                Assert.Equal(testProfile.Transport, loadedProfile.Transport);
                Assert.Equal(testProfile.UserAgentString, loadedProfile.UserAgentString);
                Assert.Equal(testProfile.UseShortHeaders, loadedProfile.UseShortHeaders);
                Assert.Equal(testProfile.DefaultPort, loadedProfile.DefaultPort);
                Assert.Equal(testProfile.PreferredCodecs.Count, loadedProfile.PreferredCodecs.Count);
                Assert.Equal(testProfile.CustomHeaders.Count, loadedProfile.CustomHeaders.Count);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void SipProfile_CloneShouldCreateIndependentCopy()
        {
            // Arrange
            var originalProfile = SipProfile.GetPredefinedProfile("Generic");
            Assert.NotNull(originalProfile);

            // Act
            var clonedProfile = originalProfile.Clone();

            // Assert
            Assert.NotNull(clonedProfile);
            Assert.Equal(originalProfile.Name, clonedProfile.Name);
            Assert.Equal(originalProfile.Description, clonedProfile.Description);
            Assert.Equal(originalProfile.RegistrationExpiry, clonedProfile.RegistrationExpiry);
            
            // Verify they are independent objects
            Assert.NotSame(originalProfile, clonedProfile);
            Assert.NotSame(originalProfile.PreferredCodecs, clonedProfile.PreferredCodecs);
            Assert.NotSame(originalProfile.CustomHeaders, clonedProfile.CustomHeaders);
        }

        [Fact]
        public void ProfileManager_ShouldValidateProfilesCorrectly()
        {
            // Arrange
            var validProfile = SipProfile.GetPredefinedProfile("Generic");
            Assert.NotNull(validProfile);

            // Act & Assert - Should not throw for valid profile
            var exception = Record.Exception(() => ProfileManager.ValidateProfile(validProfile));
            Assert.Null(exception);
        }
    }
}
using System.Collections.Generic;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Core.Interfaces
{
    /// <summary>
    /// Interface for provider-specific SIP protocol handling
    /// Implements IMP-016: Profile-Specific SIP Handling and Provider Optimization
    /// </summary>
    public interface ISipProfileHandler
    {
        /// <summary>
        /// Gets the name of the SIP profile this handler supports
        /// </summary>
        string ProfileName { get; }
        
        /// <summary>
        /// Configures the SIP client with provider-specific settings
        /// </summary>
        /// <param name="client">The SIP client to configure</param>
        /// <param name="config">The profile configuration settings</param>
        void ConfigureSipClient(SimpleSipClient client, SipProfileConfiguration config);
        
        /// <summary>
        /// Gets provider-specific custom headers to include in SIP messages
        /// </summary>
        /// <returns>Dictionary of custom headers</returns>
        Dictionary<string, string> GetCustomHeaders();
        
        /// <summary>
        /// Handles incoming INVITE requests with provider-specific processing
        /// </summary>
        /// <param name="inviteMessage">The raw INVITE message</param>
        void HandleIncomingInvite(string inviteMessage);
        
        /// <summary>
        /// Handles registration responses with provider-specific validation
        /// </summary>
        /// <param name="responseMessage">The raw registration response message</param>
        /// <param name="statusCode">The status code from the response</param>
        void HandleRegistrationResponse(string responseMessage, string statusCode);
        
        /// <summary>
        /// Handles incoming SIP responses with provider-specific processing
        /// </summary>
        /// <param name="responseMessage">The raw SIP response message</param>
        /// <param name="statusCode">The status code from the response</param>
        void HandleIncomingResponse(string responseMessage, string statusCode);
        
        /// <summary>
        /// Validates registration response for provider-specific requirements
        /// </summary>
        /// <param name="responseMessage">The raw registration response message</param>
        /// <param name="statusCode">The status code from the response</param>
        /// <returns>True if registration is valid for this provider</returns>
        bool ValidateRegistration(string responseMessage, string statusCode);
        
        /// <summary>
        /// Determines if custom routing is required for the destination
        /// </summary>
        /// <param name="destination">The destination to route to</param>
        /// <returns>True if custom routing is required</returns>
        bool RequiresCustomRouting(string destination);
        
        /// <summary>
        /// Gets the preferred codec list for this provider
        /// </summary>
        /// <returns>List of preferred codecs in priority order</returns>
        List<string> GetPreferredCodecs();
        
        /// <summary>
        /// Generates custom SDP content for the provider
        /// </summary>
        /// <param name="requestMessage">The raw SIP request message requiring SDP</param>
        /// <returns>Custom SDP content or null for default handling</returns>
        string? GenerateCustomSDP(string requestMessage);
          /// <summary>
        /// Modifies outgoing SIP messages for provider-specific requirements
        /// </summary>        /// <param name="message">The raw SIP message to modify</param>
        /// <param name="messageType">The type of SIP message (REGISTER, INVITE, etc.)</param>
        /// <returns>Modified SIP message or original if no changes needed</returns>
        string ProcessOutgoingMessage(string message, string messageType);
    }
}

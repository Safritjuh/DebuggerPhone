using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using WindowsSipPhone.Core.Protocol;
using WindowsSipPhone.Core.Validation;
using WindowsSipPhone.Core.Transactions;
using WindowsSipPhone.Core.Models;

namespace WindowsSipPhone.Tests
{
    /// <summary>
    /// RFC 3261 Compliance Test Suite
    /// Comprehensive tests to validate SIP protocol compliance
    /// </summary>
    [TestClass]
    public class RFC3261ComplianceTests
    {
        private EnhancedSipMessageFactory _messageFactory;
        private Rfc3261Validator _validator;
        private TransactionManager _transactionManager;

        [TestInitialize]
        public void Setup()
        {
            _messageFactory = new EnhancedSipMessageFactory("192.168.1.100", "testuser");
            _validator = new Rfc3261Validator();
            _transactionManager = new TransactionManager();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _transactionManager?.Dispose();
        }

        #region Message Factory Compliance Tests

        [TestMethod]
        public void CreateRegisterRequest_ShouldComplyWithRFC3261()
        {
            // Arrange
            var username = "testuser";
            var serverHost = "sip.example.com";
            var serverPort = 5060;
            uint sequenceNumber = 1;

            // Act
            var registerMessage = _messageFactory.CreateRegisterRequest(
                username, serverHost, serverPort, sequenceNumber);

            // Assert
            var validationResult = _validator.ValidateMessage(registerMessage);
            
            Assert.IsFalse(validationResult.HasCriticalErrors, 
                $"REGISTER message has critical errors: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            
            // Verify mandatory headers
            Assert.IsTrue(registerMessage.Contains("Via:"), "Missing Via header");
            Assert.IsTrue(registerMessage.Contains("From:"), "Missing From header");
            Assert.IsTrue(registerMessage.Contains("To:"), "Missing To header");
            Assert.IsTrue(registerMessage.Contains("Call-ID:"), "Missing Call-ID header");
            Assert.IsTrue(registerMessage.Contains("CSeq:"), "Missing CSeq header");
            Assert.IsTrue(registerMessage.Contains("Contact:"), "Missing Contact header");
            Assert.IsTrue(registerMessage.Contains("Max-Forwards:"), "Missing Max-Forwards header");
            
            // Verify RFC 3261 magic cookie in Via branch
            Assert.IsTrue(registerMessage.Contains("branch=z9hG4bK"), "Via branch missing RFC 3261 magic cookie");
            
            // Verify proper Content-Length
            Assert.IsTrue(registerMessage.Contains("Content-Length: 0"), "Invalid Content-Length for REGISTER");
        }

        [TestMethod]
        public void CreateInviteRequest_ShouldComplyWithRFC3261()
        {
            // Arrange
            var username = "caller";
            var targetNumber = "callee";
            var serverHost = "sip.example.com";
            var serverPort = 5060;
            uint sequenceNumber = 1;
            var callId = Guid.NewGuid().ToString();
            var fromTag = "12345";
            var sdpContent = "v=0\r\no=- 123456 654321 IN IP4 192.168.1.100\r\ns=-\r\nc=IN IP4 192.168.1.100\r\nt=0 0\r\nm=audio 5004 RTP/AVP 0 8\r\na=rtpmap:0 PCMU/8000\r\na=rtpmap:8 PCMA/8000\r\na=sendrecv\r\n";

            // Act
            var inviteMessage = _messageFactory.CreateInviteRequest(
                username, targetNumber, serverHost, serverPort, sequenceNumber, callId, fromTag, sdpContent);

            // Assert
            var validationResult = _validator.ValidateMessage(inviteMessage);
            
            Assert.IsFalse(validationResult.HasCriticalErrors, 
                $"INVITE message has critical errors: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            
            // Verify mandatory headers
            Assert.IsTrue(inviteMessage.Contains("Via:"), "Missing Via header");
            Assert.IsTrue(inviteMessage.Contains("From:"), "Missing From header");
            Assert.IsTrue(inviteMessage.Contains("To:"), "Missing To header");
            Assert.IsTrue(inviteMessage.Contains("Call-ID:"), "Missing Call-ID header");
            Assert.IsTrue(inviteMessage.Contains("CSeq:"), "Missing CSeq header");
            Assert.IsTrue(inviteMessage.Contains("Contact:"), "Missing Contact header");
            
            // Verify SDP content and proper Content-Length
            Assert.IsTrue(inviteMessage.Contains("Content-Type: application/sdp"), "Missing Content-Type header");
            var expectedContentLength = System.Text.Encoding.UTF8.GetByteCount(sdpContent);
            Assert.IsTrue(inviteMessage.Contains($"Content-Length: {expectedContentLength}"), "Incorrect Content-Length calculation");
        }

        [TestMethod]
        public void CreateByeRequest_ShouldComplyWithRFC3261()
        {
            // Arrange
            var targetUri = "sip:callee@sip.example.com";
            var callId = "test-call-id";
            var fromTag = "from-tag";
            var toTag = "to-tag";
            uint sequenceNumber = 2;

            // Act
            var byeMessage = _messageFactory.CreateByeRequest(
                targetUri, callId, fromTag, toTag, sequenceNumber);

            // Assert
            var validationResult = _validator.ValidateMessage(byeMessage);
            
            Assert.IsFalse(validationResult.HasCriticalErrors, 
                $"BYE message has critical errors: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            
            // Verify mandatory headers
            Assert.IsTrue(byeMessage.Contains("Via:"), "Missing Via header");
            Assert.IsTrue(byeMessage.Contains("From:"), "Missing From header");
            Assert.IsTrue(byeMessage.Contains("To:"), "Missing To header");
            Assert.IsTrue(byeMessage.Contains("Call-ID:"), "Missing Call-ID header");
            Assert.IsTrue(byeMessage.Contains("CSeq:"), "Missing CSeq header");
            
            // Verify BYE-specific requirements
            Assert.IsTrue(byeMessage.Contains("Content-Length: 0"), "BYE must not contain message body");
            Assert.IsFalse(byeMessage.Contains("Content-Type:"), "BYE should not have Content-Type header");
        }

        [TestMethod]
        public void CreateResponse_ShouldComplyWithRFC3261()
        {
            // Arrange
            var requestMessage = "INVITE sip:callee@example.com SIP/2.0\r\n" +
                               "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                               "From: <sip:caller@example.com>;tag=from-tag\r\n" +
                               "To: <sip:callee@example.com>\r\n" +
                               "Call-ID: test-call-id\r\n" +
                               "CSeq: 1 INVITE\r\n" +
                               "Content-Length: 0\r\n\r\n";

            var statusCode = 200;
            var reasonPhrase = "OK";

            // Act
            var responseMessage = _messageFactory.CreateResponse(
                statusCode, reasonPhrase, requestMessage);

            // Assert
            var validationResult = _validator.ValidateMessage(responseMessage);
            
            Assert.IsFalse(validationResult.HasCriticalErrors, 
                $"Response message has critical errors: {string.Join(", ", validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).Select(e => e.Message))}");
            
            // Verify response line
            Assert.IsTrue(responseMessage.StartsWith("SIP/2.0 200 OK"), "Invalid response status line");
            
            // Verify mandatory response headers copied from request
            Assert.IsTrue(responseMessage.Contains("Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345"), "Via header not copied correctly");
            Assert.IsTrue(responseMessage.Contains("From: <sip:caller@example.com>;tag=from-tag"), "From header not copied correctly");
            Assert.IsTrue(responseMessage.Contains("Call-ID: test-call-id"), "Call-ID header not copied correctly");
            Assert.IsTrue(responseMessage.Contains("CSeq: 1 INVITE"), "CSeq header not copied correctly");
            
            // Verify To tag was added for final response
            Assert.IsTrue(responseMessage.Contains("To: <sip:callee@example.com>;tag="), "To tag not added for final response");
        }

        #endregion

        #region Transaction Management Tests

        [TestMethod]
        public void CreateClientTransaction_INVITE_ShouldCreateICT()
        {
            // Arrange
            var method = "INVITE";
            var requestMessage = "INVITE sip:test@example.com SIP/2.0\r\n" +
                               "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                               "Content-Length: 0\r\n\r\n";

            // Act
            var transaction = _transactionManager.CreateClientTransaction(method, requestMessage);

            // Assert
            Assert.IsNotNull(transaction, "Transaction should be created");
            Assert.IsInstanceOfType(transaction, typeof(InviteClientTransaction), "Should create INVITE Client Transaction");
            Assert.AreEqual("INVITE", transaction.Method, "Transaction method should be INVITE");
            Assert.IsFalse(transaction.IsServerTransaction, "Should be client transaction");
        }

        [TestMethod]
        public void CreateClientTransaction_REGISTER_ShouldCreateNICT()
        {
            // Arrange
            var method = "REGISTER";
            var requestMessage = "REGISTER sip:example.com SIP/2.0\r\n" +
                               "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK54321\r\n" +
                               "Content-Length: 0\r\n\r\n";

            // Act
            var transaction = _transactionManager.CreateClientTransaction(method, requestMessage);

            // Assert
            Assert.IsNotNull(transaction, "Transaction should be created");
            Assert.IsInstanceOfType(transaction, typeof(NonInviteClientTransaction), "Should create Non-INVITE Client Transaction");
            Assert.AreEqual("REGISTER", transaction.Method, "Transaction method should be REGISTER");
            Assert.IsFalse(transaction.IsServerTransaction, "Should be client transaction");
        }

        [TestMethod]
        public void InviteClientTransaction_StateTransitions_ShouldFollowRFC3261()
        {
            // Arrange
            var transaction = new InviteClientTransaction("test-transaction-id");
            var stateChanges = new List<TransactionState>();
            
            transaction.StateChanged += (sender, args) => stateChanges.Add(args.NewState);

            var inviteMessage = "INVITE sip:test@example.com SIP/2.0\r\n" +
                              "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                              "Content-Length: 0\r\n\r\n";

            // Act & Assert
            // Initial state should be Trying
            Assert.AreEqual(TransactionState.Trying, transaction.State);

            // Send INVITE - should transition to Calling
            transaction.SendInvite(inviteMessage);
            Assert.IsTrue(stateChanges.Contains(TransactionState.Calling), "Should transition to Calling state");

            // Process 180 Ringing - should transition to Proceeding
            var ringingResponse = "SIP/2.0 180 Ringing\r\n" +
                                "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                                "From: <sip:caller@example.com>;tag=from-tag\r\n" +
                                "To: <sip:test@example.com>;tag=to-tag\r\n" +
                                "Call-ID: test-call-id\r\n" +
                                "CSeq: 1 INVITE\r\n" +
                                "Content-Length: 0\r\n\r\n";

            transaction.ProcessMessage(ringingResponse);
            Assert.IsTrue(stateChanges.Contains(TransactionState.Proceeding), "Should transition to Proceeding state");

            // Process 200 OK - should transition to Completed then Terminated
            var okResponse = "SIP/2.0 200 OK\r\n" +
                           "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                           "From: <sip:caller@example.com>;tag=from-tag\r\n" +
                           "To: <sip:test@example.com>;tag=to-tag\r\n" +
                           "Call-ID: test-call-id\r\n" +
                           "CSeq: 1 INVITE\r\n" +
                           "Contact: <sip:test@192.168.1.200:5060>\r\n" +
                           "Content-Length: 0\r\n\r\n";

            transaction.ProcessMessage(okResponse);
            Assert.IsTrue(stateChanges.Contains(TransactionState.Completed), "Should transition to Completed state");
            Assert.IsTrue(stateChanges.Contains(TransactionState.Terminated), "Should transition to Terminated state for 2xx response");
        }

        #endregion

        #region Message Validation Tests

        [TestMethod]
        public void ValidateMessage_MissingMandatoryHeaders_ShouldReturnCriticalErrors()
        {
            // Arrange
            var invalidMessage = "INVITE sip:test@example.com SIP/2.0\r\n" +
                               "From: <sip:caller@example.com>\r\n" +
                               "Content-Length: 0\r\n\r\n";

            // Act
            var validationResult = _validator.ValidateMessage(invalidMessage);

            // Assert
            Assert.IsTrue(validationResult.HasCriticalErrors, "Should have critical errors for missing mandatory headers");
            
            var criticalErrors = validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Critical).ToList();
            Assert.IsTrue(criticalErrors.Any(e => e.Message.Contains("Via")), "Should report missing Via header");
            Assert.IsTrue(criticalErrors.Any(e => e.Message.Contains("To")), "Should report missing To header");
            Assert.IsTrue(criticalErrors.Any(e => e.Message.Contains("Call-ID")), "Should report missing Call-ID header");
        }

        [TestMethod]
        public void ValidateMessage_InvalidViaBranch_ShouldReturnWarning()
        {
            // Arrange
            var messageWithInvalidBranch = "INVITE sip:test@example.com SIP/2.0\r\n" +
                                         "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=invalid-branch\r\n" +
                                         "From: <sip:caller@example.com>;tag=from-tag\r\n" +
                                         "To: <sip:test@example.com>\r\n" +
                                         "Call-ID: test-call-id\r\n" +
                                         "CSeq: 1 INVITE\r\n" +
                                         "Max-Forwards: 70\r\n" +
                                         "Content-Length: 0\r\n\r\n";

            // Act
            var validationResult = _validator.ValidateMessage(messageWithInvalidBranch);

            // Assert
            var warnings = validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Warning).ToList();
            Assert.IsTrue(warnings.Any(w => w.Message.Contains("z9hG4bK")), "Should warn about missing RFC 3261 magic cookie");
        }

        [TestMethod]
        public void ValidateMessage_IncorrectContentLength_ShouldReturnMajorError()
        {
            // Arrange
            var sdpContent = "v=0\r\no=- 123456 654321 IN IP4 192.168.1.100\r\ns=-\r\n";
            var incorrectLength = 100; // Intentionally wrong
            var actualLength = System.Text.Encoding.UTF8.GetByteCount(sdpContent);

            var messageWithIncorrectLength = "INVITE sip:test@example.com SIP/2.0\r\n" +
                                           "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                                           "From: <sip:caller@example.com>;tag=from-tag\r\n" +
                                           "To: <sip:test@example.com>\r\n" +
                                           "Call-ID: test-call-id\r\n" +
                                           "CSeq: 1 INVITE\r\n" +
                                           "Max-Forwards: 70\r\n" +
                                           "Content-Type: application/sdp\r\n" +
                                           $"Content-Length: {incorrectLength}\r\n\r\n" +
                                           sdpContent;

            // Act
            var validationResult = _validator.ValidateMessage(messageWithIncorrectLength);

            // Assert
            Assert.IsTrue(validationResult.HasMajorErrors, "Should have major errors for incorrect Content-Length");
            var majorErrors = validationResult.Errors.Where(e => e.Severity == ValidationSeverity.Major).ToList();
            Assert.IsTrue(majorErrors.Any(e => e.Message.Contains("Content-Length")), "Should report Content-Length mismatch");
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public void EndToEndTest_RegisterFlow_ShouldBeRFC3261Compliant()
        {
            // This test simulates a complete REGISTER flow with transaction management
            
            // Arrange
            var registerMessage = _messageFactory.CreateRegisterRequest("testuser", "sip.example.com", 5060, 1);
            
            // Act
            var transaction = _transactionManager.CreateClientTransaction("REGISTER", registerMessage);
            var nict = transaction as NonInviteClientTransaction;
            
            // Send REGISTER
            nict?.SendRequest(registerMessage);
            
            // Simulate 401 Unauthorized response
            var challengeResponse = "SIP/2.0 401 Unauthorized\r\n" +
                                  "Via: SIP/2.0/UDP 192.168.1.100:5060;branch=z9hG4bK12345\r\n" +
                                  "From: <sip:testuser@192.168.1.100>;tag=from-tag\r\n" +
                                  "To: <sip:testuser@sip.example.com>;tag=to-tag\r\n" +
                                  "Call-ID: test-call-id\r\n" +
                                  "CSeq: 1 REGISTER\r\n" +
                                  "WWW-Authenticate: Digest realm=\"example.com\", nonce=\"abc123\"\r\n" +
                                  "Content-Length: 0\r\n\r\n";
            
            transaction.ProcessMessage(challengeResponse);
            
            // Assert
            Assert.AreEqual(TransactionState.Completed, transaction.State, "Transaction should be in Completed state");
            
            // Validate both messages for RFC 3261 compliance
            var registerValidation = _validator.ValidateMessage(registerMessage);
            var responseValidation = _validator.ValidateMessage(challengeResponse);
            
            Assert.IsFalse(registerValidation.HasCriticalErrors, "REGISTER message should be RFC 3261 compliant");
            Assert.IsFalse(responseValidation.HasCriticalErrors, "401 response should be RFC 3261 compliant");
        }

        #endregion
    }
}

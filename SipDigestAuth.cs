using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace WindowsSipPhone
{
    public static class SipDigestAuth
    {        public static string CalculateResponse(string username, string password, string realm, 
            string nonce, string method, string uri, string nc = "00000001", string? cnonce = null)
        {
            if (string.IsNullOrEmpty(cnonce))
            {
                cnonce = Guid.NewGuid().ToString("N")[..8];
            }

            // Calculate HA1 = MD5(username:realm:password)
            var ha1Input = $"{username}:{realm}:{password}";
            var ha1 = CalculateMD5Hash(ha1Input);

            // Calculate HA2 = MD5(method:uri)
            var ha2Input = $"{method}:{uri}";
            var ha2 = CalculateMD5Hash(ha2Input);

            // Calculate response = MD5(HA1:nonce:nc:cnonce:qop:HA2)
            var responseInput = $"{ha1}:{nonce}:{nc}:{cnonce}:auth:{ha2}";
            var response = CalculateMD5Hash(responseInput);

            return response;
        }

        public static Dictionary<string, string> ParseAuthHeader(string authHeader)
        {
            var authParams = new Dictionary<string, string>();
            
            // Remove "Digest " prefix
            var digestParams = authHeader.Replace("Digest ", "").Replace("WWW-Authenticate: ", "");
            
            // Parse key=value pairs
            var regex = new Regex(@"(\w+)=""?([^"",]+)""?");
            var matches = regex.Matches(digestParams);
            
            foreach (Match match in matches)
            {
                if (match.Groups.Count == 3)
                {
                    var key = match.Groups[1].Value;
                    var value = match.Groups[2].Value.Trim('"');
                    authParams[key] = value;
                }
            }
            
            return authParams;
        }

        public static string CreateAuthorizationHeader(string username, string password, 
            string method, string uri, Dictionary<string, string> challengeParams)
        {
            if (!challengeParams.ContainsKey("realm") || !challengeParams.ContainsKey("nonce"))
            {
                throw new ArgumentException("Missing required authentication parameters");
            }

            var realm = challengeParams["realm"];
            var nonce = challengeParams["nonce"];
            var nc = "00000001";
            var cnonce = Guid.NewGuid().ToString("N")[..8];
            var qop = challengeParams.ContainsKey("qop") ? challengeParams["qop"] : "auth";

            var response = CalculateResponse(username, password, realm, nonce, method, uri, nc, cnonce);

            var authHeader = $"Digest username=\"{username}\", " +
                           $"realm=\"{realm}\", " +
                           $"nonce=\"{nonce}\", " +
                           $"uri=\"{uri}\", " +
                           $"response=\"{response}\", " +
                           $"nc={nc}, " +
                           $"cnonce=\"{cnonce}\", " +
                           $"qop={qop}";

            return authHeader;
        }

        private static string CalculateMD5Hash(string input)
        {
            using var md5 = MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }
}

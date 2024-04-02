using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Authentication;
using Autodesk.Authentication.Model;
using Autodesk.SDKManager;

namespace bucket.manager.wpf.APSUtils
{
    internal class Authentication : IAuthClient
    {
        /// <summary>
        /// Get token with id, secret and scopes
        /// </summary>
        /// <param name="id">APS Client ID</param>
        /// <param name="secret">APS Client Secret</param>
        /// <param name="scopes">Token scopes</param>
        /// <returns></returns>
        public static async Task<TwoLeggedToken> GetToken(string id, string secret, List<Scopes> scopes)
        {
            var client = new AuthenticationClient(SdkManagerHelper.Instance);
            _lastId = id;
            _lastSecret = secret;
            _lastScopes = string.Join(" ", scopes);
            return await client.GetTwoLeggedTokenAsync(id, secret, scopes);
        }

        private static string? _lastId;
        private static string? _lastSecret;
        private string? _currentScope = _lastScopes;
        private static string? _lastScopes;

        /// <summary>
        /// Implementing the IAuthClient interface
        /// </summary>
        /// <param name="scope">Scopes needed</param>
        /// <returns></returns>
        public string GetAccessToken(string scope)
        {
            _currentScope = scope;

            // If not applicable, use the environment variables
            _lastId ??= Environment.GetEnvironmentVariable("APS_CLIENT_ID");
            _lastSecret ??= Environment.GetEnvironmentVariable("APS_CLIENT_SECRET");

            // Split the scopes and get the token
            var scopes = _currentScope.Split(' ').Select(Enum.Parse<Scopes>).ToList();
            var task = GetToken(_lastId!, _lastSecret!, scopes);
            return task.GetAwaiter().GetResult().AccessToken;
        }

        /// <summary>
        /// Get the updated access token
        /// </summary>
        /// <returns></returns>
        public string GetUpdatedAccessToken()
        {
            return GetAccessToken(_currentScope?? "data:write data:read");
        }
    }

    
}

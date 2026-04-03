using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace USER.TokenGenerator
{
    public static class RefreshTokenGenerator
    {


        public static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64]; // 512 bits
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
             
            return WebEncoders.Base64UrlEncode(randomNumber);
        }
    }
}
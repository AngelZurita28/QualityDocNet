using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace QualityDoc.Helpers
{
    public static class PasswordHelper
    {
        // Sal estática para mantener compatibilidad sin cambiar la BD
        private static readonly byte[] StaticSalt = new byte[] { 0x48, 0x65, 0x6c, 0x6c, 0x6f, 0x57, 0x6f, 0x72, 0x6c, 0x64 };

        public static string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return string.Empty;

            byte[] hashed = KeyDerivation.Pbkdf2(
                password: password,
                salt: StaticSalt,
                prf: KeyDerivationPrf.HMACSHA512,
                iterationCount: 10000,
                numBytesRequested: 256 / 8);

            return Convert.ToBase64String(hashed);
        }
    }
}


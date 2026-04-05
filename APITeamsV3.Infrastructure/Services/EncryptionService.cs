using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using APITeamsV3.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace APITeamsV3.Infrastructure.Services
{
    public class EncryptionService : IEncryptionService
    {
        private const string VersionPrefix = "v2:";
        private const string DevelopmentFallbackKey = "b14ca5898a4e4133bbce2ea2315a1916";
        private readonly byte[] _key;
        private readonly List<byte[]> _decryptKeyCandidates;
        private readonly ILogger<EncryptionService> _logger;
        private int _preferredDecryptKeyIndex;

        public EncryptionService(IConfiguration configuration, IHostEnvironment environment, ILogger<EncryptionService> logger)
        {
            _logger = logger;

            var configuredKey =
                configuration["EncryptionKey"] ??
                Environment.GetEnvironmentVariable("APITEAMSV3_ENCRYPTION_KEY");

            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException("EncryptionKey configuration is required outside Development.");
                }

                configuredKey = DevelopmentFallbackKey;
                _logger.LogWarning("Using development fallback encryption key. Configure EncryptionKey before deploying.");
            }

            _key = NormalizeKey(configuredKey);
            _decryptKeyCandidates = BuildDecryptKeyCandidates(configuredKey, _key);
            _preferredDecryptKeyIndex = 0;
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            byte[] cipherBytes;
            byte[] iv;

            using (var aes = Aes.Create())
            {
                aes.Key = _key;
                aes.GenerateIV();
                iv = aes.IV;

                var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (var memoryStream = new MemoryStream())
                {
                    using (var cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (var streamWriter = new StreamWriter((Stream)cryptoStream))
                        {
                            streamWriter.Write(plainText);
                        }

                        cipherBytes = memoryStream.ToArray();
                    }
                }
            }

            var payload = new byte[iv.Length + cipherBytes.Length];
            Buffer.BlockCopy(iv, 0, payload, 0, iv.Length);
            Buffer.BlockCopy(cipherBytes, 0, payload, iv.Length, cipherBytes.Length);

            return VersionPrefix + Convert.ToBase64String(payload);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            CryptographicException? lastException = null;

            for (var attempt = 0; attempt < _decryptKeyCandidates.Count; attempt++)
            {
                var candidateIndex = (_preferredDecryptKeyIndex + attempt) % _decryptKeyCandidates.Count;
                var candidateKey = _decryptKeyCandidates[candidateIndex];

                try
                {
                    var decrypted = DecryptWithKey(cipherText, candidateKey);
                    if (candidateIndex != _preferredDecryptKeyIndex)
                    {
                        _preferredDecryptKeyIndex = candidateIndex;
                    }

                    return decrypted;
                }
                catch (CryptographicException ex)
                {
                    lastException = ex;
                }
            }

            throw lastException ?? new CryptographicException("Unable to decrypt the payload with the configured key candidates.");
        }

        private string DecryptWithKey(string cipherText, byte[] key)
        {
            if (cipherText.StartsWith(VersionPrefix, StringComparison.Ordinal))
            {
                var payload = Convert.FromBase64String(cipherText[VersionPrefix.Length..]);
                if (payload.Length < 17)
                {
                    throw new CryptographicException("Encrypted payload is invalid.");
                }

                var iv = new byte[16];
                var buffer = new byte[payload.Length - iv.Length];
                Buffer.BlockCopy(payload, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(payload, iv.Length, buffer, 0, buffer.Length);

                return DecryptInternal(buffer, iv, key);
            }

            return DecryptInternal(Convert.FromBase64String(cipherText), new byte[16], key);
        }

        private string DecryptInternal(byte[] buffer, byte[] iv, byte[] key)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                using (var memoryStream = new MemoryStream(buffer))
                using (var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                using (var streamReader = new StreamReader(cryptoStream))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }

        private static byte[] NormalizeKey(string configuredKey)
        {
            if (string.IsNullOrWhiteSpace(configuredKey))
            {
                throw new InvalidOperationException("EncryptionKey cannot be empty.");
            }

            if (TryDecodeHex(configuredKey, out var hexBytes))
            {
                if (hexBytes.Length is 16 or 24 or 32)
                {
                    return hexBytes;
                }
            }

            if (TryDecodeBase64(configuredKey, out var base64Bytes))
            {
                if (base64Bytes.Length is 16 or 24 or 32)
                {
                    return base64Bytes;
                }
            }

            var utf8Bytes = Encoding.UTF8.GetBytes(configuredKey);
            if (utf8Bytes.Length is 16 or 24 or 32)
            {
                return utf8Bytes;
            }

            throw new InvalidOperationException("EncryptionKey must be 16, 24, or 32 bytes; or a hex/base64 encoding of one of those lengths.");
        }

        private static List<byte[]> BuildDecryptKeyCandidates(string configuredKey, byte[] primaryKey)
        {
            var candidates = new List<byte[]> { primaryKey };

            if (configuredKey.Length == 32)
            {
                byte[]? alternateKey = null;

                if (TryDecodeHex(configuredKey, out var hexBytes) && hexBytes.Length == 16 && primaryKey.Length == 16)
                {
                    alternateKey = Encoding.UTF8.GetBytes(configuredKey);
                }
                else
                {
                    var utf8Bytes = Encoding.UTF8.GetBytes(configuredKey);
                    if (primaryKey.Length == 32 && utf8Bytes.Length == 32 && TryDecodeHex(configuredKey, out hexBytes) && hexBytes.Length == 16)
                    {
                        alternateKey = hexBytes;
                    }
                }

                if (alternateKey != null && !candidates.Exists(candidate => candidate.AsSpan().SequenceEqual(alternateKey)))
                {
                    candidates.Add(alternateKey);
                }
            }

            return candidates;
        }

        private static bool TryDecodeHex(string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            if (value.Length % 2 != 0)
            {
                return false;
            }

            try
            {
                bytes = Convert.FromHexString(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        private static bool TryDecodeBase64(string value, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();

            try
            {
                bytes = Convert.FromBase64String(value);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}


using System.Security.Cryptography;

namespace SecureFileExchange.Common;

public interface IEncryptionService
{
    Task<string> EncryptAsync(string plainText);
    Task<string> DecryptAsync(string encryptedText);
    Task<byte[]> EncryptBytesAsync(byte[] data);
    Task<byte[]> DecryptBytesAsync(byte[] encryptedData);
}

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    
    public AesEncryptionService(string base64Key)
    {
        _key = Convert.FromBase64String(base64Key);
    }

    public async Task<string> EncryptAsync(string plainText)
    {
        var data = System.Text.Encoding.UTF8.GetBytes(plainText);
        var encrypted = await EncryptBytesAsync(data);
        return Convert.ToBase64String(encrypted);
    }

    public async Task<string> DecryptAsync(string encryptedText)
    {
        var data = Convert.FromBase64String(encryptedText);
        var decrypted = await DecryptBytesAsync(data);
        return System.Text.Encoding.UTF8.GetString(decrypted);
    }

    public async Task<byte[]> EncryptBytesAsync(byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        using var ms = new MemoryStream();
        
        // Prepend IV to the encrypted data
        await ms.WriteAsync(aes.IV);
        
        using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
        await cs.WriteAsync(data);
        await cs.FlushFinalBlockAsync();
        
        return ms.ToArray();
    }

    public async Task<byte[]> DecryptBytesAsync(byte[] encryptedData)
    {
        using var aes = Aes.Create();
        aes.Key = _key;

        // Extract IV from the beginning of the encrypted data
        var iv = new byte[aes.BlockSize / 8];
        Array.Copy(encryptedData, 0, iv, 0, iv.Length);
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        using var ms = new MemoryStream(encryptedData, iv.Length, encryptedData.Length - iv.Length);
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var output = new MemoryStream();
        
        await cs.CopyToAsync(output);
        return output.ToArray();
    }
}

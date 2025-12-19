using System.Security.Cryptography;
using Interfaces;

namespace Services;

public class AesEncryptionService: IEncryptionService
{
    private readonly byte[] _key;

    // A chave deve ter 32 bytes para AES-256
    public AesEncryptionService(string base64Key)
    {
        _key = Convert.FromBase64String(base64Key);
    }

    public byte[] Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV(); // Cria um IV único para cada mensagem

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();

        // 1. Escreve o IV no início do stream (o destino lerá os primeiros 16 bytes)
        ms.Write(aes.IV, 0, aes.IV.Length);

        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
        {
            sw.Write(plainText);
        }

        return ms.ToArray();
    }
}
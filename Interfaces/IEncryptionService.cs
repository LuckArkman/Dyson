namespace Interfaces;

public interface IEncryptionService
{
    byte[] Encrypt(string plainText);
}
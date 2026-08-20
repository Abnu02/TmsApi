namespace TmsApi.Infrastructure.Services;

public class CryptoDemoService
{
    public string HashPassword(string plainText) =>
        BCrypt.Net.BCrypt.HashPassword(plainText, workFactor: 12);

    public bool VerifyPassword(string plainText, string hashedDbPassword) =>
        BCrypt.Net.BCrypt.Verify(plainText, hashedDbPassword);
}
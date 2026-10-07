using System.Security.Cryptography;
using System.Text;
using AutonomousAudit.Application.Data;
using Microsoft.Extensions.Options;

namespace AutonomousAudit.Infrastructure;

public sealed class AesSegredoProtector : ISegredoProtector
{
    public const string Prefixo = "aa1.";
    private readonly byte[] _chave;

    public AesSegredoProtector(IOptions<MailEncryptionOptions> options)
    {
        var secreta = options.Value.EncryptionKey?.Trim() ?? string.Empty;
        if (secreta.Length < 32)
        {
            throw new InvalidOperationException(
                "Mail:EncryptionKey nao configurada ou com menos de 32 caracteres. Informe Mail:EncryptionKey, MAIL_ENCRYPTION_KEY ou Jwt:SigningKey.");
        }

        _chave = SHA256.HashData(Encoding.UTF8.GetBytes(secreta));
    }

    public string Cifrar(string textoClaro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(textoClaro);

        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(textoClaro);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_chave, 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        var packed = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, packed, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, packed, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, packed, nonce.Length + cipher.Length, tag.Length);
        return Prefixo + Convert.ToBase64String(packed);
    }

    public string Decifrar(string textoArmazenado)
    {
        if (string.IsNullOrWhiteSpace(textoArmazenado))
        {
            throw new InvalidOperationException("Senha da conta SMTP ausente.");
        }

        if (!textoArmazenado.StartsWith(Prefixo, StringComparison.Ordinal))
        {
            return textoArmazenado;
        }

        byte[] packed;
        try
        {
            packed = Convert.FromBase64String(textoArmazenado[Prefixo.Length..]);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Senha da conta SMTP nao esta em formato cifrado valido.", ex);
        }

        if (packed.Length <= 28)
        {
            throw new InvalidOperationException("Senha da conta SMTP cifrada esta incompleta.");
        }

        var nonce = packed.AsSpan(0, 12);
        var tag = packed.AsSpan(packed.Length - 16, 16);
        var cipher = packed.AsSpan(12, packed.Length - 28);
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(_chave, 16);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Nao foi possivel abrir a senha da conta SMTP. Verifique Mail:EncryptionKey.", ex);
        }

        return Encoding.UTF8.GetString(plain);
    }
}

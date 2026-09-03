using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SignumSample.Library;

/// <summary>Result of inspecting the Authenticode signature of a file.</summary>
/// <param name="FilePath">The inspected file.</param>
/// <param name="IsSigned">True when the file carries an embedded Authenticode certificate.</param>
/// <param name="Subject">Subject of the signing certificate, when present.</param>
/// <param name="Issuer">Issuer of the signing certificate, when present.</param>
/// <param name="Thumbprint">SHA-1 thumbprint of the signing certificate, when present.</param>
/// <param name="NotAfter">Expiry date of the signing certificate, when present.</param>
/// <param name="Error">Why the signature could not be read, when applicable.</param>
public sealed record SignatureInfo(
    string FilePath,
    bool IsSigned,
    string? Subject = null,
    string? Issuer = null,
    string? Thumbprint = null,
    DateTime? NotAfter = null,
    string? Error = null);

/// <summary>
/// Reads the Authenticode certificate embedded in a PE file (.exe / .dll).
/// </summary>
/// <remarks>
/// IMPORTANT: this only extracts the certificate from the file. It does NOT validate
/// the trust chain, revocation status, timestamp, or that the digest matches the file
/// contents. It is a diagnostic aid for this sample project; real validation is done
/// by <c>signtool verify /pa</c> in the CI workflow.
/// </remarks>
public static class AuthenticodeInspector
{
    public static SignatureInfo Inspect(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new SignatureInfo(filePath, IsSigned: false, Error: "File does not exist.");
        }

        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            return new SignatureInfo(
                filePath,
                IsSigned: true,
                Subject: certificate.Subject,
                Issuer: certificate.Issuer,
                Thumbprint: certificate.Thumbprint,
                NotAfter: certificate.NotAfter);
        }
        catch (CryptographicException ex)
        {
            // CreateFromSignedFile throws here when the file carries no signature.
            // The HRESULT is reported instead of ex.Message because the latter is
            // localised by the OS, which makes CI logs inconsistent across runners.
            return new SignatureInfo(
                filePath,
                IsSigned: false,
                Error: $"No embedded Authenticode certificate could be read (HRESULT 0x{ex.HResult:X8}).");
        }
    }
}

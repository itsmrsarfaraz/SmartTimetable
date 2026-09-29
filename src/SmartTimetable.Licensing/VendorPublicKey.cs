namespace SmartTimetable.Licensing;

/// <summary>
/// The vendor's PUBLIC key, embedded in every shipped build.
/// Generate the pair with:  SmartTimetable.LicenseGenerator keygen
/// then paste the base64 line it prints into <see cref="SpkiBase64"/>.
/// The private key never goes into this repository.
/// </summary>
public static class VendorPublicKey
{
    public const string SpkiBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEaHDM2aP4cCPmiX66k+EMgYUeniEg6k1QIcvfppBGY5wtx8FrVpYDvbNYj/5odFJX4zeXJCdTgdWrYB3jcYGexw==";

    public static bool IsConfigured => SpkiBase64.Length > 0;

    public static byte[] GetBytes() => IsConfigured ? Convert.FromBase64String(SpkiBase64) : [];
}

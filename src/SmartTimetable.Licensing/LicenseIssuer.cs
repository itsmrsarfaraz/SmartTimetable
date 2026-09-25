using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartTimetable.Licensing;

public sealed class LicenseException(string message) : Exception(message);

public sealed record LicenseRequest(
    string RequestCode,
    string Customer,
    string Edition = "Standard",
    DateTimeOffset? ExpiresUtc = null,
    int? MinMatches = null);

public sealed record IssuedLicense(LicensePayload Payload, string Token);

/// <summary>Vendor-side: turns a customer's request code into a signed license. Needs the private key.</summary>
public static class LicenseIssuer
{
    /// <summary>Fewer identifiable slots than this makes machine-locking meaningless.</summary>
    public const int MinimumSlotsToIssue = 2;

    /// <summary>Tolerates replacing a disk or reinstalling Windows, but not moving to another PC.</summary>
    public static int DefaultMinMatches(int presentSlots) => presentSlots switch
    {
        >= 4 => 3,
        3 => 2,
        _ => presentSlots,
    };

    public static IssuedLicense Issue(ECDsa privateKey, LicenseRequest request, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Customer))
            throw new LicenseException("Customer name is required.");

        if (!RequestCode.TryDecode(request.RequestCode, out var fingerprint))
            throw new LicenseException("The request code is invalid or was mistyped.");

        if (fingerprint.PresentCount < MinimumSlotsToIssue)
            throw new LicenseException(
                $"This machine exposes only {fingerprint.PresentCount} usable hardware identifier(s); " +
                $"at least {MinimumSlotsToIssue} are needed to lock a license safely.");

        var minMatches = request.MinMatches ?? DefaultMinMatches(fingerprint.PresentCount);
        if (minMatches < 1 || minMatches > fingerprint.PresentCount)
            throw new LicenseException($"Minimum matches must be between 1 and {fingerprint.PresentCount}.");

        var now = (time ?? TimeProvider.System).GetUtcNow();
        now = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
        if (request.ExpiresUtc is { } expires && expires <= now)
            throw new LicenseException("The expiry date is in the past.");

        var payload = new LicensePayload
        {
            LicenseId = Guid.NewGuid(),
            Customer = request.Customer.Trim(),
            Edition = string.IsNullOrWhiteSpace(request.Edition) ? "Standard" : request.Edition.Trim(),
            IssuedUtc = now,
            ExpiresUtc = request.ExpiresUtc,
            Machine = Base64Url.EncodeToString(fingerprint.ToBytes()),
            MinMatches = minMatches,
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, LicenseJson.Options);
        var signature = privateKey.SignData(bytes, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return new IssuedLicense(payload, LicenseToken.Format(bytes, signature));
    }
}

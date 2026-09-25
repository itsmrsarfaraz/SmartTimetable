using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;

namespace SmartTimetable.Licensing;

public enum LicenseStatus
{
    Valid,
    NotFound,
    Malformed,
    InvalidSignature,
    MachineMismatch,
    Expired,
    NotConfigured,
}

public sealed record LicenseValidationResult(LicenseStatus Status, LicensePayload? Payload = null, string Message = "")
{
    public bool IsValid => Status == LicenseStatus.Valid;
}

/// <summary>App-side: verifies signature, expiry and that this computer matches the licensed machine.</summary>
public sealed class LicenseValidator
{
    private readonly byte[] _publicKeySpki;
    private readonly IMachineFingerprintProvider _fingerprints;
    private readonly TimeProvider _time;

    public LicenseValidator(byte[] publicKeySpki, IMachineFingerprintProvider fingerprints, TimeProvider? time = null)
    {
        _publicKeySpki = publicKeySpki ?? throw new ArgumentNullException(nameof(publicKeySpki));
        _fingerprints = fingerprints ?? throw new ArgumentNullException(nameof(fingerprints));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Checks signature and format only - no machine or expiry check. Used by vendor tooling.</summary>
    public LicenseValidationResult VerifySignatureOnly(string? token) => ReadAndVerify(token);

    public LicenseValidationResult Validate(string? token)
    {
        var read = ReadAndVerify(token);
        if (!read.IsValid) return read;
        var payload = read.Payload!;

        if (!TryReadMachine(payload, out var licensed))
            return new(LicenseStatus.Malformed, null, "The license contains invalid machine data.");

        var required = Math.Clamp(payload.MinMatches, 1, licensed.PresentCount);
        var matches = licensed.CountMatchesWith(_fingerprints.GetFingerprint());
        if (matches < required)
            return new(LicenseStatus.MachineMismatch, payload,
                "This license was issued for a different computer. Send a new request code to get a license for this one.");

        if (payload.ExpiresUtc is { } expires && _time.GetUtcNow() > expires)
            return new(LicenseStatus.Expired, payload, $"This license expired on {expires:yyyy-MM-dd}.");

        return new(LicenseStatus.Valid, payload, "License is valid.");
    }

    private LicenseValidationResult ReadAndVerify(string? token)
    {
        if (LicenseToken.Normalize(token) is null)
            return new(LicenseStatus.NotFound, null, "No license has been imported yet.");

        if (_publicKeySpki.Length == 0)
            return new(LicenseStatus.NotConfigured, null, "This build has no vendor public key configured.");

        if (!LicenseToken.TryParse(token, out var payloadBytes, out var signature))
            return new(LicenseStatus.Malformed, null, "The license text is not in a valid format.");

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(_publicKeySpki, out _);
        }
        catch (CryptographicException)
        {
            return new(LicenseStatus.NotConfigured, null, "The vendor public key embedded in this build is invalid.");
        }

        bool signatureOk;
        try
        {
            signatureOk = ecdsa.VerifyData(payloadBytes, signature, HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException)
        {
            signatureOk = false;
        }
        if (!signatureOk)
            return new(LicenseStatus.InvalidSignature, null, "The license signature is not valid.");

        LicensePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadBytes, LicenseJson.Options);
        }
        catch (JsonException)
        {
            payload = null;
        }
        if (payload is null || payload.Version != 1)
            return new(LicenseStatus.Malformed, null, "The license content is not supported by this version.");

        return new(LicenseStatus.Valid, payload, "Signature is valid.");
    }

    private static bool TryReadMachine(LicensePayload payload, out MachineFingerprint fingerprint)
    {
        fingerprint = null!;
        try
        {
            return MachineFingerprint.TryFromBytes(Base64Url.DecodeFromChars(payload.Machine), out fingerprint!);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

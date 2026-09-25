namespace SmartTimetable.Licensing;

/// <summary>Persists the imported license under the current Windows user's local app data.</summary>
public sealed class LicenseStore(string filePath)
{
    public string FilePath { get; } = filePath;

    public static LicenseStore ForCurrentUser() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartTimetable", "license.lic"));

    public string? Load()
    {
        try { return File.Exists(FilePath) ? File.ReadAllText(FilePath) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    /// <summary>Atomic write: a crash mid-save can never leave a half-written license.</summary>
    public void Save(string token)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, LicenseToken.ToArmored(token));
        File.Move(temp, FilePath, overwrite: true);
    }
}

/// <summary>The single entry point the desktop app uses for activation and start-up checks.</summary>
public sealed class LicenseService(
    LicenseValidator validator,
    IMachineFingerprintProvider fingerprints,
    LicenseStore store)
{
    /// <summary>Formatted request code to show the customer (they email/WhatsApp it to the vendor).</summary>
    public string GetRequestCode() => RequestCode.Encode(fingerprints.GetFingerprint());

    /// <summary>Validates the stored license; call at every start-up.</summary>
    public LicenseValidationResult CheckInstalledLicense() => validator.Validate(store.Load());

    /// <summary>Accepts pasted license text or a path to a .lic file. Stores it only if valid for this PC.</summary>
    public LicenseValidationResult Import(string tokenOrFilePath)
    {
        var input = (tokenOrFilePath ?? string.Empty).Trim().Trim('"');

        string text;
        try { text = File.Exists(input) ? File.ReadAllText(input) : input; }
        catch (IOException) { text = input; }

        var result = validator.Validate(text);
        if (result.IsValid)
            store.Save(LicenseToken.Normalize(text)!);
        return result;
    }
}

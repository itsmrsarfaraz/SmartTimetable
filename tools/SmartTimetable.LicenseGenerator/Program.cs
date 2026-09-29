using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SmartTimetable.Licensing;

// Vendor-only tool. NEVER ship this to customers and NEVER commit vendor_private.pem.
return Cli.Run(args);

internal static class Cli
{
    private const string PassphraseEnv = "STT_KEY_PASSPHRASE";

    public static int Run(string[] args)
    {
        if (args.Length == 0) return Usage();

        var command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "keygen" => KeyGen(options),
                "decode" => Decode(options),
                "issue" => Issue(options),
                "verify" => Verify(options),
                _ => Usage(),
            };
        }
        catch (LicenseException ex)
        {
            return Fail(ex.Message);
        }
        catch (CryptographicException)
        {
            return Fail("Could not read the private key - wrong passphrase or damaged file.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or FormatException)
        {
            return Fail(ex.Message);
        }
    }

    // ---- keygen -----------------------------------------------------------------------------

    private static int KeyGen(Dictionary<string, string> o)
    {
        var folder = o.GetValueOrDefault("out", ".");
        Directory.CreateDirectory(folder);
        var privatePath = Path.Combine(folder, "vendor_private.pem");
        var publicPath = Path.Combine(folder, "vendor_public.txt");
        if (File.Exists(privatePath) || File.Exists(publicPath))
            return Fail("Key files already exist in that folder. Refusing to overwrite - overwriting the private key would make every previously issued license unverifiable for future builds.");

        var passphrase = Environment.GetEnvironmentVariable(PassphraseEnv);
        if (string.IsNullOrEmpty(passphrase))
        {
            passphrase = ReadSecret("New passphrase for the private key (min 12 chars): ");
            if (ReadSecret("Repeat passphrase: ") != passphrase) return Fail("Passphrases do not match.");
        }
        if (passphrase.Length < 12) return Fail("Passphrase must be at least 12 characters.");

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var pem = key.ExportEncryptedPkcs8PrivateKeyPem(passphrase,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 600_000));
        var publicKey = Convert.ToBase64String(key.ExportSubjectPublicKeyInfo());

        File.WriteAllText(privatePath, pem);
        File.WriteAllText(publicPath, publicKey);

        Console.WriteLine($"Private key (encrypted): {Path.GetFullPath(privatePath)}");
        Console.WriteLine($"Public key file:         {Path.GetFullPath(publicPath)}");
        Console.WriteLine();
        Console.WriteLine("1) BACK UP the private key AND passphrase somewhere offline (USB in a safe, password manager).");
        Console.WriteLine("   If you lose either, you can never issue licenses for builds that contain this public key.");
        Console.WriteLine("2) Paste this into src/SmartTimetable.Licensing/VendorPublicKey.cs, then rebuild the app:");
        Console.WriteLine();
        Console.WriteLine($"    public const string SpkiBase64 = \"{publicKey}\";");
        return 0;
    }

    // ---- decode -----------------------------------------------------------------------------

    private static int Decode(Dictionary<string, string> o)
    {
        var code = Require(o, "request");
        if (!RequestCode.TryDecode(code, out var fp))
            return Fail("The request code is invalid or was mistyped.");

        Console.WriteLine($"Valid request code. Hardware identifiers present: {fp.PresentCount} of {MachineFingerprint.SlotCount}");
        for (var i = 0; i < MachineFingerprint.SlotCount; i++)
        {
            var state = ((fp.PresentMask >> i) & 1) == 1 ? "present" : "-";
            Console.WriteLine($"  {(HardwareSlot)i,-12} {state}");
        }
        Console.WriteLine($"Default minimum matches if issued: {LicenseIssuer.DefaultMinMatches(fp.PresentCount)}");
        return 0;
    }

    // ---- issue ------------------------------------------------------------------------------

    private static int Issue(Dictionary<string, string> o)
    {
        var request = Require(o, "request");
        var customer = Require(o, "customer");
        var edition = o.GetValueOrDefault("edition", "Standard");
        // Default to the vendor's Downloads folder (like CraftingPOS) so the issued key is
        // easy to find and send; --out overrides. Falls back to the current directory.
        var folder = o.TryGetValue("out", out var outFolder) && !string.IsNullOrWhiteSpace(outFolder)
            ? outFolder
            : DownloadsFolder();
        var keyPath = o.GetValueOrDefault("key", "vendor_private.pem");

        DateTimeOffset? expires = null;
        if (o.TryGetValue("expires", out var expiresText))
        {
            if (!DateTime.TryParseExact(expiresText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d))
                return Fail("--expires must look like 2027-12-31.");
            expires = new DateTimeOffset(d, TimeSpan.Zero);
        }

        int? minMatches = null;
        if (o.TryGetValue("min-matches", out var minText))
        {
            if (!int.TryParse(minText, out var m)) return Fail("--min-matches must be a number.");
            minMatches = m;
        }

        if (!File.Exists(keyPath)) return Fail($"Private key not found: {keyPath}");
        var passphrase = Environment.GetEnvironmentVariable(PassphraseEnv);
        if (string.IsNullOrEmpty(passphrase)) passphrase = ReadSecret("Private key passphrase: ");

        using var key = ECDsa.Create();
        key.ImportFromEncryptedPem(File.ReadAllText(keyPath), passphrase);

        var issued = LicenseIssuer.Issue(key, new LicenseRequest(request, customer, edition, expires, minMatches));

        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, $"SmartTimetable-{Slug(customer)}-{issued.Payload.LicenseId.ToString("N")[..8]}.lic");
        File.WriteAllText(file, LicenseToken.ToArmored(issued.Token));
        AppendLog(Path.Combine(folder, "issued_licenses.csv"), issued, request);

        Console.WriteLine($"License issued for '{issued.Payload.Customer}' ({(expires is null ? "lifetime" : "expires " + expires.Value.ToString("yyyy-MM-dd"))}).");
        Console.WriteLine($"Saved to: {Path.GetFullPath(file)}");
        Console.WriteLine("Send that .lic file to the college; they import it on the Activation screen to unlock the app on that PC.");
        Console.WriteLine();
        Console.WriteLine("Or send this single line instead (they paste it into the app):");
        Console.WriteLine(issued.Token);
        return 0;
    }

    // ---- verify -----------------------------------------------------------------------------

    private static int Verify(Dictionary<string, string> o)
    {
        var licensePath = Require(o, "license");
        var publicKeyPath = o.GetValueOrDefault("public-key", "vendor_public.txt");
        if (!File.Exists(licensePath)) return Fail($"License file not found: {licensePath}");
        if (!File.Exists(publicKeyPath)) return Fail($"Public key file not found: {publicKeyPath}");

        var validator = new LicenseValidator(
            Convert.FromBase64String(File.ReadAllText(publicKeyPath).Trim()),
            new NoMachine());
        var result = validator.VerifySignatureOnly(File.ReadAllText(licensePath));

        Console.WriteLine($"Signature check: {result.Status} - {result.Message}");
        if (result.Payload is { } p)
        {
            Console.WriteLine($"  Customer:  {p.Customer}");
            Console.WriteLine($"  Edition:   {p.Edition}");
            Console.WriteLine($"  Issued:    {p.IssuedUtc:yyyy-MM-dd HH:mm} UTC");
            Console.WriteLine($"  Expires:   {(p.ExpiresUtc is null ? "never (lifetime)" : p.ExpiresUtc.Value.ToString("yyyy-MM-dd"))}");
            Console.WriteLine($"  License ID {p.LicenseId}");
        }
        return result.IsValid ? 0 : 1;
    }

    private sealed class NoMachine : IMachineFingerprintProvider
    {
        public MachineFingerprint GetFingerprint() => new(new byte[]?[MachineFingerprint.SlotCount]);
    }

    // ---- helpers ----------------------------------------------------------------------------

    private static void AppendLog(string path, IssuedLicense issued, string requestCode)
    {
        if (!File.Exists(path))
            File.WriteAllText(path, "licenseId,customer,edition,issuedUtc,expiresUtc,minMatches,requestCode\n");

        static string Csv(string s) => "\"" + s.Replace("\"", "\"\"") + "\"";
        var p = issued.Payload;
        File.AppendAllText(path, string.Join(',',
            p.LicenseId, Csv(p.Customer), Csv(p.Edition), p.IssuedUtc.ToString("o"),
            p.ExpiresUtc?.ToString("o") ?? "", p.MinMatches, Csv(requestCode)) + "\n");
    }

    private static string Slug(string s)
    {
        var sb = new StringBuilder();
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) ? c : '-');
        return sb.ToString().Trim('-').Replace("--", "-");
    }

    // Resolves the current user's Downloads folder. Windows has no dedicated Environment
    // special folder for Downloads, so build it from the user profile; if that is not
    // available (or the folder cannot be reached) fall back to the current directory.
    private static string DownloadsFolder()
    {
        try
        {
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrEmpty(profile))
                profile = Environment.GetEnvironmentVariable("USERPROFILE") ?? "";
            if (!string.IsNullOrEmpty(profile))
            {
                var downloads = Path.Combine(profile, "Downloads");
                if (Directory.Exists(downloads)) return downloads;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // fall through to current directory
        }
        return ".";
    }

    private static string Require(Dictionary<string, string> o, string name) =>
        o.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v)
            ? v
            : throw new ArgumentException($"Missing required option --{name}");

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var name = args[i][2..];
            map[name] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
        }
        return map;
    }

    private static string ReadSecret(string prompt)
    {
        Console.Error.Write(prompt);
        if (Console.IsInputRedirected) return Console.ReadLine() ?? "";

        var sb = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); return sb.ToString(); }
            if (key.Key == ConsoleKey.Backspace) { if (sb.Length > 0) sb.Length--; continue; }
            if (!char.IsControl(key.KeyChar)) sb.Append(key.KeyChar);
        }
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine("ERROR: " + message);
        return 1;
    }

    private static int Usage()
    {
        Console.WriteLine("""
            SmartTimetable License Generator (vendor only)

              keygen  [--out <folder>]
                      Create the vendor key pair (once, ever).
              decode  --request <code>
                      Inspect a customer's request code.
              issue   --request <code> --customer "<name>" [--edition Standard]
                      [--expires yyyy-MM-dd] [--min-matches N] [--key vendor_private.pem] [--out <folder>]
                      Issue a lifetime license (omit --expires) locked to the requesting PC.
                      Saves the .lic to your Downloads folder by default; --out overrides.
              verify  --license <file> [--public-key vendor_public.txt]
                      Check a license's signature and show its contents.

            The private key passphrase is prompted, or read from the STT_KEY_PASSPHRASE variable.
            """);
        return 1;
    }
}

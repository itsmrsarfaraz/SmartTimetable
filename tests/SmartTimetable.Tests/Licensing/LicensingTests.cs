using System.Security.Cryptography;
using SmartTimetable.Licensing;
using Xunit;

namespace SmartTimetable.Tests.Licensing;

public sealed class FakeFingerprintProvider(MachineFingerprint fingerprint) : IMachineFingerprintProvider
{
    public MachineFingerprint GetFingerprint() => fingerprint;
}

public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

internal static class Machines
{
    /// <summary>A "laptop": all five identifiers present. Suffix lets tests change individual parts.</summary>
    public static MachineFingerprint Laptop(
        string guid = "GUID-A-111111", string uuid = "UUID-A-222222", string bios = "BIOS-A-333333",
        string board = "BOARD-A-444444", string disk = "DISK-A-555555") =>
        MachineFingerprint.FromHardware(new Dictionary<HardwareSlot, string?>
        {
            [HardwareSlot.MachineGuid] = guid,
            [HardwareSlot.SystemUuid] = uuid,
            [HardwareSlot.BiosSerial] = bios,
            [HardwareSlot.BoardSerial] = board,
            [HardwareSlot.DiskSerial] = disk,
        });

    /// <summary>A cheap desktop that only exposes Windows GUID and disk serial.</summary>
    public static MachineFingerprint CheapDesktop(string guid = "GUID-D-111111", string disk = "DISK-D-555555") =>
        MachineFingerprint.FromHardware(new Dictionary<HardwareSlot, string?>
        {
            [HardwareSlot.MachineGuid] = guid,
            [HardwareSlot.SystemUuid] = "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
            [HardwareSlot.BiosSerial] = "To Be Filled By O.E.M.",
            [HardwareSlot.BoardSerial] = "Default string",
            [HardwareSlot.DiskSerial] = disk,
        });
}

public sealed class RequestCodeTests
{
    [Fact]
    public void Encode_then_decode_round_trips()
    {
        var original = Machines.Laptop();
        var code = RequestCode.Encode(original);

        Assert.True(RequestCode.TryDecode(code, out var decoded));
        Assert.Equal(original.ToBytes(), decoded!.ToBytes());
    }

    [Fact]
    public void Decode_is_forgiving_about_case_spacing_and_dashes()
    {
        var code = RequestCode.Encode(Machines.Laptop());
        var sloppy = " " + code.ToLowerInvariant().Replace("-", "  ") + " ";

        Assert.True(RequestCode.TryDecode(sloppy, out _));
    }

    [Fact]
    public void A_single_mistyped_character_is_rejected()
    {
        var code = RequestCode.Encode(Machines.Laptop());
        var chars = code.ToCharArray();
        var i = 8;                                   // inside the first hash, not a dash
        chars[i] = chars[i] == 'A' ? 'B' : 'A';

        Assert.False(RequestCode.TryDecode(new string(chars), out _));
    }

    [Fact]
    public void Garbage_and_empty_input_are_rejected()
    {
        Assert.False(RequestCode.TryDecode(null, out _));
        Assert.False(RequestCode.TryDecode("", out _));
        Assert.False(RequestCode.TryDecode("HELLO-WORLD", out _));
    }

    [Fact]
    public void Junk_hardware_values_are_treated_as_absent()
    {
        var desktop = Machines.CheapDesktop();

        Assert.Equal(2, desktop.PresentCount);
    }

    [Fact]
    public void Different_machines_produce_different_codes()
    {
        Assert.False(RequestCode.Encode(Machines.Laptop()) == RequestCode.Encode(Machines.Laptop(guid: "GUID-B-999999")));
    }
}

public sealed class LicenseTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    private readonly ECDsa _vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private byte[] PublicKey => _vendorKey.ExportSubjectPublicKeyInfo();

    private string IssueFor(MachineFingerprint machine, DateTimeOffset? expires = null, int? min = null)
    {
        var request = new LicenseRequest(RequestCode.Encode(machine), "Beaconhouse Test College", "Standard", expires, min);
        return LicenseIssuer.Issue(_vendorKey, request, new FakeClock(Now)).Token;
    }

    private LicenseValidator ValidatorOn(MachineFingerprint machine, FakeClock? clock = null) =>
        new(PublicKey, new FakeFingerprintProvider(machine), clock ?? new FakeClock(Now));

    [Fact]
    public void License_is_valid_on_the_machine_it_was_issued_for()
    {
        var laptop = Machines.Laptop();
        var result = ValidatorOn(laptop).Validate(IssueFor(laptop));

        Assert.True(result.IsValid);
        Assert.Equal("Beaconhouse Test College", result.Payload!.Customer);
        Assert.Null(result.Payload.ExpiresUtc);   // lifetime
    }

    [Fact]
    public void License_is_rejected_on_a_different_computer()
    {
        var token = IssueFor(Machines.Laptop());
        var otherLaptop = Machines.Laptop("G2-XXXXXX", "U2-XXXXXX", "B2-XXXXXX", "M2-XXXXXX", "D2-XXXXXX");

        var result = ValidatorOn(otherLaptop).Validate(token);

        Assert.Equal(LicenseStatus.MachineMismatch, result.Status);
    }

    [Fact]
    public void Reinstalling_windows_does_not_break_the_license()
    {
        var token = IssueFor(Machines.Laptop());
        var afterReinstall = Machines.Laptop(guid: "GUID-NEW-000001");   // only MachineGuid changed

        Assert.True(ValidatorOn(afterReinstall).Validate(token).IsValid);
    }

    [Fact]
    public void Reinstall_plus_new_disk_still_works_but_a_third_change_does_not()
    {
        var token = IssueFor(Machines.Laptop());

        var twoChanges = Machines.Laptop(guid: "GUID-NEW-000001", disk: "DISK-NEW-000001");
        Assert.True(ValidatorOn(twoChanges).Validate(token).IsValid);

        var threeChanges = Machines.Laptop(guid: "GUID-NEW-000001", disk: "DISK-NEW-000001", board: "BOARD-NEW-0001");
        Assert.Equal(LicenseStatus.MachineMismatch, ValidatorOn(threeChanges).Validate(token).Status);
    }

    [Fact]
    public void Cheap_desktop_with_two_identifiers_needs_both_to_match()
    {
        var desktop = Machines.CheapDesktop();
        var token = IssueFor(desktop);

        Assert.True(ValidatorOn(desktop).Validate(token).IsValid);
        Assert.Equal(LicenseStatus.MachineMismatch,
            ValidatorOn(Machines.CheapDesktop(disk: "DISK-NEW-000001")).Validate(token).Status);
    }

    [Fact]
    public void Issuing_refuses_machines_with_too_few_identifiers()
    {
        var weak = MachineFingerprint.FromHardware(new Dictionary<HardwareSlot, string?>
        {
            [HardwareSlot.MachineGuid] = "GUID-ONLY-111111",
        });

        var ex = Assert.Throws<LicenseException>(() => IssueFor(weak));
        Assert.True(ex.Message.Contains("hardware identifier"));
    }

    [Fact]
    public void Tampering_with_the_payload_invalidates_the_signature()
    {
        var laptop = Machines.Laptop();
        var token = IssueFor(laptop);
        var parts = token.Split('.');
        var payload = System.Buffers.Text.Base64Url.DecodeFromChars(parts[1]);
        var text = System.Text.Encoding.UTF8.GetString(payload).Replace("Beaconhouse Test College", "Somebody Else Colleg");
        var forged = $"{parts[0]}.{System.Buffers.Text.Base64Url.EncodeToString(System.Text.Encoding.UTF8.GetBytes(text))}.{parts[2]}";

        Assert.Equal(LicenseStatus.InvalidSignature, ValidatorOn(laptop).Validate(forged).Status);
    }

    [Fact]
    public void License_signed_by_a_different_vendor_key_is_rejected()
    {
        var laptop = Machines.Laptop();
        using var attackerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var forged = LicenseIssuer.Issue(attackerKey,
            new LicenseRequest(RequestCode.Encode(laptop), "Pirate"), new FakeClock(Now)).Token;

        Assert.Equal(LicenseStatus.InvalidSignature, ValidatorOn(laptop).Validate(forged).Status);
    }

    [Fact]
    public void Expiring_license_stops_working_after_its_date_but_lifetime_never_does()
    {
        var laptop = Machines.Laptop();
        var expiring = IssueFor(laptop, expires: Now.AddDays(30));
        var lifetime = IssueFor(laptop);
        var later = new FakeClock(Now.AddYears(50));

        Assert.True(ValidatorOn(laptop, new FakeClock(Now.AddDays(29))).Validate(expiring).IsValid);
        Assert.Equal(LicenseStatus.Expired, ValidatorOn(laptop, later).Validate(expiring).Status);
        Assert.True(ValidatorOn(laptop, later).Validate(lifetime).IsValid);
    }

    [Fact]
    public void Missing_or_garbage_license_is_reported_clearly()
    {
        var validator = ValidatorOn(Machines.Laptop());

        Assert.Equal(LicenseStatus.NotFound, validator.Validate(null).Status);
        Assert.Equal(LicenseStatus.NotFound, validator.Validate("   ").Status);
        Assert.Equal(LicenseStatus.Malformed, validator.Validate("not a license").Status);
        Assert.Equal(LicenseStatus.Malformed, validator.Validate("STT1.@@@.@@@").Status);
    }

    [Fact]
    public void Build_without_a_public_key_reports_not_configured()
    {
        var laptop = Machines.Laptop();
        var validator = new LicenseValidator([], new FakeFingerprintProvider(laptop));

        Assert.Equal(LicenseStatus.NotConfigured, validator.Validate(IssueFor(laptop)).Status);
    }

    [Fact]
    public void Armored_license_files_with_line_breaks_are_accepted()
    {
        var laptop = Machines.Laptop();
        var armored = LicenseToken.ToArmored(IssueFor(laptop));

        Assert.True(armored.Contains("-----BEGIN"));
        Assert.True(ValidatorOn(laptop).Validate(armored).IsValid);
    }
}

public sealed class LicenseServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "stt-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ECDsa _vendorKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    private LicenseService CreateService(MachineFingerprint machine)
    {
        var provider = new FakeFingerprintProvider(machine);
        var validator = new LicenseValidator(_vendorKey.ExportSubjectPublicKeyInfo(), provider);
        return new LicenseService(validator, provider, new LicenseStore(Path.Combine(_folder, "license.lic")));
    }

    private string Issue(MachineFingerprint machine) =>
        LicenseIssuer.Issue(_vendorKey, new LicenseRequest(RequestCode.Encode(machine), "Test College")).Token;

    [Fact]
    public void Full_activation_flow_persists_across_restarts()
    {
        var laptop = Machines.Laptop();

        var firstRun = CreateService(laptop);
        Assert.Equal(LicenseStatus.NotFound, firstRun.CheckInstalledLicense().Status);

        // customer sends this code to the vendor; vendor answers with a license
        var requestCode = firstRun.GetRequestCode();
        var token = LicenseIssuer.Issue(_vendorKey, new LicenseRequest(requestCode, "Test College")).Token;

        Assert.True(firstRun.Import(token).IsValid);

        var afterRestart = CreateService(laptop);
        Assert.True(afterRestart.CheckInstalledLicense().IsValid);
    }

    [Fact]
    public void Import_accepts_a_path_to_a_lic_file()
    {
        var laptop = Machines.Laptop();
        Directory.CreateDirectory(_folder);
        var file = Path.Combine(_folder, "customer.lic");
        File.WriteAllText(file, LicenseToken.ToArmored(Issue(laptop)));

        Assert.True(CreateService(laptop).Import("\"" + file + "\"").IsValid);
    }

    [Fact]
    public void Importing_a_license_for_another_pc_is_refused_and_not_stored()
    {
        var token = Issue(Machines.Laptop());
        var otherPc = Machines.Laptop("G2-XXXXXX", "U2-XXXXXX", "B2-XXXXXX", "M2-XXXXXX", "D2-XXXXXX");
        var service = CreateService(otherPc);

        Assert.Equal(LicenseStatus.MachineMismatch, service.Import(token).Status);
        Assert.Equal(LicenseStatus.NotFound, service.CheckInstalledLicense().Status);
    }

    [Fact]
    public void Copying_the_installed_license_file_to_another_pc_does_not_activate_it()
    {
        var laptop = Machines.Laptop();
        CreateService(laptop).Import(Issue(laptop));

        var otherPc = Machines.Laptop("G2-XXXXXX", "U2-XXXXXX", "B2-XXXXXX", "M2-XXXXXX", "D2-XXXXXX");
        // same license.lic file on disk, but the validator now sees a different machine
        Assert.Equal(LicenseStatus.MachineMismatch, CreateService(otherPc).CheckInstalledLicense().Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
        _vendorKey.Dispose();
    }
}

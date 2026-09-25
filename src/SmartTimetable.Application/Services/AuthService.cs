using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Application.Services;

/// <summary>Single-admin authentication service. Uses a fresh unit of work per call.</summary>
public sealed class AuthService : IAuthService
{
    private readonly Func<IUnitOfWork> _uowFactory;
    private readonly IPasswordHasher _hasher;

    public AuthService(Func<IUnitOfWork> uowFactory, IPasswordHasher hasher)
    {
        _uowFactory = uowFactory;
        _hasher = hasher;
    }

    public async Task<bool> AnyUserExistsAsync(CancellationToken ct = default)
    {
        using var uow = _uowFactory();
        return await uow.Users.AnyAsync(ct);
    }

    public async Task EnsureSeedAdminAsync(string username, string password, string displayName, CancellationToken ct = default)
    {
        using var uow = _uowFactory();
        if (await uow.Users.AnyAsync(ct))
            return;

        var (hash, salt) = _hasher.Hash(password);
        await uow.Users.AddAsync(new AdminUser
        {
            Username = username.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = displayName,
            Role = UserRole.SuperAdmin
        }, ct);
        await uow.SaveChangesAsync(ct);
    }

    public async Task<AdminUser?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        using var uow = _uowFactory();
        var user = await uow.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null)
            return null;

        return _hasher.Verify(password, user.PasswordHash, user.PasswordSalt) ? user : null;
    }
}

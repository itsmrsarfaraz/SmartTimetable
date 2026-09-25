using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Common;

namespace SmartTimetable.Infrastructure.Persistence;

/// <summary>Generic EF Core implementation of <see cref="IRepository{T}"/>.</summary>
public sealed class EfRepository<T> : IRepository<T> where T : Entity
{
    private readonly AppDbContext _db;
    private readonly DbSet<T> _set;

    public EfRepository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public IQueryable<T> Query() => _set;

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _set.FindAsync(new object?[] { id }, ct);

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _set.FirstOrDefaultAsync(predicate, ct);

    public async Task<List<T>> ListAsync(CancellationToken ct = default) =>
        await _set.ToListAsync(ct);

    public async Task<List<T>> ListAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        await _set.Where(predicate).ToListAsync(ct);

    public async Task<bool> AnyAsync(CancellationToken ct = default) =>
        await _set.AnyAsync(ct);

    public async Task<int> CountAsync(CancellationToken ct = default) =>
        await _set.CountAsync(ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await _set.AddAsync(entity, ct);

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default) =>
        await _set.AddRangeAsync(entities, ct);

    public void Update(T entity) => _set.Update(entity);

    public void Remove(T entity) => _set.Remove(entity);

    public void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);
}

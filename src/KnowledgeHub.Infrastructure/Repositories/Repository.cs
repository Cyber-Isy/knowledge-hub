using System.Linq.Expressions;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Models;
using KnowledgeHub.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<T> DbSet;

    public Repository(ApplicationDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await DbSet.FindAsync([id], ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().ToListAsync(ct);

    public async Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
        => await DbSet.AsNoTracking().Where(predicate).ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
        await Context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        DbSet.Remove(entity);
        await Context.SaveChangesAsync(ct);
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
        => predicate is null
            ? await DbSet.CountAsync(ct)
            : await DbSet.CountAsync(predicate, ct);

    public async Task<PagedResult<T>> GetPagedAsync(
        PaginationParams pagination, Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();

        if (predicate is not null)
            query = query.Where(predicate);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(ct);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }
}

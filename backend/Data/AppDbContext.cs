using JuggerHub.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace JuggerHub.Data;

/// <summary>
/// The application's EF Core context. Backed by PostgreSQL (Npgsql) and built on
/// ASP.NET Core Identity with <see cref="Guid"/> keys (UUIDv7).
/// </summary>
/// <remarks>
/// Registers <see cref="AuditFieldsInterceptor"/> so audit timestamps are set
/// automatically. The initial migration creates the Identity schema; future
/// domain entities derive from <see cref="BaseEntity"/>.
/// </remarks>
public class AppDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
    }
}

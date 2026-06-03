using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TranzaPay.Domain.Entities;
using TranzaPay.Domain.Enums;
using TranzaPay.Infrastructure.Services;

namespace TranzaPay.Infrastructure.Data;

public static class DbSeeder
{
    public static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid SystemWalletId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    public static async Task InitializeAsync(TranzaPayDbContext db, ILogger logger, CancellationToken cancellationToken = default)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Users.AnyAsync(x => x.Id == SystemUserId, cancellationToken))
        {
            db.Users.Add(new User
            {
                Id = SystemUserId,
                FullName = "TranzaPay System",
                Email = "system@tranzapay.local",
                PasswordHash = PasswordHasher.Hash(Guid.NewGuid().ToString("N")),
                Role = UserRole.Admin,
                IsActive = false
            });
        }

        if (!await db.Wallets.AnyAsync(x => x.Id == SystemWalletId, cancellationToken))
        {
            db.Wallets.Add(new Wallet
            {
                Id = SystemWalletId,
                UserId = SystemUserId,
                Currency = "USD",
                Balance = 1_000_000_000m,
                Status = WalletStatus.Active
            });
        }

        if (!await db.Users.AnyAsync(x => x.Id == AdminUserId, cancellationToken))
        {
            db.Users.Add(new User
            {
                Id = AdminUserId,
                FullName = "TranzaPay Admin",
                Email = "admin@tranzapay.local",
                PasswordHash = PasswordHasher.Hash("Admin123!"),
                Role = UserRole.Admin,
                IsActive = true
            });

            db.AuditLogs.Add(new AuditLog
            {
                UserId = AdminUserId,
                Action = "SeedAdmin",
                EntityName = "Users",
                EntityId = AdminUserId.ToString(),
                NewValues = """{"email":"admin@tranzapay.local","password":"Admin123!"}"""
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Database initialized. Default admin: admin@tranzapay.local / Admin123!");
    }
}

using Microsoft.EntityFrameworkCore;
using TranzaPay.Domain.Entities;
using TranzaPay.Domain.Enums;

namespace TranzaPay.Infrastructure.Data;

public sealed class TranzaPayDbContext(DbContextOptions<TranzaPayDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Wallet> Wallets => Set<Wallet>();
    public DbSet<TransactionRecord> Transactions => Set<TransactionRecord>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<WithdrawalRequest> WithdrawalRequests => Set<WithdrawalRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(150).IsRequired();
            entity.Property(x => x.PasswordHash).IsRequired();
            entity.Property(x => x.Role).HasMaxLength(50).HasConversion<string>().HasDefaultValue(UserRole.User);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Currency).HasMaxLength(10).HasDefaultValue("USD");
            entity.Property(x => x.Balance).HasPrecision(18, 2).HasDefaultValue(0);
            entity.Property(x => x.Status).HasMaxLength(50).HasConversion<string>().HasDefaultValue(WalletStatus.Active);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.User).WithMany(x => x.Wallets).HasForeignKey(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.Currency }).IsUnique();
        });

        modelBuilder.Entity<TransactionRecord>(entity =>
        {
            entity.ToTable("Transactions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Reference).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(50).HasConversion<string>().IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Currency).HasMaxLength(10).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(50).HasConversion<string>().IsRequired();
            entity.Property(x => x.Description).HasMaxLength(255);
            entity.Property(x => x.IdempotencyKey).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(x => x.Reference).IsUnique();
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.ToTable("LedgerEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntryType).HasMaxLength(20).HasConversion<string>().IsRequired();
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.BalanceBefore).HasPrecision(18, 2);
            entity.Property(x => x.BalanceAfter).HasPrecision(18, 2);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.Transaction).WithMany(x => x.LedgerEntries).HasForeignKey(x => x.TransactionId);
            entity.HasOne(x => x.Wallet).WithMany(x => x.LedgerEntries).HasForeignKey(x => x.WalletId);
        });

        modelBuilder.Entity<WithdrawalRequest>(entity =>
        {
            entity.ToTable("WithdrawalRequests");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasMaxLength(50).HasConversion<string>().HasDefaultValue(WithdrawalStatus.Pending);
            entity.Property(x => x.AdminRemarks).HasMaxLength(255);
            entity.Property(x => x.RequestedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId);
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Action).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(100);
            entity.Property(x => x.IpAddress).HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}

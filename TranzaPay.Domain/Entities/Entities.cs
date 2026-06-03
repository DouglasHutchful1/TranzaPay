using TranzaPay.Domain.Enums;

namespace TranzaPay.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Wallet> Wallets { get; set; } = [];
}

public sealed class Wallet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal Balance { get; set; }
    public WalletStatus Status { get; set; } = WalletStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? User { get; set; }
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
}

public sealed class TransactionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? Description { get; set; }
    public string? IdempotencyKey { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LedgerEntry> LedgerEntries { get; set; } = [];
}

public sealed class LedgerEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TransactionId { get; set; }
    public Guid WalletId { get; set; }
    public LedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public TransactionRecord? Transaction { get; set; }
    public Wallet? Wallet { get; set; }
}

public sealed class WithdrawalRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; }
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;
    public string? AdminRemarks { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }

    public User? User { get; set; }
    public Wallet? Wallet { get; set; }
}

public sealed class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

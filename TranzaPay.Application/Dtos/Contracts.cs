using TranzaPay.Domain.Enums;

namespace TranzaPay.Application.Dtos;

public sealed record RegisterRequest(string FullName, string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User);

public sealed record UserDto(Guid Id, string FullName, string Email, UserRole Role);
public sealed record WalletDto(Guid Id, Guid UserId, string Currency, decimal Balance, WalletStatus Status, DateTime CreatedAt);
public sealed record DepositRequest(Guid WalletId, decimal Amount, string? IdempotencyKey);
public sealed record TransferRequest(Guid FromWalletId, Guid ToWalletId, decimal Amount, string? IdempotencyKey, string? Description);
public sealed record WithdrawalRequestCreate(Guid WalletId, decimal Amount);
public sealed record ReviewWithdrawalRequest(string Decision, string? Remarks);

public sealed record TransactionDto(
    Guid Id,
    string Reference,
    TransactionType Type,
    decimal Amount,
    string Currency,
    TransactionStatus Status,
    string? Description,
    string? IdempotencyKey,
    Guid? CreatedBy,
    DateTime CreatedAt,
    IReadOnlyCollection<LedgerEntryDto> LedgerEntries);

public sealed record LedgerEntryDto(
    Guid Id,
    Guid WalletId,
    LedgerEntryType EntryType,
    decimal Amount,
    decimal BalanceBefore,
    decimal BalanceAfter,
    DateTime CreatedAt);

public sealed record WithdrawalRequestDto(
    Guid Id,
    Guid UserId,
    Guid WalletId,
    decimal Amount,
    WithdrawalStatus Status,
    string? AdminRemarks,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedBy);

public sealed record AuditLogDto(
    Guid Id,
    Guid? UserId,
    string Action,
    string EntityName,
    string? EntityId,
    string? OldValues,
    string? NewValues,
    string? IpAddress,
    DateTime CreatedAt);

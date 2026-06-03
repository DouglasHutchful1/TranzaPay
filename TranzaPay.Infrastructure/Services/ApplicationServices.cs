using System.Data;
using Microsoft.EntityFrameworkCore;
using TranzaPay.Application.Common;
using TranzaPay.Application.Dtos;
using TranzaPay.Application.Interfaces;
using TranzaPay.Domain.Entities;
using TranzaPay.Domain.Enums;
using TranzaPay.Infrastructure.Data;
using static TranzaPay.Infrastructure.Services.ServiceHelpers;

namespace TranzaPay.Infrastructure.Services;

public sealed class AuthService(
    TranzaPayDbContext db,
    ITokenService tokenService,
    ICurrentUserService currentUser) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new AppException("Full name, email, and password are required.");
        }

        if (request.Password.Length < 8)
        {
            throw new AppException("Password must be at least 8 characters.");
        }

        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(x => x.Email == email, cancellationToken))
        {
            throw new AppException("Email is already registered.", 409);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = UserRole.User
        };

        var wallet = new Wallet
        {
            UserId = user.Id,
            Currency = "USD",
            Balance = 0
        };

        db.Users.Add(user);
        db.Wallets.Add(wallet);
        AddAudit("Register", "Users", user.Id, null, $$"""{"email":"{{email}}","walletId":"{{wallet.Id}}"}""");

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return tokenService.CreateToken(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null || !user.IsActive || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new AppException("Invalid email or password.", 401);
        }

        AddAudit("Login", "Users", user.Id, null, $$"""{"email":"{{email}}"}""", user.Id);
        await db.SaveChangesAsync(cancellationToken);

        return tokenService.CreateToken(user);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private void AddAudit(string action, string entity, Guid entityId, string? oldValues, string? newValues, Guid? userId = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId ?? currentUser.UserId,
            Action = action,
            EntityName = entity,
            EntityId = entityId.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = currentUser.IpAddress
        });
    }
}

public sealed class WalletService(TranzaPayDbContext db, ICurrentUserService currentUser) : IWalletService
{
    public async Task<IReadOnlyCollection<WalletDto>> GetMyWalletsAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        return await db.Wallets
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderBy(x => x.Currency)
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<WalletDto>> GetAllWalletsAsync(CancellationToken cancellationToken)
    {
        return await db.Wallets
            .AsNoTracking()
            .Where(x => x.UserId != DbSeeder.SystemUserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<WalletDto> GetWalletAsync(Guid walletId, CancellationToken cancellationToken)
    {
        var wallet = await db.Wallets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == walletId, cancellationToken)
            ?? throw new AppException("Wallet not found.", 404);

        if (!IsAdmin() && wallet.UserId != RequireUser())
        {
            throw new AppException("You do not have access to this wallet.", 403);
        }

        return wallet.ToDto();
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new AppException("Authentication is required.", 401);
    private bool IsAdmin() => string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);
}

public sealed class DepositService(TranzaPayDbContext db, ICurrentUserService currentUser) : IDepositService
{
    public async Task<TransactionDto> SimulateDepositAsync(DepositRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        var amount = ValidateAmount(request.Amount);
        var idempotencyKey = NormalizeKey(request.IdempotencyKey);

        if (idempotencyKey is not null)
        {
            var existing = await FindByIdempotencyKey(idempotencyKey, TransactionType.Deposit, userId, cancellationToken);
            if (existing is not null)
            {
                return existing.ToDto();
            }
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var wallet = await LoadWalletForMoneyMove(request.WalletId, cancellationToken);
        if (!IsAdmin() && wallet.UserId != userId)
        {
            throw new AppException("You can only deposit into your own wallet.", 403);
        }

        var systemWallet = await db.Wallets.SingleAsync(x => x.Id == DbSeeder.SystemWalletId, cancellationToken);
        var transaction = NewTransaction(TransactionType.Deposit, amount, wallet.Currency, TransactionStatus.Completed, userId, idempotencyKey, "Deposit simulation");

        db.Transactions.Add(transaction);
        AddEntry(transaction, systemWallet, LedgerEntryType.Debit, amount, allowNegative: false);
        AddEntry(transaction, wallet, LedgerEntryType.Credit, amount, allowNegative: false);
        AddAudit("DepositSimulated", "Transactions", transaction.Id, null, $$"""{"amount":{{amount}},"walletId":"{{wallet.Id}}"}""", userId);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return transaction.ToDto();
    }

    private async Task<TransactionRecord?> FindByIdempotencyKey(string key, TransactionType type, Guid userId, CancellationToken cancellationToken)
    {
        return await db.Transactions
            .Include(x => x.LedgerEntries)
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key && x.Type == type && x.CreatedBy == userId, cancellationToken);
    }

    private async Task<Wallet> LoadWalletForMoneyMove(Guid walletId, CancellationToken cancellationToken)
    {
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == walletId, cancellationToken)
            ?? throw new AppException("Wallet not found.", 404);

        if (wallet.Status != WalletStatus.Active)
        {
            throw new AppException("Wallet is not active.");
        }

        return wallet;
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new AppException("Authentication is required.", 401);
    private bool IsAdmin() => string.Equals(currentUser.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase);

    private void AddAudit(string action, string entity, Guid entityId, string? oldValues, string? newValues, Guid userId)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entity,
            EntityId = entityId.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = currentUser.IpAddress
        });
    }
}

public sealed class TransferService(TranzaPayDbContext db, ICurrentUserService currentUser) : ITransferService
{
    public async Task<TransactionDto> TransferAsync(TransferRequest request, CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        var amount = ValidateAmount(request.Amount);
        var idempotencyKey = NormalizeKey(request.IdempotencyKey);

        if (request.FromWalletId == request.ToWalletId)
        {
            throw new AppException("Source and destination wallets must be different.");
        }

        if (idempotencyKey is not null)
        {
            var existing = await db.Transactions
                .Include(x => x.LedgerEntries)
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey && x.Type == TransactionType.Transfer && x.CreatedBy == userId, cancellationToken);

            if (existing is not null)
            {
                return existing.ToDto();
            }
        }

        await using var dbTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var fromWallet = await LoadWallet(request.FromWalletId, cancellationToken);
        var toWallet = await LoadWallet(request.ToWalletId, cancellationToken);

        if (fromWallet.UserId != userId)
        {
            throw new AppException("You can only transfer from your own wallet.", 403);
        }

        if (!string.Equals(fromWallet.Currency, toWallet.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("Wallet currencies must match.");
        }

        var transaction = NewTransaction(TransactionType.Transfer, amount, fromWallet.Currency, TransactionStatus.Completed, userId, idempotencyKey, request.Description ?? "Wallet transfer");

        db.Transactions.Add(transaction);
        AddEntry(transaction, fromWallet, LedgerEntryType.Debit, amount, allowNegative: false);
        AddEntry(transaction, toWallet, LedgerEntryType.Credit, amount, allowNegative: false);
        AddAudit("TransferCompleted", "Transactions", transaction.Id, null, $$"""{"amount":{{amount}},"fromWalletId":"{{fromWallet.Id}}","toWalletId":"{{toWallet.Id}}"}""", userId);

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return transaction.ToDto();
    }

    private async Task<Wallet> LoadWallet(Guid walletId, CancellationToken cancellationToken)
    {
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == walletId, cancellationToken)
            ?? throw new AppException("Wallet not found.", 404);

        if (wallet.Status != WalletStatus.Active)
        {
            throw new AppException("Wallet is not active.");
        }

        return wallet;
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new AppException("Authentication is required.", 401);

    private void AddAudit(string action, string entity, Guid entityId, string? oldValues, string? newValues, Guid userId)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entity,
            EntityId = entityId.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = currentUser.IpAddress
        });
    }
}

public sealed class WithdrawalService(TranzaPayDbContext db, ICurrentUserService currentUser) : IWithdrawalService
{
    public async Task<WithdrawalRequestDto> CreateAsync(WithdrawalRequestCreate request, CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        var amount = ValidateAmount(request.Amount);

        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.Id == request.WalletId, cancellationToken)
            ?? throw new AppException("Wallet not found.", 404);

        if (wallet.UserId != userId)
        {
            throw new AppException("You can only withdraw from your own wallet.", 403);
        }

        if (wallet.Status != WalletStatus.Active)
        {
            throw new AppException("Wallet is not active.");
        }

        if (wallet.Balance < amount)
        {
            throw new AppException("Insufficient wallet balance.");
        }

        var withdrawal = new WithdrawalRequest
        {
            UserId = userId,
            WalletId = wallet.Id,
            Amount = amount
        };

        db.WithdrawalRequests.Add(withdrawal);
        AddAudit("WithdrawalRequested", "WithdrawalRequests", withdrawal.Id, null, $$"""{"amount":{{amount}},"walletId":"{{wallet.Id}}"}""", userId);
        await db.SaveChangesAsync(cancellationToken);

        return withdrawal.ToDto();
    }

    public async Task<IReadOnlyCollection<WithdrawalRequestDto>> GetMineAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUser();
        return await db.WithdrawalRequests
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RequestedAt)
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<WithdrawalRequestDto>> GetPendingAsync(CancellationToken cancellationToken)
    {
        return await db.WithdrawalRequests
            .AsNoTracking()
            .Where(x => x.Status == WithdrawalStatus.Pending)
            .OrderBy(x => x.RequestedAt)
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }

    public async Task<WithdrawalRequestDto> ReviewAsync(Guid withdrawalId, ReviewWithdrawalRequest request, CancellationToken cancellationToken)
    {
        var adminId = RequireUser();
        var decision = request.Decision.Trim();

        await using var dbTransaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var withdrawal = await db.WithdrawalRequests.SingleOrDefaultAsync(x => x.Id == withdrawalId, cancellationToken)
            ?? throw new AppException("Withdrawal request not found.", 404);

        if (withdrawal.Status != WithdrawalStatus.Pending)
        {
            throw new AppException("Withdrawal request has already been reviewed.", 409);
        }

        var oldValues = $$"""{"status":"{{withdrawal.Status}}"}""";
        withdrawal.AdminRemarks = request.Remarks?.Trim();
        withdrawal.ReviewedAt = DateTime.UtcNow;
        withdrawal.ReviewedBy = adminId;

        if (string.Equals(decision, "approve", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(decision, "approved", StringComparison.OrdinalIgnoreCase))
        {
            var wallet = await db.Wallets.SingleAsync(x => x.Id == withdrawal.WalletId, cancellationToken);
            if (wallet.Balance < withdrawal.Amount)
            {
                throw new AppException("Insufficient wallet balance at approval time.");
            }

            var systemWallet = await db.Wallets.SingleAsync(x => x.Id == DbSeeder.SystemWalletId, cancellationToken);
            var transaction = NewTransaction(TransactionType.Withdrawal, withdrawal.Amount, wallet.Currency, TransactionStatus.Completed, adminId, null, "Withdrawal approved");

            withdrawal.Status = WithdrawalStatus.Approved;
            db.Transactions.Add(transaction);
            AddEntry(transaction, wallet, LedgerEntryType.Debit, withdrawal.Amount, allowNegative: false);
            AddEntry(transaction, systemWallet, LedgerEntryType.Credit, withdrawal.Amount, allowNegative: false);
            AddAudit("WithdrawalApproved", "WithdrawalRequests", withdrawal.Id, oldValues, $$"""{"status":"{{withdrawal.Status}}","reviewedBy":"{{adminId}}"}""", adminId);
        }
        else if (string.Equals(decision, "reject", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(decision, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            withdrawal.Status = WithdrawalStatus.Rejected;
            AddAudit("WithdrawalRejected", "WithdrawalRequests", withdrawal.Id, oldValues, $$"""{"status":"{{withdrawal.Status}}","reviewedBy":"{{adminId}}"}""", adminId);
        }
        else
        {
            throw new AppException("Decision must be approve or reject.");
        }

        await db.SaveChangesAsync(cancellationToken);
        await dbTransaction.CommitAsync(cancellationToken);

        return withdrawal.ToDto();
    }

    private Guid RequireUser() => currentUser.UserId ?? throw new AppException("Authentication is required.", 401);

    private void AddAudit(string action, string entity, Guid entityId, string? oldValues, string? newValues, Guid userId)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityName = entity,
            EntityId = entityId.ToString(),
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = currentUser.IpAddress
        });
    }
}

public sealed class TransactionQueryService(TranzaPayDbContext db, ICurrentUserService currentUser) : ITransactionQueryService
{
    public async Task<IReadOnlyCollection<TransactionDto>> GetMineAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new AppException("Authentication is required.", 401);

        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(x => x.LedgerEntries)
            .ThenInclude(x => x.Wallet)
            .Where(x => x.CreatedBy == userId || x.LedgerEntries.Any(entry => entry.Wallet != null && entry.Wallet.UserId == userId))
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return transactions.Select(x => x.ToDto()).ToList();
    }

    public async Task<IReadOnlyCollection<TransactionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var transactions = await db.Transactions
            .AsNoTracking()
            .Include(x => x.LedgerEntries)
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(cancellationToken);

        return transactions.Select(x => x.ToDto()).ToList();
    }
}

public sealed class AuditLogService(TranzaPayDbContext db) : IAuditLogService
{
    public async Task<IReadOnlyCollection<AuditLogDto>> GetRecentAsync(CancellationToken cancellationToken)
    {
        return await db.AuditLogs
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .Select(x => x.ToDto())
            .ToListAsync(cancellationToken);
    }
}

internal static class ServiceHelpers
{
    public static decimal ValidateAmount(decimal amount)
    {
        if (amount <= 0)
        {
            throw new AppException("Amount must be greater than zero.");
        }

        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    public static string? NormalizeKey(string? key)
    {
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    public static TransactionRecord NewTransaction(
        TransactionType type,
        decimal amount,
        string currency,
        TransactionStatus status,
        Guid userId,
        string? idempotencyKey,
        string description)
    {
        return new TransactionRecord
        {
            Reference = $"TP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8]}".ToUpperInvariant(),
            Type = type,
            Amount = amount,
            Currency = currency,
            Status = status,
            Description = description,
            IdempotencyKey = idempotencyKey,
            CreatedBy = userId
        };
    }

    public static void AddEntry(TransactionRecord transaction, Wallet wallet, LedgerEntryType entryType, decimal amount, bool allowNegative)
    {
        var before = wallet.Balance;
        var after = entryType == LedgerEntryType.Credit ? before + amount : before - amount;

        if (!allowNegative && after < 0)
        {
            throw new AppException("Insufficient wallet balance.");
        }

        wallet.Balance = after;
        transaction.LedgerEntries.Add(new LedgerEntry
        {
            TransactionId = transaction.Id,
            WalletId = wallet.Id,
            EntryType = entryType,
            Amount = amount,
            BalanceBefore = before,
            BalanceAfter = after
        });
    }
}

internal static class DtoMapping
{
    public static WalletDto ToDto(this Wallet wallet)
    {
        return new WalletDto(wallet.Id, wallet.UserId, wallet.Currency, wallet.Balance, wallet.Status, wallet.CreatedAt);
    }

    public static TransactionDto ToDto(this TransactionRecord transaction)
    {
        return new TransactionDto(
            transaction.Id,
            transaction.Reference,
            transaction.Type,
            transaction.Amount,
            transaction.Currency,
            transaction.Status,
            transaction.Description,
            transaction.IdempotencyKey,
            transaction.CreatedBy,
            transaction.CreatedAt,
            transaction.LedgerEntries.OrderBy(x => x.CreatedAt).Select(x => x.ToDto()).ToList());
    }

    public static LedgerEntryDto ToDto(this LedgerEntry entry)
    {
        return new LedgerEntryDto(entry.Id, entry.WalletId, entry.EntryType, entry.Amount, entry.BalanceBefore, entry.BalanceAfter, entry.CreatedAt);
    }

    public static WithdrawalRequestDto ToDto(this WithdrawalRequest request)
    {
        return new WithdrawalRequestDto(request.Id, request.UserId, request.WalletId, request.Amount, request.Status, request.AdminRemarks, request.RequestedAt, request.ReviewedAt, request.ReviewedBy);
    }

    public static AuditLogDto ToDto(this AuditLog log)
    {
        return new AuditLogDto(log.Id, log.UserId, log.Action, log.EntityName, log.EntityId, log.OldValues, log.NewValues, log.IpAddress, log.CreatedAt);
    }
}

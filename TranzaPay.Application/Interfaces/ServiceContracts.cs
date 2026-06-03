using TranzaPay.Application.Dtos;
using TranzaPay.Domain.Entities;

namespace TranzaPay.Application.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Role { get; }
    string? IpAddress { get; }
}

public interface ITokenService
{
    AuthResponse CreateToken(User user);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public interface IWalletService
{
    Task<IReadOnlyCollection<WalletDto>> GetMyWalletsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WalletDto>> GetAllWalletsAsync(CancellationToken cancellationToken);
    Task<WalletDto> GetWalletAsync(Guid walletId, CancellationToken cancellationToken);
}

public interface IDepositService
{
    Task<TransactionDto> SimulateDepositAsync(DepositRequest request, CancellationToken cancellationToken);
}

public interface ITransferService
{
    Task<TransactionDto> TransferAsync(TransferRequest request, CancellationToken cancellationToken);
}

public interface IWithdrawalService
{
    Task<WithdrawalRequestDto> CreateAsync(WithdrawalRequestCreate request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WithdrawalRequestDto>> GetMineAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<WithdrawalRequestDto>> GetPendingAsync(CancellationToken cancellationToken);
    Task<WithdrawalRequestDto> ReviewAsync(Guid withdrawalId, ReviewWithdrawalRequest request, CancellationToken cancellationToken);
}

public interface ITransactionQueryService
{
    Task<IReadOnlyCollection<TransactionDto>> GetMineAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TransactionDto>> GetAllAsync(CancellationToken cancellationToken);
}

public interface IAuditLogService
{
    Task<IReadOnlyCollection<AuditLogDto>> GetRecentAsync(CancellationToken cancellationToken);
}

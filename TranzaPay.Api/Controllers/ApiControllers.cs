using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TranzaPay.Application.Dtos;
using TranzaPay.Application.Interfaces;

namespace TranzaPay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("register")]
    public Task<AuthResponse> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        return authService.RegisterAsync(request, cancellationToken);
    }

    [HttpPost("login")]
    public Task<AuthResponse> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        return authService.LoginAsync(request, cancellationToken);
    }
}

[ApiController]
[Authorize]
[Route("api/wallets")]
public sealed class WalletsController(IWalletService walletService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<WalletDto>> Mine(CancellationToken cancellationToken)
    {
        return walletService.GetMyWalletsAsync(cancellationToken);
    }

    [HttpGet("{walletId:guid}")]
    public Task<WalletDto> Get(Guid walletId, CancellationToken cancellationToken)
    {
        return walletService.GetWalletAsync(walletId, cancellationToken);
    }
}

[ApiController]
[Authorize]
[Route("api/deposits")]
public sealed class DepositsController(IDepositService depositService) : ControllerBase
{
    [HttpPost("simulate")]
    public Task<TransactionDto> Simulate(DepositRequest request, CancellationToken cancellationToken)
    {
        return depositService.SimulateDepositAsync(request, cancellationToken);
    }
}

[ApiController]
[Authorize]
[Route("api/transfers")]
public sealed class TransfersController(ITransferService transferService) : ControllerBase
{
    [HttpPost]
    public Task<TransactionDto> Transfer(TransferRequest request, CancellationToken cancellationToken)
    {
        return transferService.TransferAsync(request, cancellationToken);
    }
}

[ApiController]
[Authorize]
[Route("api/withdrawals")]
public sealed class WithdrawalsController(IWithdrawalService withdrawalService) : ControllerBase
{
    [HttpPost]
    public Task<WithdrawalRequestDto> Create(WithdrawalRequestCreate request, CancellationToken cancellationToken)
    {
        return withdrawalService.CreateAsync(request, cancellationToken);
    }

    [HttpGet]
    public Task<IReadOnlyCollection<WithdrawalRequestDto>> Mine(CancellationToken cancellationToken)
    {
        return withdrawalService.GetMineAsync(cancellationToken);
    }
}

[ApiController]
[Authorize]
[Route("api/transactions")]
public sealed class TransactionsController(ITransactionQueryService transactionQueryService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<TransactionDto>> Mine(CancellationToken cancellationToken)
    {
        return transactionQueryService.GetMineAsync(cancellationToken);
    }
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/wallets")]
public sealed class AdminWalletsController(IWalletService walletService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<WalletDto>> All(CancellationToken cancellationToken)
    {
        return walletService.GetAllWalletsAsync(cancellationToken);
    }
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/withdrawals")]
public sealed class AdminWithdrawalsController(IWithdrawalService withdrawalService) : ControllerBase
{
    [HttpGet("pending")]
    public Task<IReadOnlyCollection<WithdrawalRequestDto>> Pending(CancellationToken cancellationToken)
    {
        return withdrawalService.GetPendingAsync(cancellationToken);
    }

    [HttpPost("{withdrawalId:guid}/review")]
    public Task<WithdrawalRequestDto> Review(Guid withdrawalId, ReviewWithdrawalRequest request, CancellationToken cancellationToken)
    {
        return withdrawalService.ReviewAsync(withdrawalId, request, cancellationToken);
    }
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/transactions")]
public sealed class AdminTransactionsController(ITransactionQueryService transactionQueryService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<TransactionDto>> All(CancellationToken cancellationToken)
    {
        return transactionQueryService.GetAllAsync(cancellationToken);
    }
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyCollection<AuditLogDto>> Recent(CancellationToken cancellationToken)
    {
        return auditLogService.GetRecentAsync(cancellationToken);
    }
}

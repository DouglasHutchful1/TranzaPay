using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TranzaPay.Application.Interfaces;
using TranzaPay.Infrastructure.Data;
using TranzaPay.Infrastructure.Services;

namespace TranzaPay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<TranzaPayDbContext>(options => options.UseNpgsql(connectionString));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWalletService, WalletService>();
        services.AddScoped<IDepositService, DepositService>();
        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<IWithdrawalService, WithdrawalService>();
        services.AddScoped<ITransactionQueryService, TransactionQueryService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        return services;
    }
}

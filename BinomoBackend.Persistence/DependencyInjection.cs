
using BinomoBackend.Application.Interfaces;
using BinomoBackend.Application.Services;
using BinomoBackend.Domain.Interfaces;
using BinomoBackend.Persistence.Redis;
using BinomoBackend.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace BinomoBackend.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Services
        services.AddScoped<ITradingService, TradingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPriceObserver, LiquidationService>();
        services.AddScoped<IPriceObserver, LimitOrderService>();
        
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IRedisPositionRepository, RedisPositionRepository>();
        services.AddScoped<ILimitOrderRepository, RedisLimitOrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        
        // // Redis
        // services.AddSingleton<IConnectionMultiplexer>(sp =>
        //     ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));


        return services;
    }
}
using ClaimsManagement.Application.Abstractions;
using ClaimsManagement.Application.Services;
using ClaimsManagement.Domain.Services;
using ClaimsManagement.Infrastructure.Messaging;
using ClaimsManagement.Infrastructure.Persistence;
using ClaimsManagement.Infrastructure.Persistence.Repositories;
using ClaimsManagement.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClaimsManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ClaimsDatabase") ?? "Data Source=claims.db";
        services.AddDbContext<ClaimsDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<IClaimRepository, ClaimRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IClaimReferenceGenerator, ClaimReferenceGenerator>();

        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
        services.AddHostedService<NotificationConsumerBackgroundService>();

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ClaimSubmissionService>();
        services.AddScoped<ClaimAssignmentService>();
        services.AddScoped<ClaimQueryService>();
        services.AddScoped<InformationRequestService>();
        services.AddScoped<ClaimDecisionService>();
        services.AddScoped<UserService>();
        services.AddScoped<DashboardService>();

        return services;
    }
}

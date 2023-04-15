﻿using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ProjectManagement.CompanyAPI.Abstractions;
using ProjectManagement.CompanyAPI.Authorization;
using ProjectManagement.CompanyAPI.Configuration;
using ProjectManagement.CompanyAPI.Data;
using ProjectManagement.CompanyAPI.Mapping;
using ProjectManagement.CompanyAPI.Services;
using Steeltoe.Common.Http.Discovery;
using Steeltoe.Connector.PostgreSql;
using Steeltoe.Connector.RabbitMQ;
using Steeltoe.Discovery.Client;
using Steeltoe.Management.Endpoint;
using Steeltoe.Management.Endpoint.Health;
using Steeltoe.Management.Endpoint.Info;
using Steeltoe.Management.Endpoint.Refresh;
using Steeltoe.Messaging.RabbitMQ.Config;
using Steeltoe.Messaging.RabbitMQ.Extensions;
using Winton.Extensions.Configuration.Consul;

namespace ProjectManagement.CompanyAPI.Extensions;

[ExcludeFromCodeCoverage]
public static class DependencyInjectionExtensions
{
    private const string IntegrationEventQueue = "integration_event_queue_new";

    private static readonly string[] Actions = { "read", "write", "update", "delete" };

    public static void AddConsulKv(this IConfigurationBuilder builder, ConsulKVSettings settings)
    {
        builder.AddConsul(settings.Key, options =>
        {
            options.ConsulConfigurationOptions = config =>
            {
                config.Address = new Uri(settings.Url);
                config.Token = settings.Token;
            };

            options.Optional = false;
            options.ReloadOnChange = true;
        });
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("Default"));
        });

        services.AddPostgresHealthContributor(configuration);
    }

    private static void AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        Auth0Settings auth0Settings = new ();
        configuration.GetRequiredSection(nameof(Auth0Settings)).Bind(auth0Settings);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Authority = auth0Settings.Authority;
            options.Audience = auth0Settings.Audience;
        });

        services.AddAuthorization(options => { options.AddCrudPolicies("company"); });
        services.AddSingleton<IAuthorizationHandler, ScopeRequirementHandler>();
    }

    private static void AddApiDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Company API",
                Description = "Company Microservice",
            });

            string xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFileName));

            options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT Authorization header using the Bearer scheme.",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "bearer",
                        },
                    },
                    Array.Empty<string>()
                },
            });
        });
    }

    private static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
    }

    private static void AddTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        TelemetrySettings telemetrySettings = new ();
        configuration.GetRequiredSection(nameof(TelemetrySettings)).Bind(telemetrySettings);

        services.AddOpenTelemetry()
            .ConfigureResource(c =>
            {
                c.AddService(telemetrySettings.ServiceName, serviceVersion: telemetrySettings.ServiceVersion,
                    autoGenerateServiceInstanceId: true);
            })
            .WithTracing(b =>
                b.AddAspNetCoreInstrumentation()
                    .AddOtlpExporter(options => { options.Endpoint = new Uri(telemetrySettings.Endpoint); })
            );
    }

    private static void AddServiceDiscovery(this IServiceCollection services)
    {
        services.AddDiscoveryClient();

        services.AddHttpContextAccessor();
        services
            .AddHttpClient("projects")
            .AddServiceDiscovery()
            .ConfigureHttpClient((serviceProvider, options) =>
            {
                IHttpContextAccessor httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();

                if (httpContextAccessor.HttpContext == null)
                {
                    return;
                }

                string? bearerToken = httpContextAccessor.HttpContext.Request.Headers["Authorization"]
                    .FirstOrDefault(h =>
                        !string.IsNullOrEmpty(h) &&
                        h.StartsWith("bearer ", StringComparison.InvariantCultureIgnoreCase));

                if (!string.IsNullOrEmpty(bearerToken))
                {
                    options.DefaultRequestHeaders.Add("Authorization", bearerToken);
                }
            })
            .AddTypedClient<IProjectService, ProjectService>();
    }

    private static void AddActuators(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthActuator(configuration);
        services.AddInfoActuator(configuration);
        services.AddHealthChecks();
        services.AddRefreshActuator();
        services.ActivateActuatorEndpoints();
    }

    private static void AddSteeltoeRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMQConnection(configuration);

        // Add Steeltoe Rabbit services, use JSON serialization
        services.AddRabbitServices(true);

        // Add Steeltoe RabbitAdmin services to get queues declared
        services.AddRabbitAdmin();

        // Add Steeltoe RabbitTemplate for sending/receiving
        services.AddRabbitTemplate();

        // Add a queue to the message container that the rabbit admin will discover and declare at startup
        services.AddRabbitQueue(new Queue(IntegrationEventQueue));

        services.AddSingleton<IMessagePublisher, RabbitMQMessagePublisher>();
    }

    private static void AddCrudPolicies(this AuthorizationOptions options, string resource)
    {
        foreach (string action in Actions)
        {
            options.AddPolicy($"{action}:{resource}",
                policy => policy.Requirements.Add(new ScopeRequirement($"{action}:{resource}")));
        }
    }

    public static void RegisterDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddActuators(configuration);
        services.AddApiDocumentation();
        services.AddApplicationServices();
        services.AddAutoMapper(typeof(CompanyProfile));
        services.AddControllers();
        services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddPersistence(configuration);
        services.AddSecurity(configuration);
        services.AddServiceDiscovery();
        services.AddSteeltoeRabbitMq(configuration);
        services.AddTelemetry(configuration);
        services.AddValidatorsFromAssemblyContaining(typeof(Program));

        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy => { policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); });
        });
    }
}
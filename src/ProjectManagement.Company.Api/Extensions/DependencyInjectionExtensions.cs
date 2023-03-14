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
using Steeltoe.Discovery.Client;
using Winton.Extensions.Configuration.Consul;

namespace ProjectManagement.CompanyAPI.Extensions;

public static class DependencyInjectionExtensions
{
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
        PersistenceSettings persistenceSettings = new () { ConnectionString = string.Empty };
        configuration.GetRequiredSection(nameof(PersistenceSettings)).Bind(persistenceSettings);
        
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped(typeof(IReadRepository<>), typeof(EfRepository<>));
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(persistenceSettings.ConnectionString);
        });
    }

    private static void AddSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Authority = "https://afrozeprojectmanagement.us.auth0.com/";
            options.Audience = "company";
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
    
    public static void RegisterDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApiDocumentation();
        services.AddApplicationServices();
        services.AddAutoMapper(typeof(CompanyProfile));
        services.AddControllers();
        services.AddDiscoveryClient();
        services.AddMediatR(options => options.RegisterServicesFromAssembly(typeof(Program).Assembly));
        services.AddPersistence(configuration);
        services.AddSecurity();
        services.AddTelemetry(configuration);
        services.AddValidatorsFromAssemblyContaining(typeof(Program));
    }

    private static readonly string[] _actions = { "read", "write", "update", "delete" };

    private static void AddCrudPolicies(this AuthorizationOptions options, string resource)
    {
        foreach (string action in _actions)
        {
            options.AddPolicy($"{action}:{resource}",
                policy => policy.Requirements.Add(new ScopeRequirement($"{action}:{resource}")));
        }
    }
}
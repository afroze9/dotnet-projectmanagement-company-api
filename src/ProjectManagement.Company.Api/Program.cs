﻿using ProjectManagement.CompanyAPI.Extensions;
using Serilog;

namespace ProjectManagement.CompanyAPI;

[ExcludeFromCodeCoverage]
public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddApplicationConfiguration();
        builder.Logging.AddApplicationLogging(builder.Configuration);
        builder.Services.RegisterDependencies(builder.Configuration);

        WebApplication app = builder.Build();
        app.Configure().Run();

        Log.CloseAndFlush();
    }
}

[ExcludeFromCodeCoverage]
public static class AppConfigurationExtensions
{
    public static WebApplication Configure(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        return app;
    }
}

using System.Reflection;
using Microsoft.OpenApi.Models;
using ProjectManagement.CompanyAPI.Configuration;
using ProjectManagement.CompanyAPI.Extensions;
using ProjectManagement.CompanyAPI.Mapping;
using Steeltoe.Discovery.Client;

namespace ProjectManagement.CompanyAPI;

public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Settings for docker
        builder.Configuration.AddJsonFile("hostsettings.json", true);

        // Settings for consul kv
        ConsulKVSettings consulKvSettings = new ();
        builder.Configuration.GetRequiredSection("ConsulKV").Bind(consulKvSettings);
        builder.Configuration.AddConsulKV(consulKvSettings);

        ApplicationSettings applicationSettings = new () { ConnectionString = string.Empty };
        builder.Configuration.GetRequiredSection("ApplicationSettings").Bind(applicationSettings);

        // Add services to the container.
        builder.Services.AddDiscoveryClient();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Version = "v1",
                Title = "Company API",
                Description = "Company Microservice",
            });

            string xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFileName));
        });

        builder.Services.AddAutoMapper(typeof(CompanyProfile));

        builder.Services.AddServices(applicationSettings);
        
        WebApplication app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
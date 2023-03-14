﻿using ProjectManagement.CompanyAPI.Configuration;

namespace ProjectManagement.CompanyAPI.Extensions;

public static class ConfigurationExtensions
{
    public static void AddApplicationConfiguration(this ConfigurationManager configuration)
    {
        // Settings for docker
        configuration.AddJsonFile("hostsettings.json", true);

        // Settings for consul kv
        ConsulKVSettings consulKvSettings = new ();
        configuration.GetRequiredSection("ConsulKV").Bind(consulKvSettings);
        configuration.AddConsulKv(consulKvSettings);
    }
}
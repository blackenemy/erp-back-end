module.exports = {
  apps: [
    {
      name: "hubs-backend-rule",
      script: "dotnet",
      args: "RuleService.dll",
      cwd: "/var/www/hubs-backend/RuleService",
      env: {
        ASPNETCORE_ENVIRONMENT: "Production",
        ASPNETCORE_URLS: "http://+:5002"
      },
      instances: 1,
      exec_mode: "cluster"
    },
    {
      name: "hubs-backend-pricing",
      script: "dotnet",
      args: "PricingService.dll",
      cwd: "/var/www/hubs-backend/PricingService",
      env: {
        ASPNETCORE_ENVIRONMENT: "Production",
        ASPNETCORE_URLS: "http://+:5001",
        RuleServiceUrl: "http://localhost:5002"
      },
      instances: 1,
      exec_mode: "cluster"
    }
  ]
};

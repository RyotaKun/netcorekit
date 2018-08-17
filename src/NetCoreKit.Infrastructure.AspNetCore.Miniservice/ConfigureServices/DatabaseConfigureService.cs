using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Logging;
using NetCoreKit.Infrastructure.AspNetCore.Miniservice.Options;
using NetCoreKit.Infrastructure.EfCore.Db;
using NetCoreKit.Infrastructure.EfCore.Extensions;

namespace NetCoreKit.Infrastructure.AspNetCore.Miniservice.ConfigureServices
{
  public class DatabaseConfigureService : IDbConfigureServices
  {
    public int Priority { get; } = 100;

    public void Configure<TDbContext>(IServiceCollection services) where TDbContext : DbContext
    {
      IdentityModelEventSource.ShowPII = true;

      var svcProvider = services.BuildServiceProvider();
      var config = svcProvider.GetRequiredService<IConfiguration>();

      if (!config.GetValue<bool>("SqlDatabase:Enabled"))
      {
        return;
      }

      var serviceParams = svcProvider.GetRequiredService<ServiceParams>();
      var extendOptionsBuilder = svcProvider.GetRequiredService<IExtendDbContextOptionsBuilder>();
      var connStringFactory = svcProvider.GetRequiredService<IDatabaseConnectionStringFactory>();

      //TODO: refactor it
      var assemblies = serviceParams["assemblies"] as HashSet<Assembly>;
      var fisrtAssembly = assemblies?.FirstOrDefault();

      services.AddOptions()
        .Configure<PersistenceOptions>(config.GetSection("EfCore"));

      void OptionsBuilderAction(DbContextOptionsBuilder o)
      {
        extendOptionsBuilder.Extend(o, connStringFactory, fisrtAssembly?.GetName().Name);
      }

      services.AddDbContextPool<TDbContext>(OptionsBuilderAction);
      services.AddSingleton<TDbContext>();
      services.AddSingleton<DbContext>(resolver => resolver.GetRequiredService<TDbContext>());
      services.AddEfCore();

      // healthcheck and migration automatically
      services.AddSingleton<DbHealthCheckAndMigration, DbHealthCheckAndMigration>();
    }
  }

  public class DbHealthCheckAndMigration : IExternalSystem
  {
    private readonly IServiceProvider _svcProvider;

    public DbHealthCheckAndMigration(IServiceProvider svcProvider)
    {
      _svcProvider = svcProvider;
    }

    public Task<bool> Connect()
    {
      return Task.Run(() => _svcProvider.MigrateDbContext() != null);
    }
  }
}

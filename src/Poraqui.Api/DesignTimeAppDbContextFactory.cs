using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Api;

public sealed class DesignTimeAppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddUserSecrets<Program>(optional: true);
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
            // Used only by migration scaffolding when private configuration is absent. It is not a shared DB.
            ?? "Server=localhost;Database=PoraquiDesignOnly;Integrated Security=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}

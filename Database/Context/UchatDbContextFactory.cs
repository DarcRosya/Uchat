using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Uchat.Database.Context;

public class UchatDbContextFactory : IDesignTimeDbContextFactory<UchatDbContext>
{
    public UchatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UchatDbContext>();
        
        // Попытка получить connection string из переменной окружения
        var connectionString = Environment.GetEnvironmentVariable("UCHAT_CONNECTION_STRING");
        
        if (!string.IsNullOrEmpty(connectionString))
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=uchat;Username=uchat;Password=uchat123");
        }
        
        return new UchatDbContext(optionsBuilder.Options);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;

namespace Uchat.Database.Context;

public class UchatDbContextFactory : IDesignTimeDbContextFactory<UchatDbContext>
{
    public UchatDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UchatDbContext>();
        
        // PostgreSQL из Docker для всех
        optionsBuilder.UseNpgsql("Host=localhost;Port=5433;Database=uchat;Username=uchat;Password=uchat123");
        
        return new UchatDbContext(optionsBuilder.Options);
    }
}

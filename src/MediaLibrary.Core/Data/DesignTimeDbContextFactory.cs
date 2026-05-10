using Microsoft.EntityFrameworkCore.Design;

namespace MediaLibrary.Core.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        return new AppDbContext(AppDbContextOptionsFactory.Create());
    }
}

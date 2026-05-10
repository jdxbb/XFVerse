using MediaLibrary.Core.Helpers;
using Microsoft.EntityFrameworkCore;

namespace MediaLibrary.Core.Data;

public static class AppDbContextOptionsFactory
{
    public static DbContextOptions<AppDbContext> Create(string? databaseFilePath = null)
    {
        var resolvedPath = databaseFilePath ?? AppPaths.GetDatabaseFilePath();
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlite($"Data Source={resolvedPath}");
        return optionsBuilder.Options;
    }
}

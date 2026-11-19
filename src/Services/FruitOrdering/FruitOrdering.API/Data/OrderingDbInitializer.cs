namespace FruitOrdering.API.Data;

public static class OrderingDbInitializer
{
    public static async Task InitializeAsync(
        OrderingDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("Ordering database schema verified.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Concurrent EnsureCreatedAsync detected on ordering_db; continuing startup.");
        }
    }
}

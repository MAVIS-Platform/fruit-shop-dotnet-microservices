using FruitBasket.API.Models;
using FruitBasket.API.Repositories;

namespace FruitBasket.API.Services;

public sealed class BasketService(
    IBasketRepository basketRepository,
    ILogger<BasketService> logger) : IBasketService
{
    public async Task<CustomerBasket> GetBasketAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var basket = await basketRepository.GetBasketAsync(customerId, cancellationToken);
        return basket ?? new CustomerBasket(customerId, []);
    }

    public async Task<CustomerBasket> UpdateBasketAsync(
        string customerId,
        UpdateBasketRequest request,
        CancellationToken cancellationToken = default)
    {
        var items = request.Items
            .Select(i => new BasketItem(i.ProductId, i.ProductName.Trim(), i.UnitPrice, i.Quantity, i.Unit.Trim()))
            .ToList();

        var basket = new CustomerBasket(customerId, items);
        var updated = await basketRepository.UpdateBasketAsync(basket, cancellationToken);

        logger.LogInformation("Updated basket for customer {CustomerId} with {ItemCount} items.", customerId, items.Count);
        return updated;
    }

    public async Task DeleteBasketAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await basketRepository.DeleteBasketAsync(customerId, cancellationToken);
        logger.LogInformation("Deleted basket for customer {CustomerId}.", customerId);
    }
}

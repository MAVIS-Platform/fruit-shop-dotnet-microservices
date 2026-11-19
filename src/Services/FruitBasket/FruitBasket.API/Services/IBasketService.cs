using FruitBasket.API.Models;

namespace FruitBasket.API.Services;

public interface IBasketService
{
    Task<CustomerBasket> GetBasketAsync(string customerId, CancellationToken cancellationToken = default);

    Task<CustomerBasket> UpdateBasketAsync(
        string customerId,
        UpdateBasketRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteBasketAsync(string customerId, CancellationToken cancellationToken = default);
}

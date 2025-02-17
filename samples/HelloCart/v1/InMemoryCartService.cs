using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Fusion;

namespace Samples.HelloCart.V1
{
    public class InMemoryCartService : ICartService
    {
        private readonly ConcurrentDictionary<string, Cart> _carts = new();
        private readonly IProductService _products;

        public InMemoryCartService(IProductService products) => _products = products;

        public virtual Task Edit(EditCommand<Cart> command, CancellationToken cancellationToken = default)
        {
            var (cartId, cart) = command;
            if (string.IsNullOrEmpty(cartId))
                throw new ArgumentOutOfRangeException(nameof(command));
            if (Computed.IsInvalidating()) {
                _ = TryGet(cartId, default);
                return Task.CompletedTask;
            }

            if (cart == null)
                _carts.Remove(cartId, out _);
            else
                _carts[cartId] = cart;
            return Task.CompletedTask;
        }

        public virtual Task<Cart?> TryGet(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_carts.GetValueOrDefault(id));

        public virtual async Task<decimal> GetTotal(string id, CancellationToken cancellationToken = default)
        {
            var cart = await TryGet(id, cancellationToken);
            if (cart == null)
                return 0;
            var total = 0M;
            foreach (var (productId, quantity) in cart.Items) {
                var product = await _products.TryGet(productId, cancellationToken);
                total += (product?.Price ?? 0M) * quantity;
            }
            return total;
        }
    }
}

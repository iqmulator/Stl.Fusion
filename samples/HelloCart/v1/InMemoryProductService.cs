using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Fusion;

namespace Samples.HelloCart.V1
{
    public class InMemoryProductService : IProductService
    {
        private readonly ConcurrentDictionary<string, Product> _products = new();

        public virtual Task Edit(EditCommand<Product> command, CancellationToken cancellationToken = default)
        {
            var (productId, product) = command;
            if (string.IsNullOrEmpty(productId))
                throw new ArgumentOutOfRangeException(nameof(command));
            if (Computed.IsInvalidating()) {
                _ = TryGet(productId, default);
                return Task.CompletedTask;
            }

            if (product == null)
                _products.Remove(productId, out _);
            else
                _products[productId] = product;
            return Task.CompletedTask;
        }

        public virtual Task<Product?> TryGet(string id, CancellationToken cancellationToken = default)
            => Task.FromResult(_products.GetValueOrDefault(id));
    }
}

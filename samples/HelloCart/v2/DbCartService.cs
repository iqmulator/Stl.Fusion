using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Stl.Async;
using Stl.Fusion;
using Stl.Fusion.EntityFramework;

namespace Samples.HelloCart.V2
{
    public class DbCartService : DbServiceBase<AppDbContext>, ICartService
    {
        private readonly IProductService _products;

        public DbCartService(IServiceProvider services, IProductService products)
            : base(services)
            => _products = products;

        public virtual async Task Edit(EditCommand<Cart> command, CancellationToken cancellationToken = default)
        {
            var (cartId, cart) = command;
            if (string.IsNullOrEmpty(cartId))
                throw new ArgumentOutOfRangeException(nameof(command));
            if (Computed.IsInvalidating()) {
                _ = TryGet(cartId, default);
                return;
            }

            await using var dbContext = await CreateCommandDbContext(cancellationToken);
            var dbCart = await dbContext.Carts.FindAsync(ComposeKey(cartId), cancellationToken);
            if (cart == null) {
                if (dbCart != null)
                    dbContext.Remove(dbCart);
            }
            else {
                if (dbCart != null) {
                    await dbContext.Entry(dbCart).Collection(c => c.Items).LoadAsync(cancellationToken);
                    // Removing what doesn't exist in cart.Items
                    dbCart.Items.RemoveAll(i => !cart.Items.ContainsKey(i.DbProductId));
                    // Updating the ones that exist in both collections
                    foreach (var dbCartItem in dbCart.Items)
                        dbCartItem.Quantity = cart.Items[dbCartItem.DbProductId];
                    // Adding the new ones
                    var existingProductIds = dbCart.Items.Select(i => i.DbProductId).ToHashSet();
                    foreach (var item in cart.Items.Where(i => !existingProductIds.Contains(i.Key))) {
                        var dbCartItem = new DbCartItem() {
                            DbCartId = cartId,
                            DbProductId = item.Key,
                            Quantity = item.Value,
                        };
                        dbCart.Items.Add(dbCartItem);
                    }
                }
                else
                    dbContext.Add(new DbCart() {
                        Id = cartId,
                        Items = cart.Items.Select(i => new DbCartItem() {
                            DbCartId = cartId,
                            DbProductId = i.Key,
                            Quantity = i.Value,
                        }).ToList(),
                    });
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        public virtual async Task<Cart?> TryGet(string id, CancellationToken cancellationToken = default)
        {
            await using var dbContext = CreateDbContext();
            dbContext.EnableChangeTracking(); // Otherwise LoadAsync below won't work
            var dbCart = await dbContext.Carts.FindAsync(ComposeKey(id), cancellationToken);
            if (dbCart == null)
                return null;
            await dbContext.Entry(dbCart).Collection(c => c.Items).LoadAsync(cancellationToken);
            return new Cart() {
                Id = dbCart.Id,
                Items = dbCart.Items.ToImmutableDictionary(i => i.DbProductId, i => i.Quantity),
            };
        }

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

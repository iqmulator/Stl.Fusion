using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stl.Fusion.Operations;

namespace Stl.Fusion.EntityFramework.Operations
{
    public interface IDbOperationLog<in TDbContext>
        where TDbContext : DbContext
    {
        DbOperation New(string? id = null, string? agentId = null, object? command = null);
        Task<DbOperation> Add(TDbContext dbContext, IOperation operation, CancellationToken cancellationToken);
        Task<DbOperation?> TryGet(TDbContext dbContext, string id, CancellationToken cancellationToken);

        Task<List<DbOperation>> ListNewlyCommitted(DateTime minCommitTime, int maxCount, CancellationToken cancellationToken);
        Task<int> Trim(DateTime minCommitTime, int maxCount, CancellationToken cancellationToken);
    }

    public class DbOperationLog<TDbContext, TDbOperation> : DbServiceBase<TDbContext>, IDbOperationLog<TDbContext>
        where TDbContext : DbContext
        where TDbOperation : DbOperation, new()
    {
        protected AgentInfo AgentInfo { get; }

        public DbOperationLog(IServiceProvider services)
            : base(services)
        {
            AgentInfo = services.GetRequiredService<AgentInfo>();
        }

        public DbOperation New(string? id = null, string? agentId = null, object? command = null)
            => new TDbOperation() {
                Id = id ?? Ulid.NewUlid().ToString(),
                AgentId = agentId ?? AgentInfo.Id,
                StartTime = Clocks.SystemClock.Now,
                Command = command,
            };

        public virtual async Task<DbOperation> Add(TDbContext dbContext,
            IOperation operation, CancellationToken cancellationToken)
        {
            // dbContext shouldn't use tracking!
            var dbOperation = (TDbOperation) operation;
            await dbContext.AddAsync((object) dbOperation, cancellationToken).ConfigureAwait(false);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return dbOperation;
        }

        public virtual async Task<DbOperation?> TryGet(TDbContext dbContext,
            string id, CancellationToken cancellationToken)
        {
            // dbContext shouldn't use tracking!
            var dbOperation = await dbContext.Set<TDbOperation>().AsQueryable()
                .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
                .ConfigureAwait(false);
            return dbOperation;
        }

        public virtual async Task<List<DbOperation>> ListNewlyCommitted(
            DateTime minCommitTime, int maxCount, CancellationToken cancellationToken)
        {
            await using var dbContext = CreateDbContext();
            var operations = await dbContext.Set<TDbOperation>().AsQueryable()
                .Where(o => o.CommitTime >= minCommitTime)
                .OrderBy(o => o.CommitTime)
                .Take(maxCount)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            return operations.Cast<DbOperation>().ToList()!;
        }

        public virtual async Task<int> Trim(DateTime minCommitTime, int maxCount, CancellationToken cancellationToken)
        {
            await using var dbContext = CreateDbContext(true);
            dbContext.DisableChangeTracking();
            var operations = await dbContext.Set<TDbOperation>().AsQueryable()
                .Where(o => o.CommitTime < minCommitTime)
                .OrderBy(o => o.CommitTime)
                .Take(maxCount)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            if (operations.Count == 0)
                return 0;
            foreach (var operation in operations)
                dbContext.Remove(operation);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return operations.Count;
        }
    }
}

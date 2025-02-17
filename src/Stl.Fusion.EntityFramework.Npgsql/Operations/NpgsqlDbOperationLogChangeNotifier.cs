using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Stl.Async;
using Stl.CommandR;
using Stl.Fusion.Operations;
using Stl.Locking;
using Errors = Stl.Internal.Errors;

namespace Stl.Fusion.EntityFramework.Npgsql.Operations
{
    public class NpgsqlDbOperationLogChangeNotifier<TDbContext> : DbServiceBase<TDbContext>,
        IOperationCompletionListener, IDisposable
        where TDbContext : DbContext
    {
        public NpgsqlDbOperationLogChangeTrackingOptions<TDbContext> Options { get; }
        protected AgentInfo AgentInfo { get; }
        protected TDbContext? DbContext { get; set; }
        protected AsyncLock AsyncLock { get; }
        protected bool IsDisposed { get; set; }

        public NpgsqlDbOperationLogChangeNotifier(
            NpgsqlDbOperationLogChangeTrackingOptions<TDbContext> options,
            AgentInfo agentInfo,
            IServiceProvider services)
            : base(services)
        {
            Options = options;
            AgentInfo = agentInfo;
            AsyncLock = new AsyncLock(ReentryMode.CheckedFail);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (IsDisposed || !disposing)
                return;
            IsDisposed = true;
            using var suppressing = ExecutionContextExt.SuppressFlow();
            _ = Task.Run(async () => {
                using (await AsyncLock.Lock()) {
                    var dbContext = DbContext;
                    if (dbContext != null)
                        await dbContext.DisposeAsync();
                }
            });
        }

        public Task OnOperationCompleted(IOperation operation)
        {
            if (operation.AgentId != AgentInfo.Id.Value) // Only local commands require notification
                return Task.CompletedTask;
            var commandContext = CommandContext.Current;
            if (commandContext != null) { // It's a command
                var operationScope = commandContext.Items.TryGet<DbOperationScope<TDbContext>>();
                if (operationScope == null || !operationScope.IsUsed) // But it didn't change anything related to TDbContext
                    return Task.CompletedTask;
            }
            // If it wasn't command, we pessimistically assume it changed something
            using var _ = ExecutionContextExt.SuppressFlow();
            Task.Run(Notify);
            return Task.CompletedTask;
        }

        // Protected methods

        protected virtual async Task Notify()
        {
            var qPayload = AgentInfo.Id.Value.Replace("'", "''");
            TDbContext? dbContext = null;
            while (true) {
                try {
                    using (await AsyncLock.Lock()) {
                        if (IsDisposed)
                            return;
                        dbContext = DbContext ??= CreateDbContext();
                        await dbContext.Database
                            .ExecuteSqlRawAsync($"NOTIFY {Options.ChannelName}, '{qPayload}'")
                            .ConfigureAwait(false);
                    }
                    return;
                }
                catch (Exception e) {
                    Log.LogError(e, "Notification failed - retrying");
                    DbContext = null;
                    _ = dbContext?.DisposeAsync(); // Doesn't matter if it fails
                    await Clocks.CoarseCpuClock.Delay(Options.RetryDelay).ConfigureAwait(false);
                }
            }
        }
    }
}

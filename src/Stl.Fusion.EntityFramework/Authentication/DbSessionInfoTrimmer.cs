using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Stl.Fusion.EntityFramework.Authentication
{
    public abstract class DbSessionInfoTrimmer<TDbContext> : DbWakeSleepProcessBase<TDbContext>
        where TDbContext : DbContext
    {
        public class Options
        {
            public TimeSpan CheckInterval { get; set; } = TimeSpan.FromHours(1);
            public TimeSpan MaxSessionAge { get; set; } = TimeSpan.FromDays(60);
            public int BatchSize { get; set; } = 1000;
            public bool IsLoggingEnabled { get; set; } = true;
        }

        protected TimeSpan CheckInterval { get; init; }
        protected TimeSpan MaxSessionAge { get; init; }
        protected int BatchSize { get; init; }
        protected Random Random { get; init; }

        protected int LastTrimCount { get; set; }
        protected bool IsLoggingEnabled { get; set; }
        protected LogLevel LogLevel { get; set; } = LogLevel.Information;

        public DbSessionInfoTrimmer(Options? options, IServiceProvider services)
            : base(services)
        {
            options ??= new();
            IsLoggingEnabled = options.IsLoggingEnabled && Log.IsEnabled(LogLevel);

            CheckInterval = options.CheckInterval;
            MaxSessionAge = options.MaxSessionAge;
            BatchSize = options.BatchSize;
            Random = new Random();
        }
    }

    public class DbSessionInfoTrimmer<TDbContext, TDbSessionInfo, TDbUserId> : DbSessionInfoTrimmer<TDbContext>
        where TDbContext : DbContext
        where TDbSessionInfo : DbSessionInfo<TDbUserId>, new()
        where TDbUserId : notnull
    {
        protected IDbSessionInfoRepo<TDbContext, TDbSessionInfo, TDbUserId> Sessions { get; }

        public DbSessionInfoTrimmer(Options? options, IServiceProvider services)
            : base(options ??= new(), services)
            => Sessions = services.GetRequiredService<IDbSessionInfoRepo<TDbContext, TDbSessionInfo, TDbUserId>>();

        protected override async Task WakeUp(CancellationToken cancellationToken)
        {
            var minLastSeenAt = (Clocks.SystemClock.Now - MaxSessionAge).ToDateTime();
            LastTrimCount = await Sessions
                .Trim(minLastSeenAt, BatchSize, cancellationToken)
                .ConfigureAwait(false);

            if (LastTrimCount > 0 && IsLoggingEnabled)
                Log.Log(LogLevel, "Trimmed {Count} sessions", LastTrimCount);
        }

        protected override Task Sleep(Exception? error, CancellationToken cancellationToken)
        {
            var delay = default(TimeSpan);
            if (error != null)
                delay = TimeSpan.FromMilliseconds(1000 * Random.NextDouble());
            else if (LastTrimCount < BatchSize)
                delay = CheckInterval + TimeSpan.FromMilliseconds(100 * Random.NextDouble());
            return Clocks.CoarseCpuClock.Delay(delay, cancellationToken);
        }
    }
}

using System;

namespace DistilleryDiscovery
{
    public interface ITimeProvider { DateTime UtcNow { get; } }

    public sealed class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    public sealed class AdjustableTimeProvider : ITimeProvider
    {
        private readonly ITimeProvider source;
        private TimeSpan offset;
        public AdjustableTimeProvider(ITimeProvider source = null) => this.source = source ?? new SystemTimeProvider();
        public DateTime UtcNow => source.UtcNow.Add(offset);
        public void Advance(TimeSpan duration) => offset = offset.Add(duration);
    }
}

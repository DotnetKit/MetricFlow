namespace DotnetKit.MetricFlow.Core.Abstractions
{
    public abstract class CounterBase : ICounter
    {
        private long _inCount = 0;
        private long _outCount = 0;
        private long _totalDuration = 0;

        private long _maxDuration = 0;
        private long _minDuration = 0;
        private long _averageDuration = 0;

        public long InCount => _inCount;
        public long OutCount => _outCount;

        public abstract void Start();

        public abstract long Stop();

        public virtual long Inc()
        {
            Start();
            return Interlocked.Increment(ref _inCount);
        }

        public virtual long Dec()
        {
            UpdateState(Stop());
            return Interlocked.Increment(ref _outCount);
        }

        public TimeSpan TotalDuration => new TimeSpan(_totalDuration);

        public TimeSpan AverageDuration => new TimeSpan(_averageDuration);

        public TimeSpan MinDuration => new TimeSpan(_minDuration);

        public TimeSpan MaxDuration => new TimeSpan(_maxDuration);

        protected void UpdateState(long duration)
        {
            Interlocked.Exchange(ref _totalDuration, _totalDuration + duration);
            if (_maxDuration < duration)
            {
                Interlocked.Exchange(ref _maxDuration, duration);
            }
            if (_minDuration == 0 || _minDuration > duration)
            {
                Interlocked.Exchange(ref _minDuration, duration);
            }
            if (_averageDuration != 0)
            {
                Interlocked.Exchange(ref _averageDuration, (_averageDuration + duration) / 2);
            }
            else
            {
                Interlocked.Exchange(ref _averageDuration, duration);
            }
        }
    }
}
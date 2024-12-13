namespace CustomCounters
{
    public class UtcCounter : CounterBase
    {
        private long _inTicks = 0;
        private long _outTicks = 0;


        public override void Start()
        {
            Interlocked.Exchange(ref _inTicks, DateTimeOffset.UtcNow.Ticks);
            Interlocked.Exchange(ref _outTicks, DateTimeOffset.UtcNow.Ticks);
        }
        public override long Stop()
        {
            return _outTicks - Interlocked.Exchange(ref _outTicks, DateTimeOffset.UtcNow.Ticks);

        }

    }
}
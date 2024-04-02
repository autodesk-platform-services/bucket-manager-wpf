using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using bucket.manager.wpf.ViewModels;

namespace bucket.manager.wpf.Utils
{
    // Timer provided by .NET isn't very precise by default, write one with stopwatch.
    class HighPrecisionTimer : IDisposable
    {
        public class TickEvent : EventArgs
        {
            public long Total;
            public TickEvent(long total) { Total = total; }
        }

        public event EventHandler<TickEvent>? Elapsed;
        public long Interval = 100;
        public BucketItemVM? BucketItem;
        protected CancellationTokenSource CancellationSource = new();

        public void Start()
        {
            CancellationSource.Cancel();
            CancellationSource = new CancellationTokenSource();
            var stopWatch = Stopwatch.StartNew();
            long leftTicks = Interval;
            var task = new Task(() =>
            {
                var total = 0L;
                while (!CancellationSource.IsCancellationRequested)
                {
                    var timePassed = leftTicks - stopWatch.ElapsedMilliseconds;

                    Console.WriteLine(DateTime.Now);
                    if (timePassed <= 0)
                    {
                        if (Elapsed != null)
                        {
                            Elapsed.Invoke(this, new TickEvent(leftTicks));
                        }
                        // Compensate for next run
                        total += stopWatch.ElapsedMilliseconds;
                        leftTicks = Interval + timePassed;
                        stopWatch.Restart();
                    }

                    Thread.Sleep(1);
                }
            }, CancellationSource.Token, TaskCreationOptions.LongRunning);
            task.Start();
        }

        public void Stop()
        {
            CancellationSource.Cancel();
        }

        public void Dispose()
        {
            CancellationSource.Cancel();
        }
    }
}

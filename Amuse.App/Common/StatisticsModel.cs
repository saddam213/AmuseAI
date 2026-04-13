using System;
using System.Diagnostics;
using System.Windows.Threading;
using TensorStack.Python.Common;
using TensorStack.WPF;

namespace Amuse.App.Common
{
    public class StatisticsModel : BaseModel
    {
        private readonly DispatcherTimer _dispatcherTimer;
        private float _iterationsPerSecond;
        private float _secondsPerIteration;
        private long _timestamp;
        private TimeSpan _elapsed;

        public StatisticsModel(Dispatcher dispatcher)
        {
            _dispatcherTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(200), DispatcherPriority.Background, UpdateProgress, dispatcher);
            _dispatcherTimer.Stop();
        }

        public void Start()
        {
            _timestamp = Stopwatch.GetTimestamp();
            _dispatcherTimer.Start();
        }

        public void Stop()
        {
            _dispatcherTimer.Stop();
            Elapsed = Stopwatch.GetElapsedTime(_timestamp);
        }

        public void Clear()
        {
            Stop();
            _timestamp = 0;
            IterationsPerSecond = 0;
            SecondsPerIteration = 0;
            Elapsed = TimeSpan.Zero;
        }

        public void Update(PipelineProgress progress)
        {
            IterationsPerSecond = progress.IterationsPerSecond;
            SecondsPerIteration = progress.SecondsPerIteration;
        }

        public TimeSpan Elapsed
        {
            get { return _elapsed; }
            set { SetProperty(ref _elapsed, value); }
        }

        public float IterationsPerSecond
        {
            get { return _iterationsPerSecond; }
            set { SetProperty(ref _iterationsPerSecond, value); }
        }

        public float SecondsPerIteration
        {
            get { return _secondsPerIteration; }
            set { SetProperty(ref _secondsPerIteration, value); }
        }


        private void UpdateProgress(object sender, EventArgs e)
        {
            if (_timestamp == 0)
                return; ;

            Elapsed = Stopwatch.GetElapsedTime(_timestamp);
        }
    }
}

using System.Timers;

namespace Agent.RV.Utils
{
    public class UpdateChecker
    {
        private Timer _updateChecker;
        private readonly double _interval;

        /// <summary>
        /// Checks for new updates at a set interval.
        /// 
        /// Defaults to 43,200,000 milliseconds (== 12 hours).
        /// </summary>
        /// <param name="interval"></param>
        public UpdateChecker(double interval = 43200000)
        {
            _interval = interval;
        }

        public void Enable(ElapsedEventHandler handler)
        {
            _updateChecker = new Timer(_interval);
            _updateChecker.Elapsed += handler;
            _updateChecker.Enabled = true;
        }
    }
}

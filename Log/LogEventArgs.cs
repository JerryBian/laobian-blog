using System;

namespace Laobian.Blog.Log
{
    public class LogEventArgs : EventArgs
    {
        public LogEventArgs(BlogLog log)
        {
            Log = log;
        }

        public BlogLog Log { get; }
    }
}

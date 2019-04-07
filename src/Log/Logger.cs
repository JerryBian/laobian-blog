using System;
using Laobian.Common.Blog;

namespace Laobian.Blog.Log
{
    public class Logger : ILogger
    {
        public void Visit(VisitLogCategory visitLogCategory, BlogLog log)
        {
            NewVisitLog?.Invoke(this, new VisitLogEventArgs(visitLogCategory, log));
        }

        public void Status(int statusCode, BlogLog log)
        {
            NewStatusLog?.Invoke(this, new StatusLogEventArgs(statusCode, log));
        }

        public event EventHandler<VisitLogEventArgs> NewVisitLog;
        public event EventHandler<StatusLogEventArgs> NewStatusLog;
    }
}

using System;
using Laobian.Common.Blog;

namespace Laobian.Blog.Log
{
    public interface ILogger
    {
        void Visit(VisitLogCategory visitLogCategory, BlogLog log);

        void Status(int statusCode, BlogLog log);

        event EventHandler<VisitLogEventArgs> NewVisitLog;

        event EventHandler<StatusLogEventArgs> NewStatusLog;
    }
}

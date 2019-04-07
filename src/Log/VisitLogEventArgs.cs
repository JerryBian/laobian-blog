using Laobian.Common.Blog;

namespace Laobian.Blog.Log
{
    public class VisitLogEventArgs : LogEventArgs
    {
        public VisitLogEventArgs(VisitLogCategory category, BlogLog log) : base(log)
        {
            Category = category;
        }

        public VisitLogCategory Category { get; }
    }
}

namespace Laobian.Blog.Log
{
    public class StatusLogEventArgs : LogEventArgs
    {
        public StatusLogEventArgs(int statusCode, BlogLog log) : base(log)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}

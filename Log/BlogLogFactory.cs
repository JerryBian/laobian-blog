using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;

namespace Laobian.Blog.Log
{
    public class BlogLogFactory
    {
        public static BlogLog Create(HttpRequest request)
        {
            var log = new BlogLog
            {
                RemoteIp = request.HttpContext.Connection.RemoteIpAddress.ToString(),
                Accept = request.Headers[HeaderNames.Accept].ToString(),
                AcceptLanguage = request.Headers[HeaderNames.AcceptLanguage].ToString(),
                Cookie = request.Headers[HeaderNames.Cookie].ToString(),
                Host = request.Headers[HeaderNames.Host].ToString(),
                Referer = request.Headers[HeaderNames.Referer].ToString(),
                UserAgent = request.Headers[HeaderNames.UserAgent].ToString(),
                Via = request.Headers[HeaderNames.Via].ToString(),
                FullUrl = request.GetDisplayUrl()
            };

            return log;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Laobian.Blog.Models;
using System.Threading.Tasks;
using System.Linq;
using System;
using System.Xml.Serialization;
using System.Collections.Generic;
using Laobian.Blog.Log;
using Laobian.Common.Base;
using Laobian.Common.Blog;
using Laobian.Common.Setting;

namespace Laobian.Blog.Controllers
{
    public class HomeController : Controller
    {
        private readonly IPostRepository _postRepository;
        private readonly ILogger _logger;

        public HomeController(IPostRepository postRepository, ILogger logger)
        {
            _postRepository = postRepository;
            _logger = logger;
        }

        public async Task<IActionResult> Index([FromQuery] int p)
        {
            const int pageSize = 8;
            var allPosts = await _postRepository.GetPostsAsync();
            var publishedPosts = allPosts.Where(ap => ap.Publish).OrderByDescending(ap => ap.PublishTime).ToList();
            var pagination = new Pagination(p, (int)Math.Ceiling(publishedPosts.Count() / (double)pageSize));
            var posts = publishedPosts.ToPaged(pageSize, pagination.CurrentPage);

            ViewData["robots"] = "index,follow,archive";
            ViewData["canonical"] = "/";

            if (pagination.CurrentPage > 1)
            {
                ViewData["Title"] = $"第{pagination.CurrentPage}页";
                ViewData["robots"] = "noindex,nofollow";
            }

            _logger.Visit(VisitLogCategory.Home, BlogLogFactory.Create(Request));
            return View(new PagedPostViewModel { Pagination = pagination, Posts = posts, Url = Request.Path });
        }

        [Route("{year:int}/{month:int}/{url}.html")]
        public async Task<IActionResult> Post(int year, int month, string url)
        {
            var posts = await _postRepository.GetPostsAsync();
            var post = posts.FirstOrDefault(ps => ps.PublishTime.Year == year && ps.PublishTime.Month == month && string.Equals(ps.Url, url, StringComparison.OrdinalIgnoreCase));
            if(post == null)
            {
                return NotFound();
            }

            ViewData["robots"] = "index,follow,archive";
            ViewData["canonical"] = post.GetFullUrl();
            ViewData["title"] = post.Title;

            var log = BlogLogFactory.Create(Request);
            log.Id = post.Id;
            _logger.Visit(VisitLogCategory.Post, log);
            return View(post);
        }

        [Route("/rss")]
        public async Task<IActionResult> Rss()
        {
            var allPosts = await _postRepository.GetPostsAsync();
            var publishedPosts = allPosts.Where(ap => ap.Publish).OrderByDescending(ap => ap.PublishTime).ToList();

            var rss = new RssRoot
            {
                Version = "2.0",
                Channel = new RssChannel
                {
                    Title = AppSetting.Default.BlogName,
                    Copyright = $"Copyright {DateTime.UtcNow.Year}, {AppSetting.Default.ChineseName}({AppSetting.Default.AdminFullName})",
                    Docs = "http://www.rssboard.org/rss-specification",
                    Language = "zh-cn",
                    Link = "https://blog.laobian.me",
                    PubDate = DateTime.UtcNow,
                    Category = new List<string> { "blog", "life", "博客" },
                    WebMaster = $"{AppSetting.Default.AdminEmail} ({AppSetting.Default.ChineseName})",
                    ManagingEditor = $"{AppSetting.Default.AdminEmail} ({AppSetting.Default.ChineseName})",
                    Ttl = 60,
                    LastBuildDate = DateTime.UtcNow,
                    Description = "个人博客，记录技术心得以及生活感悟。"
                }
            };
            foreach (var blogPost in publishedPosts)
            {
                var item = new ChannelItem
                {
                    Author = AppSetting.Default.AdminEmail,
                    Guid = blogPost.GetFullUrl(),
                    Link = blogPost.GetFullUrl(),
                    Description = MarkdownConverter.ToHtml(blogPost.Content),
                    Title = blogPost.Title,
                    PubDate = blogPost.PublishTime
                };

                rss.Channel.Items.Add(item);
            }

            var xml = SerializeToXml(rss);

            _logger.Visit(VisitLogCategory.Rss, BlogLogFactory.Create(Request));
            return Content(xml, "application/rss+xml");
        }

        [Route("/about")]
        public IActionResult About()
        {
            ViewData["robots"] = "index,follow,archive";
            ViewData["canonical"] = "/about";
            ViewData["title"] = "关于我";

            _logger.Visit(VisitLogCategory.About, BlogLogFactory.Create(Request));
            return View();
        }

        private static string SerializeToXml<T>(T obj)
        {
            using (var sw = new Utf8StringWriter())
            {
                var xmlNamespaces = new XmlSerializerNamespaces();
                xmlNamespaces.Add(string.Empty, string.Empty);
                var serializer = new XmlSerializer(obj.GetType());
                serializer.Serialize(sw, obj, xmlNamespaces);
                return sw.ToString();
            }
        }
    }
}

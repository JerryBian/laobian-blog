using Laobian.Blog.Models;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Laobian.Blog.TagHelpers
{
    public class PaginationTagHelper : TagHelper
    {
        private const string PaginationParameterName = "p";
        private const int MaxVisibleItems = 6;

        public Pagination Pagination { get; set; }

        public string Url { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.TagName = "nav";
            output.Attributes.SetAttribute("id", "pagination");

            // leave it empty while total pages equal to 1 or 0
            if (Pagination.TotalPages <= 1) return;

            var ul = "<ul class='pagination justify-content-center'>";
            ul = AddPreviousItem(ul);

            if(Pagination.CurrentPage == 1)
            {
                ul += GetActiveItem("1");
            }
            else
            {
                ul += GetLinkItem("1", Url);
            }

            for (var i = 2; i <= Pagination.TotalPages; i++)
            {
                if(Pagination.CurrentPage == i)
                {
                    ul += GetActiveItem(i.ToString());
                }
                else
                {
                    ul += GetLinkItem(i.ToString(), $"{Url}?{PaginationParameterName}={i}");
                }
            }

            // force display all items
            //if (Pagination.TotalPages <= MaxVisibleItems)
            //{
            //    for (var i = 1; i <= Pagination.TotalPages; i++)
            //        if (Pagination.CurrentPage == i)
            //            ul += $"<li class='page-item active'><span>{i}</span></li>";
            //        else
            //            ul +=
            //                $"<li class='page-item'><a class='page-link' href='{Url}?{PaginationParameterName}={i}'>{i}</a></li>";
            //}
            //else
            //{
            //    if (Pagination.CurrentPage < 5)
            //    {
            //        for (var i = 1; i <= 5; i++)
            //        {

            //        }
            //            if (Pagination.CurrentPage == i)
            //                ul += $"<li class='page-item active'><span>{i}</span></li>";
            //            else
            //                ul +=
            //                    $"<li class='page-item'><a class='page-link' href='{Url}?{PaginationParameterName}={i}'>{i}</a></li>";

            //        ul += "<li class=\'page-item\'><span>...</span></li>";
            //        ul +=
            //            $"<li class='page-item'><a class='page-link' href='{Url}?{PaginationParameterName}={Pagination.TotalPages}'>{Pagination.TotalPages}</a></li>";
            //    }
            //    else
            //    {
            //        ul +=
            //            $"<li class='page-item'><a class='page-link' href='{Url}?{PaginationParameterName}={1}'>{1}</a></li>";
            //        ul += "<li class=\'page-item\'><span>...</span></li>";
            //        for (var i = Pagination.CurrentPage - 2; i <= Pagination.CurrentPage + 2; i++)
            //            if (Pagination.CurrentPage == i)
            //                ul += $"<li class='page-item active'><span>{i}</span></li>";
            //            else
            //                ul +=
            //                    $"<li class='page-item'><a class='page-link' href='{Url}?{PaginationParameterName}={i}'>{i}</a></li>";

            //        ul += "<li class=\'page-item\'><span>...</span></li>";
            //        ul +=
            //            $"<li class='page-item'><a class='page-link' href='{Url}?{PaginationParameterName}={Pagination.TotalPages}'>{Pagination.TotalPages}</a></li>";
            //    }
            //}

            ul = AddNextItem(ul);

            ul += "</ul>";
            output.Content.SetHtmlContent(ul);
        }

        private string AddNextItem(string ul)
        {
            if (Pagination.CurrentPage != Pagination.TotalPages)
                ul += $@"<li class='page-item'>
        <a class='page-link' href='{Url}?{PaginationParameterName}={Pagination.CurrentPage + 1}' aria-label='Next'>
        <span aria-hidden='true'>&rarr;</span>
        <span class='sr-only'>&rarr;</span>
      </a>
    </li>";
            return ul;
        }

        private string AddPreviousItem(string ul)
        {
            if (Pagination.CurrentPage <= 1)
            {
                return ul;
            }

            // hide query parameter for previous page being 1
            if (Pagination.CurrentPage == 2)
            {
                ul += $@"<li class='page-item'>
        <a class='page-link' href='{Url}' aria-label='Previous'>
        <span aria-hidden='true'>&larr;</span>
        <span class='sr-only'>&larr;</span>
      </a>
    </li>";
                return ul;
            }

            ul += $@"<li class='page-item'>
        <a class='page-link' href='{Url}?{PaginationParameterName}={Pagination.CurrentPage - 1}' aria-label='Previous'>
        <span aria-hidden='true'>&larr;</span>
        <span class='sr-only'>&larr;</span>
      </a>
    </li>";
            return ul;
        }

        private string GetLinkItem(string name, string url)
        {
            return $"<li class='page-item'><a class='page-link' href='{url}'>{name}</a></li>";
        }

        private string GetActiveItem(string name)
        {
            return $"<li class='page-item active'><span>{name}</span></li>";
        }
    }
}
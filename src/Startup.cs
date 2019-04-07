using System;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Laobian.Blog.Log;
using Laobian.Common;
using Laobian.Common.Azure;
using Laobian.Common.Base;
using Laobian.Common.Blog;
using Laobian.Common.Cache;
using Laobian.Common.Notification;
using Laobian.Common.Setting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace Laobian.Blog
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(HtmlEncoder.Create(UnicodeRanges.BasicLatin, UnicodeRanges.CjkUnifiedIdeographs));
            services.AddSingleton<IAzureBlobClient, AzureBlobClient>();
            services.AddSingleton<ICacheClient, MemoryCacheClient>();
            services.AddSingleton<IPostRepository, PostRepository>();
            services.AddHostedService<VisitLogHostService>();
            services.AddHostedService<StatusLogHostService>();
            services.AddSingleton<IEmailEmitter, EmailEmitter>();
            services.AddSingleton<ILogger, Logger>();

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, IApplicationLifetime applicationLifetime)
        {
            var emailService = app.ApplicationServices.GetRequiredService<IEmailEmitter>();
            var logger = app.ApplicationServices.GetRequiredService<ILogger>();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    ExceptionHandler = async context =>
                    {
                        var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
                        var message = $"<p>URL: {context.Request.GetDisplayUrl()}</p> <p>IP: {context.Connection.RemoteIpAddress}</p> <p>Message: {errorFeature.Error.Message}</p>";
                        await emailService.EmitErrorAsync($"<div>{message}</div>", errorFeature.Error);
                        context.Response.ContentType = "text/plain";
                        await context.Response.WriteAsync($"Something was wrong! Please contact {AppSetting.Default.AdminEmail}.");
                    }
                });

                applicationLifetime.ApplicationStarted.Register(async () =>
                {
                    SystemState.StartupTime = DateTime.UtcNow;
                    const string msg = "Blog is started successfully.";
                    await emailService.EmitStatusAsync($"<p>{msg}</p>");
                });

                applicationLifetime.ApplicationStopping.Register(async () =>
                {
                    const string msg = "Blog is stopping...";
                    await emailService.EmitStatusAsync($"<p>{msg}</p>");
                });

                applicationLifetime.ApplicationStopping.Register(async () =>
                {
                    const string msg = "Blog is stopped successfully.";
                    await emailService.EmitStatusAsync($"<p>{msg}</p>");
                });
            }

            app.UseStatusCodePages(async context =>
            {
                var statusLog = BlogLogFactory.Create(context.HttpContext.Request);
                logger.Status(context.HttpContext.Response.StatusCode, statusLog);
                context.HttpContext.Response.ContentType = "text/plain";
                await context.HttpContext.Response.WriteAsync(
                    "Status code page, status code: " +
                    context.HttpContext.Response.StatusCode);
            });

            var provider = new FileExtensionContentTypeProvider();
            provider.Mappings[".webmanifest"] = "application/manifest+json";

            app.UseStaticFiles(new StaticFileOptions
            {
                ContentTypeProvider = provider,
                OnPrepareResponse = ctx =>
                {
                    const int durationInSeconds = 60 * 60 * 24 * 30;
                    ctx.Context.Response.Headers[HeaderNames.CacheControl] =
                        "public,max-age=" + durationInSeconds;
                }
            });

            app.UseMvcWithDefaultRoute();
        }
    }
}

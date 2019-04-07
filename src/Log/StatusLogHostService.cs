using Laobian.Common;
using Laobian.Common.Azure;
using Laobian.Common.Base;
using Laobian.Common.Notification;
using Laobian.Common.Setting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Laobian.Blog.Log
{
    public class StatusLogHostService : LogHostService
    {
        public StatusLogHostService(
            ILogger logger,
            IEmailEmitter emailEmitter,
            IAzureBlobClient azureClient) : base(logger, azureClient, emailEmitter)
        {
            Logger.NewStatusLog += (sender, args) =>
            {
                var statusCode = PrivateBlobResolver.Normalize($"{args.StatusCode}");
                Add($"{GetBaseContainerName()}/{statusCode}", args.Log);
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var logs = GetPendingLogs();
                var logsCount = logs.SelectMany(ls => ls.Value).Count();
                if (logsCount > 0 && (logsCount > AppSetting.Default.StatusLogBufferSize ||
                                      DateTime.UtcNow - LastFlushedAt > AppSetting.Default.StatusLogFlushInterval))
                {
                    try
                    {
                        var affectedLogs = await Flush();
                        await SendEmailAsync(affectedLogs);
                        SystemState.StatusLogsCount = GetStoredLogs().SelectMany(ls => ls.Value).Sum(s => s.Value);
                    }
                    catch (Exception ex)
                    {
                        await EmailEmitter.EmitErrorAsync($"<p>Status Log host service error: {ex.Message}</p>", ex);
                    }
                }


                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        protected override async Task InitAsync()
        {
            await base.InitAsync();
            SystemState.StatusLogsCount = GetStoredLogs().SelectMany(ls => ls.Value).Sum(s => s.Value);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitAsync();
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            var affectedLogs = await Flush();
            await SendEmailAsync(affectedLogs);
            await base.StopAsync(cancellationToken);
        }

        private async Task SendEmailAsync(ConcurrentDictionary<string, List<BlogLog>> logs)
        {
            var messages = new StringBuilder();
            foreach (var log in logs)
            {
                messages.AppendFormat($"<p><small>STATUS: {log.Key}, COUNT: {log.Value.Count}</small></p>");
                var rows = new StringBuilder();
                foreach (var blogLog in log.Value.OrderByDescending(l => l.When))
                {
                    var columns = new StringBuilder();
                    columns.AppendLine($"<details open><summary>{PrivateBlobResolver.GetName(log.Key)}</summary><table><tbody>");
                    columns.AppendFormat("<tr><td>Full URL:</td><td>{0}</td></tr>", blogLog.FullUrl);
                    columns.AppendFormat("<tr><td>Remote IP:</td><td>{0}</td></tr>", blogLog.RemoteIp);
                    columns.AppendFormat("<tr><td>When:</td><td>{0}</td></tr>", blogLog.When.ToChinaTime().ToIso8601());
                    columns.AppendFormat("<tr><td>User Agent:</td><td>{0}</td></tr>", blogLog.UserAgent);
                    columns.AppendLine("</tbody></table></details>");
                    rows.AppendLine(columns.ToString());
                }

                messages.Append(rows);
            }

            if (messages.Length > 0)
            {
                await EmailEmitter.EmitStatusAsync(messages.ToString());
            }
        }

        protected override string GetBaseContainerName()
        {
            return PrivateBlobResolver.ComposeBlobName(BaseContainer, subFolders: "status");
        }
    }
}

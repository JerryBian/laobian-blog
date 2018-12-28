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
        private readonly string _emailTemplate;

        public StatusLogHostService(
            ILogger logger,
            IEmailEmitter emailEmitter,
            IAzureBlobClient azureClient) : base(logger, azureClient, emailEmitter)
        {
            _emailTemplate = @"<style type='text/css'>
body,
html,
h2 {{
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, 'Open Sans', 'Helvetica Neue', sans-serif;
}}

#main table {{
  width: 100%;
  overflow-x: auto;
  border: 1px solid #34495e;
  border-collapse: collapse;
}}

#main table thead {{
  background-color: #74b9ff;
}}

#main table th,
#main table td {{
  text-align: left;
  padding: 12px;
  border-right: 1px solid #34495e;
}}

#main table tr {{
  border-bottom: 1px solid #34495e;
}}

#main table tr:nth-child(even) {{
  background-color: #dfe6e9;
}}
</style><p>Status code: {0}, generated at {1}.</p><table role='presentation'>
								  <thead><tr><th>URL</th><th>IP Address</th><th>Time</th><th>User Agent</th></tr></thead>
                                  <tbody>
									{2}
									
                                  </tbody>
                                </table>";
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
                        SystemState.StatusLogs = GetStoredLogs().SelectMany(ls => ls.Value).Sum(s => s.Value);
                    }
                    catch (Exception ex)
                    {
                        await EmailEmitter.EmitErrorAsync($"<p>Status Log host service error: {ex.Message}</p>", ex);
                    }
                }


                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
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
                var rows = new StringBuilder();
                foreach (var blogLog in log.Value.OrderByDescending(l => l.When))
                {
                    var columns = new StringBuilder();
                    columns.AppendFormat("<td>{0}</td>", blogLog.FullUrl);
                    columns.AppendFormat("<td>{0}</td>", blogLog.RemoteIp);
                    columns.AppendFormat("<td>{0}</td>", blogLog.When.ToChinaTime().ToIso8601());
                    columns.AppendFormat("<td>{0}</td>", blogLog.UserAgent);
                    rows.AppendFormat("<tr>{0}</tr>", columns);
                }

                messages.AppendFormat(_emailTemplate, PrivateBlobResolver.GetName(log.Key), DateTime.UtcNow.ToChinaTime().ToIso8601(), rows);
            }

            if (messages.Length > 0)
            {
                await EmailEmitter.EmitHealthyAsync(messages.ToString());
            }
        }

        protected override string GetBaseContainerName()
        {
            return PrivateBlobResolver.GetBlobName(BaseContainer, subFolder: "status");
        }
    }
}

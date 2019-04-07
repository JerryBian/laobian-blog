using Laobian.Common;
using Laobian.Common.Azure;
using Laobian.Common.Base;
using Laobian.Common.Blog;
using Laobian.Common.Notification;
using Laobian.Common.Setting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Laobian.Blog.Log
{
    public class VisitLogHostService : LogHostService
    {
        public VisitLogHostService(
            ILogger logger,
            IEmailEmitter emailEmitter,
            IAzureBlobClient azureClient) : base(logger, azureClient, emailEmitter)
        {
            Logger.NewVisitLog += (sender, args) =>
            {
                var blobKey = GetBlobKey(args.Category, args.Log.Id.Normal());
                Add(blobKey, args.Log);
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var logs = GetPendingLogs();
                var logsCount = logs.SelectMany(ls => ls.Value).Count();
                if (logsCount > 0 && (logsCount > AppSetting.Default.VisitLogBufferSize ||
                    DateTime.UtcNow - LastFlushedAt > AppSetting.Default.VisitLogFlushInterval))
                {
                    await ExecuteInternalAsync();
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        protected override string GetBaseContainerName()
        {
            return PrivateBlobResolver.ComposeBlobName(BaseContainer, subFolders: "visit");
        }

        protected override async Task InitAsync()
        {
            await base.InitAsync();
            SetPostsVisitCount();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            await InitAsync();
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await ExecuteInternalAsync();
            await base.StopAsync(cancellationToken);
        }

        private string GetBlobKey(VisitLogCategory category, string id)
        {
            var subName = category.ToString();
            if (category == VisitLogCategory.Post)
            {
                if (!Guid.TryParse(id, out _))
                {
                    throw new ArgumentException($"While category set to Post, the id: {id} must be GUID format.");
                }

                subName = $"{VisitLogCategory.Post}/{id}";
            }

            return PrivateBlobResolver.ComposeBlobName($"{GetBaseContainerName()}", subFolders: subName);
        }

        private void SetPostsVisitCount()
        {
            var logs = GetStoredLogs();
            SystemState.PostsVisitCount.Clear();
            foreach (var log in logs)
            {
                if (PrivateBlobResolver.GetParent(log.Key) ==
                    PrivateBlobResolver.ComposeBlobName(GetBaseContainerName(), subFolders: VisitLogCategory.Post.ToString()))
                {
                    var postId = PrivateBlobResolver.GetName(log.Key);
                    if (Guid.TryParse(postId, out var id))
                    {
                        SystemState.PostsVisitCount[id] = log.Value.Select(l => l.Value).Sum();
                    }
                }
            }

            SystemState.VisitLogsCount = logs.SelectMany(ls => ls.Value).Sum(s => s.Value);
        }

        private async Task ExecuteInternalAsync()
        {
            try
            {
                // flush all logs to Azure Blob
                await Flush();

                // after flush, we also need to update Posts visit count in cache
                SetPostsVisitCount();
            }
            catch (Exception ex)
            {
                await EmailEmitter.EmitErrorAsync($"<p>Visit Log host service error: {ex.Message}</p>", ex);
            }
        }
    }
}

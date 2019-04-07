using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Laobian.Common.Azure;
using Laobian.Common.Base;
using Laobian.Common.Notification;
using Microsoft.Extensions.Hosting;

namespace Laobian.Blog.Log
{
    public abstract class LogHostService : BackgroundService
    {
        protected const string BaseContainer = "blog/log";

        private readonly IAzureBlobClient _azureBlobClient;
        private readonly ConcurrentDictionary<string, List<BlogLog>> _pendingLogs; // this is only exists in memory
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _storedLogs;
        protected readonly ILogger Logger;
        protected IEmailEmitter EmailEmitter;

        protected DateTime LastFlushedAt;

        protected LogHostService(ILogger logger, IAzureBlobClient azureClient, IEmailEmitter emailEmitter)
        {
            Logger = logger;
            _azureBlobClient = azureClient;
            EmailEmitter = emailEmitter;
            LastFlushedAt = DateTime.UtcNow;
            _pendingLogs = new ConcurrentDictionary<string, List<BlogLog>>();
            _storedLogs = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();
        }

        protected abstract string GetBaseContainerName();

        // we always return copy of In-Memory logs, so that new logs will allow to add at the same time
        /// <summary>
        ///     return all logs stored in memory
        /// </summary>
        /// <returns></returns>
        protected IDictionary<string, IDictionary<string, int>> GetStoredLogs()
        {
            var logs = new Dictionary<string, IDictionary<string, int>>();
            foreach (var storedLog in _storedLogs.ToList()) logs.Add(storedLog.Key, storedLog.Value);

            return logs;
        }

        // we always return copy of In-Memory logs, so that new logs will allow to add at the same time
        protected ConcurrentDictionary<string, List<BlogLog>> GetPendingLogs()
        {
            var logs = new ConcurrentDictionary<string, List<BlogLog>>();
            foreach (var log in _pendingLogs.ToList()) logs.TryAdd(log.Key, log.Value.ToList());

            return logs;
        }

        // only add to pending buffer
        protected void Add(string blobKey, BlogLog log)
        {
            _pendingLogs.AddOrUpdate(blobKey, s =>
            {
                var list = new List<BlogLog> { log };
                return list;
            }, (s, list) =>
            {
                list.Add(log);
                return list;
            });
        }

        protected virtual async Task InitAsync()
        {
            _pendingLogs.Clear();
            _storedLogs.Clear();

            foreach (var item in await _azureBlobClient.ListAsync(BlobContainer.Private, GetBaseContainerName()))
                using (item.Stream)
                {
                    var logs = SerializeHelper.FromProtoBuf<List<BlogLog>>(item.Stream);
                    _storedLogs.AddOrUpdate(
                        PrivateBlobResolver.GetParent(item.BlobName),
                        key =>
                        {
                            var values = new ConcurrentDictionary<string, int>();
                            values.TryAdd(PrivateBlobResolver.GetName(item.BlobName), logs.Count);
                            return values;
                        },
                        (key, values) =>
                        {
                            values.TryAdd(PrivateBlobResolver.GetName(item.BlobName), logs.Count);
                            return values;
                        });
                }
        }

        protected virtual async Task<ConcurrentDictionary<string, List<BlogLog>>> Flush(
            ConcurrentDictionary<string, List<BlogLog>> pendingLogs = null)
        {
            pendingLogs = pendingLogs ?? GetPendingLogs(); // get copy of pending buffer
            if (pendingLogs == null || !pendingLogs.Any()) return pendingLogs;

            foreach (var pair in pendingLogs)
            {
                foreach (var item in pair.Value.GroupBy(p => p.When.ToMonthLite()))
                {
                    var blobName = PrivateBlobResolver.ComposeBlobName(pair.Key, item.Key);
                    var storedLogs = await _azureBlobClient.DownloadAsync<List<BlogLog>>(
                                         BlobContainer.Private,
                                         blobName,
                                         BlobType.ProtoBuf) ?? new List<BlogLog>();
                    storedLogs.AddRange(item);
                    await _azureBlobClient.UploadAsync(BlobContainer.Private, blobName, BlobType.ProtoBuf, storedLogs);
                    _storedLogs.AddOrUpdate(
                        pair.Key,
                        key =>
                        {
                            var values = new ConcurrentDictionary<string, int>();
                            values.TryAdd(item.Key, storedLogs.Count);
                            return values;
                        },
                        (key, values) =>
                        {
                            values.AddOrUpdate(item.Key, _ => storedLogs.Count, (_, __) => storedLogs.Count);
                            return values;
                        });
                }

                _pendingLogs.TryRemove(pair.Key, out _);
            }

            LastFlushedAt = DateTime.UtcNow;
            return pendingLogs;
        }
    }
}
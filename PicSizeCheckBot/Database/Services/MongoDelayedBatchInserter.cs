﻿using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace TehGM.WolfBots.PicSizeCheckBot.Database.Services
{
    public class MongoDelayedBatchInserter<TKey, TItem> : IDisposable
    {
        private IMongoCollection<TItem> _collection;
        private readonly ReplaceOptions _defaultReplaceOptions;

        private readonly TimeSpan _delay;
        private readonly IDictionary<TKey, MongoDelayedInsert<TItem>> _batchedInserts;
        private TaskCompletionSource<object> _batchTcs;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public MongoDelayedBatchInserter(TimeSpan delay, IEqualityComparer<TKey> comparer)
        {
            this._delay = delay;
            this._batchedInserts = new Dictionary<TKey, MongoDelayedInsert<TItem>>(comparer);
            this._defaultReplaceOptions = new ReplaceOptions() { IsUpsert = true, BypassDocumentValidation = false };
        }

        public MongoDelayedBatchInserter(TimeSpan delay)
            : this(delay, EqualityComparer<TKey>.Default) { }

        public void UpdateCollection(IMongoCollection<TItem> collection)
        {
            this._collection = collection;
        }

        public async Task BatchAsync(TKey key, MongoDelayedInsert<TItem> item, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this._batchedInserts[key] = item;
                if (_batchTcs != null)
                    return;
                _ = BatchDelayAsync();
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Flush()
        {
            _batchTcs?.TrySetResult(null);
        }

        private async Task BatchDelayAsync()
        {
            _batchTcs = new TaskCompletionSource<object>();
            Task delayTask = Task.Delay(this._delay);

            await Task.WhenAny(_batchTcs.Task, delayTask).ConfigureAwait(false);

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (KeyValuePair<TKey, MongoDelayedInsert<TItem>> inserts in _batchedInserts)
                    await _collection.ReplaceOneAsync(inserts.Value.Filter, inserts.Value.Item, inserts.Value.ReplaceOptions ?? _defaultReplaceOptions).ConfigureAwait(false);
                _batchedInserts.Clear();
            }
            finally
            {
                _batchTcs = null;
                _lock.Release();
            }
        }

        public void Dispose()
        {
            _batchTcs?.TrySetCanceled();
            _lock?.Dispose();
        }
    }

    public class MongoDelayedInsert<T>
    {
        public readonly Expression<Func<T, bool>> Filter;
        public readonly T Item;
        public ReplaceOptions ReplaceOptions;

        public MongoDelayedInsert(Expression<Func<T, bool>> filter, T item, ReplaceOptions replaceOptions)
        {
            this.Filter = filter;
            this.Item = item;
            this.ReplaceOptions = replaceOptions;
        }
    }
}

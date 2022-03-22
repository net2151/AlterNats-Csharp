﻿using AlterNats.Commands;
using AlterNats.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AlterNats;

internal sealed class SubscriptionManager : IDisposable
{
    readonly object gate = new object(); // lock for add/remove, publish can avoid lock.
    readonly ConcurrentDictionary<int, RefCountSubscription> bySubscriptionId = new();
    readonly ConcurrentDictionary<string, RefCountSubscription> byStringKey = new();

    readonly NatsConnection connection;

    int subscriptionId = 0; // unique alphanumeric subscription ID, generated by the client(per connection).

    public SubscriptionManager(NatsConnection connection)
    {
        this.connection = connection;
    }

    public IDisposable Add<T>(string key, Action<T> handler)
    {
        lock (gate)
        {
            if (byStringKey.TryGetValue(key, out var subscription))
            {
                if (subscription.ElementType != typeof(T))
                {
                    throw new InvalidOperationException($"Register different type on same key. RegisteredType:{subscription.ElementType.FullName} NewType:{typeof(T).FullName}");
                }

                subscription.AddCallback(handler);
            }
            else
            {
                var sid = Interlocked.Increment(ref subscriptionId);

                subscription = new RefCountSubscription(sid, key, typeof(T));
                subscription.AddCallback(handler);
                bySubscriptionId[sid] = subscription;
                byStringKey[key] = subscription;

                // TODO: if cannot added, must return info.
                connection.Subscribe(key, sid, handler);
            }

            // TODO:make disposable
            throw new NotImplementedException();
        }
    }

    public void Publish(int subscriptionId, ReadOnlySequence<byte> buffer)
    {
        if (bySubscriptionId.TryGetValue(subscriptionId, out var subscription))
        {
            var list = subscription.Callbacks.GetValues();
            var item = PublishCallbackThreadPoolWorkItemFactory.Create(subscription.ElementType, connection.Options, buffer, list);
            ThreadPool.UnsafeQueueUserWorkItem(item, preferLocal: false);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            // remove all references.
            foreach (var item in bySubscriptionId)
            {
                item.Value.Callbacks.Dispose();
            }

            bySubscriptionId.Clear();
            byStringKey.Clear();
        }
    }
}


internal sealed class RefCountSubscription
{
    public int SubscriptionId { get; }
    public string Key { get; }
    public int ReferenceCount { get; private set; }
    public Type ElementType { get; }
    public FreeList<object> Callbacks { get; }

    public RefCountSubscription(int subscriptionId, string key, Type elementType)
    {
        SubscriptionId = subscriptionId;
        Key = key;
        ReferenceCount = 0;
        ElementType = elementType;
        Callbacks = new FreeList<object>();
    }

    public void AddCallback(object callback)
    {
        Callbacks.Add(callback);
        ReferenceCount++;
    }
}

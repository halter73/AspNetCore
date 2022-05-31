// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace StreamR;

public class StreamManager
{
    private readonly IHubContext<StreamHub> _streamHubContext;
    private readonly ConcurrentDictionary<string, StreamHolder> _streams = new();

    public StreamManager(IHubContext<StreamHub> streamHubContext)
    {
        _streamHubContext = streamHubContext;
    }

    public List<string> ListStreams()
    {
        var streamList = new List<string>();
        foreach (var item in _streams)
        {
            streamList.Add(item.Key);
        }
        return streamList;
    }

    public async Task RunStreamAsync(string streamName, IAsyncEnumerable<string> stream)
    {
        if (!_streams.TryAdd(streamName, new()))
        {
            throw new HubException($"Stream name '{streamName}' already in use.");
        }

        try
        {
            await _streamHubContext.Clients.All.SendAsync("NewStream", streamName);
            await _streamHubContext.Clients.Group(streamName).SendAsync("ReceiveStream", streamName, stream);
        }
        finally
        {
            _streams.TryRemove(streamName, out _);
            await _streamHubContext.Clients.All.SendAsync("RemoveStream", streamName);
        }
    }

    public Task SubscribeAsync(string connectionId, string streamName)
    {
        if (!_streams.TryGetValue(streamName, out var source))
        {
            throw new HubException($"Stream '{streamName}' does not exist.");
        }

        return _streamHubContext.Groups.AddToGroupAsync(connectionId, streamName);
    }

    public Task UnsubscribeAsync(string connectionId, string streamName)
    {
        return _streamHubContext.Groups.RemoveFromGroupAsync(connectionId, streamName);
    }

    private class StreamHolder
    {
    }
}

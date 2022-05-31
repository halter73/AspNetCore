// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.SignalR;

namespace StreamR;

public class StreamHub : Hub
{
    private readonly StreamManager _streamManager;

    public StreamHub(StreamManager streamManager)
    {
        _streamManager = streamManager;
    }

    public List<string> ListStreams() => _streamManager.ListStreams();

    public Task WatchStream(string streamName) =>
        _streamManager.SubscribeAsync(Context.ConnectionId, streamName);

    public Task RunStream(string streamName, IAsyncEnumerable<string> streamContent) =>
        _streamManager.RunStreamAsync(streamName, streamContent);
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Internal;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR;

/// <summary>
/// A default in-memory lifetime manager abstraction for <see cref="Hub"/> instances.
/// </summary>
public class DefaultHubLifetimeManager<THub> : HubLifetimeManager<THub> where THub : Hub
{
    private readonly HubConnectionStore _connections = new HubConnectionStore();
    private readonly HubGroupList _groups = new HubGroupList();
    private readonly ILogger _logger;
    private readonly ClientResultsManager _clientResultsManager = new();
    private ulong _lastInvocationId;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultHubLifetimeManager{THub}"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DefaultHubLifetimeManager(ILogger<DefaultHubLifetimeManager<THub>> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (connectionId == null)
        {
            throw new ArgumentNullException(nameof(connectionId));
        }

        if (groupName == null)
        {
            throw new ArgumentNullException(nameof(groupName));
        }

        var connection = _connections[connectionId];
        if (connection == null)
        {
            return Task.CompletedTask;
        }

        _groups.Add(connection, groupName);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        if (connectionId == null)
        {
            throw new ArgumentNullException(nameof(connectionId));
        }

        if (groupName == null)
        {
            throw new ArgumentNullException(nameof(groupName));
        }

        var connection = _connections[connectionId];
        if (connection == null)
        {
            return Task.CompletedTask;
        }

        _groups.Remove(connectionId, groupName);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SendAllAsync(string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendToAllConnections(methodName, args, include: null, state: null, cancellationToken);
    }

    private Task SendToAllConnections(string methodName, object?[] args, Func<HubConnectionContext, object?, bool>? include, object? state = null, CancellationToken cancellationToken = default)
    {
        List<Task>? tasks = null;
        SerializedHubMessage? message = null;

        // foreach over HubConnectionStore avoids allocating an enumerator
        foreach (var connection in _connections)
        {
            if (include != null && !include(connection, state))
            {
                continue;
            }

            if (message == null)
            {
                message = DefaultHubLifetimeManager<THub>.CreateSerializedInvocationMessage(methodName, args);
            }

            var task = connection.WriteAsync(message, cancellationToken);

            if (!task.IsCompletedSuccessfully)
            {
                if (tasks == null)
                {
                    tasks = new List<Task>();
                }

                tasks.Add(task.AsTask());
            }
            else
            {
                // If it's a IValueTaskSource backed ValueTask,
                // inform it its result has been read so it can reset
                task.GetAwaiter().GetResult();
            }
        }

        if (tasks == null)
        {
            return Task.CompletedTask;
        }

        // Some connections are slow
        return Task.WhenAll(tasks);
    }

    // Tasks and message are passed by ref so they can be lazily created inside the method post-filtering,
    // while still being re-usable when sending to multiple groups
    private void SendToGroupConnections(string methodName, object?[] args, string groupName, IReadOnlyList<string>? excludedConnectionIds, ref List<Task>? tasks, ref SerializedHubMessage? message, CancellationToken cancellationToken)
    {
        var readers = PackageStreamingParams(ref args, out var streamIds);

        var connections = _groups[groupName];

        if (connections is not null)
        {
            // foreach over ConcurrentDictionary avoids allocating an enumerator
            foreach ((_, var connection) in connections)
            {
                if (excludedConnectionIds?.Contains(connection.ConnectionId) is true)
                {
                    continue;
                }

                if (message is null)
                {
                    if (streamIds is null)
                    {
                        message = CreateSerializedInvocationMessage(methodName, args);
                    }
                    else
                    {
                        message = new SerializedHubMessage(new StreamInvocationMessage(null!, methodName, args, streamIds?.ToArray()));
                        // Mark message as sent initially
                        connection.Items[message] = true;
                    }
                }

                HandleWriteTask(ref tasks, connection.WriteAsync(message, cancellationToken));
            }
        }

        if (readers is not null)
        {
            message ??= new SerializedHubMessage(new StreamInvocationMessage(null!, methodName, args, streamIds?.ToArray()));

            foreach ((var streamId, var reader) in readers)
            {
                ValueTask writeStreamTask;

                // For each stream that needs to be sent, run a "send items" task in the background.
                // This reads from the channel, attaches streamId, and sends to server.
                // A single background thread here quickly gets messy.
                if (ReflectionHelper.IsIAsyncEnumerable(reader.GetType()))
                {
                    writeStreamTask = (ValueTask)_sendIAsyncStreamItemsMethod
                        .MakeGenericMethod(reader.GetType().GetInterface("IAsyncEnumerable`1")!.GetGenericArguments())
                        .Invoke(this, new object?[] { groupName, excludedConnectionIds, streamId, message, reader, cancellationToken })!;
                }
                else
                {
                    writeStreamTask = (ValueTask)_sendStreamItemsMethod
                        .MakeGenericMethod(reader.GetType().GetGenericArguments())
                        .Invoke(this, new object?[] { groupName, excludedConnectionIds, streamId, message, reader, cancellationToken })!;
                }

                HandleWriteTask(ref tasks, writeStreamTask);
            }
        }
    }

    /// <inheritdoc />
    public override Task SendConnectionAsync(string connectionId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (connectionId == null)
        {
            throw new ArgumentNullException(nameof(connectionId));
        }

        var connection = _connections[connectionId];

        if (connection == null)
        {
            return Task.CompletedTask;
        }

        // We're sending to a single connection
        // Write message directly to connection without caching it in memory
        var message = CreateInvocationMessage(methodName, args);

        return connection.WriteAsync(message, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public override Task SendGroupAsync(string groupName, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (groupName == null)
        {
            throw new ArgumentNullException(nameof(groupName));
        }

        // Can't optimize for sending to a single connection in a group because
        // group might be modified inbetween checking and sending
        List<Task>? tasks = null;
        SerializedHubMessage? message = null;
        SendToGroupConnections(methodName, args, groupName, null, ref tasks, ref message, cancellationToken);

        if (tasks != null)
        {
            return Task.WhenAll(tasks);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SendGroupsAsync(IReadOnlyList<string> groupNames, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        // Each task represents the list of tasks for each of the writes within a group
        List<Task>? tasks = null;
        SerializedHubMessage? message = null;

        foreach (var groupName in groupNames)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                throw new InvalidOperationException("Cannot send to an empty group name.");
            }

            SendToGroupConnections(methodName, args, groupName, null, ref tasks, ref message, cancellationToken);
        }

        if (tasks != null)
        {
            return Task.WhenAll(tasks);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SendGroupExceptAsync(string groupName, string methodName, object?[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
    {
        if (groupName == null)
        {
            throw new ArgumentNullException(nameof(groupName));
        }

        var group = _groups[groupName];
        if (group != null)
        {
            List<Task>? tasks = null;
            SerializedHubMessage? message = null;

            SendToGroupConnections(methodName, args, groupName, excludedConnectionIds, ref tasks, ref message, cancellationToken);

            if (tasks != null)
            {
                return Task.WhenAll(tasks);
            }
        }

        return Task.CompletedTask;
    }

    private static SerializedHubMessage CreateSerializedInvocationMessage(string methodName, object?[] args)
    {
        return new SerializedHubMessage(CreateInvocationMessage(methodName, args));
    }

    private static HubMessage CreateInvocationMessage(string methodName, object?[] args)
    {
        return new InvocationMessage(methodName, args);
    }

    /// <inheritdoc />
    public override Task SendUserAsync(string userId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendToAllConnections(methodName, args, (connection, state) => string.Equals(connection.UserIdentifier, (string)state!, StringComparison.Ordinal), userId, cancellationToken);
    }

    /// <inheritdoc />
    public override Task OnConnectedAsync(HubConnectionContext connection)
    {
        _connections.Add(connection);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(HubConnectionContext connection)
    {
        _connections.Remove(connection);
        _groups.RemoveDisconnectedConnection(connection.ConnectionId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task SendAllExceptAsync(string methodName, object?[] args, IReadOnlyList<string> excludedConnectionIds, CancellationToken cancellationToken = default)
    {
        return SendToAllConnections(methodName, args, (connection, state) => !((IReadOnlyList<string>)state!).Contains(connection.ConnectionId), excludedConnectionIds, cancellationToken);
    }

    /// <inheritdoc />
    public override Task SendConnectionsAsync(IReadOnlyList<string> connectionIds, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendToAllConnections(methodName, args, (connection, state) => ((IReadOnlyList<string>)state!).Contains(connection.ConnectionId), connectionIds, cancellationToken);
    }

    /// <inheritdoc />
    public override Task SendUsersAsync(IReadOnlyList<string> userIds, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        return SendToAllConnections(methodName, args, (connection, state) => ((IReadOnlyList<string>)state!).Contains(connection.UserIdentifier), userIds, cancellationToken);
    }

    /// <inheritdoc/>
    public override async Task<T> InvokeConnectionAsync<T>(string connectionId, string methodName, object?[] args, CancellationToken cancellationToken = default)
    {
        if (connectionId == null)
        {
            throw new ArgumentNullException(nameof(connectionId));
        }

        var connection = _connections[connectionId];

        if (connection == null)
        {
            throw new IOException($"Connection '{connectionId}' does not exist.");
        }

        var invocationId = Interlocked.Increment(ref _lastInvocationId).ToString(NumberFormatInfo.InvariantInfo);
        using var _ = CancellationTokenUtils.CreateLinkedToken(cancellationToken,
            connection.ConnectionAborted, out var linkedToken);
        var task = _clientResultsManager.AddInvocation<T>(connectionId, invocationId, linkedToken);

        try
        {
            // We're sending to a single connection
            // Write message directly to connection without caching it in memory
            var message = new InvocationMessage(invocationId, methodName, args);

            await connection.WriteAsync(message, cancellationToken);
        }
        catch
        {
            _clientResultsManager.RemoveInvocation(invocationId);
            throw;
        }

        try
        {
            return await task;
        }
        catch
        {
            // ConnectionAborted will trigger a generic "Canceled" exception from the task, let's convert it into a more specific message.
            if (connection.ConnectionAborted.IsCancellationRequested)
            {
                throw new IOException($"Connection '{connectionId}' disconnected.");
            }
            throw;
        }
    }

    /// <inheritdoc/>
    public override Task SetConnectionResultAsync(string connectionId, CompletionMessage result)
    {
        _clientResultsManager.TryCompleteResult(connectionId, result);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public override bool TryGetReturnType(string invocationId, [NotNullWhen(true)] out Type? type)
    {
        if (_clientResultsManager.TryGetType(invocationId, out type))
        {
            return true;
        }
        type = null;
        return false;
    }

    private static long _nextStreamId;
    private static readonly MethodInfo _sendStreamItemsMethod = typeof(DefaultHubLifetimeManager<THub>).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name.Equals(nameof(SendStreamItems)));
    private static readonly MethodInfo _sendIAsyncStreamItemsMethod = typeof(DefaultHubLifetimeManager<THub>).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance).Single(m => m.Name.Equals(nameof(SendIAsyncEnumerableStreamItems)));

    private static Dictionary<string, object>? PackageStreamingParams(ref object?[] args, out List<string>? streamIds)
    {
        Dictionary<string, object>? readers = null;
        streamIds = null;
        var newArgsCount = args.Length;
        const int MaxStackSize = 256;
        Span<bool> isStreaming = args.Length <= MaxStackSize
            ? stackalloc bool[MaxStackSize].Slice(0, args.Length)
            : new bool[args.Length];
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg is not null && ReflectionHelper.IsStreamingType(arg.GetType()))
            {
                isStreaming[i] = true;
                newArgsCount--;

                if (readers is null)
                {
                    readers = new Dictionary<string, object>();
                }
                if (streamIds is null)
                {
                    streamIds = new List<string>();
                }

                var id = Interlocked.Increment(ref _nextStreamId).ToString(CultureInfo.InvariantCulture)!;

                readers[id] = arg;
                streamIds.Add(id);

                //Log.StartingStream(_logger, id);
            }
        }

        if (newArgsCount == args.Length)
        {
            return null;
        }

        var newArgs = newArgsCount > 0
            ? new object?[newArgsCount]
            : Array.Empty<object?>();
        int newArgsIndex = 0;

        for (var i = 0; i < args.Length; i++)
        {
            if (!isStreaming[i])
            {
                newArgs[newArgsIndex] = args[i];
                newArgsIndex++;
            }
        }

        args = newArgs;
        return readers;
    }

    // this is called via reflection using the `_sendStreamItemsMethod` field
    private async ValueTask SendStreamItems<T>(string groupName, IReadOnlyList<string>? excludedConnectionIds, string streamId, SerializedHubMessage originalInvocation, ChannelReader<T> reader, CancellationToken cancellationToken)
    {
        List<Task>? tasks = null;

        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (!cancellationToken.IsCancellationRequested && reader.TryRead(out var item))
            {
                SendStreamItem(groupName, streamId, item, excludedConnectionIds, originalInvocation, ref tasks, cancellationToken);
                //Log.SendingStreamItem(_logger, streamId);

                if (tasks is { Count: > 0 })
                {
                    foreach (var task in tasks)
                    {
                        await task;
                    }
                    tasks.Clear();
                }
            }
        }
    }

    // this is called via reflection using the `_sendIAsyncStreamItemsMethod` field
    private async ValueTask SendIAsyncEnumerableStreamItems<T>(string groupName, IReadOnlyList<string>? excludedConnectionIds, string streamId, SerializedHubMessage originalInvocation, IAsyncEnumerable<T> stream, CancellationToken cancellationToken)
    {
        List<Task>? tasks = null;

        await foreach (var item in stream)
        {
            SendStreamItem(groupName, streamId, item, excludedConnectionIds, originalInvocation, ref tasks, cancellationToken);
            //Log.SendingStreamItem(_logger, streamId);

            if (tasks is { Count: > 0 })
            {
                foreach (var task in tasks)
                {
                    await task;
                }
                tasks.Clear();
            }
        }
    }

    private void SendStreamItem<T>(string groupName, string streamId, T item, IReadOnlyList<string>? excludedConnectionIds, SerializedHubMessage originalInvocation, ref List<Task>? tasks, CancellationToken cancellationToken)
    {
        var connections = _groups[groupName];
        if (connections is null)
        {
            return;
        }

        var streamItemMessage = new StreamItemMessage(streamId, item);
        foreach ((_, var connection) in connections)
        {
            if (excludedConnectionIds?.Contains(connection.ConnectionId) is true)
            {
                continue;
            }

            // TODO: Fix this to not leak.
            if (connection.Items[originalInvocation] is not true)
            {
                connection.Items[originalInvocation] = true;
                HandleWriteTask(ref tasks, connection.WriteAsync(originalInvocation, cancellationToken));
            }

            HandleWriteTask(ref tasks, connection.WriteAsync(streamItemMessage, cancellationToken));
        }
    }

    private static void HandleWriteTask(ref List<Task>? tasks, ValueTask task)
    {
        if (!task.IsCompletedSuccessfully)
        {
            if (tasks == null)
            {
                tasks = new List<Task>();
            }

            tasks.Add(task.AsTask());
        }
        else
        {
            // If it's a IValueTaskSource backed ValueTask,
            // inform it its result has been read so it can reset
            task.GetAwaiter().GetResult();
        }
    }
}

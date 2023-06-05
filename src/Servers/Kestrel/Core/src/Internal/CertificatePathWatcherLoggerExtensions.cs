// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal;

internal static partial class CertificatePathWatcherLoggerExtensions
{
    [LoggerMessage(0, LogLevel.Warning, @"Directory ""{dir}"" does not exist so changes to the certificate ""{path}"" will not be tracked", EventName = "DirectoryDoesNotExist")]
    public static partial void DirectoryDoesNotExist(this ILogger<CertificatePathWatcher> logger, string dir, string path);

    [LoggerMessage(1, LogLevel.Warning, @"Attempted to remove watch from unwatched path ""{path}""", EventName = "UnknownFile")]
    public static partial void UnknownFile(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(2, LogLevel.Warning, @"Attempted to remove unknown observer from path ""{path}""", EventName = "UnknownObserver")]
    public static partial void UnknownObserver(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(3, LogLevel.Debug, @"Created directory watcher for ""{dir}""", EventName = "CreatedDirectoryWatcher")]
    public static partial void CreatedDirectoryWatcher(this ILogger<CertificatePathWatcher> logger, string dir);

    [LoggerMessage(4, LogLevel.Debug, @"Created file watcher for ""{path}""", EventName = "CreatedFileWatcher")]
    public static partial void CreatedFileWatcher(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(5, LogLevel.Debug, @"Removed directory watcher for ""{dir}""", EventName = "RemovedDirectoryWatcher")]
    public static partial void RemovedDirectoryWatcher(this ILogger<CertificatePathWatcher> logger, string dir);

    [LoggerMessage(6, LogLevel.Debug, @"Removed file watcher for ""{path}""", EventName = "RemovedFileWatcher")]
    public static partial void RemovedFileWatcher(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(7, LogLevel.Debug, @"Error retrieving last modified time for ""{path}""", EventName = "LastModifiedTimeError")]
    public static partial void LastModifiedTimeError(this ILogger<CertificatePathWatcher> logger, string path, Exception e);

    [LoggerMessage(8, LogLevel.Debug, @"Ignored event for presently untracked file ""{path}""", EventName = "UntrackedFileEvent")]
    public static partial void UntrackedFileEvent(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(9, LogLevel.Debug, @"Ignored out-of-order event for file ""{path}""", EventName = "OutOfOrderEvent")]
    public static partial void OutOfOrderEvent(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(10, LogLevel.Trace, @"Reused existing observer on file watcher for ""{path}""", EventName = "ReusedObserver")]
    public static partial void ReusedObserver(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(11, LogLevel.Trace, @"Added observer to file watcher for ""{path}""", EventName = "AddedObserver")]
    public static partial void AddedObserver(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(12, LogLevel.Trace, @"Removed observer from file watcher for ""{path}""", EventName = "RemovedObserver")]
    public static partial void RemovedObserver(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(13, LogLevel.Trace, @"File ""{path}"" now has {count} observers", EventName = "ObserverCount")]
    public static partial void ObserverCount(this ILogger<CertificatePathWatcher> logger, string path, int count);

    [LoggerMessage(14, LogLevel.Trace, @"Directory ""{dir}"" now has watchers on {count} files", EventName = "FileCount")]
    public static partial void FileCount(this ILogger<CertificatePathWatcher> logger, string dir, int count);

    [LoggerMessage(15, LogLevel.Trace, @"Ignored event since last modified time for ""{path}"" was unavailable", EventName = "EventWithoutLastModifiedTime")]
    public static partial void EventWithoutLastModifiedTime(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(16, LogLevel.Trace, @"Ignored redundant event for ""{path}""", EventName = "RedundantEvent")]
    public static partial void RedundantEvent(this ILogger<CertificatePathWatcher> logger, string path);

    [LoggerMessage(17, LogLevel.Trace, @"Flagged {count} observers of ""{path}"" as changed", EventName = "FlaggedObservers")]
    public static partial void FlaggedObservers(this ILogger<CertificatePathWatcher> logger, string path, int count);
}

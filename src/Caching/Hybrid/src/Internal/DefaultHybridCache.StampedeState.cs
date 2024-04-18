// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Microsoft.Extensions.Caching.Hybrid.Internal;

partial class DefaultHybridCache
{
    internal abstract class StampedeState
#if NETCOREAPP3_0_OR_GREATER
        : IThreadPoolWorkItem
#endif
    {
        private readonly DefaultHybridCache _cache;

        public StampedeKey Key { get; }

        /// <summary>
        /// Create a stamped token optionally with shared cancellation support
        /// </summary>
        protected StampedeState(DefaultHybridCache cache, in StampedeKey key, bool canBeCanceled)
        {
            _cache = cache;
            Key = key;
            if (canBeCanceled)
            {
                // if the first (or any) caller can't be cancelled; we'll never get to zero; no point tracking
                // (in reality, all callers usually use the same path, so cancellation is usually "all" or "none")
                sharedCancellation = new();
                SharedToken = sharedCancellation.Token;
            }
            else
            {
                SharedToken = CancellationToken.None;
            }
        }

        /// <summary>
        /// Create a stamped token using a fixed cancellation token
        /// </summary>
        protected StampedeState(DefaultHybridCache cache, in StampedeKey key, CancellationToken token)
        {
            _cache = cache;
            Key = key;
            SharedToken = token;
        }

#if !NETCOREAPP3_0_OR_GREATER
        protected static readonly WaitCallback SharedWaitCallback = static obj => Unsafe.As<StampedeState>(obj).Execute();
#endif

        protected DefaultHybridCache Cache => _cache;

        public abstract void Execute();

        protected int MaximumPayloadBytes => _cache.MaximumPayloadBytes;

        public override string ToString() => Key.ToString();

        // because multiple callers can enlist, we need to track when the *last* caller cancels
        // (and keep going until then); that means we need to run with custom cancellation
        private readonly CancellationTokenSource? sharedCancellation;

        protected abstract void SetCanceled();

        public readonly CancellationToken SharedToken;

        public int DebugCallerCount => Volatile.Read(ref activeCallers);

        public abstract Type Type { get; }

        private int activeCallers = 1;
        public void RemoveCaller()
        {
            // note that TryAddCaller has protections to avoid getting back from zero
            if (Interlocked.Decrement(ref activeCallers) == 0)
            {
                // we're the last to leave; turn off the lights
                sharedCancellation?.Cancel();
                SetCanceled();
            }
        }

        public bool TryAddCaller() // essentially just interlocked-increment, but with a leading zero check and overflow detection
        {
            int oldValue = Volatile.Read(ref activeCallers);
            do
            {
                if (oldValue == 0)
                {
                    return false; // already burned
                }

                var updated = Interlocked.CompareExchange(ref activeCallers, checked(oldValue + 1), oldValue);
                if (updated == oldValue)
                {
                    return true; // we exchanged
                }
                oldValue = updated; // we failed, but we have an updated state
            } while (true);
        }
    }

    private void RemoveStampede(StampedeKey key) => _currentOperations.TryRemove(key, out _);
}

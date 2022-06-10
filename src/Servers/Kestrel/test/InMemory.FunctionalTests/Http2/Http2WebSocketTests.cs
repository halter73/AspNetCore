// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Net.Http;
using System.Net.Http.HPack;
using System.Runtime.ExceptionServices;
using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http2;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Infrastructure;
using Microsoft.AspNetCore.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Tests;

// https://datatracker.ietf.org/doc/html/rfc8441
public class Http2WebSocketTests : Http2TestBase
{
    [Fact]
    public async Task HEADERS_Received_ExtendedCONNECTMethod_Received()
    {
        await InitializeConnectionAsync(async context =>
        {
            var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
            Assert.True(connectFeature.IsExtendedConnect);
            Assert.Equal(HttpMethods.Connect, context.Request.Method);
            Assert.Equal("websocket", connectFeature.Protocol);
            Assert.False(context.Request.Headers.TryGetValue(":protocol", out var _));
            Assert.Equal("http", context.Request.Scheme);
            Assert.Equal("/chat", context.Request.Path.Value);
            Assert.Equal("server.example.com", context.Request.Host.Value);
            Assert.Equal("chat, superchat", context.Request.Headers.WebSocketSubProtocols);
            Assert.Equal("permessage-deflate", context.Request.Headers.SecWebSocketExtensions);
            Assert.Equal("13", context.Request.Headers.SecWebSocketVersion);
            Assert.Equal("http://www.example.com", context.Request.Headers.Origin);

            Assert.Equal(0, await context.Request.Body.ReadAsync(new byte[1]));
        });

        // HEADERS + END_HEADERS
        // :method = CONNECT
        // :protocol = websocket
        // :scheme = https
        // :path = /chat
        // :authority = server.example.com
        // sec-websocket-protocol = chat, superchat
        // sec-websocket-extensions = permessage-deflate
        // sec-websocket-version = 13
        // origin = http://www.example.com
        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "websocket"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/chat"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "server.example.com"),
            new KeyValuePair<string, string>(HeaderNames.WebSocketSubProtocols, "chat, superchat"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketExtensions, "permessage-deflate"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketVersion, "13"),
            new KeyValuePair<string, string>(HeaderNames.Origin, "http://www.example.com"),
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS, headers);
        await SendDataAsync(1, Array.Empty<byte>(), endStream: true);

        var headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 36,
            withFlags: (byte)(Http2HeadersFrameFlags.END_HEADERS | Http2HeadersFrameFlags.END_STREAM),
            withStreamId: 1);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);

        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(3, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);
        Assert.Equal("0", _decodedHeaders["content-length"]);
    }

    [Fact]
    public async Task ExtendedCONNET_AcceptAsyncStream_IsNotLimitedByMinRequestBodyDataRate()
    {
        var limits = _serviceContext.ServerOptions.Limits;

        // Use non-default value to ensure the min request and response rates aren't mixed up.
        limits.MinRequestBodyDataRate = new MinDataRate(480, TimeSpan.FromSeconds(2.5));

        await InitializeConnectionAsync(async context =>
        {
            var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
            var stream = await connectFeature.AcceptAsync();
            Assert.Equal(0, await stream.ReadAsync(new byte[1])); // FAILS! ConnectionAbortedException: 'Reading the request body timed out due to data arriving too slowly. See MinRequestBodyDataRate.'
            await stream.WriteAsync(new byte[] { 0x01 });
        });

        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "websocket"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/chat"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "server.example.com"),
            new KeyValuePair<string, string>(HeaderNames.WebSocketSubProtocols, "chat, superchat"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketExtensions, "permessage-deflate"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketVersion, "13"),
            new KeyValuePair<string, string>(HeaderNames.Origin, "http://www.example.com"),
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS, headers);

        // Don't send any more data and advance just to and then past the grace period.
        AdvanceClock(limits.MinRequestBodyDataRate.GracePeriod + TimeSpan.FromTicks(1));

        _mockTimeoutHandler.Verify(h => h.OnTimeout(It.IsAny<TimeoutReason>()), Times.Never); // FAILS! Invoked Times.Once.

        await SendDataAsync(1, Array.Empty<byte>(), endStream: true);

        var headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 32,
            withFlags: (byte)Http2HeadersFrameFlags.END_HEADERS,
            withStreamId: 1);

        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(2, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);

        var dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 1,
            withFlags: (byte)Http2DataFrameFlags.NONE,
            withStreamId: 1);
        Assert.Equal(0x01, dataFrame.Payload.Span[0]);

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 0,
            withFlags: (byte)Http2DataFrameFlags.END_STREAM,
            withStreamId: 1);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);
    }

    public async Task ExtendedCONNET_AcceptAsyncStream_IsNotLimitedByMaxRequestBodySize()
    {
        var limits = _serviceContext.ServerOptions.Limits;

        // We're going to send more than the MaxRequestBodySize bytes from the client to the server over the connection
        // Since this is not a request body, this should be allowed like it would be for an upgraded connection.
        limits.MaxRequestBodySize = 5;

        await InitializeConnectionAsync(async context =>
        {
            var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
            var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            // Extended connects don't have a meaningful request body to limit. It doesn't make sense for this value to change just because AcceptAsync() was called.
            Assert.True(maxRequestBodySizeFeature.IsReadOnly); // FAILS!
            Assert.Null(maxRequestBodySizeFeature.MaxRequestBodySize); // FAILS!

            var stream = await connectFeature.AcceptAsync();
            // NOW it has the right value, but it should have had it before.
            Assert.True(maxRequestBodySizeFeature.IsReadOnly);
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream); // FAILS! because the client sent more than the MaxRequestBodySize but it's not a body.
            Assert.Equal(_serviceContext.ServerOptions.Limits.MaxRequestBodySize + 1, memoryStream.Length);
            await stream.WriteAsync(new byte[] { 0x01 });
        });

        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "websocket"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/chat"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "server.example.com"),
            new KeyValuePair<string, string>(HeaderNames.WebSocketSubProtocols, "chat, superchat"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketExtensions, "permessage-deflate"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketVersion, "13"),
            new KeyValuePair<string, string>(HeaderNames.Origin, "http://www.example.com"),
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS, headers);

        await SendDataAsync(1, new byte[(int)limits.MaxRequestBodySize + 1], endStream: true);

        var headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 32,
            withFlags: (byte)Http2HeadersFrameFlags.END_HEADERS,
            withStreamId: 1);

        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(2, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);

        var dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 1,
            withFlags: (byte)Http2DataFrameFlags.NONE,
            withStreamId: 1);
        Assert.Equal(0x01, dataFrame.Payload.Span[0]); // FAILS! because Response.Body.WriteAsync wrote 0x00 instead of 0x01.

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 0,
            withFlags: (byte)Http2DataFrameFlags.END_STREAM,
            withStreamId: 1);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);
    }

    [Fact]
    public async Task HEADERS_Received_ExtendedCONNECTMethod_DoesNotProvideUsableBodyStreams()
    {
        await InitializeConnectionAsync(async context =>
        {
            var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();

            // The below should really throw instead, but nooping is better than our current Response.Body behavior.
            Assert.Equal(0, await context.Request.Body.ReadAsync(new byte[1]));
            // Tell me this doesn't get sent in a DATA frame with a 200 status code!!!
            // This will be interpreted as a valid data from the server to client over the connection rather than a normal response.
            // I think both reading and writing should throw, but both should at least noop.
            await context.Response.Body.WriteAsync(new byte[1] { 0x00 });

            var stream = await connectFeature.AcceptAsync();
            await stream.WriteAsync(new byte[] { 0x01 });
        });

        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "websocket"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/chat"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "server.example.com"),
            new KeyValuePair<string, string>(HeaderNames.WebSocketSubProtocols, "chat, superchat"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketExtensions, "permessage-deflate"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketVersion, "13"),
            new KeyValuePair<string, string>(HeaderNames.Origin, "http://www.example.com"),
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS, headers);

        await SendDataAsync(1, new byte[(int)_serviceContext.ServerOptions.Limits.MaxRequestBodySize + 1], endStream: true);

        var headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 32,
            withFlags: (byte)Http2HeadersFrameFlags.END_HEADERS,
            withStreamId: 1);

        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(2, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);

        var dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 1,
            withFlags: (byte)Http2DataFrameFlags.NONE,
            withStreamId: 1);
        Assert.Equal(0x01, dataFrame.Payload.Span[0]); // FAILS! because Response.Body.WriteAsync wrote 0x00 instead of 0x01.

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 0,
            withFlags: (byte)Http2DataFrameFlags.END_STREAM,
            withStreamId: 1);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);
    }

    [Fact]
    public async Task HEADERS_Received_ExtendedCONNECTMethod_Accepted()
    {
        await InitializeConnectionAsync(async context =>
        {
            var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
            Assert.True(connectFeature.IsExtendedConnect);
            Assert.Equal(HttpMethods.Connect, context.Request.Method);
            Assert.Equal("websocket", connectFeature.Protocol);
            Assert.False(context.Request.Headers.TryGetValue(":protocol", out var _));
            Assert.Equal("http", context.Request.Scheme);
            Assert.Equal("/chat", context.Request.Path.Value);
            Assert.Equal("server.example.com", context.Request.Host.Value);
            Assert.Equal("chat, superchat", context.Request.Headers.WebSocketSubProtocols);
            Assert.Equal("permessage-deflate", context.Request.Headers.SecWebSocketExtensions);
            Assert.Equal("13", context.Request.Headers.SecWebSocketVersion);
            Assert.Equal("http://www.example.com", context.Request.Headers.Origin);

            await context.Response.Body.WriteAsync(new byte[1]);
            Assert.Equal(0, await context.Request.Body.ReadAsync(new byte[1]));

            var maxRequestBodySizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            Assert.True(maxRequestBodySizeFeature.IsReadOnly);
            var stream = await connectFeature.AcceptAsync();
            Assert.True(maxRequestBodySizeFeature.IsReadOnly);
            Assert.Equal(0, await stream.ReadAsync(new byte[1]));
            await stream.WriteAsync(new byte[] { 0x01 });
        });

        // HEADERS + END_HEADERS
        // :method = CONNECT
        // :protocol = websocket
        // :scheme = https
        // :path = /chat
        // :authority = server.example.com
        // sec-websocket-protocol = chat, superchat
        // sec-websocket-extensions = permessage-deflate
        // sec-websocket-version = 13
        // origin = http://www.example.com
        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "websocket"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/chat"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "server.example.com"),
            new KeyValuePair<string, string>(HeaderNames.WebSocketSubProtocols, "chat, superchat"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketExtensions, "permessage-deflate"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketVersion, "13"),
            new KeyValuePair<string, string>(HeaderNames.Origin, "http://www.example.com"),
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS, headers);
        await SendDataAsync(1, Array.Empty<byte>(), endStream: true);

        var headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 32,
            withFlags: (byte)Http2HeadersFrameFlags.END_HEADERS,
            withStreamId: 1);

        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(2, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);

        var dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 1,
            withFlags: (byte)Http2DataFrameFlags.NONE,
            withStreamId: 1);
        Assert.Equal(0x01, dataFrame.Payload.Span[0]);

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 0,
            withFlags: (byte)Http2DataFrameFlags.END_STREAM,
            withStreamId: 1);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);
    }

    [Fact]
    public async Task HEADERS_Received_SecondRequest_Accepted()
    {
        // Add stream to Http2Connection._completedStreams inline with SetResult().
        var appDelegateTcs = new TaskCompletionSource();
        await InitializeConnectionAsync(async context =>
        {
            var connectFeature = context.Features.Get<IHttpExtendedConnectFeature>();
            Assert.True(connectFeature.IsExtendedConnect);
            Assert.Equal(HttpMethods.Connect, context.Request.Method);
            Assert.Equal("websocket", connectFeature.Protocol);
            Assert.False(context.Request.Headers.TryGetValue(":protocol", out var _));
            Assert.Equal("http", context.Request.Scheme);
            Assert.Equal("/chat", context.Request.Path.Value);
            Assert.Equal("server.example.com", context.Request.Host.Value);
            Assert.Equal("chat, superchat", context.Request.Headers.WebSocketSubProtocols);
            Assert.Equal("permessage-deflate", context.Request.Headers.SecWebSocketExtensions);
            Assert.Equal("13", context.Request.Headers.SecWebSocketVersion);
            Assert.Equal("http://www.example.com", context.Request.Headers.Origin);

            Assert.Equal(0, await context.Request.Body.ReadAsync(new byte[1]));

            var stream = await connectFeature.AcceptAsync();
            Assert.Equal(0, await stream.ReadAsync(new byte[1]));
            await stream.WriteAsync(new byte[] { 0x01 });
            await appDelegateTcs.Task;
        });

        // HEADERS + END_HEADERS
        // :method = CONNECT
        // :protocol = websocket
        // :scheme = https
        // :path = /chat
        // :authority = server.example.com
        // sec-websocket-protocol = chat, superchat
        // sec-websocket-extensions = permessage-deflate
        // sec-websocket-version = 13
        // origin = http://www.example.com
        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "websocket"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/chat"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "server.example.com"),
            new KeyValuePair<string, string>(HeaderNames.WebSocketSubProtocols, "chat, superchat"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketExtensions, "permessage-deflate"),
            new KeyValuePair<string, string>(HeaderNames.SecWebSocketVersion, "13"),
            new KeyValuePair<string, string>(HeaderNames.Origin, "http://www.example.com"),
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS, headers);
        await SendDataAsync(1, Array.Empty<byte>(), endStream: true);

        var headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 32,
            withFlags: (byte)Http2HeadersFrameFlags.END_HEADERS,
            withStreamId: 1);

        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(2, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);

        var dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 1,
            withFlags: (byte)Http2DataFrameFlags.NONE,
            withStreamId: 1);
        Assert.Equal(0x01, dataFrame.Payload.Span[0]);

        appDelegateTcs.TrySetResult();

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 0,
            withFlags: (byte)Http2DataFrameFlags.END_STREAM,
            withStreamId: 1);

        // TriggerTick will trigger the stream to be returned to the pool so we can assert it
        TriggerTick();

        // Stream has been returned to the pool
        Assert.Equal(1, _connection.StreamPool.Count);
        Assert.True(_connection.StreamPool.TryPeek(out var pooledStream));

        await SendHeadersAsync(3, Http2HeadersFrameFlags.END_HEADERS, headers);
        await SendDataAsync(3, Array.Empty<byte>(), endStream: true);

        headersFrame = await ExpectAsync(Http2FrameType.HEADERS,
            withLength: 2,
            withFlags: (byte)Http2HeadersFrameFlags.END_HEADERS,
            withStreamId: 3);

        _decodedHeaders.Clear();
        _hpackDecoder.Decode(headersFrame.PayloadSequence, endHeaders: false, handler: this);

        Assert.Equal(2, _decodedHeaders.Count);
        Assert.Contains("date", _decodedHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal("200", _decodedHeaders[HeaderNames.Status]);

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 1,
            withFlags: (byte)Http2DataFrameFlags.NONE,
            withStreamId: 3);
        Assert.Equal(0x01, dataFrame.Payload.Span[0]);

        dataFrame = await ExpectAsync(Http2FrameType.DATA,
            withLength: 0,
            withFlags: (byte)Http2DataFrameFlags.END_STREAM,
            withStreamId: 3);

        await StopConnectionAsync(expectedLastStreamId: 3, ignoreNonGoAwayFrames: false);
    }

    [Theory]
    [InlineData(":path", "/")]
    [InlineData(":scheme", "http")]
    public async Task HEADERS_Received_ExtendedCONNECTMethod_WithoutSchemeOrPath_Reset(string headerName, string value)
    {
        await InitializeConnectionAsync(_noopApplication);

        // :path and :scheme are required with :protocol, :authority is optional
        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "CONNECT"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "WebSocket"),
            new KeyValuePair<string, string>(headerName, value)
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS | Http2HeadersFrameFlags.END_STREAM, headers);

        await WaitForStreamErrorAsync(expectedStreamId: 1, Http2ErrorCode.PROTOCOL_ERROR, CoreStrings.ConnectRequestsWithProtocolRequireSchemeAndPath);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);
    }

    [Fact]
    public async Task HEADERS_Received_ProtocolWithoutCONNECTMethod_Reset()
    {
        await InitializeConnectionAsync(_noopApplication);

        var headers = new[]
        {
            new KeyValuePair<string, string>(HeaderNames.Method, "GET"),
            new KeyValuePair<string, string>(HeaderNames.Path, "/"),
            new KeyValuePair<string, string>(HeaderNames.Scheme, "http"),
            new KeyValuePair<string, string>(HeaderNames.Authority, "example.com"),
            new KeyValuePair<string, string>(HeaderNames.Protocol, "WebSocket")
        };
        await SendHeadersAsync(1, Http2HeadersFrameFlags.END_HEADERS | Http2HeadersFrameFlags.END_STREAM, headers);

        await WaitForStreamErrorAsync(expectedStreamId: 1, Http2ErrorCode.PROTOCOL_ERROR, CoreStrings.ProtocolRequiresConnect);

        await StopConnectionAsync(expectedLastStreamId: 1, ignoreNonGoAwayFrames: false);
    }
}

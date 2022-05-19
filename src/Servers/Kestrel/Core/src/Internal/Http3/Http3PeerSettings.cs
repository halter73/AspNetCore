// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http3;

internal sealed class Http3PeerSettings
{
    // Note these are protocol defaults, not Kestrel defaults.
    public const uint DefaultHeaderTableSize = 0;
    public const uint DefaultMaxRequestHeaderFieldSize = uint.MaxValue;
    public const uint DefaultEnableWebTransport = 0;
    public const uint DefaultH3Datagram = 0;

    public uint HeaderTableSize { get; internal set; } = DefaultHeaderTableSize;
    public uint MaxRequestHeaderFieldSectionSize { get; internal set; } = DefaultMaxRequestHeaderFieldSize;
    public uint EnableWebTransport { get; internal set; } = DefaultEnableWebTransport;
    public uint H3Datagram { get; internal set; } = DefaultH3Datagram;

    // Gets the settings that are different from the protocol defaults (as opposed to the server defaults).
    internal List<Http3PeerSetting> GetNonProtocolDefaults()
    {
        // By default, there are only two settings that is sent from server to client.
        // Set capacity to that value.
        var list = new List<Http3PeerSetting>(3);

        if (HeaderTableSize != DefaultHeaderTableSize)
        {
            list.Add(new Http3PeerSetting(Http3SettingType.QPackMaxTableCapacity, HeaderTableSize));
        }

        if (MaxRequestHeaderFieldSectionSize != DefaultMaxRequestHeaderFieldSize)
        {
            list.Add(new Http3PeerSetting(Http3SettingType.MaxFieldSectionSize, MaxRequestHeaderFieldSectionSize));
        }

        // indicate that Kestrel supports WebTransport (TODO move into WebTransport project and inline)
        list.Add(new Http3PeerSetting(Http3SettingType.EnableWebTransport, EnableWebTransport));

        // indicate that Kestrel supports Http3 datgrams (TODO move into WebTransport project and inline)
        list.Add(new Http3PeerSetting(Http3SettingType.H3Datagram, H3Datagram));

        return list;
    }
}

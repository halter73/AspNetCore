// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace Microsoft.AspNetCore.Http.Tests
{
    public class RequestCookiesCollectionTests
    {
        [Theory]
        [InlineData("key=value", "key", "value")]
        [InlineData("__secure-key=value", "__secure-key", "value")]
        [InlineData("key%2C=%21value", "key,", "!value")]
        [InlineData("ke%23y%2C=val%5Eue", "ke#y,", "val^ue")]
        [InlineData("base64=QUI%2BREU%2FRw%3D%3D", "base64", "QUI+REU/Rw==")]
        [InlineData("base64=QUI+REU/Rw==", "base64", "QUI+REU/Rw==")]
        public void UnEscapesValues(string input, string expectedKey, string expectedValue)
        {
            var cookies = RequestCookieCollection.Parse(new StringValues(input));

            Assert.Equal(1, cookies.Count);
            Assert.Equal(Uri.EscapeDataString(expectedKey), cookies.Keys.Single());
            Assert.Equal(expectedValue, cookies[expectedKey]);
        }

        [Theory]
        [InlineData("key=value", "key", "value")]
        [InlineData("__secure-key=value", "__secure-key", "value")]
        [InlineData("key%2C=%21value", "key,", "!value")]
        [InlineData("ke%23y%2C=val%5Eue", "ke#y,", "val^ue")]
        [InlineData("base64=QUI%2BREU%2FRw%3D%3D", "base64", "QUI+REU/Rw==")]
        [InlineData("base64=QUI+REU/Rw==", "base64", "QUI+REU/Rw==")]
        public void AppContextSwitchUnEscapesKeyValues(string input, string expectedKey, string expectedValue)
        {
            var cookies = RequestCookieCollection.ParseInternal(new StringValues(input), enableCookieNameDecoding: true);

            Assert.Equal(1, cookies.Count);
            Assert.Equal(expectedKey, cookies.Keys.Single());
            Assert.Equal(expectedValue, cookies[expectedKey]);
        }
    }
}

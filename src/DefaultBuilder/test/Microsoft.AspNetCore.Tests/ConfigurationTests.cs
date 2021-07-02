// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Tests
{
    public class ConfigurationTests
    {
        [Fact]
        public void AutoUpdates()
        {
            var config = new Configuration();

            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "TestKey", "TestValue" },
            });

            Assert.Equal("TestValue", config["TestKey"]);
        }

        [Fact]
        public void TriggersReloadTokenOnSourceAddition()
        {
            var config = new Configuration();

            var reloadToken = ((IConfiguration)config).GetReloadToken();

            Assert.False(reloadToken.HasChanged);

            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                { "TestKey", "TestValue" },
            });

            Assert.True(reloadToken.HasChanged);
        }


        [Fact]
        public void SettingValuesWorksWithoutManuallyAddingSource()
        {
            var config = new Configuration
            {
                ["TestKey"] = "TestValue",
            };

            Assert.Equal("TestValue", config["TestKey"]);
        }

        [Fact]
        public void SettingValuesDoesNotTriggerReloadTokenWithOrWithoutAutoUpdate()
        {
            var config = new Configuration();
            var reloadToken = ((IConfiguration)config).GetReloadToken();

            config["TestKey"] = "TestValue";

            Assert.Equal("TestValue", config["TestKey"]);

            // ConfigurationRoot doesn't fire the token today when the setter is called. Maybe we should change that.
            // At least you can manually call Configuration.Update() to fire a reload though this reloads all sources unnecessarily.
            Assert.False(reloadToken.HasChanged);
        }

        [Fact]
        public void SettingIConfigurationBuilderPropertiesWorks()
        {
            var config = new Configuration();

            var configBuilder = (IConfigurationBuilder)config;

            var reloadToken = ((IConfiguration)config).GetReloadToken();

            configBuilder.Properties["TestKey"] = "TestValue";

            Assert.Equal("TestValue", configBuilder.Properties["TestKey"]);

            // Changing properties should not change config keys or fire reload token.
            Assert.Null(config["TestKey"]);
            Assert.False(reloadToken.HasChanged);
        }

        [Fact]
        public void RootDisposesProviders()
        {
            var provider1 = new TestConfigurationProvider("foo", "foo-value");
            var provider2 = new DisposableTestConfigurationProvider("bar", "bar-value");
            var provider3 = new TestConfigurationProvider("baz", "baz-value");
            var provider4 = new DisposableTestConfigurationProvider("qux", "qux-value");
            var provider5 = new DisposableTestConfigurationProvider("quux", "quux-value");

            var config = new Configuration();
            IConfigurationBuilder builder = config;

            builder.Add(new TestConfigurationSource(provider1));
            builder.Add(new TestConfigurationSource(provider2));
            builder.Add(new TestConfigurationSource(provider3));
            builder.Add(new TestConfigurationSource(provider4));
            builder.Add(new TestConfigurationSource(provider5));

            Assert.Equal("foo-value", config["foo"]);
            Assert.Equal("bar-value", config["bar"]);
            Assert.Equal("baz-value", config["baz"]);
            Assert.Equal("qux-value", config["qux"]);
            Assert.Equal("quux-value", config["quux"]);

            config.Dispose();

            Assert.True(provider2.IsDisposed);
            Assert.True(provider4.IsDisposed);
            Assert.True(provider5.IsDisposed);
        }

        [Fact]
        public void RootDisposesChangeTokenRegistrations()
        {
            var changeToken = new TestChangeToken();
            var providerMock = new Mock<IConfigurationProvider>();
            providerMock.Setup(p => p.GetReloadToken()).Returns(changeToken);

            var config = new Configuration();

            ((IConfigurationBuilder)config).Add(new TestConfigurationSource(providerMock.Object));

            Assert.NotEmpty(changeToken.Callbacks);

            config.Dispose();

            Assert.Empty(changeToken.Callbacks);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ChainedConfigurationIsDisposed(bool shouldDispose)
        {
            var provider = new DisposableTestConfigurationProvider("foo", "foo-value");
            var chainedConfig = new ConfigurationRoot(new IConfigurationProvider[] {
                provider
            });

            var config = new Configuration();

            config.AddConfiguration(chainedConfig, shouldDisposeConfiguration: shouldDispose);

            Assert.False(provider.IsDisposed);

            (config as IDisposable).Dispose();

            Assert.Equal(shouldDispose, provider.IsDisposed);
        }

        private class TestConfigurationSource : IConfigurationSource
        {
            private readonly IConfigurationProvider _provider;

            public TestConfigurationSource(IConfigurationProvider provider)
            {
                _provider = provider;
            }

            public IConfigurationProvider Build(IConfigurationBuilder builder)
            {
                return _provider;
            }
        }

        private class TestConfigurationProvider : ConfigurationProvider
        {
            public TestConfigurationProvider(string key, string value)
                => Data.Add(key, value);
        }

        private class DisposableTestConfigurationProvider : ConfigurationProvider, IDisposable
        {
            public bool IsDisposed { get; set; }

            public DisposableTestConfigurationProvider(string key, string value)
                => Data.Add(key, value);

            public void Dispose()
                => IsDisposed = true;
        }

        private class TestChangeToken : IChangeToken
        {
            public List<(Action<object>, object)> Callbacks { get; } = new List<(Action<object>, object)>();

            public bool HasChanged => false;

            public bool ActiveChangeCallbacks => true;

            public IDisposable RegisterChangeCallback(Action<object> callback, object state)
            {
                var item = (callback, state);
                Callbacks.Add(item);
                return new DisposableAction(() => Callbacks.Remove(item));
            }

            private class DisposableAction : IDisposable
            {
                private Action _action;

                public DisposableAction(Action action)
                {
                    _action = action;
                }

                public void Dispose()
                {
                    var a = _action;
                    if (a != null)
                    {
                        _action = null;
                        a();
                    }
                }
            }
        }
    }
}

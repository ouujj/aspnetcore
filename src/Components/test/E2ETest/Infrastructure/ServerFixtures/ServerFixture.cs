// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.AspNetCore.E2ETesting;

namespace Microsoft.AspNetCore.Components.E2ETest.Infrastructure.ServerFixtures
{
    public abstract class ServerFixture : IDisposable
    {
        private static readonly Lazy<Dictionary<string, string>> _projects = new Lazy<Dictionary<string, string>>(FindProjects);

        public Uri RootUri => _rootUriInitializer.Value;

        private readonly Lazy<Uri> _rootUriInitializer;

        public ServerFixture()
        {
            _rootUriInitializer = new Lazy<Uri>(() =>
            {
                var uri = new Uri(StartAndGetRootUri());
                if (E2ETestOptions.Instance.SauceTest)
                {
                    uri = new UriBuilder(uri.Scheme, E2ETestOptions.Instance.Sauce.HostName, uri.Port).Uri;
                }

                return uri;
            });
        }

        public abstract void Dispose();

        protected abstract string StartAndGetRootUri();

        private static Dictionary<string, string> FindProjects()
        {
            return typeof(ServerFixture).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                .Where(m => m.Key.StartsWith("TestAssemblyApplication["))
                .ToDictionary(m =>
                    m.Key.Replace("TestAssemblyApplication", "").TrimStart('[').TrimEnd(']'),
                    m => m.Value);
        }

        public static string FindSampleOrTestSitePath(string projectName)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("helix")))
            {
                var dir = Path.Combine(AppContext.BaseDirectory, projectName);
                if (!Directory.Exists(dir)
                {
                    throw new ArgumentException($"Cannot find a sample or test site directory: '{dir}'.");
                }
                return dir;
            }
        
            var projects = _projects.Value;
            if (projects.TryGetValue(projectName, out var dir))
            {
                return dir;
            }

            throw new ArgumentException($"Cannot find a sample or test site with name '{projectName}'.");
        }

        protected static void RunInBackgroundThread(Action action)
        {
            var isDone = new ManualResetEvent(false);

            ExceptionDispatchInfo edi = null;
            new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    edi = ExceptionDispatchInfo.Capture(ex);
                }

                isDone.Set();
            }).Start();

            if (!isDone.WaitOne(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("Timed out waiting for: " + action);
            }

            if (edi != null)
            {
                throw edi.SourceException;
            }
        }
    }
}

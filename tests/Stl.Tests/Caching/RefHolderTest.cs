using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Stl.Caching;
using Stl.Testing;
using Stl.Testing.Collections;
using Xunit;

namespace Stl.Tests.Caching
{
    [Collection(nameof(TimeSensitiveTests)), Trait("Category", nameof(TimeSensitiveTests))]
    public class RefHolderTest
    {
        [Fact]
        public void BasicTest()
        {
            var holder = new RefHolder();
            var count = TestRunnerInfo.IsBuildAgent() ? 100 : 1000;
            var objects = Enumerable.Range(0, count).Select(i => i.ToString()).ToArray();

            var holds = new HashSet<IDisposable>();
            foreach (var o in objects)
                holds.Add(holder.Hold(o));

            holder.IsEmpty.Should().BeFalse();

            // HashSet randomizes the order of disposal
            foreach (var hold in holds)
                hold.Dispose();
            holder.IsEmpty.Should().BeTrue();
        }
    }
}

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HackPDM.Core;
using Xunit;

namespace HackPDM.Tests.Core;

public class AsyncHelperTests
{
    [Fact]
    public async Task WaitUntil_ConditionBecomesTrue_ReturnsTrue()
    {
        int checks = 0;
        Assert.True(await AsyncHelper.WaitUntil(() => ++checks >= 3, pollingMs: 10, msTimeout: 5_000));
    }

    [Fact]
    public async Task WaitUntil_Timeout_ReturnsFalse()
        => Assert.False(await AsyncHelper.WaitUntil(() => false, pollingMs: 10, msTimeout: 50));

    [Fact]
    public async Task WaitUntil_Cancelled_StopsAnIndefiniteWait()
    {
        using CancellationTokenSource cancel = new(100);
        Stopwatch watch = Stopwatch.StartNew();

        bool met = await AsyncHelper.WaitUntil(() => false, pollingMs: 20, msTimeout: -1, token: cancel.Token);

        Assert.False(met);
        Assert.True(watch.Elapsed.TotalSeconds < 5, "the token ended the wait");
    }
}

namespace GmrFinder.IntegrationTests;

public static class AsyncWaiter
{
    private static readonly TimeSpan s_pollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(30);

    public static async Task<bool> WaitForAsync(
        Func<Task<bool>> condition,
        CancellationToken cancellationToken = default
    )
    {
        var deadline = DateTime.UtcNow + s_timeout;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (await condition().ConfigureAwait(false))
            {
                return true;
            }

            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < s_pollInterval ? remaining : s_pollInterval, cancellationToken)
                .ConfigureAwait(false);
        }

        return false;
    }
}

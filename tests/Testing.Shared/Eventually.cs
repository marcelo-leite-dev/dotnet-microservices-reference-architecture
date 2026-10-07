namespace Testing.Shared;

/// <summary>Polls an eventually-consistent read model until it reaches the expected state.</summary>
public static class Eventually
{
    public static async Task<T> MatchesAsync<T>(Func<Task<T>> probe, Func<T, bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        T last;

        do
        {
            last = await probe();
            if (condition(last))
            {
                return last;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
        while (DateTime.UtcNow < deadline);

        return last;
    }
}

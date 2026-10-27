namespace Badminton.Core.Policies;

public static class GroupCostAllocation
{
    public static IReadOnlyDictionary<int, int> Allocate(int total, IReadOnlyDictionary<int, int> weights)
    {
        if (total < 0 || weights.Count == 0 || weights.Values.Any(x => x < 0) || weights.Values.Sum(x => (long)x) == 0)
            throw new ArgumentException("Invalid amount or participation weights.");
        var denominator = weights.Values.Sum(x => (long)x);
        var shares = weights.ToDictionary(x => x.Key, x => (int)((long)total * x.Value / denominator));
        var remaining = total - shares.Values.Sum();
        foreach (var userId in weights.OrderByDescending(x => (long)total * x.Value % denominator).ThenBy(x => x.Key).Take(remaining).Select(x => x.Key))
            shares[userId]++;
        return shares;
    }
}

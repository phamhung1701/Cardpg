using System.Collections.Generic;

public static class ListExtensions
{
    private static readonly System.Random _rng = new System.Random();

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = _rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public static T RandomPick<T>(this IList<T> list)
    {
        if (list.Count == 0) return default;
        return list[_rng.Next(list.Count)];
    }

    public static void RemoveRange<T>(this IList<T> list, int index, int count)
    {
        for (int i = count - 1; i >= 0; i--)
        {
            if (index + i < list.Count)
                list.RemoveAt(index + i);
        }
    }
}

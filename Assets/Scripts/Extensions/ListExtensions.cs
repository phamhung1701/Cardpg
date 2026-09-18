using System;
using System.Collections.Generic;

public static class ListExtensions
{
    public static void Shuffle<T>(this IList<T> list, IRandomSource random)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        if (random == null) throw new ArgumentNullException(nameof(random));

        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.NextInt(0, n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public static T RandomPick<T>(this IList<T> list, IRandomSource random)
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        if (random == null) throw new ArgumentNullException(nameof(random));
        return list.Count == 0 ? default : list[random.NextInt(0, list.Count)];
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

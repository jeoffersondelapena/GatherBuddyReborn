#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace GatherBuddy.ForkLogic;

public static class QueueRules
{
    // The end is read once: a loop that re-read it walked its own copies and never ended, freezing the game.
    public static int AppendCopies<T>(List<T> queue, int from, Func<T, bool> match, Func<T, T> copy)
    {
        var copied = 0;
        var end    = queue.Count;
        for (var i = Math.Max(from, 0); i < end; i++)
        {
            var item = queue[i];
            if (!match(item))
                continue;

            queue.Add(copy(item));
            copied++;
        }

        return copied;
    }

    public static int MarkRange<T>(IReadOnlyList<T> queue, int from, int end, Func<T, bool> match, Action<T, int> mark)
    {
        var marked = 0;
        var bound  = Math.Min(end, queue.Count);
        for (var i = Math.Max(from, 0); i < bound; i++)
        {
            var item = queue[i];
            if (!match(item))
                continue;

            mark(item, i);
            marked++;
        }

        return marked;
    }

    public static List<T> ProducersFirst<T>(IEnumerable<T> precrafts, Func<T, uint> id, Func<T, uint> makes, Func<T, IEnumerable<uint>> needs)
    {
        var items  = precrafts.ToList();
        var makers = items.ToLookup(makes);
        var seen   = new HashSet<uint>();
        var result = new List<T>(items.Count);

        void Visit(T item)
        {
            if (!seen.Add(id(item)))
                return;

            foreach (var need in needs(item))
            foreach (var maker in makers[need])
                Visit(maker);
            result.Add(item);
        }

        foreach (var item in items)
            Visit(item);
        return result;
    }

    public static T? PickRecipe<T>(IReadOnlyList<T> recipes, uint neededBy, Func<T, uint> classOf, Func<T, bool> canCraftNow) where T : struct
    {
        if (recipes.Count == 0)
            return null;
        if (recipes.Count == 1)
            return recipes[0];

        T? own = null, usable = null;
        foreach (var recipe in recipes)
        {
            var can = canCraftNow(recipe);
            if (classOf(recipe) == neededBy)
            {
                if (can)
                    return recipe;

                own ??= recipe;
            }

            if (can)
                usable ??= recipe;
        }

        return usable ?? own ?? recipes[0];
    }

    // without the one try a day, a recipe its crafter cannot make HQ would be crafted again on every start
    public static bool OnlyHqCounts(bool listOption, string triedDay, IEnumerable<uint> tried, uint recipeId, string today)
        => listOption && !(triedDay == today && tried.Contains(recipeId));

    public static (int Taken, int NQ, int HQ) TakeHeld(int nq, int hq, int requested, bool onlyHq)
    {
        var usableNq = onlyHq ? 0 : nq;
        var taken    = Math.Max(0, Math.Min(requested, usableNq + hq));
        var fromNq   = Math.Min(taken, usableNq);
        return (taken, nq - fromNq, hq - (taken - fromNq));
    }
}

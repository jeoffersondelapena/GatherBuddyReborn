using GatherBuddy.ForkLogic;
using Xunit;

public class QueueRulesTests
{
    private sealed class Craft(uint recipe)
    {
        public uint Recipe  { get; } = recipe;
        public bool Skipped { get; set; }
        public bool IsCopy  { get; init; }
    }

    private static List<Craft> Queue(params uint[] recipes)
        => recipes.Select(r => new Craft(r)).ToList();

    [Fact]
    public void Putting_a_recipe_off_appends_one_copy_per_remaining_entry_and_ends()
    {
        var queue = Queue(1, 7, 2, 7, 7);

        // the match also fits the copies: a loop that walked them would never end, so the copier gives up loudly
        var made   = 0;
        var copied = QueueRules.AppendCopies(queue, 1, c => c.Recipe == 7,
            c => ++made > 100 ? throw new InvalidOperationException("the loop is walking its own copies") : new Craft(c.Recipe) { IsCopy = true });

        Assert.Equal(3, copied);
        Assert.Equal(new uint[] { 1, 7, 2, 7, 7, 7, 7, 7 }, queue.Select(c => c.Recipe));
        Assert.Equal(3, queue.Count(c => c.IsCopy));
        Assert.All(queue.Take(5), c => Assert.False(c.IsCopy));
    }

    [Fact]
    public void Entries_before_the_current_one_and_skipped_ones_are_not_copied()
    {
        var queue = Queue(7, 7, 7);
        queue[2].Skipped = true;

        var copied = QueueRules.AppendCopies(queue, 1, c => c.Recipe == 7 && !c.Skipped, c => new Craft(c.Recipe) { IsCopy = true });

        Assert.Equal(1, copied);
        Assert.Equal(4, queue.Count);
        Assert.Equal(0, QueueRules.AppendCopies(Queue(), 0, _ => true, c => c));
        Assert.Equal(0, QueueRules.AppendCopies(queue, 99, _ => true, c => c));
    }

    [Fact]
    public void Skipping_the_originals_leaves_their_copies_at_the_end_to_be_tried()
    {
        var queue = Queue(7, 3, 7);
        var end   = queue.Count;

        QueueRules.AppendCopies(queue, 0, c => c.Recipe == 7 && !c.Skipped, c => new Craft(c.Recipe) { IsCopy = true });
        var marked = QueueRules.MarkRange(queue, 0, end, c => c.Recipe == 7 && !c.Skipped, (c, _) => c.Skipped = true);

        Assert.Equal(2, marked);
        Assert.All(queue.Where(c => c.IsCopy), c => Assert.False(c.Skipped));
        Assert.All(queue.Where(c => !c.IsCopy && c.Recipe == 7), c => Assert.True(c.Skipped));
        Assert.False(queue[1].Skipped);
    }

    [Fact]
    public void Marking_stays_inside_its_range()
    {
        var queue   = Queue(7, 7, 7, 7);
        var indexes = new List<int>();

        Assert.Equal(2, QueueRules.MarkRange(queue, 1, 3, c => c.Recipe == 7, (c, i) => { c.Skipped = true; indexes.Add(i); }));
        Assert.Equal(new[] { 1, 2 }, indexes);
        Assert.Equal(new[] { false, true, true, false }, queue.Select(c => c.Skipped));
        Assert.Equal(1, QueueRules.MarkRange(queue, 3, 99, c => !c.Skipped, (c, _) => c.Skipped = true));
    }

    private readonly record struct Pre(uint Id, uint Makes, uint[] Needs);

    private static uint[] Order(params Pre[] precrafts)
        => QueueRules.ProducersFirst(precrafts, p => p.Id, p => p.Makes, p => p.Needs).Select(p => p.Id).ToArray();

    [Fact]
    public void A_material_is_queued_before_what_needs_it()
    {
        // plate (needs ingot) was planned before the ingot, as the planner does
        Assert.Equal(new uint[] { 170, 173 }, Order(new Pre(173, 5058, new uint[] { 5056 }), new Pre(170, 5056, new uint[] { 5106 })));
    }

    [Fact]
    public void Every_recipe_that_makes_a_material_comes_before_its_consumer()
    {
        // two classes make the same ingot in one plan: rivets need it, whichever recipe made it
        var order = Order(
            new Pre(5, 5091, new uint[] { 5056 }),
            new Pre(1, 5056, Array.Empty<uint>()),
            new Pre(173, 5058, new uint[] { 5056 }),
            new Pre(170, 5056, Array.Empty<uint>()));

        Assert.Equal(new uint[] { 1, 170, 5, 173 }, order);
    }

    [Fact]
    public void Unrelated_precrafts_keep_their_order_and_nothing_is_lost()
    {
        Assert.Equal(new uint[] { 9, 4, 6 }, Order(new Pre(9, 90, Array.Empty<uint>()), new Pre(4, 40, Array.Empty<uint>()), new Pre(6, 60, new uint[] { 12345 })));
        Assert.Empty(Order());
    }

    [Fact]
    public void A_loop_in_the_recipes_still_ends()
    {
        var order = Order(new Pre(1, 10, new uint[] { 20 }), new Pre(2, 20, new uint[] { 10 }), new Pre(3, 30, new uint[] { 30 }));
        Assert.Equal(new uint[] { 1, 2, 3 }, order.OrderBy(id => id));
    }

    private readonly record struct Recipe(uint Id, uint Class, bool CanCraft);

    private static uint? Pick(uint neededBy, params Recipe[] recipes)
        => QueueRules.PickRecipe(recipes, neededBy, r => r.Class, r => r.CanCraft)?.Id;

    [Fact]
    public void The_class_that_needs_a_material_makes_it_when_it_can()
        => Assert.Equal(170u, Pick(2, new Recipe(1, 1, true), new Recipe(170, 2, true)));

    [Fact]
    public void Otherwise_the_first_class_that_can_craft_it_now_does()
    {
        Assert.Equal(1u, Pick(2, new Recipe(1, 1, true), new Recipe(170, 2, false)));
        Assert.Equal(688u, Pick(0, new Recipe(5, 1, false), new Recipe(688, 3, true), new Recipe(700, 4, true)));
    }

    [Fact]
    public void When_nobody_can_the_needing_class_keeps_its_own_recipe()
    {
        Assert.Equal(189u, Pick(2, new Recipe(43, 1, false), new Recipe(189, 2, false)));
        Assert.Equal(43u, Pick(5, new Recipe(43, 1, false), new Recipe(189, 2, false)));
    }

    [Fact]
    public void A_class_with_two_recipes_for_the_item_uses_the_first_it_can_craft()
        => Assert.Equal(12u, Pick(2, new Recipe(10, 1, true), new Recipe(11, 2, false), new Recipe(12, 2, true)));

    [Fact]
    public void One_recipe_is_taken_as_it_is_and_none_gives_none()
    {
        Assert.Equal(663u, QueueRules.PickRecipe(new[] { new Recipe(663, 3, false) }, 2, r => r.Class, _ => throw new InvalidOperationException("not asked"))?.Id);
        Assert.Null(Pick(2));
    }

    [Fact]
    public void With_only_HQ_counting_an_NQ_copy_is_left_alone_and_the_item_still_counts_as_missing()
    {
        Assert.Equal((0, 2, 0), QueueRules.TakeHeld(nq: 2, hq: 0, requested: 1, onlyHq: true));
        Assert.Equal((1, 2, 0), QueueRules.TakeHeld(nq: 2, hq: 1, requested: 1, onlyHq: true));
        Assert.Equal((1, 1, 1), QueueRules.TakeHeld(nq: 2, hq: 1, requested: 1, onlyHq: false));
        Assert.Equal((3, 0, 0), QueueRules.TakeHeld(nq: 2, hq: 1, requested: 5, onlyHq: false));
        Assert.Equal((0, 2, 1), QueueRules.TakeHeld(nq: 2, hq: 1, requested: 0, onlyHq: false));
    }

    [Fact]
    public void A_recipe_gets_one_try_a_day_before_any_copy_counts_again()
    {
        uint[] tried = [61];
        Assert.True(QueueRules.OnlyHqCounts(true, "2026-10-03", tried, 197, "2026-10-03"));
        Assert.False(QueueRules.OnlyHqCounts(true, "2026-10-03", tried, 61, "2026-10-03"));
        Assert.True(QueueRules.OnlyHqCounts(true, "2026-10-02", tried, 61, "2026-10-03"));
        Assert.False(QueueRules.OnlyHqCounts(false, "", [], 61, "2026-10-03"));
    }
}

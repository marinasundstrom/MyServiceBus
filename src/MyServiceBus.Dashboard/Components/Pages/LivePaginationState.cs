namespace MyServiceBus.Dashboard.Components.Pages;

internal sealed class LivePaginationState<T>(int pageSize, Func<T, string> itemSignature)
{
    private IReadOnlyList<T>? heldItems;
    private string? heldSignature;
    private int offset;

    public int PageSize { get; } = pageSize;

    public IReadOnlyList<T> Page(IReadOnlyList<T> currentItems)
    {
        var source = Source(currentItems);
        return source.Skip(EffectiveOffset(source.Count)).Take(PageSize).ToArray();
    }

    public int Offset(IReadOnlyList<T> currentItems) => EffectiveOffset(Source(currentItems).Count);

    public int Total(IReadOnlyList<T> currentItems) => Source(currentItems).Count;

    public bool UpdatesAvailable(IReadOnlyList<T> currentItems)
        => heldItems is not null
            && !string.Equals(heldSignature, Signature(currentItems), StringComparison.Ordinal);

    public void MoveTo(int value, IReadOnlyList<T> currentItems)
    {
        if (value <= 0)
        {
            offset = 0;
            heldItems = null;
            heldSignature = null;
            return;
        }

        if (heldItems is null)
        {
            heldItems = currentItems.ToArray();
            heldSignature = Signature(currentItems);
        }

        offset = value;
    }

    public void ShowLatest() => MoveTo(0, []);

    private IReadOnlyList<T> Source(IReadOnlyList<T> currentItems) => heldItems ?? currentItems;

    private int EffectiveOffset(int count)
        => count == 0 ? 0 : Math.Min(offset, (count - 1) / PageSize * PageSize);

    private string Signature(IReadOnlyList<T> items)
        => string.Join('\u001e', items.Select(itemSignature));
}

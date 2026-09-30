namespace Clean.Core.Apps;

public readonly record struct TreemapRect(double X, double Y, double Width, double Height);

// Squarified treemap (Bruls, Huizing, van Wijk): tiles stay close to squares, which keeps them readable.
public static class Treemap
{
    public static IReadOnlyList<TreemapRect> Layout(IReadOnlyList<double> values, double width, double height)
    {
        var rects = new TreemapRect[values.Count];
        var total = values.Where(value => value > 0).Sum();
        if (values.Count == 0 || width <= 0 || height <= 0 || total <= 0)
        {
            return rects;
        }

        // Values become areas; they must be sorted from largest to smallest for the algorithm to work.
        var scale = width * height / total;
        var order = Enumerable.Range(0, values.Count)
            .Where(index => values[index] > 0)
            .OrderByDescending(index => values[index])
            .ToList();

        var free = new TreemapRect(0, 0, width, height);
        var row = new List<int>();
        var position = 0;

        while (position < order.Count)
        {
            var index = order[position];
            var side = Math.Min(free.Width, free.Height);
            if (row.Count == 0 || Worst(row, index, side, values, scale) <= Worst(row, null, side, values, scale))
            {
                row.Add(index);
                position++;
                continue;
            }

            free = PlaceRow(row, free, values, scale, rects);
            row.Clear();
        }

        if (row.Count > 0)
        {
            PlaceRow(row, free, values, scale, rects);
        }

        return rects;
    }

    private static double Worst(List<int> row, int? extra, double side, IReadOnlyList<double> values, double scale)
    {
        var areas = row.Select(index => values[index] * scale).ToList();
        if (extra is { } added)
        {
            areas.Add(values[added] * scale);
        }

        var sum = areas.Sum();
        var sideSquared = side * side;
        return areas.Max(area => Math.Max(sideSquared * area / (sum * sum), sum * sum / (sideSquared * area)));
    }

    private static TreemapRect PlaceRow(List<int> row, TreemapRect free, IReadOnlyList<double> values, double scale, TreemapRect[] rects)
    {
        var rowArea = row.Sum(index => values[index] * scale);

        // The row fills the shorter side of the free space, then the rest is laid out next to it.
        if (free.Width >= free.Height)
        {
            var rowWidth = rowArea / free.Height;
            var y = free.Y;
            foreach (var index in row)
            {
                var tileHeight = values[index] * scale / rowWidth;
                rects[index] = new TreemapRect(free.X, y, rowWidth, tileHeight);
                y += tileHeight;
            }

            return free with { X = free.X + rowWidth, Width = Math.Max(0, free.Width - rowWidth) };
        }

        var rowHeight = rowArea / free.Width;
        var x = free.X;
        foreach (var index in row)
        {
            var tileWidth = values[index] * scale / rowHeight;
            rects[index] = new TreemapRect(x, free.Y, tileWidth, rowHeight);
            x += tileWidth;
        }

        return free with { Y = free.Y + rowHeight, Height = Math.Max(0, free.Height - rowHeight) };
    }
}

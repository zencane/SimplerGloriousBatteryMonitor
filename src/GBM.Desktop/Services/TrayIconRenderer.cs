using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace GBM.Desktop.Services;

internal static class TrayIconRenderer
{
    private const int IconSize = 32;
    private const int MaxCachedIcons = 96;
    private static readonly object CacheLock = new();
    private static readonly Dictionary<IconCacheKey, CacheEntry> IconCache = new();
    private static readonly LinkedList<IconCacheKey> CacheOrder = new();

    private static readonly Color TealColor = Color.Parse("#00E5CC");
    private static readonly Color AmberColor = Color.Parse("#FF8A50");
    private static readonly Color RedColor = Color.Parse("#FF5252");
    private static readonly Color DarkRedColor = Color.Parse("#8B0000");
    private static readonly Color GreenColor = Color.Parse("#1DB954");
    private static readonly Color GrayColor = Color.Parse("#6B7280");

    public static WindowIcon? RenderIcon(int level, bool isCharging, bool isConnected)
    {
        level = Math.Clamp(level, 0, 100);
        var cacheKey = isConnected
            ? new IconCacheKey(level, isCharging, true)
            : new IconCacheKey(0, false, false);

        try
        {
            lock (CacheLock)
            {
                if (IconCache.TryGetValue(cacheKey, out var cachedEntry))
                {
                    TouchCacheEntry(cachedEntry);
                    return cachedEntry.Icon;
                }

                var canvas = new IconCanvas
                {
                    Level = cacheKey.Level,
                    IsCharging = cacheKey.IsCharging,
                    IsConnected = cacheKey.IsConnected,
                    Width = IconSize,
                    Height = IconSize
                };

                canvas.Measure(new Size(IconSize, IconSize));
                canvas.Arrange(new Rect(0, 0, IconSize, IconSize));

                using var rtb = new RenderTargetBitmap(new PixelSize(IconSize, IconSize), new Vector(96, 96));
                rtb.Render(canvas);

                var icon = new WindowIcon(rtb);
                AddCacheEntry(cacheKey, icon);
                return icon;
            }
        }
        catch
        {
            return null;
        }
    }

    private static void TouchCacheEntry(CacheEntry entry)
    {
        if (entry.Node == CacheOrder.First)
            return;

        CacheOrder.Remove(entry.Node);
        CacheOrder.AddFirst(entry.Node);
    }

    private static void AddCacheEntry(IconCacheKey key, WindowIcon icon)
    {
        var node = new LinkedListNode<IconCacheKey>(key);
        CacheOrder.AddFirst(node);
        IconCache[key] = new CacheEntry(icon, node);

        if (IconCache.Count <= MaxCachedIcons)
            return;

        var lastNode = CacheOrder.Last;
        if (lastNode == null)
            return;

        CacheOrder.RemoveLast();
        IconCache.Remove(lastNode.Value);
    }

    private sealed class CacheEntry
    {
        public CacheEntry(WindowIcon icon, LinkedListNode<IconCacheKey> node)
        {
            Icon = icon;
            Node = node;
        }

        public WindowIcon Icon { get; }
        public LinkedListNode<IconCacheKey> Node { get; }
    }

    private readonly record struct IconCacheKey(
        int Level,
        bool IsCharging,
        bool IsConnected);

    private static Color GetFillColor(int level, bool isCharging, bool isConnected)
    {
        if (!isConnected) return GrayColor;
        if (isCharging) return GreenColor;
        if (level > 40) return TealColor;
        if (level >= 20) return AmberColor;
        if (level >= 10) return RedColor;
        return DarkRedColor;
    }

    private sealed class IconCanvas : Control
    {
        public int Level { get; init; }
        public bool IsCharging { get; init; }
        public bool IsConnected { get; init; }

        public override void Render(DrawingContext context)
        {
            var fillColor = GetFillColor(Level, IsCharging, IsConnected);
            var text = IsConnected ? Level.ToString() : "--";
            var fontSize = Level >= 100 ? 14.0 : (Level >= 10 || !IsConnected ? 18.0 : 22.0);

            var formattedText = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold),
                fontSize,
                new SolidColorBrush(fillColor));

            var x = (IconSize - formattedText.Width) / 2.0;
            var y = (IconSize - formattedText.Height) / 2.0;
            context.DrawText(formattedText, new Point(x, y));

            if (IsCharging && IsConnected)
            {
                context.DrawRectangle(
                    new SolidColorBrush(GreenColor),
                    null,
                    new RoundedRect(new Rect(24, 24, 6, 6), 3));
            }
        }
    }
}

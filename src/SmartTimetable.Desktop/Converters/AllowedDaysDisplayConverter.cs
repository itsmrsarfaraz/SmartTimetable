using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace SmartTimetable.Desktop.Converters;

/// <summary>
/// Formats a <c>ClassSubject.AllowedDaysCsv</c> (comma-separated Weekday ints, 1=Mon..6=Sat,
/// 0=Sun) into a short human label for the weekly-subjects grid. An empty value means the
/// subject is unrestricted, shown as "All days".
/// </summary>
public sealed class AllowedDaysDisplayConverter : IValueConverter
{
    /// <summary>Shared instance so the view can reference it with {x:Static}.</summary>
    public static readonly AllowedDaysDisplayConverter Instance = new();

    // Display order: Mon..Sat then Sun, matching the demand editor.
    private static readonly (int Value, string Label)[] WeekOrder =
    {
        (1, "Mon"), (2, "Tue"), (3, "Wed"), (4, "Thu"), (5, "Fri"), (6, "Sat"), (0, "Sun"),
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var csv = value as string;
        if (string.IsNullOrWhiteSpace(csv)) return "All days";

        var set = new HashSet<int>();
        foreach (var tok in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(tok, out int v)) set.Add(v);

        if (set.Count == 0) return "All days";

        var parts = WeekOrder.Where(d => set.Contains(d.Value)).Select(d => d.Label);
        return string.Join(", ", parts);
    }

    // Display-only converter. A DataGridTextColumn binding is TwoWay by default, so Avalonia
    // may invoke ConvertBack even for a read-only cell — return the "skip this write" sentinel
    // instead of throwing, which would otherwise bubble up as an unhandled crash.
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartTimetable.Desktop;

/// <summary>
/// Resolves a View for a given ViewModel by naming convention:
/// <c>...ViewModels.FooViewModel</c> -&gt; <c>...Views.FooView</c>.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        if (data is null)
            return new TextBlock { Text = "No view model." };

        string name = data.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type is not null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "View not found: " + name };
    }

    public bool Match(object? data) => data is ObservableObject;
}

using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// Base class for the shell's navigable pages. Each page exposes a display
/// <see cref="Title"/> and an async <see cref="LoadAsync"/> hook the shell calls
/// whenever the page becomes visible so it can (re)load its data.
/// </summary>
public abstract partial class PageViewModel : ObservableObject
{
    /// <summary>Heading shown in the shell's content header.</summary>
    public abstract string Title { get; }

    /// <summary>Optional one-line description shown under the title.</summary>
    public virtual string Description => string.Empty;

    /// <summary>Called by the shell when the page is shown. Override to load data.</summary>
    public virtual Task LoadAsync() => Task.CompletedTask;
}

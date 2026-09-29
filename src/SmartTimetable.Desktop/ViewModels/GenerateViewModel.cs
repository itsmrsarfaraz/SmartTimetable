using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Application.Solving;
using SmartTimetable.Domain.Enums;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>A selectable generation strategy with a friendly label.</summary>
public partial class StrategyOption : ObservableObject
{
    public GenerationStrategy Strategy { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Blurb { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

/// <summary>
/// Runs the OR-Tools solver for the active session and lists the resulting
/// timetable options. Each option can be opened in the timetable viewer.
/// </summary>
public partial class GenerateViewModel : PageViewModel
{
    private readonly ITimetableGenerationService _generation;
    private readonly AppState _state;
    private CancellationTokenSource? _cts;

    public override string Title => "Generate Timetable";
    public override string Description => "Pick one or more optimization strategies and let the solver build conflict-free timetables.";

    /// <summary>Raised when the user opens a produced option; carries the timetable id.</summary>
    public event Action<int>? OpenTimetableRequested;

    public ObservableCollection<StrategyOption> Strategies { get; } = new();
    public ObservableCollection<TimetableOptionSummary> Options { get; } = new();

    /// <summary>The Custom option, exposed so the view can show its weight editors only when it is ticked.</summary>
    public StrategyOption CustomStrategy { get; }

    [ObservableProperty] private int _maxSeconds = 15;
    [ObservableProperty] private int _randomSeed = 1;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private bool _hasResults;

    /// <summary>
    /// True when a run has finished but produced no rows at all (a guard rail stopped it,
    /// or it threw). In that case the results grid is empty, so the view shows a large,
    /// centred message with <see cref="Status"/> instead of a blank panel — otherwise the
    /// only explanation sits in small text on the left and reads as "nothing happened".
    /// </summary>
    public bool ShowEmptyState => !IsBusy && !HasResults && !string.IsNullOrEmpty(Status);

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));
    partial void OnHasResultsChanged(bool value) => OnPropertyChanged(nameof(ShowEmptyState));
    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(ShowEmptyState));

    // ----- Custom-strategy weights (0 disables that objective) -----
    [ObservableProperty] private int _weightTeacherPref = 2;
    [ObservableProperty] private int _weightLastPeriod = 2;
    [ObservableProperty] private int _weightAfternoon = 1;
    [ObservableProperty] private int _weightUnderMin = 1;
    [ObservableProperty] private int _weightLabOffPeak = 1;
    [ObservableProperty] private int _weightPeriodPref = 2;
    [ObservableProperty] private int _weightPairing = 2;

    public GenerateViewModel(ITimetableGenerationService generation, AppState state)
    {
        _generation = generation;
        _state = state;

        Strategies.Add(new StrategyOption { Strategy = GenerationStrategy.Balanced, Name = "Balanced", Blurb = "A sensible mix of every objective.", IsSelected = true });
        Strategies.Add(new StrategyOption { Strategy = GenerationStrategy.TeacherFriendly, Name = "Teacher Friendly", Blurb = "Respects teacher preferences and workload first." });
        Strategies.Add(new StrategyOption { Strategy = GenerationStrategy.StudentFriendly, Name = "Student Friendly", Blurb = "Front-loads the day and avoids last periods for students." });
        Strategies.Add(new StrategyOption { Strategy = GenerationStrategy.RoomOptimization, Name = "Room Optimization", Blurb = "Keeps labs and shared rooms used efficiently." });

        CustomStrategy = new StrategyOption { Strategy = GenerationStrategy.Custom, Name = "Custom", Blurb = "Dial in your own weights below and generate a timetable tuned to them." };
        Strategies.Add(CustomStrategy);
    }

    private bool CanGenerate() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        var chosen = Strategies.Where(s => s.IsSelected).Select(s => s.Strategy).ToList();
        if (chosen.Count == 0)
        {
            Status = "Select at least one strategy.";
            return;
        }
        if (_state.ActiveSessionId == 0)
        {
            Status = "No active academic session was found. Restart the app so the sample data is created.";
            return;
        }

        Options.Clear();
        HasResults = false;
        IsBusy = true;
        GenerateCommand.NotifyCanExecuteChanged();
        Status = "Solving… this can take up to " + (MaxSeconds * chosen.Count) + " seconds.";

        var options = new SolverOptions
        {
            Strategies = chosen,
            MaxSecondsPerStrategy = Math.Max(1, MaxSeconds),
            RandomSeed = RandomSeed,
            CustomWeights = new SolverCustomWeights
            {
                TeacherPref = WeightTeacherPref,
                LastPeriod = WeightLastPeriod,
                Afternoon = WeightAfternoon,
                UnderMin = WeightUnderMin,
                LabOffPeak = WeightLabOffPeak,
                PeriodPref = WeightPeriodPref,
                Pairing = WeightPairing
            }
        };

        _cts = new CancellationTokenSource();
        try
        {
            int sessionId = _state.ActiveSessionId;
            var token = _cts.Token;
            GenerationReport report = await Task.Run(() => _generation.GenerateAsync(sessionId, options, token), token);

            foreach (var o in report.Options)
                Options.Add(o);

            HasResults = Options.Count > 0;
            Status = report.Message;
        }
        catch (OperationCanceledException)
        {
            Status = "Generation cancelled.";
        }
        catch (Exception ex)
        {
            // Surface the innermost message — EF wraps the useful text (e.g. a missing
            // column from an older database) inside an outer exception.
            var detail = ex.InnerException?.Message ?? ex.Message;
            Status = "Generation failed: " + detail;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
            GenerateCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void Open(TimetableOptionSummary? option)
    {
        if (option is null) return;

        // Infeasible strategies are still listed (so the admin can see the solver tried
        // them) but they were never saved, so there is nothing to open. Firing the open
        // event here would land on the timetable screen's "No timetable yet" message and
        // read as if the whole feature were broken. Explain it in place instead.
        if (!option.IsFeasible || option.TimetableId == 0)
        {
            Status = $"“{option.Name}” has no timetable to open — the solver could not fit " +
                     "every lesson for that strategy. Try another option that produced a result, " +
                     "or adjust the inputs and generate again.";
            return;
        }

        OpenTimetableRequested?.Invoke(option.TimetableId);
    }
}

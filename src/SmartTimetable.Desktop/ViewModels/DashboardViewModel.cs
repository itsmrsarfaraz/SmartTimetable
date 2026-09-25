using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SmartTimetable.Application.Abstractions;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>Landing page: at-a-glance counts of the configured academic data.</summary>
public partial class DashboardViewModel : PageViewModel
{
    private readonly System.Func<IUnitOfWork> _uow;
    private readonly AppState _state;

    public override string Title => "Dashboard";
    public override string Description => "A quick overview of your current academic setup.";

    [ObservableProperty] private int _subjectCount;
    [ObservableProperty] private int _teacherCount;
    [ObservableProperty] private int _classCount;
    [ObservableProperty] private int _roomCount;
    [ObservableProperty] private int _periodCount;
    [ObservableProperty] private int _timetableCount;
    [ObservableProperty] private string _sessionName = string.Empty;
    [ObservableProperty] private string _licensedTo = string.Empty;

    public DashboardViewModel(System.Func<IUnitOfWork> uow, AppState state)
    {
        _uow = uow;
        _state = state;
    }

    public override async Task LoadAsync()
    {
        LicensedTo = _state.LicensedTo ?? "Unlicensed";

        using var uow = _uow();
        SubjectCount = await uow.Subjects.CountAsync();
        TeacherCount = await uow.Teachers.CountAsync();
        ClassCount = await uow.Classes.CountAsync();
        RoomCount = await uow.Rooms.CountAsync();
        PeriodCount = await uow.Periods.CountAsync();
        TimetableCount = await uow.Timetables.CountAsync();

        var session = await uow.Sessions.GetByIdAsync(_state.ActiveSessionId);
        SessionName = session?.Name ?? "No active session";
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using SmartTimetable.Application.Abstractions;
using SmartTimetable.Domain.Entities;

namespace SmartTimetable.Desktop.ViewModels;

/// <summary>
/// The main application shell shown after login: a left navigation rail plus a
/// swappable content area. Owns page navigation and surfaces the signed-in user,
/// active academic session and licensee in the header.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly AppState _state;
    private readonly IAppLicenseService _license;
    private readonly Func<IUnitOfWork> _uow;

    /// <summary>Raised when the admin signs out; the root swaps back to the login screen.</summary>
    public event Action? LogoutRequested;

    [ObservableProperty] private PageViewModel? _currentPage;
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _licensedTo = string.Empty;
    [ObservableProperty] private string _activeSessionName = "…";
    [ObservableProperty] private string _activePage = "Dashboard";

    public ShellViewModel(
        IServiceProvider services,
        AppState state,
        IAppLicenseService license,
        Func<IUnitOfWork> uow)
    {
        _services = services;
        _state = state;
        _license = license;
        _uow = uow;
    }

    public void Initialize(AdminUser user)
    {
        _state.CurrentUser = user;
        UserName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;

        LicensedTo = _license.LicensedTo() ?? "Unlicensed";
        _state.LicensedTo = LicensedTo;

        NavigateDashboard();
        _ = InitSessionAsync();
    }

    private async Task InitSessionAsync()
    {
        try
        {
            using var uow = _uow();
            var active = await uow.Sessions.ListAsync(s => s.IsActive);
            var session = active.FirstOrDefault();
            if (session is null)
            {
                var all = await uow.Sessions.ListAsync();
                session = all.OrderByDescending(s => s.Id).FirstOrDefault();
            }

            if (session is not null)
            {
                _state.ActiveSessionId = session.Id;
                ActiveSessionName = session.Name;
            }
            else
            {
                ActiveSessionName = "No session";
            }
        }
        catch
        {
            ActiveSessionName = "No session";
        }
    }

    private void Show(PageViewModel vm, string key)
    {
        ActivePage = key;
        CurrentPage = vm;
        _ = vm.LoadAsync();
    }

    [RelayCommand] private void NavigateDashboard() => Show(_services.GetRequiredService<DashboardViewModel>(), "Dashboard");
    [RelayCommand] private void NavigateAcademicStructure() => Show(_services.GetRequiredService<AcademicStructureViewModel>(), "AcademicStructure");
    [RelayCommand] private void NavigateSubjects() => Show(_services.GetRequiredService<SubjectsViewModel>(), "Subjects");
    [RelayCommand] private void NavigateRooms() => Show(_services.GetRequiredService<RoomsViewModel>(), "Rooms");
    [RelayCommand] private void NavigatePeriods() => Show(_services.GetRequiredService<PeriodsViewModel>(), "Periods");
    [RelayCommand] private void NavigateTeachers() => Show(_services.GetRequiredService<TeachersViewModel>(), "Teachers");
    [RelayCommand] private void NavigateAvailability() => Show(_services.GetRequiredService<TeacherAvailabilityViewModel>(), "Availability");
    [RelayCommand] private void NavigateClasses() => Show(_services.GetRequiredService<ClassesViewModel>(), "Classes");
    [RelayCommand] private void NavigateCombined() => Show(_services.GetRequiredService<CombinedClassesViewModel>(), "Combined");
    [RelayCommand] private void NavigateContradictions() => Show(_services.GetRequiredService<ContradictorySubjectsViewModel>(), "Contradictions");
    [RelayCommand] private void NavigatePairing() => Show(_services.GetRequiredService<SubjectPairingViewModel>(), "Pairing");
    [RelayCommand] private void NavigateMapping() => Show(_services.GetRequiredService<TeacherSubjectViewModel>(), "Mapping");
    [RelayCommand] private void NavigateTeacherPeriods() => Show(_services.GetRequiredService<TeacherPeriodsViewModel>(), "TeacherPeriods");

    [RelayCommand]
    private void NavigateGenerate()
    {
        var vm = _services.GetRequiredService<GenerateViewModel>();
        vm.OpenTimetableRequested -= OnOpenTimetable;
        vm.OpenTimetableRequested += OnOpenTimetable;
        Show(vm, "Generate");
    }

    [RelayCommand]
    private void NavigateTimetable()
    {
        var vm = _services.GetRequiredService<TimetableViewModel>();
        vm.SetTimetable(0); // 0 => load the selected/most-recent timetable
        Show(vm, "Timetable");
    }

    private void OnOpenTimetable(int timetableId)
    {
        var vm = _services.GetRequiredService<TimetableViewModel>();
        vm.SetTimetable(timetableId);
        Show(vm, "Timetable");
    }

    [RelayCommand]
    private void Logout()
    {
        _state.CurrentUser = null;
        LogoutRequested?.Invoke();
    }
}

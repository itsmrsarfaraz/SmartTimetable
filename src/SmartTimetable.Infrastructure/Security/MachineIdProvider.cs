// This file is intentionally empty.
//
// The former Gen-1 machine identifier (SHA-256 over physical MAC addresses + machine
// name) has been removed. Machine binding is now owned entirely by the
// SmartTimetable.Licensing project via MachineFingerprintProvider + RequestCode, and
// surfaced to the UI through IAppLicenseService.GetMachineId().
//
// It is safe to delete this file from source control (it was blanked here only because
// the build sandbox could not remove files directly).

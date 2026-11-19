using Admissions.Dtos;
using Admissions.Models;

namespace Admissions.Services;

public interface IAdmissionService
{
    // years
    Task<List<YearResponse>> GetYearsAsync();
    Task<YearResponse> CreateYearAsync(CreateYearRequest req);
    Task ActivateYearAsync(string id);

    // classes
    Task<List<ClassResponse>> GetClassesAsync(bool? activeOnly, string? yearId, string? search);
    Task<ClassResponse?> GetClassAsync(string id);
    Task<ClassResponse> CreateClassAsync(CreateClassRequest req);
    Task<ClassResponse?> UpdateClassAsync(string id, UpdateClassRequest req);

    // applications
    Task<HoldResponse> ApplyAsync(string classId, string nid);
    Task<HoldResponse?> GetHoldAsync(string holdId);
    Task<HoldResponse?> ConfirmAsync(string holdId, string nid, PayRequest? pay = null);
    Task<bool> CancelAsync(string holdId, string nid);
    Task<List<MyHoldResponse>> GetMyHoldsAsync(string nid);
    Task<PagedResponse<AdminApplicationResponse>> GetApplicationsAsync(ApplicationFilter filter);

    // insights
    Task<DashboardResponse> GetDashboardAsync();
}

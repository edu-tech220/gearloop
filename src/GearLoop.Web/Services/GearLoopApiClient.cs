using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GearLoop.Web.Dtos;

namespace GearLoop.Web.Services;

/// Thin HTTP wrapper around the GearLoop backend microservice. The Razor Pages frontend never
/// talks to the database directly - every read and write crosses this boundary.
public class GearLoopApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GearLoopApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<List<BranchDto>> GetBranchesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<BranchDto>>(HttpMethod.Get, "api/branches", null, cancellationToken);

    public Task<PagedResult<EquipmentModelDto>> GetEquipmentModelsAsync(
        string? category, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = $"api/equipment-models?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(category))
        {
            query += $"&category={Uri.EscapeDataString(category)}";
        }

        return SendAsync<PagedResult<EquipmentModelDto>>(HttpMethod.Get, query, null, cancellationToken);
    }

    public Task<EquipmentModelDto> GetEquipmentModelAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync<EquipmentModelDto>(HttpMethod.Get, $"api/equipment-models/{id}", null, cancellationToken);

    public Task<AvailabilityResponse> GetAvailabilityAsync(
        int modelId, int branchId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default) =>
        SendAsync<AvailabilityResponse>(
            HttpMethod.Get,
            $"api/equipment-models/{modelId}/availability?branchId={branchId}&startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}",
            null,
            cancellationToken);

    public Task<AuthResponse> RegisterAsync(string fullName, string email, string password, CancellationToken cancellationToken = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/register", new { fullName, email, password }, cancellationToken, anonymous: true);

    public Task<AuthResponse> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/auth/login", new { email, password }, cancellationToken, anonymous: true);

    public Task<ReservationCreationResponse> CreateReservationAsync(
        int equipmentModelId, int branchId, DateOnly startDate, DateOnly endDate, bool joinWaitlistIfUnavailable,
        CancellationToken cancellationToken = default) =>
        SendAsync<ReservationCreationResponse>(
            HttpMethod.Post, "api/reservations",
            new { equipmentModelId, branchId, startDate, endDate, joinWaitlistIfUnavailable },
            cancellationToken);

    public Task<PagedResult<ReservationDto>> GetMyReservationsAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        SendAsync<PagedResult<ReservationDto>>(HttpMethod.Get, $"api/reservations/mine?page={page}&pageSize={pageSize}", null, cancellationToken);

    public Task<PagedResult<ReservationDto>> GetAllReservationsAsync(
        ReservationStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = $"api/reservations?page={page}&pageSize={pageSize}";
        if (status is not null)
        {
            query += $"&status={status}";
        }

        return SendAsync<PagedResult<ReservationDto>>(HttpMethod.Get, query, null, cancellationToken);
    }

    public Task<ReservationDto> CancelReservationAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync<ReservationDto>(HttpMethod.Post, $"api/reservations/{id}/cancel", null, cancellationToken);

    public Task<ReservationDto> CheckoutAsync(int id, CancellationToken cancellationToken = default) =>
        SendAsync<ReservationDto>(HttpMethod.Post, $"api/reservations/{id}/checkout", null, cancellationToken);

    public Task<ReservationDto> ReturnAsync(
        int id, DateOnly actualReturnDate, UnitCondition condition, CancellationToken cancellationToken = default) =>
        SendAsync<ReservationDto>(
            HttpMethod.Post, $"api/reservations/{id}/return", new { actualReturnDate, condition }, cancellationToken);

    public Task<PagedResult<WaitlistEntryDto>> GetWaitlistAsync(int page, int pageSize, CancellationToken cancellationToken = default) =>
        SendAsync<PagedResult<WaitlistEntryDto>>(HttpMethod.Get, $"api/waitlist?page={page}&pageSize={pageSize}", null, cancellationToken);

    private async Task<T> SendAsync<T>(
        HttpMethod method, string requestUri, object? body, CancellationToken cancellationToken, bool anonymous = false)
    {
        using var request = new HttpRequestMessage(method, requestUri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        if (!anonymous)
        {
            var token = _httpContextAccessor.HttpContext?.User.FindFirst("access_token")?.Value;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await TryReadProblemDetailAsync(response, cancellationToken);
            throw new ApiException((int)response.StatusCode, detail ?? $"GearLoop API request failed with status {(int)response.StatusCode}.");
        }

        if (typeof(T) == typeof(object) || response.Content.Headers.ContentLength == 0)
        {
            return default!;
        }

        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken))!;
    }

    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions, cancellationToken);
            return problem.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

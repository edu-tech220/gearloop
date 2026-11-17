using System.Net;
using System.Net.Http.Json;
using GearLoop.Api.Common;
using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Dtos;
using GearLoop.Api.Tests.Infrastructure;
using Xunit;

namespace GearLoop.Api.Tests;

public class ReservationsApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    private static int _dayOffsetCounter = 1000;

    public ReservationsApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    /// Each test reserves its own, far-future, non-overlapping date window so tests sharing the
    /// class-level seeded inventory never contend for the same unit regardless of run order.
    private static (DateOnly Start, DateOnly End) NextDateWindow(int days = 3)
    {
        var offset = Interlocked.Add(ref _dayOffsetCounter, 10);
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(offset);
        return (start, start.AddDays(days));
    }

    private async Task<(int BranchId, int ModelId)> GetSeededBranchAndModelAsync(HttpClient client)
    {
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("api/branches", TestJson.Options);
        var models = await client.GetFromJsonAsync<PagedResult<EquipmentModelDto>>("api/equipment-models?pageSize=50", TestJson.Options);
        return (branches!.First().Id, models!.Items.First().Id);
    }

    [Fact]
    public async Task CreateReservation_ThenFetchAndCancel_FullLifecycleSucceeds()
    {
        var client = _factory.CreateClient();
        var token = await client.RegisterAsync();
        client.UseBearerToken(token);

        var (branchId, modelId) = await GetSeededBranchAndModelAsync(client);
        var (start, end) = NextDateWindow();

        var createResponse = await client.PostAsJsonAsync("api/reservations", new CreateReservationRequest(modelId, branchId, start, end), TestJson.Options);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<ReservationCreationResponse>(TestJson.Options);
        Assert.NotNull(created!.Reservation);
        var reservationId = created.Reservation!.Id;

        var getResponse = await client.GetAsync($"api/reservations/{reservationId}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<ReservationDto>(TestJson.Options);
        Assert.Equal(ReservationStatus.Confirmed, fetched!.Status);

        var mine = await client.GetFromJsonAsync<PagedResult<ReservationDto>>("api/reservations/mine", TestJson.Options);
        Assert.Contains(mine!.Items, r => r.Id == reservationId);

        var cancelResponse = await client.PostAsync($"api/reservations/{reservationId}/cancel", null);
        cancelResponse.EnsureSuccessStatusCode();
        var cancelled = await cancelResponse.Content.ReadFromJsonAsync<ReservationDto>(TestJson.Options);
        Assert.Equal(ReservationStatus.Cancelled, cancelled!.Status);
    }

    [Fact]
    public async Task GetReservation_BelongingToAnotherCustomer_ReturnsNotFound()
    {
        var client = _factory.CreateClient();
        var ownerToken = await client.RegisterAsync();
        client.UseBearerToken(ownerToken);

        var (branchId, modelId) = await GetSeededBranchAndModelAsync(client);
        var (start, end) = NextDateWindow();
        var created = await client.PostAsJsonAsync("api/reservations", new CreateReservationRequest(modelId, branchId, start, end), TestJson.Options);
        var reservation = (await created.Content.ReadFromJsonAsync<ReservationCreationResponse>(TestJson.Options))!.Reservation!;

        var otherClient = _factory.CreateClient();
        otherClient.UseBearerToken(await otherClient.RegisterAsync());

        var response = await otherClient.GetAsync($"api/reservations/{reservation.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateReservation_WithoutAuthentication_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var (branchId, modelId) = await GetSeededBranchAndModelAsync(client);
        var (start, end) = NextDateWindow();

        var response = await client.PostAsJsonAsync("api/reservations", new CreateReservationRequest(modelId, branchId, start, end), TestJson.Options);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAllReservations_AsCustomer_ReturnsForbidden()
    {
        var client = _factory.CreateClient();
        client.UseBearerToken(await client.RegisterAsync());

        var response = await client.GetAsync("api/reservations");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StaffCheckoutAndReturn_UpdatesStatusAndAppliesFees()
    {
        var customerClient = _factory.CreateClient();
        customerClient.UseBearerToken(await customerClient.RegisterAsync());

        var (branchId, modelId) = await GetSeededBranchAndModelAsync(customerClient);
        var (start, end) = NextDateWindow(days: 2);

        var created = await customerClient.PostAsJsonAsync("api/reservations", new CreateReservationRequest(modelId, branchId, start, end), TestJson.Options);
        var reservation = (await created.Content.ReadFromJsonAsync<ReservationCreationResponse>(TestJson.Options))!.Reservation!;

        var staffClient = _factory.CreateClient();
        staffClient.UseBearerToken(await staffClient.LoginAsync(DbSeeder.DemoStaffEmail, DbSeeder.DemoStaffPassword));

        var checkoutResponse = await staffClient.PostAsync($"api/reservations/{reservation.Id}/checkout", null);
        checkoutResponse.EnsureSuccessStatusCode();
        var checkedOut = await checkoutResponse.Content.ReadFromJsonAsync<ReservationDto>(TestJson.Options);
        Assert.Equal(ReservationStatus.CheckedOut, checkedOut!.Status);
        Assert.NotNull(checkedOut.AssignedUnitId);

        var lateReturnDate = end.AddDays(1);
        var returnResponse = await staffClient.PostAsJsonAsync(
            $"api/reservations/{reservation.Id}/return", new ReturnReservationRequest(lateReturnDate, UnitCondition.Fair), TestJson.Options);
        returnResponse.EnsureSuccessStatusCode();
        var returned = await returnResponse.Content.ReadFromJsonAsync<ReservationDto>(TestJson.Options);

        Assert.Equal(ReservationStatus.Returned, returned!.Status);
        Assert.True(returned.Charge!.LateFeeAmount > 0);
        Assert.True(returned.Charge.DamageFeeAmount > 0);
    }

    [Fact]
    public async Task GetEquipmentModels_SupportsPagination()
    {
        var client = _factory.CreateClient();
        var page = await client.GetFromJsonAsync<PagedResult<EquipmentModelDto>>("api/equipment-models?page=1&pageSize=2", TestJson.Options);

        Assert.NotNull(page);
        Assert.True(page!.Items.Count <= 2);
        Assert.True(page.TotalCount >= 3);
    }

    [Fact]
    public async Task CreateReservation_WhenFullyBooked_CanJoinWaitlistInstead()
    {
        var client = _factory.CreateClient();
        client.UseBearerToken(await client.RegisterAsync());

        var models = await client.GetFromJsonAsync<PagedResult<EquipmentModelDto>>("api/equipment-models?pageSize=50", TestJson.Options);
        var droneModel = models!.Items.First(m => m.Name.Contains("Drone"));
        var branches = await client.GetFromJsonAsync<List<BranchDto>>("api/branches", TestJson.Options);
        var branchId = branches!.First().Id;
        var (start, end) = NextDateWindow();

        // The seeded Drone model has exactly one unit per branch - the first booking exhausts it.
        var first = await client.PostAsJsonAsync("api/reservations", new CreateReservationRequest(droneModel.Id, branchId, start, end), TestJson.Options);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var secondClient = _factory.CreateClient();
        secondClient.UseBearerToken(await secondClient.RegisterAsync());
        var second = await secondClient.PostAsJsonAsync(
            "api/reservations", new CreateReservationRequest(droneModel.Id, branchId, start, end, JoinWaitlistIfUnavailable: true), TestJson.Options);

        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        var waitlisted = await second.Content.ReadFromJsonAsync<ReservationCreationResponse>(TestJson.Options);
        Assert.Null(waitlisted!.Reservation);
        Assert.NotNull(waitlisted.WaitlistEntry);
    }
}

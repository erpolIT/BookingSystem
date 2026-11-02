using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BookingSystem.Data;
using BookingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Writes are allowed only in explicitly configured disposable verification databases.
var connection = Environment.GetEnvironmentVariable("ConnectionStrings__BookingDatabase")
    ?? throw new InvalidOperationException("An explicit test database connection is required; User Secrets are not read.");
var connectionSettings = new NpgsqlConnectionStringBuilder(connection);
var expectedDatabase = connectionSettings.Database!;
if (expectedDatabase != "booking_ci" && !expectedDatabase.StartsWith("booking_verify_", StringComparison.Ordinal))
    throw new InvalidOperationException("Use booking_ci or a disposable booking_verify_* database.");
var apiUrl = Environment.GetEnvironmentVariable("BOOKING_TEST_API")
    ?? throw new InvalidOperationException("BOOKING_TEST_API is required.");
await using var db = new BookingDbContext(new DbContextOptionsBuilder<BookingDbContext>().UseNpgsql(connection).Options);
await db.Database.OpenConnectionAsync();
await using (var command = db.Database.GetDbConnection().CreateCommand())
{
    command.CommandText = "SELECT current_database()";
    Check((string?)await command.ExecuteScalarAsync() == expectedDatabase, "Database identity mismatch.");
}
var marker = "Price snapshot test " + Guid.NewGuid().ToString("N");
var provider = new Provider { Name = marker };
db.Providers.Add(provider);
await db.SaveChangesAsync();
var service = new Service { ProviderId = provider.Id, Name = marker, DurationMinutes = 30, Price = 60m };
db.Services.Add(service);
db.WorkingHours.Add(new WorkingHours { ProviderId = provider.Id, DayOfWeek = DayOfWeek.Monday, StartTime = new(9, 0), EndTime = new(17, 0) });
await db.SaveChangesAsync();
using var http = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
{ BaseAddress = new Uri(apiUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromSeconds(30) };
var fixture = await http.GetFromJsonAsync<JsonElement>($"services/{service.Id}");
Check(fixture.GetProperty("name").GetString() == marker, "API is not connected to the expected test database; refusing API writes.");

async Task<HttpResponseMessage> Mutate(string path, object? body = null, string? key = null)
{
    var csrf = await http.GetFromJsonAsync<JsonElement>("auth/csrf");
    using var request = new HttpRequestMessage(HttpMethod.Post, path);
    request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
    if (key is not null) request.Headers.Add("Idempotency-Key", key);
    if (body is not null) request.Content = JsonContent.Create(body);
    return await http.SendAsync(request);
}
var credentials = new { email = "price-test-" + Guid.NewGuid().ToString("N") + "@example.com", password = "Test!Aa1" + Guid.NewGuid().ToString("N") };
using (var registered = await Mutate("auth/register", credentials)) Check(registered.StatusCode == HttpStatusCode.Created, "Registration failed.");
string userId;
using (var loggedIn = await Mutate("auth/login", credentials))
{
    Check(loggedIn.IsSuccessStatusCode, "Login failed.");
    var body = await loggedIn.Content.ReadFromJsonAsync<JsonElement>();
    userId = body.GetProperty("user").GetProperty("id").GetString()!;
}
var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime).AddDays(7);
while (day.DayOfWeek != DayOfWeek.Monday) day = day.AddDays(1);
var slots = await http.GetFromJsonAsync<JsonElement>($"services/{service.Id}/available-slots?date={day:yyyy-MM-dd}");
var firstStart = slots[0].GetProperty("startTime").GetDateTimeOffset();
var secondStart = slots[4].GetProperty("startTime").GetDateTimeOffset();
var key = Guid.NewGuid().ToString();
var payload = new { serviceId = service.Id, startTime = firstStart, customerName = "Price Test", customerEmail = credentials.email, priceAtBooking = 0.01m, price = 0.01m };
int firstId;
using (var created = await Mutate("bookings", payload, key))
{
    Check(created.StatusCode == HttpStatusCode.Created, "Booking creation failed.");
    var booking = await created.Content.ReadFromJsonAsync<JsonElement>();
    Check(booking.GetProperty("priceAtBooking").GetDecimal() == 60m, "Client price was accepted instead of the catalogue price.");
    firstId = booking.GetProperty("id").GetInt32();
}
await db.Services.Where(s => s.Id == service.Id).ExecuteUpdateAsync(update => update.SetProperty(s => s.Price, 80.25m));
var oldBooking = await http.GetFromJsonAsync<JsonElement>($"bookings/{firstId}");
Check(oldBooking.GetProperty("priceAtBooking").GetDecimal() == 60m, "Catalogue edit changed the stored booking price.");
using (var replay = await Mutate("bookings", payload, key))
{
    Check(replay.StatusCode == HttpStatusCode.OK, "Replay was not recognised.");
    var booking = await replay.Content.ReadFromJsonAsync<JsonElement>();
    Check(booking.GetProperty("id").GetInt32() == firstId && booking.GetProperty("priceAtBooking").GetDecimal() == 60m, "Replay changed the original snapshot.");
}
int secondId;
using (var created = await Mutate("bookings", new { serviceId = service.Id, startTime = secondStart, customerName = "Price Test", customerEmail = credentials.email }, Guid.NewGuid().ToString()))
{
    Check(created.StatusCode == HttpStatusCode.Created, "Second booking failed.");
    var booking = await created.Content.ReadFromJsonAsync<JsonElement>();
    Check(booking.GetProperty("priceAtBooking").GetDecimal() == 80.25m, "New booking did not use the updated decimal price.");
    secondId = booking.GetProperty("id").GetInt32();
}
var legacy = new Booking { ServiceId = service.Id, ProviderId = provider.Id, UserId = userId,
    StartTime = firstStart.AddDays(7), EndTime = firstStart.AddDays(7).AddMinutes(30),
    CustomerName = "Legacy Price Test", CustomerEmail = credentials.email, PriceAtBooking = null };
db.Bookings.Add(legacy);
await db.SaveChangesAsync();
var legacyResponse = await http.GetFromJsonAsync<JsonElement>($"bookings/{legacy.Id}");
Check(legacyResponse.GetProperty("priceAtBooking").ValueKind == JsonValueKind.Null, "Unknown historical price was invented.");
var list = await http.GetFromJsonAsync<JsonElement>("bookings");
Check(list.EnumerateArray().Single(b => b.GetProperty("id").GetInt32() == firstId).GetProperty("priceAtBooking").GetDecimal() == 60m, "Customer list lost the snapshot.");
foreach (var invalidPrice in new[] { 0m, -1m })
{
    var rejected = false;
    try { await db.Bookings.Where(b => b.Id == firstId).ExecuteUpdateAsync(update => update.SetProperty(b => b.PriceAtBooking, invalidPrice)); }
    catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.CheckViolation && exception.ConstraintName == "CK_Bookings_Price") { rejected = true; }
    Check(rejected, "Database accepted a non-positive booking price.");
}
foreach (var id in new[] { firstId, secondId, legacy.Id })
{
    using var cancelled = await Mutate($"bookings/{id}/cancel");
    Check(cancelled.StatusCode == HttpStatusCode.NoContent, "Fixture cancellation failed.");
}
Console.WriteLine("Price snapshots passed: forged price ignored; catalogue change preserved history/replay; new decimal price used; legacy NULL retained; invalid database prices rejected.");

static void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

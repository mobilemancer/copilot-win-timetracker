using System.Net.Http.Headers;
using System.Text.Json;
using TimeTracker.Domain;

namespace TimeTracker.Infrastructure;

public sealed class GraphCalendarClient
{
    private static readonly HttpClient HttpClient = new();

    public async Task<IReadOnlyList<CalendarMeeting>> GetCompletedMeetingsAsync(
        string accountDisplayName,
        string accessToken,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildUri(rangeStart, rangeEnd));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("value", out var value))
        {
            return [];
        }

        var items = new List<CalendarMeeting>();
        foreach (var element in value.EnumerateArray())
        {
            var end = ParseDateTimeOffset(element.GetProperty("end"));
            if (end > now)
            {
                continue;
            }

            items.Add(new CalendarMeeting
            {
                AccountDisplayName = accountDisplayName,
                EventId = element.GetProperty("id").GetString() ?? string.Empty,
                Subject = element.GetProperty("subject").GetString() ?? string.Empty,
                BodyPreview = element.TryGetProperty("bodyPreview", out var bodyPreview) ? bodyPreview.GetString() : null,
                StartTime = ParseDateTimeOffset(element.GetProperty("start")),
                EndTime = end,
                OrganizerEmail = ReadEmail(element, "organizer"),
                AttendeeEmails = ReadAttendees(element),
            });
        }

        return items;
    }

    private static Uri BuildUri(DateTimeOffset rangeStart, DateTimeOffset rangeEnd)
    {
        var query = new Dictionary<string, string>
        {
            ["startDateTime"] = Uri.EscapeDataString(rangeStart.UtcDateTime.ToString("O")),
            ["endDateTime"] = Uri.EscapeDataString(rangeEnd.UtcDateTime.ToString("O")),
            ["$select"] = Uri.EscapeDataString("id,subject,bodyPreview,start,end,organizer,attendees"),
            ["$top"] = "100",
        };

        var queryString = string.Join("&", query.Select(pair => $"{pair.Key}={pair.Value}"));
        return new Uri($"https://graph.microsoft.com/v1.0/me/calendarView?{queryString}");
    }

    private static DateTimeOffset ParseDateTimeOffset(JsonElement value)
    {
        var dateTime = value.GetProperty("dateTime").GetString() ?? throw new InvalidOperationException("Missing Graph dateTime.");
        var timeZone = value.GetProperty("timeZone").GetString();

        if (DateTimeOffset.TryParse(dateTime, out var parsedOffset))
        {
            return parsedOffset;
        }

        if (DateTime.TryParse(dateTime, out var parsedDateTime))
        {
            var offset = TimeZoneInfo.Local.GetUtcOffset(parsedDateTime);
            if (!string.IsNullOrWhiteSpace(timeZone))
            {
                try
                {
                    var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
                    offset = timeZoneInfo.GetUtcOffset(parsedDateTime);
                }
                catch
                {
                    // Fall back to local offset if the Graph timezone ID is unavailable on Windows.
                }
            }

            return new DateTimeOffset(parsedDateTime, offset);
        }

        throw new InvalidOperationException("Unable to parse Graph date/time value.");
    }

    private static string? ReadEmail(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || !property.TryGetProperty("emailAddress", out var email)
            || !email.TryGetProperty("address", out var address))
        {
            return null;
        }

        return address.GetString();
    }

    private static IReadOnlyList<string> ReadAttendees(JsonElement element)
    {
        if (!element.TryGetProperty("attendees", out var attendees))
        {
            return [];
        }

        return attendees.EnumerateArray()
            .Select(attendee =>
            {
                if (!attendee.TryGetProperty("emailAddress", out var emailAddress)
                    || !emailAddress.TryGetProperty("address", out var address))
                {
                    return null;
                }

                return address.GetString();
            })
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Cast<string>()
            .ToList();
    }
}

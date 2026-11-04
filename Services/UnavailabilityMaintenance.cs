using Microsoft.EntityFrameworkCore;
using RefApp.Data;

namespace RefApp.Services;

public static class UnavailabilityMaintenance
{
    /// <summary>Keeps the table clean: an unavailability is expired once its end date is before today.</summary>
    public static Task DeleteExpiredAsync(ApplicationDbContext context, DateTime todayUtcDate, CancellationToken cancellationToken) =>
        context.Unavailabilities
            .Where(u => u.EndDate < todayUtcDate)
            .ExecuteDeleteAsync(cancellationToken);
}

using GSDIShoppingApi.Data;
using Microsoft.EntityFrameworkCore;

namespace GSDIShoppingApi.Services;

public class SystemParametersService(GSDIShoppingDbContext db) : ISystemParametersService
{
    public async Task<string> GetValueAsync(string key, string defaultValue, CancellationToken cancellationToken)
    {
        var value = await db.SystemParameters
            .Where(p => p.Key == key)
            .Select(p => p.Value)
            .SingleOrDefaultAsync(cancellationToken);

        return value ?? defaultValue;
    }
}

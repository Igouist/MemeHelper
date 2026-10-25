using System.Data.Common;

namespace MemeHelper.Api.Database;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken ct);
}

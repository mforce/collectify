using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Collectify.Infrastructure.Data;

/// <summary>
/// No-op interceptor proving a resolved context used the production
/// <see cref="CollectifyDbContextExtensions.AddCollectifyDbContext"/> registration.
/// </summary>
public sealed class CollectifyDbContextRegistrationMarker : SaveChangesInterceptor;

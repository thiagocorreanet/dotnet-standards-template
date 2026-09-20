using System.Reflection;
using Shared.Contracts.Common;
using Shared.Contracts.Identity;
using Shared.Http.Endpoints;
using Module.Venues.UseCases.ListVenues;
using Module.Venues.UseCases.ListRooms;
using Module.Venues.UseCases.GetVenue;
namespace Module.Venues.Shared;
internal sealed class VenuesAccessPolicy(ICurrentUser user) : IModuleAccessPolicy
{
    public Assembly ModuleAssembly => typeof(VenuesModule).Assembly;
    public Task<bool> CanExecuteAsync(object request, CancellationToken ct) => Task.FromResult(user.IsAuthenticated &&
        (request is ListVenuesRequest or ListRoomsRequest or GetVenueRequest || user.HasRole(DefaultRoles.Administrator)));
}

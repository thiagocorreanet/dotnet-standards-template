using Microsoft.AspNetCore.Mvc;
namespace Module.Events.UseCases.ListTracks;
public sealed record ListTracksRequest([FromRoute(Name="id")] Guid EventId, [FromQuery] bool? IsActive);

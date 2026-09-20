using Shared.Contracts.Integration;
using Shared.Contracts.Identity;
using Shouldly;
namespace Tests.Unit.Identity;
public sealed class EventContractTests
{
    [Fact]
    public void All_the_events_has_contract_stable_and_single()
    {
        var contracts = typeof(IIntegrationEvent).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(IIntegrationEvent).IsAssignableFrom(t))
            .Select(t => EventContractAttribute.For(t).Name).ToArray();
        contracts.Distinct().Count().ShouldBe(contracts.Length);
        contracts.ShouldAllBe(x => x.EndsWith(".v1"));
    }
    [Fact]
    public void Event_of_user_not_copies_data_personal()
    {
        var properties = typeof(UserRegistered).GetProperties().Select(p => p.Name).ToArray();
        properties.ShouldNotContain("UserEmail");
        properties.ShouldNotContain("UserName");
    }
}

using Module.People.Domain;
using Shared.Contracts.People;
using Shouldly;

namespace Tests.Unit.People;

public sealed class PersonTests
{
    private static Person CreatePersonDefault() =>
        Person.Create("  Maria Silva ", "  Maria.Silva@Company.COM ", " (27) 99999-0000 ", "529.982.247-25", "  Globalsys ", "", "   ", null);

    [Fact]
    public void Create_should_normalize_email_document_and_fields_optional()
    {
        var person = CreatePersonDefault();

        person.Id.ShouldNotBe(Guid.Empty);
        person.PersonName.ShouldBe("Maria Silva");
        person.PersonEmail.ShouldBe("maria.silva@company.com");
        person.PersonPhone.ShouldBe("(27) 99999-0000");
        person.PersonDocument.ShouldBe("52998224725");
        person.PersonCompany.ShouldBe("Globalsys");
        person.PersonJobTitle.ShouldBeNull();
        person.PersonShortBio.ShouldBeNull();
        person.PersonPhotoUrl.ShouldBeNull();
        person.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_should_record_event_PersonCreated_with_data_normalized()
    {
        var person = CreatePersonDefault();

        var eventEntity = person.Events.ShouldHaveSingleItem().ShouldBeOfType<PersonCreated>();
        eventEntity.PersonId.ShouldBe(person.Id);
        System.Text.Json.JsonSerializer.Serialize(eventEntity).ShouldNotContain(person.PersonEmail);
    }

    [Fact]
    public void Create_without_document_should_preserve_document_null()
    {
        var person = Person.Create("João", "joao@test.local", null, "  ", null, null, null, null);

        person.PersonDocument.ShouldBeNull();
    }

    [Fact]
    public void Update_should_replace_all_the_fields_and_status()
    {
        var person = CreatePersonDefault();
        person.ClearEvents();

        person.Update("Maria S.", "OTHER@EMAIL.COM", null, null, null, "Arquiteta", "Bio", "https://photo.local/m.png", isActive: false);

        person.PersonName.ShouldBe("Maria S.");
        person.PersonEmail.ShouldBe("other@email.com");
        person.PersonPhone.ShouldBeNull();
        person.PersonDocument.ShouldBeNull();
        person.PersonCompany.ShouldBeNull();
        person.PersonJobTitle.ShouldBe("Arquiteta");
        person.PersonShortBio.ShouldBe("Bio");
        person.PersonPhotoUrl.ShouldBe("https://photo.local/m.png");
        person.IsActive.ShouldBeFalse();
        person.Events.ShouldBeEmpty();
    }

    [Fact]
    public void MarkDeleted_should_record_event_PersonDeleted()
    {
        var person = CreatePersonDefault();
        person.ClearEvents();

        person.MarkDeleted();

        person.Events.ShouldHaveSingleItem().ShouldBeOfType<PersonDeleted>().PersonId.ShouldBe(person.Id);
    }

    [Theory]
    [InlineData("  A@B.com ", "a@b.com")]
    [InlineData("x@y.z", "x@y.z")]
    public void NormalizeEmail_should_apply_trim_and_lowercase(string input, string expected) => Person.NormalizeEmail(input).ShouldBe(expected);
}

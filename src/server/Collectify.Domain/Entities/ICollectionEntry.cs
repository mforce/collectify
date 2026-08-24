using Collectify.Domain.Enums;

namespace Collectify.Domain.Entities;

public interface ICollectionEntry
{
    int Id { get; }
    // Settable: the generic module constructs a new TEntity via an object
    // initializer (`new TEntity { OwnerId = ownerId }`) and, on update,
    // replaces the whole Tags collection and bumps UpdatedAt — all through
    // this interface, so those members need a setter here even though the
    // rest stay read-only for the generic handlers' purposes.
    string OwnerId { get; set; }
    string Title { get; }
    int? Year { get; }
    CollectionStatus Status { get; }
    int? PersonalRating { get; }
    string? ImagePath { get; set; }
    DateTime AddedAt { get; }
    DateTime UpdatedAt { get; set; }
    ICollection<Tag> Tags { get; set; }
    ICollection<Genre> Genres { get; set; }
}

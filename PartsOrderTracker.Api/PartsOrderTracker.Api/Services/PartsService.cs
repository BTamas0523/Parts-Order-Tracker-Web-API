using MongoDB.Bson;
using MongoDB.Driver;
using PartsOrderTracker.Api.Dtos;
using PartsOrderTracker.Api.Models;

namespace PartsOrderTracker.Api.Services;

public sealed class PartsService
{
  private readonly IMongoCollection<PartDocument> _parts;

  public PartsService(IMongoDatabase database)
  {
    _parts = database.GetCollection<PartDocument>("parts");
  }

  public async Task<IReadOnlyList<PartResponse>> GetActiveAsync(CancellationToken cancellationToken)
  {
    var documents = await _parts
      .Find(part => part.IsActive)
      .SortBy(part => part.Name)
      .ToListAsync(cancellationToken);

    return documents.Select(ToResponse).ToList();
  }

  public async Task<PartDocument?> GetActiveByIdAsync(ObjectId id, CancellationToken cancellationToken)
  {
    return await _parts
      .Find(part => part.Id == id && part.IsActive)
      .FirstOrDefaultAsync(cancellationToken);
  }

  private static PartResponse ToResponse(PartDocument part) => new(
    part.Id.ToString(),
    part.ArticleNumber,
    part.Name,
    part.Manufacturer,
    part.Description,
    part.Category,
    part.Price,
    part.ImageUrl,
    part.IsStock);
}

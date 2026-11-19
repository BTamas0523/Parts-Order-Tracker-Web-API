namespace PartsOrderTracker.Api.Dtos;

public sealed record PartResponse(
  string Id,
  string ArticleNumber,
  string Name,
  string Manufacturer,
  string Description,
  string Category,
  decimal Price,
  string ImageUrl,
  bool InStock);

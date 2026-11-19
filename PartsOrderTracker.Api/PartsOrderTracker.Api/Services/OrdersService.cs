using MongoDB.Bson;
using MongoDB.Driver;
using PartsOrderTracker.Api.Dtos;
using PartsOrderTracker.Api.Models;

namespace PartsOrderTracker.Api.Services;

public sealed class OrdersService
{
  private readonly IMongoCollection<OrderDocument> _orders;
  private readonly PartsService _partsService;
  private readonly ICurrentUserService _currentUser;
  private readonly TimeProvider _timeProvider;

  public OrdersService(
    IMongoDatabase database,
    PartsService partsService,
    ICurrentUserService currentUser,
    TimeProvider timeProvider)
  {
    _orders = database.GetCollection<OrderDocument>("orders");
    _partsService = partsService;
    _currentUser = currentUser;
    _timeProvider = timeProvider;
  }

  public async Task<IReadOnlyList<OrderResponse>> GetMineAsync(CancellationToken cancellationToken)
  {
    var documents = await _orders
      .Find(order => order.UserId == _currentUser.UserId)
      .SortByDescending(order => order.CreatedAt)
      .Limit(100)
      .ToListAsync(cancellationToken);

    return documents.Select(ToResponse).ToList();
  }

  public async Task<OrderResponse?> GetMineByIdAsync(ObjectId id, CancellationToken cancellationToken)
  {
    var document = await _orders
      .Find(order => order.Id == id && order.UserId == _currentUser.UserId)
      .FirstOrDefaultAsync(cancellationToken);

    return document is null ? null : ToResponse(document);
  }

  public async Task<OrderResponse?> CreateAsync(
    IReadOnlyList<(ObjectId PartId, int Quantity)> lines,
    CancellationToken cancellationToken)
  {
    var items = new List<OrderItemDocument>();
    foreach (var (partId, quantity) in lines)
    {
      var part = await _partsService.GetActiveByIdAsync(partId, cancellationToken);
      if (part is null)
      {
        return null;
      }

      items.Add(new OrderItemDocument
      {
        PartId = part.Id,
        ArticleNumber = part.ArticleNumber,
        Name = part.Name,
        Manufacturer = part.Manufacturer,
        Quantity = quantity
      });
    }

    var now = _timeProvider.GetUtcNow().UtcDateTime;
    var order = new OrderDocument
    {
      Id = ObjectId.GenerateNewId(),
      UserId = _currentUser.UserId,
      Items = items,
      Status = OrderStatus.Received,
      CreatedAt = now,
      UpdatedAt = now,
      NextStatusChangeAt = now.AddSeconds(10),
      UserName = _currentUser.UserName,
      UserEmail = _currentUser.UserEmail,
      ReceivedAt = now
    };

    await _orders.InsertOneAsync(order, cancellationToken: cancellationToken);
    return ToResponse(order);
  }

  private static IEnumerable<OrderItemDocument> GetItems(OrderDocument order)
  {
    if (order.Items.Count > 0 || order.LegacyPart is null)
    {
      return order.Items;
    }

    order.LegacyPart.Quantity = order.LegacyQuantity ?? 1;
    return [order.LegacyPart];
  }

  private static OrderResponse ToResponse(OrderDocument order) => new(
    order.Id.ToString(),
    GetItems(order).Select(item => new OrderItemResponse(
      item.PartId?.ToString(),
      item.ArticleNumber,
      item.Name,
      item.Manufacturer,
      item.Quantity)).ToList(),
    order.Status,
    order.CreatedAt,
    order.UpdatedAt,
    order.NextStatusChangeAt,
    order.UserName,
    order.UserEmail,
    order.ReceivedAt,
    order.ProcessingAt,
    order.DispatchedAt,
    order.ExpectedArrival,
    order.TrackingId);
}

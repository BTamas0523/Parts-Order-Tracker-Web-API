# PartsOrderTracker.API

`PartsOrderTracker.API` is the secured HTTP API of the Parts Order Tracker
application. It provides access to active parts, allows authenticated users to
create and retrieve their own orders, and hosts an Azure SignalR hub for live
order-status notifications.

## Table of contents

- [Technology overview](#technology-overview)
- [Prerequisites](#prerequisites)
- [Configuration](#configuration)
- [Running locally](#running-locally)
- [Authentication](#authentication)
- [API endpoints](#api-endpoints)
- [Real-time order updates](#real-time-order-updates)
- [Data models](#data-models)
- [Error handling](#error-handling)
- [MongoDB collections and indexes](#mongodb-collections-and-indexes)
- [Architecture and behavior](#architecture-and-behavior)
- [Development commands](#development-commands)

## Technology overview

| Area | Technology |
| --- | --- |
| Runtime | .NET 10 / ASP.NET Core Web API |
| Data storage | MongoDB |
| Authentication | Microsoft Entra ID with JWT bearer tokens |
| Real-time messaging | Azure SignalR Service |
| API description | OpenAPI and Swagger UI in the Development environment |
| JSON format | camelCase property names and string enum values |

Important solution and project files:

```text
PartsOrderTracker.Api/
├── PartsOrderTracker.Api.slnx
├── README.md
└── PartsOrderTracker.Api/
    ├── Controllers/       HTTP endpoints
    ├── Dtos/              request and response models
    ├── Hubs/              SignalR hubs
    ├── Models/            MongoDB documents
    ├── Services/          business and data-access logic
    ├── mongo-indexes.js   MongoDB indexes
    └── Program.cs         application configuration
```

## Prerequisites

- .NET 10 SDK
- an accessible MongoDB or MongoDB Atlas database
- an Azure SignalR Service instance
- a Microsoft Entra ID application registration for the API
- a valid access token for testing the secured endpoints
- a status-processing worker that publishes `OrderStatusChanged` messages if
  live order updates are required

Check the installed SDKs with:

```powershell
dotnet --list-sdks
```

## Configuration

The application uses the standard ASP.NET Core configuration providers. Store
sensitive local-development values in .NET User Secrets.

Navigate to the API project directory:

```powershell
cd "Web API\PartsOrderTracker.Api\PartsOrderTracker.Api"
```

Configure the MongoDB connection:

```powershell
dotnet user-secrets set "MongoDb:ConnectionString" "<mongodb-connection-string>"
```

Configure Azure SignalR Service:

```powershell
dotnet user-secrets set "Azure:SignalR:ConnectionString" "<azure-signalr-connection-string>"
```

Available configuration settings:

| Key | Required | Description |
| --- | --- | --- |
| `MongoDb:ConnectionString` | yes | MongoDB connection string. |
| `MongoDb:DatabaseName` | yes | Database name. The current implementation only accepts the exact value `Parts_order_tracker_dev`. |
| `Azure:SignalR:ConnectionString` | yes | Azure SignalR Service connection string used for hub negotiation and message routing. |
| `AzureAd:Instance` | yes | Base URL of the Microsoft identity platform. |
| `AzureAd:TenantId` | yes | Entra tenant identifier. |
| `AzureAd:ClientId` | yes | Client ID of the API application registration. |
| `AzureAd:Audience` | yes | Expected audience of the access token. |

> Do not commit passwords, MongoDB connection strings, or other secrets to
> source control. Use User Secrets during development and a secure secret store
> or environment variables in deployed environments.

## Running locally

From the solution directory:

```powershell
dotnet restore .\PartsOrderTracker.Api.slnx
dotnet run --project .\PartsOrderTracker.Api\PartsOrderTracker.Api.csproj --launch-profile https
```

Default development URLs:

| Resource | URL |
| --- | --- |
| API over HTTPS | `https://localhost:7146` |
| API over HTTP | `http://localhost:5029` |
| Swagger UI | `https://localhost:7146/swagger` |
| OpenAPI document | `https://localhost:7146/openapi/v1.json` |

Trust the development HTTPS certificate if it has not been accepted yet:

```powershell
dotnet dev-certs https --trust
```

Swagger and the OpenAPI endpoint are only available in the `Development`
environment. The API's CORS policy permits requests from the UI at
`https://localhost:7053` and allows credentials, as required by the SignalR
browser connection.

## Authentication

All controller endpoints require authentication. Clients must send a JWT access
token issued by Microsoft Entra ID:

```http
Authorization: Bearer <access-token>
```

The API identifies the user through the access token's `oid` claim. When an order
is created, it also stores the `name` claim and either the `preferred_username`
or `email` claim.

The included Blazor WebAssembly client requests the following delegated scope:

```text
api://<api-client-id>/access_as_user
```

The API returns `401 Unauthorized` when the token is missing, invalid, or has an
incorrect audience. Order endpoints only expose orders belonging to the
authenticated user.

## API endpoints

The examples below use the following base URL:

```text
BASE_URL=https://localhost:7146
```

### List active parts

```http
GET /api/parts
```

Returns all active parts in ascending order by name.

**Successful response — `200 OK`**

```json
[
  {
    "id": "6908a93ecdcce35769335bb8",
    "articleNumber": "BRK-001",
    "name": "Brake Pad Set",
    "manufacturer": "Example Parts",
    "description": "Front axle brake pad set",
    "category": "Brakes",
    "price": 79.9,
    "imageUrl": "https://example.test/images/brake-pad.jpg",
    "inStock": true
  }
]
```

### List the current user's orders

```http
GET /api/orders/my
```

Returns up to 100 of the authenticated user's most recent orders, sorted by
creation time in descending order.

**Successful response — `200 OK`**

```json
[
  {
    "id": "6908b05acdcce35769335bc1",
    "items": [
      {
        "partId": "6908a93ecdcce35769335bb8",
        "articleNumber": "BRK-001",
        "name": "Brake Pad Set",
        "manufacturer": "Example Parts",
        "quantity": 2
      }
    ],
    "status": "Received",
    "createdAt": "2026-11-03T08:30:00Z",
    "updatedAt": "2026-11-03T08:30:00Z",
    "nextStatusChangeAt": "2026-11-03T08:30:10Z",
    "userName": "Example User",
    "userEmail": "user@example.test",
    "receivedAt": "2026-11-03T08:30:00Z",
    "processingAt": null,
    "dispatchedAt": null,
    "expectedArrival": null,
    "trackingId": ""
  }
]
```

### Retrieve one order

```http
GET /api/orders/{id}
```

| Parameter | Type | Description |
| --- | --- | --- |
| `id` | string | A MongoDB ObjectId consisting of 24 hexadecimal characters. |

**Responses**

| Status | Meaning |
| --- | --- |
| `200 OK` | The order exists and belongs to the authenticated user. |
| `400 Bad Request` | The `id` is not a valid MongoDB ObjectId. |
| `404 Not Found` | The order does not exist or belongs to another user. |

### Create an order

```http
POST /api/orders
Content-Type: application/json
```

**Request body**

```json
{
  "items": [
    {
      "partId": "6908a93ecdcce35769335bb8",
      "quantity": 2
    },
    {
      "partId": "6908a9b7cdcce35769335bba",
      "quantity": 1
    }
  ]
}
```

**Validation rules**

| Field | Rule |
| --- | --- |
| `items` | Required and must contain at least one item. |
| `items[].partId` | Required, must be a valid MongoDB ObjectId, and must reference an active part. |
| `items[].quantity` | Integer between 1 and 100, inclusive. |

**Responses**

| Status | Meaning |
| --- | --- |
| `201 Created` | The order was created. The `Location` header points to the order retrieval endpoint. |
| `400 Bad Request` | The request or one of its values is invalid, or a referenced part does not exist or is inactive. |

New orders start in the `Received` status. The API stores a snapshot of each
part's article number, name, and manufacturer at creation time. This means an
existing order can still be displayed correctly after the corresponding part is
modified.

## Real-time order updates

The API maps an `OrdersHub` at:

```text
/hubs/orders
```

The ASP.NET Core application handles negotiation while Azure SignalR Service
manages the live client connection and message routing.

### Hub subscription

After connecting, the UI invokes the following hub method:

```text
Subscribe(userId)
```

`OrdersHub` adds the connection to a SignalR group whose name is the supplied
`userId`. The UI uses the signed-in user's Microsoft Entra object ID (`oid`) as
this value. The status-processing worker must publish notifications to the group
with the same name.

### Server-to-client event

The UI listens for the following message:

```text
OrderStatusChanged
```

Expected payload:

```json
{
  "orderId": "6908b05acdcce35769335bc1",
  "status": "Processing"
}
```

The payload notifies the client that an order changed. The UI does not update its
local model directly from this payload; it retrieves the latest order list from
the REST API instead.

### Message flow

```text
Status-processing worker
        │  OrderStatusChanged → group: user oid
        ▼
Azure SignalR Service
        │
        ▼
OrdersHub-connected UI
        │
        └── GET /api/orders/my
            GET /api/parts
```

The worker that changes order statuses and publishes notifications is external
to this API project. The API hosts the hub and performs Azure SignalR negotiation
and routing only.

> The current hub accepts a client-supplied group name and is not mapped with an
> authorization requirement. For production use, derive the group from the
> authenticated connection's claims and require authorization on the hub instead
> of trusting `userId` supplied by the client.

## Data models

### `PartResponse`

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | MongoDB ObjectId. |
| `articleNumber` | string | Unique article number. |
| `name` | string | Part name. |
| `manufacturer` | string | Manufacturer. |
| `description` | string | Part description. |
| `category` | string | Part category. |
| `price` | decimal | Price. The model does not specify a currency. |
| `imageUrl` | string | URL of the product image. |
| `inStock` | boolean | Indicates whether the part is in stock. |

### `OrderResponse`

| Field | Type | Description |
| --- | --- | --- |
| `id` | string | MongoDB ObjectId. |
| `items` | array | Items included in the order. |
| `status` | string | `Received`, `Processing`, or `Dispatched`. |
| `createdAt` | ISO 8601 datetime | Creation time in UTC. |
| `updatedAt` | ISO 8601 datetime | Last update time in UTC. |
| `nextStatusChangeAt` | ISO 8601 datetime or null | Scheduled time of the next status change. |
| `userName` | string | Name of the user who created the order. |
| `userEmail` | string | Email address of the user who created the order. |
| `receivedAt` | ISO 8601 datetime or null | Time the order entered the `Received` status. |
| `processingAt` | ISO 8601 datetime or null | Time the order entered the `Processing` status. |
| `dispatchedAt` | ISO 8601 datetime or null | Time the order entered the `Dispatched` status. |
| `expectedArrival` | ISO 8601 datetime or null | Expected arrival time. |
| `trackingId` | string | Shipment tracking identifier; initially empty. |

### `OrderItemResponse`

| Field | Type | Description |
| --- | --- | --- |
| `partId` | string or null | Part identifier; it may be missing from legacy records. |
| `articleNumber` | string | Article number captured when the order was created. |
| `name` | string | Part name captured when the order was created. |
| `manufacturer` | string | Manufacturer captured when the order was created. |
| `quantity` | integer | Ordered quantity. |

## Error handling

Model-validation failures return an RFC 7807-compatible
`ValidationProblemDetails` response. Example for an invalid order ID:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "id": [
      "The order ID must be a valid MongoDB ObjectId."
    ]
  }
}
```

Common status codes:

| Status | Meaning |
| --- | --- |
| `400 Bad Request` | Invalid request or failed validation. |
| `401 Unauthorized` | No valid access token was provided. |
| `404 Not Found` | The requested user-owned resource was not found. |
| `500 Internal Server Error` | Unhandled server-side error. |

## MongoDB collections and indexes

The API uses two collections:

- `parts` stores parts and their activity and stock information;
- `orders` stores user orders with embedded order items.

The `PartsOrderTracker.Api/mongo-indexes.js` script creates the recommended
indexes:

| Collection | Index | Purpose |
| --- | --- | --- |
| `parts` | unique `articleNumber` | Ensures that article numbers are unique. |
| `orders` | `userId`, descending `createdAt` | Supports efficient retrieval of a user's latest orders. |
| `orders` | `status`, `nextStatusChangeAt` | Supports searches for scheduled status transitions. |

Run the script from MongoDB Shell after connecting to the intended Atlas cluster:

```javascript
load("mongo-indexes.js")
```

The script only creates indexes. It does not modify or delete documents.

## Architecture and behavior

```text
Blazor UI / HTTP client
        │
        ├── Bearer token ─────────► ASP.NET Core controllers
        │                               │
        │                               ├── PartsService ──► MongoDB: parts
        │                               │
        │                               └── OrdersService ─► MongoDB: orders
        │                                        │
        │                                        └── ICurrentUserService
        │                                            └── JWT claims: oid, name, email
        │
        └── SignalR connection ───► OrdersHub ◄──► Azure SignalR Service

External status worker
        │  group message
        ▼
Azure SignalR Service
```

- Controllers validate HTTP parameters and MongoDB ObjectIds.
- Services contain query, creation, and data-access logic.
- `OrdersService` filters every order query by the current user's `oid` claim.
- The model supports legacy order documents that use a single `part` and
  `quantity` field instead of the current `items` array.
- `nextStatusChangeAt` supports the external status-processing worker. This API
  project does not contain the background process itself.
- `OrdersHub` provides subscription and routing; it does not modify order data.

## Development commands

Build the API:

```powershell
dotnet build .\PartsOrderTracker.Api.slnx
```

Verify formatting:

```powershell
dotnet format .\PartsOrderTracker.Api.slnx --verify-no-changes
```

Use `PartsOrderTracker.Api/PartsOrderTracker.Api.http` to test the API manually.
Set a valid bearer token in the file's `token` variable before sending requests.

The repository does not currently contain an automated test project.

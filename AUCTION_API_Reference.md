# Auction Service — REST API Reference

**Base URL:** `http://localhost:5001` (direct) or `http://localhost:5000/auction` (via API Gateway)
**Auth:** JWT Bearer token. Pass as `Authorization: Bearer <token>` header.
> Endpoints marked **[Public]** do not require a token.

---

## Standard Response Envelope

All endpoints return the same JSON envelope:

```json
{
  "success": true,
  "message": "Operation successful",
  "statusCode": 200,
  "data": { ... },
  "errors": []
}
```

---

## 1. Auction Endpoints

### GET /api/auctions — List All Auctions **[Public]**

Paginated list of auctions with optional filters.

**Query Parameters:**

| Parameter  | Type   | Default | Description                                                  |
|------------|--------|---------|--------------------------------------------------------------|
| `Status`   | string | —       | `Upcoming`, `Live`, `Ended`, `Cancelled`, `UnVerified`       |
| `Page`     | int    | 1       | Page number                                                  |
| `PageSize` | int    | 20      | Items per page                                               |

**Example Request:**
```
GET /api/auctions?Status=Live&Page=1&PageSize=10
```

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 1,
        "productId": 42,
        "createdByUserId": 7,
        "startingPrice": 500.00,
        "reservePrice": 1000.00,
        "minBidIncrement": 50.00,
        "startDate": "2026-03-17T10:00:00Z",
        "endDate": "2026-03-17T18:00:00Z",
        "status": "Live",
        "currentHighestBid": 750.00,
        "totalBids": 5,
        "timeRemainingSeconds": 28800.0,
        "createdAt": "2026-03-16T09:00:00Z"
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 10
  }
}
```

---

### GET /api/auctions/{auctionId} — Get Auction Detail **[Public]**

Returns full auction detail including recent bids, winner info, watcher/viewer counts.

**Path Parameter:** `auctionId` (int)

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "productId": 42,
    "createdByUserId": 7,
    "startingPrice": 500.00,
    "reservePrice": 1000.00,
    "minBidIncrement": 50.00,
    "startDate": "2026-03-17T10:00:00Z",
    "endDate": "2026-03-17T18:00:00Z",
    "status": "Live",
    "currentHighestBid": 750.00,
    "totalBids": 5,
    "timeRemainingSeconds": 28800.0,
    "watcherCount": 12,
    "liveViewerCount": 3,
    "highestBid": {
      "id": 9,
      "auctionId": 1,
      "maskedBidder": "7***3",
      "amount": 750.00,
      "status": "Active",
      "placedAt": "2026-03-17T12:30:00Z"
    },
    "winner": null,
    "recentBids": [
      {
        "id": 9,
        "auctionId": 1,
        "maskedBidder": "7***3",
        "amount": 750.00,
        "status": "Active",
        "placedAt": "2026-03-17T12:30:00Z"
      }
    ],
    "createdAt": "2026-03-16T09:00:00Z"
  }
}
```

**Errors:**
- `404` — Auction not found

---

### GET /api/auctions/{auctionId}/winner — Get Winner Info **[Public]**

Returns winner info after an auction ends.

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "auctionId": 1,
    "winnerUserId": 23,
    "finalPrice": 1200.00,
    "closedAt": "2026-03-17T18:00:05Z"
  }
}
```

**Errors:**
- `404` — No winner yet (auction still live or no bids met reserve)

---

### POST /api/auctions — Create Auction **[Auth Required — Verified Users Only]**

Creates a new auction for a verified product. The caller must be a verified user (JWT claim `IsVerified=true`). The endpoint calls the Verify service via API Gateway to confirm the product is verified before creating.

**Request Body:**
```json
{
  "productId": 42,
  "startingPrice": 500.00,
  "reservePrice": 1000.00,
  "minBidIncrement": 50.00,
  "startDate": "2026-03-18T10:00:00Z",
  "endDate": "2026-03-18T18:00:00Z"
}
```

| Field            | Type     | Required | Description                                   |
|------------------|----------|----------|-----------------------------------------------|
| `productId`      | int      | ✅       | Must be a verified product owned by the user  |
| `startingPrice`  | decimal  | ✅       | Minimum first bid                             |
| `reservePrice`   | decimal  | ❌       | Minimum price to declare a winner             |
| `minBidIncrement`| decimal  | ✅       | Each bid must exceed previous by this amount  |
| `startDate`      | DateTime | ✅       | UTC datetime when auction goes live            |
| `endDate`        | DateTime | ✅       | UTC datetime when auction closes               |

**Response `201 Created`:**
```json
{
  "success": true,
  "message": "Auction created successfully",
  "statusCode": 201,
  "data": { /* AuctionResponse */ }
}
```

**Also publishes:** `AuctionCreated` event via RabbitMQ.

**Errors:**
- `403` — User is not verified
- `404` — Product not found or not verified by admin

---

### PATCH /api/auctions/{auctionId} — Update Auction **[Auth Required — Owner Only]**

Update auction details. Only allowed while auction is in `Upcoming` status, before any bids.

**Request Body (all fields optional):**
```json
{
  "startingPrice": 600.00,
  "reservePrice": 1200.00,
  "minBidIncrement": 100.00,
  "startDate": "2026-03-18T11:00:00Z",
  "endDate": "2026-03-18T19:00:00Z"
}
```

**Errors:**
- `403` — Not the owner
- `400` — Auction is already Live or Ended
- `400` — Cannot change startingPrice after bids exist

---

### DELETE /api/auctions/{auctionId} — Cancel Auction **[Auth Required — Owner Only]**

Cancels an upcoming auction. Cannot cancel if `Live` or `Ended`.

**Response `200 OK`:**
```json
{ "success": true, "message": "Auction cancelled", "data": true }
```

**Also publishes:** `AuctionCancelled` event via RabbitMQ.

**Errors:**
- `403` — Not the owner
- `400` — Auction is Live or already Ended

---

### GET /api/auctions/created — My Created Auctions **[Auth Required]**

Returns all auctions created by the logged-in user.

**Response `200 OK`:** Array of `AuctionResponse` inside `data`.

---

### GET /api/auctions/participated — My Participated Auctions **[Auth Required]**

Returns all auctions where the logged-in user has placed at least one bid.

**Response `200 OK`:** Array of `AuctionResponse` inside `data`.

---

## 2. Bid Endpoints

### POST /api/auctions/{auctionId}/bids — Place a Bid **[Auth Required]**

Places a bid on a live auction. The bid amount must be ≥ `currentHighestBid + minBidIncrement` (or `startingPrice` if no bids yet).

**Path Parameter:** `auctionId` (int)

**Request Body:**
```json
{
  "amount": 800.00
}
```

**Response `201 Created`:**
```json
{
  "success": true,
  "message": "Bid placed successfully",
  "statusCode": 201,
  "data": {
    "id": 10,
    "auctionId": 1,
    "maskedBidder": "2***5",
    "amount": 800.00,
    "status": "Active",
    "placedAt": "2026-03-17T13:00:00Z"
  }
}
```

> **Auto-Extend:** If a bid is placed within the last 2 minutes of the auction, the `endDate` is extended by 2 minutes (up to a maximum number of extensions). A `TimerTick` SignalR event is pushed to all clients in the room.

**Also publishes:** `AuctionBidPlaced` event via RabbitMQ.

**Errors:**
- `400` — Amount below minimum required
- `400` — Auction is not live
- `403` — Owner cannot bid on their own auction
- `400` — Please wait before placing another bid (Redis lock, 5s cooldown)

---

### GET /api/auctions/{auctionId}/bids — Bid History **[Public]**

Returns paginated bid history for an auction.

**Query Parameters:** `page` (default 1), `pageSize` (default 20)

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "items": [
      {
        "id": 10,
        "auctionId": 1,
        "maskedBidder": "2***5",
        "amount": 800.00,
        "status": "Active",
        "placedAt": "2026-03-17T13:00:00Z"
      }
    ],
    "totalCount": 10,
    "page": 1,
    "pageSize": 20
  }
}
```

---

### GET /api/auctions/{auctionId}/bids/highest — Highest Bid **[Public]**

Returns the current highest bid (served from Redis cache for speed).

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "bidId": 10,
    "userId": 25,
    "amount": 800.00,
    "placedAt": "2026-03-17T13:00:00Z"
  }
}
```

---

### GET /api/auctions/{auctionId}/bids/mine — My Bids **[Auth Required]**

Returns all bids placed by the logged-in user on a specific auction, with a flag indicating whether each bid is the current winning bid.

**Response `200 OK`:**
```json
{
  "success": true,
  "data": [
    {
      "id": 10,
      "auctionId": 1,
      "userId": 25,
      "maskedBidder": "2***5",
      "amount": 800.00,
      "status": "Active",
      "placedAt": "2026-03-17T13:00:00Z",
      "isCurrentlyWinning": true
    }
  ]
}
```

---

## 3. Watchlist Endpoints

### POST /api/auctions/{auctionId}/watch — Add to Watchlist **[Auth Required]**

Adds the auction to the user's watchlist.

**Response `200 OK`:** `{ "success": true, "message": "...", "data": true }`

---

### DELETE /api/auctions/{auctionId}/watch — Remove from Watchlist **[Auth Required]**

**Response `200 OK`:** `{ "success": true, "message": "...", "data": true }`

---

### GET /api/auctions/watched — My Watchlist **[Auth Required]**

Returns all auctions the user is watching.

**Response `200 OK`:** Array of `AuctionResponse` inside `data`.

---

## 4. Admin Endpoints

### GET /api/admin/auctions — All Auctions **[Admin Role]**

Same as public listing but accessible to admins only with full filter support.

---

### PATCH /api/admin/auctions/{auctionId}/force-close — Force Close **[Admin Role]**

Admin can force-close any live auction immediately, declaring the winner if applicable.

**Response `200 OK`:**
```json
{
  "success": true,
  "message": "Auction force-closed by admin",
  "data": { /* WinnerResponse or null */ }
}
```

---

## 5. Auction Status Values

| Status       | Description                                      |
|--------------|--------------------------------------------------|
| `Upcoming`   | Created, not yet started                         |
| `Live`       | Currently accepting bids                         |
| `Ended`      | Closed (winner declared or no winner)            |
| `Cancelled`  | Cancelled by owner before going live             |
| `UnVerified` | Paused because verifier un-verified the product  |

---

## 6. RabbitMQ Events Published

The Auction service publishes these events that other services can consume:

| Event                 | When Published                                       |
|-----------------------|------------------------------------------------------|
| `AuctionCreated`      | New auction created                                  |
| `AuctionStarted`      | Scheduler starts the auction                         |
| `AuctionBidPlaced`    | A valid bid is placed                                |
| `AuctionEndingSoon`   | Scheduler detects < 5 minutes remaining              |
| `AuctionClosed`       | Scheduler or admin closes the auction                |
| `AuctionWinnerDeclared` | After close with a valid winner (reserve met)      |
| `AuctionCancelled`    | Owner cancels an upcoming auction                    |

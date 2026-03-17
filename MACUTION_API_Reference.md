# MACUTION — Complete API Reference
**All endpoints route through the API Gateway at `http://localhost:5000`**

---

## Architecture Overview

```
Frontend
    │
    ▼
API Gateway  :5000   (YARP Reverse Proxy)
    │
    ├─ /api/user/**          → User Service       :8080
    ├─ /api/admin/**         → User Service       :8080  (admin login & user management)
    ├─ /api/admin-request/** → Admin Service      :5087  (verification request management)
    ├─ /api/admin-product/** → Admin Service      :5087  (product approval)
    ├─ /api/product/**       → Product Service    :5088
    └─ /api/verify/**        → Verify Service     :5089

Auction Service :5001  (Direct — NOT through gateway)
SignalR Hub     ws://localhost:5001/hubs/auction
```

> **Auth:** All protected endpoints require `Authorization: Bearer <JWT>` header.
> **Response Envelope:** All responses use `{ success, message, statusCode, data, errors }`.

---

## Standard Response Envelope

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

## 1. User Service — via Gateway `:5000`

### 1.1 User Registration & Auth

#### `POST /api/user/create` — Register New User
**Auth:** None (public)  
**Body:** `multipart/form-data`

| Field        | Type   | Required | Description            |
|--------------|--------|----------|------------------------|
| `name`       | string | ✅       | Full name              |
| `email`      | string | ✅       | Email address          |
| `password`   | string | ✅       | Password               |
| `profileImage` | file | ❌       | Avatar image upload    |

**Response `200 OK`:**
```json
{ "success": true, "message": "User created", "data": { "id": 1, "name": "...", "email": "..." } }
```

---

#### `POST /api/user/login` — User Login
**Auth:** None (public)  
**Body:** `application/json`
```json
{ "email": "user@example.com", "password": "yourpassword" }
```
**Response `200 OK`:**
```json
{ "success": true, "data": { "token": "eyJ...", "userId": 1, "role": "USER" } }
```

---

#### `PATCH /api/user/profile` — Update My Profile
**Auth:** Required (any role)  
**Body:** `application/json`
```json
{ "name": "New Name", "bio": "..." }
```
**Response `200 OK`:** Updated profile data.

---

#### `GET /api/user/profile/{id}` — Get User Profile
**Auth:** Required  
**Path:** `id` — user ID to look up (pass `0` to get your own profile)

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "id": 1,
    "name": "Jane Doe",
    "email": "jane@example.com",
    "role": "SELLER",
    "isVerified": true
  }
}
```

---

### 1.2 Admin User Management (in User Service)

> Routed via `/api/admin/**` → User Service `:8080`

#### `POST /api/admin/signup` — Create Admin Account
**Auth:** None (public — used for initial setup)  
**Body:** `application/json`
```json
{ "name": "Admin Name", "email": "admin@example.com", "password": "adminpass" }
```

---

#### `POST /api/admin/Login` — Admin Login
**Auth:** None (public)  
**Body:** `application/json`
```json
{ "email": "admin@example.com", "password": "adminpass" }
```
**Response `200 OK`:**
```json
{ "success": true, "data": { "token": "eyJ...", "userId": 2, "role": "ADMIN" } }
```

---

#### `GET /api/admin/verified` — List Verified Users **[Admin]**
**Auth:** Required — `ADMIN` role  
**Query:** `page` (default 1), `size` (default 10)

**Response `200 OK`:** Paginated list of verified user requests.

---

#### `GET /api/admin/pending` — List Pending Verification Requests **[Admin]**
**Auth:** Required — `ADMIN` role  
**Query:** `page` (default 1), `size` (default 10)

**Response `200 OK`:** Paginated list of pending requests.

---

## 2. Admin Service — via Gateway `:5000`

> All admin-request and admin-product routes go to Admin Service `:5087`

### 2.1 Request Management `/api/admin-request/**`

> All endpoints require `ADMIN` role unless marked [Public].

#### `GET /api/admin-request/verify/{RequestId}` — Approve a Verification Request
**Auth:** Admin  
**Path:** `RequestId` (int) — the request to approve

**Response `200 OK`:**
```json
{ "success": true, "message": "Request verified successfully", "data": { ... } }
```
**Also publishes:** `AdminRegistrationRequested` event → RabbitMQ

---

#### `GET /api/admin-request/grant-rights/{requestId}` — Grant USER Rights
**Auth:** Admin  
**Path:** `requestId` — grants seller/user rights to the requester

---

#### `GET /api/admin-request/revoke-rights/{requestId}` — Revoke USER Rights
**Auth:** Admin

---

#### `GET /api/admin-request/revoke-verification/{requestId}` — Revoke Verification
**Auth:** Admin  
Revokes admin/verifier status from a previously verified user.

---

#### `GET /api/admin-request/details/{id}` — Get Request Details **[Public]**
**Auth:** None  
**Path:** `id` — request ID

**Response `200 OK`:** Full request details object.

---

#### `GET /api/admin-request/user/{userId}` — Get Requests by User **[Public]**
**Auth:** None  
**Path:** `userId`

**Response `200 OK`:** Array of all requests made by the specified user.

---

#### `GET /api/admin-request/pending` — All Pending Requests **[Public]**
**Auth:** None

**Response `200 OK`:** List of all pending verification requests.

---

#### `GET /api/admin-request/verified` — All Verified Requests **[Public]**
**Auth:** None

**Response `200 OK`:** List of all approved/verified requests.

---

#### `GET /api/admin-request/dashboard` — Admin Dashboard Stats **[Public]**
**Auth:** None

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "totalRequests": 50,
    "pendingCount": 12,
    "verifiedCount": 38
  }
}
```

---

### 2.2 Product Approval `/api/admin-product/**`

#### `POST /api/admin-product/verify` — Approve a Product for Auction **[Admin]**
**Auth:** Required — `ADMIN` role  
**Body:** `application/json`
```json
{
  "productId": 42,
  "verifierId": 5,
  "description": "Product meets all criteria"
}
```

**Response `200 OK`:**
```json
{ "success": true, "message": "Product verified", "data": { ... } }
```
**Also publishes:** `ProductVerifyRequested` event → RabbitMQ

---

#### `DELETE /api/admin-product/unverify/{id}` — Un-Verify a Product **[Admin]**
**Auth:** Required — `ADMIN` role  
**Path:** `id` — product ID  
**Query:** `description` — reason for un-verification (optional)

**Response `200 OK`:** 
```json
{ "success": true, "message": "Product unverified", "data": { ... } }
```
**Also publishes:** `ProductUnverifyRequested` + `ProductUnverified` events → RabbitMQ

---

## 3. Product Service — via Gateway `:5000`

> Routes: `/api/product/**` → Product Service `:5088`  
> **Role required:** `SELLER` or `USER` for all write operations

### `POST /api/product` — Create Product **[SELLER / USER]**
**Auth:** Required  
**Body:** `application/json`
```json
{
  "name": "Vintage Watch",
  "description": "1960s Swiss automatic watch",
  "categoryId": 3,
  "basePrice": 500.00
}
```

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "id": 42,
    "name": "Vintage Watch",
    "description": "...",
    "userId": 7,
    "isVerified": false,
    "images": [],
    "createdAt": "2026-03-17T08:00:00Z"
  }
}
```
**Also publishes:** `ProductCreatedForVerification` event → RabbitMQ

---

### `PATCH /api/product/{productId}` — Update Product **[SELLER / USER — Owner Only]**
**Auth:** Required  
**Path:** `productId` (int)  
**Body:** `multipart/form-data` (partial update — only include fields to change)

| Field          | Type    | Description              |
|----------------|---------|--------------------------|
| `name`         | string  | Product name             |
| `description`  | string  | Description              |
| `basePrice`    | decimal | Base price               |

**Response `200 OK`:** Updated `ProductDto`.  
**Errors:** `403` Not owner, `404` Not found

---

### `DELETE /api/product/{productId}` — Delete Product **[SELLER / USER — Owner Only]**
**Auth:** Required  
**Path:** `productId` (int)

**Response `200 OK`:** Deleted `ProductDto`.  
**Also publishes:** `ProductDeleted` + `productDeleteImage` events → RabbitMQ  
**Errors:** `403` Not owner, `404` Not found

---

### `GET /api/product/all` — List My Products **[SELLER / USER / ADMIN]**
**Auth:** Required  
**Query Parameters:**

| Param  | Type   | Description                  |
|--------|--------|------------------------------|
| `page` | int    | Page number (default 1)      |
| `size` | int    | Page size (default 10)       |
| `name` | string | Search by product name       |

**Response `200 OK`:** Array of `ProductDto` belonging to the logged-in user.

---

### `POST /api/product/images` — Add Images to Product **[SELLER / USER — Owner Only]**
**Auth:** Required  
**Body:** `multipart/form-data`

| Field       | Type         | Required | Description             |
|-------------|--------------|----------|-------------------------|
| `productId` | int          | ✅       | Product to add images to|
| `images`    | file[]       | ✅       | Image files to upload   |

**Response `200 OK`:** Updated `ProductDto` with new images.

---

### `DELETE /api/product/{productId}/images/{imageId}` — Delete Product Image **[SELLER / USER — Owner Only]**
**Auth:** Required  
**Path:** `productId`, `imageId`

**Response `200 OK`:** Updated `ProductDto`.  
**Also publishes:** `productDeleteImage` event → RabbitMQ (removes from Cloudinary)

---

## 4. Verify Service — via Gateway `:5000`

> Routes: `/api/verify/**` → Verify Service `:5089`

### `POST /api/verify/product` — Verify a Product **[Admin]**
**Auth:** Required — `ADMIN` role  
**Body:** `application/json`
```json
{
  "productId": 42,
  "description": "All documents in order"
}
```

**Response `200 OK`:**
```json
{
  "success": true,
  "message": "Product verified",
  "data": {
    "verificationId": 10,
    "productId": 42,
    "verifierId": 5,
    "status": "Verified",
    "verifiedAt": "2026-03-17T10:00:00Z"
  }
}
```
**Also publishes:** `ProductVerified` event → RabbitMQ

---

### `DELETE /api/verify/product/{productId}` — Un-Verify a Product **[Admin]**
**Auth:** Required — `ADMIN` role  
**Path:** `productId` (int)  
**Body:** `application/json` (optional string reason)
```json
"Product recalled for re-inspection"
```

**Response `200 OK`:** Success message.  
**Also publishes:** `ProductUnverified` + `ProductUnverifiedFromService` events → RabbitMQ

---

### `GET /api/verify/status/{productId}` — Get Product Verification Status **[Public]**
**Auth:** None  
**Path:** `productId` (int)

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "productId": 42,
    "status": "Verified",
    "verifierId": 5,
    "verifiedAt": "2026-03-17T10:00:00Z"
  }
}
```
> This endpoint is also internally called by the Auction service when creating a new auction.

**Errors:** `404` — Product not verified / not found in verify service

---

### `GET /api/verify/my-products` — Products Verified by Me **[Admin]**
**Auth:** Required — `ADMIN` role  
**Query:** `searchName` (string), `page` (default 1), `size` (default 10)

**Response `200 OK`:** Paginated list of products this admin has verified.

---

### `GET /api/verify/unverified-products` — Pending Unverified Products **[Admin]**
**Auth:** Required — `ADMIN` role  
**Query:** `searchName` (string), `page` (default 1), `size` (default 10)

**Response `200 OK`:** Paginated list of products awaiting verification.

---

## 5. Auction Service — Direct `:5001`

> **Not behind API Gateway.** Call directly: `http://localhost:5001`  
> **CORS:** Allows any origin (`SetIsOriginAllowed(_ => true)`)

### 5.1 Auction CRUD

#### `GET /api/auctions` — List Auctions **[Public]**
**Query:** `Status` (Upcoming/Live/Ended/Cancelled/UnVerified), `Page` (default 1), `PageSize` (default 20)

**Response `200 OK`:**
```json
{
  "success": true,
  "data": {
    "items": [ { "id": 1, "productId": 42, "status": "Live", "currentHighestBid": 800.00, "totalBids": 5, "timeRemainingSeconds": 28800 } ],
    "totalCount": 1, "page": 1, "pageSize": 20
  }
}
```

---

#### `GET /api/auctions/{auctionId}` — Get Auction Detail **[Public]**
Returns full auction details including recent bids, winner, viewer/watcher counts.

```json
{
  "success": true,
  "data": {
    "id": 1, "productId": 42, "status": "Live",
    "startingPrice": 500.00, "reservePrice": 1000.00, "minBidIncrement": 50.00,
    "currentHighestBid": 800.00, "totalBids": 5,
    "watcherCount": 12, "liveViewerCount": 3,
    "timeRemainingSeconds": 28800,
    "highestBid": { "id": 9, "maskedBidder": "7***3", "amount": 800.00 },
    "winner": null,
    "recentBids": [ ... ]
  }
}
```

---

#### `GET /api/auctions/{auctionId}/winner` — Get Winner **[Public]**
```json
{ "success": true, "data": { "auctionId": 1, "winnerUserId": 23, "finalPrice": 1200.00, "closedAt": "2026-03-17T18:00:00Z" } }
```
**Errors:** `404` — No winner yet

---

#### `POST /api/auctions` — Create Auction **[Auth — Verified Users Only]**
**Auth:** Required. The logged-in user must have `IsVerified=true` in their JWT claims.  
Internally calls `GET /api/verify/status/{productId}` via gateway to confirm the product is verified.

**Body:**
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
**Response `201 Created`:** Created `AuctionResponse`.  
**Also publishes:** `AuctionCreated` event → RabbitMQ  
**Errors:** `403` Not verified, `404` Product not verified by admin

---

#### `PATCH /api/auctions/{auctionId}` — Update Auction **[Auth — Owner Only]**
Only allowed while status = `Upcoming` (before any bids for starting price).
```json
{ "startingPrice": 600.00, "reservePrice": 1200.00, "startDate": "...", "endDate": "..." }
```

---

#### `DELETE /api/auctions/{auctionId}` — Cancel Auction **[Auth — Owner Only]**
Only allowed while status = `Upcoming`.  
**Also publishes:** `AuctionCancelled` → RabbitMQ

---

#### `GET /api/auctions/created` — My Created Auctions **[Auth]**
Returns all auctions created by the logged-in user.

---

#### `GET /api/auctions/participated` — My Participated Auctions **[Auth]**
Returns all auctions where the logged-in user has placed bids.

---

### 5.2 Bid Endpoints

#### `POST /api/auctions/{auctionId}/bids` — Place a Bid **[Auth]**
Must be ≥ `currentHighestBid + minBidIncrement`. Redis lock prevents double-bids (5s cooldown).  
Auto-extends auction by 2 minutes if bid placed in last 2 minutes of auction.

```json
{ "amount": 850.00 }
```
**Response `201 Created`:**
```json
{ "success": true, "data": { "id": 10, "auctionId": 1, "maskedBidder": "2***5", "amount": 850.00, "status": "Active", "placedAt": "..." } }
```
**Also publishes:** `AuctionBidPlaced` → RabbitMQ  
**Also pushes:** `BidPlaced` SignalR event to all room members

---

#### `GET /api/auctions/{auctionId}/bids` — Bid History **[Public]**
**Query:** `page` (default 1), `pageSize` (default 20)

---

#### `GET /api/auctions/{auctionId}/bids/highest` — Current Highest Bid **[Public]**
Served from Redis cache for speed.
```json
{ "success": true, "data": { "bidId": 10, "userId": 25, "amount": 850.00, "placedAt": "..." } }
```

---

#### `GET /api/auctions/{auctionId}/bids/mine` — My Bids on This Auction **[Auth]**
```json
{
  "success": true,
  "data": [ { "id": 10, "amount": 850.00, "status": "Active", "isCurrentlyWinning": true } ]
}
```

---

### 5.3 Watchlist Endpoints

#### `POST /api/auctions/{auctionId}/watch` — Watch Auction **[Auth]**
#### `DELETE /api/auctions/{auctionId}/watch` — Unwatch Auction **[Auth]**
#### `GET /api/auctions/watched` — My Watchlist **[Auth]**

---

### 5.4 Admin Auction Endpoints

#### `GET /api/admin/auctions` — All Auctions **[ADMIN]**
Same as public GET list but requires ADMIN role. Full filter support.

#### `PATCH /api/admin/auctions/{auctionId}/force-close` — Force Close **[ADMIN]**
Immediately closes a live auction and declares a winner if applicable.

---

### 5.5 Auction Status Values

| Status       | Description                                       |
|--------------|---------------------------------------------------|
| `Upcoming`   | Created, waiting for StartDate                    |
| `Live`       | Currently accepting bids                          |
| `Ended`      | Closed — winner declared or reserve not met       |
| `Cancelled`  | Cancelled by owner (Upcoming only)                |
| `UnVerified` | Paused — verifier un-verified the product mid-auction |

---

## 6. SignalR Hub — Auction Real-Time

**URL:** `ws://localhost:5001/hubs/auction`  
**Auth:** Pass JWT as query string: `?access_token=<token>`

### Connection

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5001/hubs/auction", {
    accessTokenFactory: () => localStorage.getItem("token")
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
await connection.invoke("JoinAuction", "42"); // join auction room
```

### Client → Server Methods

| Method | Args | Description |
|--------|------|-------------|
| `JoinAuction` | `auctionId: string` | Subscribe to an auction room |
| `LeaveAuction` | `auctionId: string` | Unsubscribe from a room |

### Server → Client Events

| Event | Payload | Description |
|-------|---------|-------------|
| `BidPlaced` | `{ bidId, maskedBidder, amount, placedAt, newEndDate }` | A new bid was placed |
| `ViewerCountUpdated` | `count: number` | Someone joined/left the room |
| `AuctionStarted` | `{ auctionId }` | Auction went live |
| `AuctionClosed` | `{ auctionId, winnerUserId?, finalPrice?, status? }` | Auction ended |
| `AuctionEndingSoon` | `{ auctionId, minutesRemaining }` | < 5 minutes left |
| `TimerTick` | `{ auctionId, secondsRemaining }` | Countdown sync (every 30s) |
| `AuctionMessage` | `{ message }` | Info notification (e.g. auto-extend) |
| `AuctionAborted` | `{ auctionId, reason }` | Product deleted mid-auction |
| `AuctionUnverified` | `{ auctionId, reason }` | Product un-verified mid-auction |

---

## 7. RabbitMQ Events (Cross-Service)

| Event | Published By | Consumed By | Purpose |
|-------|-------------|-------------|---------|
| `AdminRegistrationRequested` | Admin Service | — | Admin signup notification |
| `ProductCreatedForVerification` | Product Service | Verify Service | Product needs verification |
| `ProductDeleted` | Product Service | Auction Service | Cancel auctions for deleted product |
| `ProductVerifyRequested` | Admin Service | — | Verifier assigned to product |
| `ProductUnverifyRequested` | Admin Service | — | Verifier removed from product |
| `ProductVerified` | Verify Service | Auction Service | Product cleared for auction |
| `ProductUnverified` | Verify Service | Auction Service | Close any live auctions for product |
| `ProductUnverifiedFromService` | Verify Service | — | Internal audit trail |
| `productDeleteImage` | Product Service | Cloudinary Service | Remove image from CDN |
| `AuctionCreated` | Auction Service | — | Auction registered |
| `AuctionStarted` | Auction Scheduler | — | Auction is now live |
| `AuctionBidPlaced` | Auction Service | — | A bid was accepted |
| `AuctionEndingSoon` | Auction Scheduler | — | < 5 minutes warning |
| `AuctionClosed` | Auction Service | — | Auction ended |
| `AuctionWinnerDeclared` | Auction Service | — | Winner notification |
| `AuctionCancelled` | Auction Service | — | Auction cancelled |

---

## 8. Service Port Map

| Service | Port | Gateway Path |
|---------|------|-------------|
| API Gateway | 5000 | — |
| User Service | 8080 | `/api/user/**`, `/api/admin/**` |
| Admin Service | 5087 | `/api/admin-request/**`, `/api/admin-product/**` |
| Product Service | 5088 | `/api/product/**` |
| Verify Service | 5089 | `/api/verify/**` |
| Auction Service | **5001 (Direct)** | Not in gateway |
| Cloudinary Service | — | Internal only |
| RabbitMQ | 5672 | Internal only |
| Redis | 6379 | Internal only |

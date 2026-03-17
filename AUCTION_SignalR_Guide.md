# Auction Service — SignalR Hub Guide

**Hub URL:** `http://localhost:5001/hubs/auction`  
(via API Gateway: `http://localhost:5000/auction-hub/hubs/auction` — check gateway config)

---

## Overview

The Auction service exposes a **SignalR hub** at `/hubs/auction` for real-time auction updates. Clients connect to this hub to receive live updates about bids, timers, auction state changes, viewer counts, and important alerts.

---

## Authentication

SignalR uses JWT Bearer authentication. For WebSocket connections, the browser cannot set custom headers, so the **token must be passed as a query string parameter** (`access_token`):

```javascript
import * as signalR from "@microsoft/signalr";

const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5001/hubs/auction", {
    accessTokenFactory: () => localStorage.getItem("token") // your JWT
  })
  .withAutomaticReconnect()
  .build();

await connection.start();
```

> The hub automatically reads `?access_token=<token>` from the query string for WebSocket connections.

---

## Methods You Can CALL (Client → Server)

### `JoinAuction(auctionId: string)`

Call this when the user opens an auction page. Subscribes them to the auction's real-time room and increments the live viewer count.

```javascript
await connection.invoke("JoinAuction", "42"); // auctionId as string
```

**Effect:**
- User joins SignalR group `auction_42`
- Viewer count is incremented in Redis
- All users in the room receive a `ViewerCountUpdated` event

---

### `LeaveAuction(auctionId: string)`

Call this when the user navigates away from the auction page.

```javascript
await connection.invoke("LeaveAuction", "42");
```

**Effect:**
- User leaves SignalR group `auction_42`
- Viewer count is decremented in Redis
- All remaining users receive `ViewerCountUpdated`

---

## Events You RECEIVE (Server → Client)

Register listeners **before** calling `Start()` on the connection.

---

### `BidPlaced` — New Bid Was Placed

Fired every time a valid bid is placed on this auction.

```javascript
connection.on("BidPlaced", (data) => {
  console.log(data);
  // {
  //   bidId: 10,
  //   maskedBidder: "2***5",   // privacy-masked user ID
  //   amount: 800.00,
  //   placedAt: "2026-03-17T13:00:00Z",
  //   newEndDate: "2026-03-17T18:02:00Z"  // may extend if bid in last 2 min
  // }
});
```

**Use this to:** Update the highest bid display, bid history list, and timer (if `newEndDate` changed).

---

### `ViewerCountUpdated` — Live Viewer Count Changed

Fired when a user joins or leaves the auction room.

```javascript
connection.on("ViewerCountUpdated", (count) => {
  console.log(`${count} people watching`);
  // count: number (e.g. 7)
});
```

---

### `AuctionStarted` — Auction Went Live

Fired when the scheduler transitions the auction from `Upcoming` → `Live`.

```javascript
connection.on("AuctionStarted", (data) => {
  console.log(data);
  // { auctionId: 1 }
});
```

**Use this to:** Enable the bid input, start your local countdown, update status badge.

---

### `AuctionClosed` — Auction Ended

Fired when the auction closes (scheduler, admin force-close, or owner cancellation).

```javascript
connection.on("AuctionClosed", (data) => {
  console.log(data);
  // When there IS a winner:
  // {
  //   auctionId: 1,
  //   winnerUserId: 23,
  //   finalPrice: 1200.00,
  //   closedAt: "2026-03-17T18:00:05Z"
  // }
  //
  // When NO winner (reserve not met or no bids):
  // { auctionId: 1, status: "ended_no_winner" }
  //
  // When owner cancels:
  // { status: "cancelled" }
});
```

**Use this to:** Show the winner banner, disable the bid input, display final price.

---

### `AuctionEndingSoon` — Less Than 5 Minutes Left

Fired by the scheduler when the auction has fewer than 5 minutes remaining.

```javascript
connection.on("AuctionEndingSoon", (data) => {
  console.log(data);
  // { auctionId: 1, minutesRemaining: 4 }
});
```

**Use this to:** Show a warning banner, pulse/highlight the countdown timer.

---

### `TimerTick` — Countdown Update (every 30 seconds)

The scheduler ticks every 30 seconds and pushes the remaining time for ALL live auctions.

```javascript
connection.on("TimerTick", (data) => {
  console.log(data);
  // { auctionId: 1, secondsRemaining: 28740.5 }
});
```

> **Note:** Also fired immediately when a bid is placed in the last 2 minutes (auto-extend). Always use `secondsRemaining` from this event to sync your displayed countdown rather than relying solely on local time.

**Use this to:** Sync the displayed countdown timer with the server.

---

### `AuctionMessage` — General Info Message

A plain informational message pushed for important events (e.g. auto-extension notification).

```javascript
connection.on("AuctionMessage", (data) => {
  console.log(data);
  // { message: "Bid placed in last 2 minutes — auction extended by 2 minutes" }
});
```

**Use this to:** Show a toast/snackbar notification to all bidders.

---

### `AuctionAborted` — Auction Aborted (Product Deleted)

Fired when the product associated with this auction is **deleted** by its owner while the auction is live. The auction is immediately cancelled.

```javascript
connection.on("AuctionAborted", (data) => {
  console.log(data);
  // { auctionId: 1, reason: "Product deleted by owner" }
});
```

**Use this to:** Show an alert to all bidders that the auction was aborted, disable bidding, refund notifications.

---

### `AuctionUnverified` — Product Un-Verified During Auction

Fired when the verifier **un-verifies** the product while the auction is live. The auction is paused/closed with `UnVerified` status.

```javascript
connection.on("AuctionUnverified", (data) => {
  console.log(data);
  // { auctionId: 1, reason: "Product un-verified during live auction" }
});
```

**Use this to:** Alert bidders that the auction is on hold, show a warning message.

---

## Full React/TypeScript Example

```typescript
import * as signalR from "@microsoft/signalr";
import { useEffect, useRef } from "react";

export function useAuctionHub(auctionId: number) {
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("http://localhost:5001/hubs/auction", {
        accessTokenFactory: () => localStorage.getItem("token") ?? ""
      })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Register all events BEFORE starting
    connection.on("BidPlaced", (data) => {
      // update bid list & timer
    });

    connection.on("ViewerCountUpdated", (count: number) => {
      // update viewer badge
    });

    connection.on("AuctionStarted", ({ auctionId }) => {
      // enable bid input
    });

    connection.on("AuctionClosed", (data) => {
      // show winner or no-winner message
    });

    connection.on("AuctionEndingSoon", ({ minutesRemaining }) => {
      // show warning banner
    });

    connection.on("TimerTick", ({ secondsRemaining }) => {
      // sync countdown
    });

    connection.on("AuctionMessage", ({ message }) => {
      // show toast
    });

    connection.on("AuctionAborted", ({ reason }) => {
      // show abort alert
    });

    connection.on("AuctionUnverified", ({ reason }) => {
      // show unverified warning
    });

    connection.start().then(() => {
      connection.invoke("JoinAuction", String(auctionId));
    });

    connectionRef.current = connection;

    return () => {
      connection.invoke("LeaveAuction", String(auctionId)).finally(() => {
        connection.stop();
      });
    };
  }, [auctionId]);

  return connectionRef;
}
```

---

## Event Summary Table

| Event Name            | Trigger                                      | Payload Fields                                              |
|-----------------------|----------------------------------------------|-------------------------------------------------------------|
| `BidPlaced`           | Valid bid placed                             | `bidId`, `maskedBidder`, `amount`, `placedAt`, `newEndDate` |
| `ViewerCountUpdated`  | User joins or leaves room                    | `count` (number)                                            |
| `AuctionStarted`      | Scheduler starts auction                     | `auctionId`                                                 |
| `AuctionClosed`       | Auction ends (any reason)                    | `auctionId`, `winnerUserId?`, `finalPrice?`, `status?`     |
| `AuctionEndingSoon`   | < 5 minutes remaining                        | `auctionId`, `minutesRemaining`                             |
| `TimerTick`           | Every 30s or on auto-extend                  | `auctionId`, `secondsRemaining`                             |
| `AuctionMessage`      | Auto-extend or other info notifications      | `message`                                                   |
| `AuctionAborted`      | Product deleted while auction is live        | `auctionId`, `reason`                                       |
| `AuctionUnverified`   | Product un-verified while auction is live    | `auctionId`, `reason`                                       |

---

## Required Package

```bash
npm install @microsoft/signalr
```

---

## CORS Note

The Auction service is configured to **allow any origin** (`SetIsOriginAllowed(_ => true)`), so any frontend URL (localhost:3000, localhost:5173, production domain, etc.) can connect without configuration changes.

namespace Messaging.Contracts;

// ── User / Admin contracts ────────────────────────────────────────────────────

/// <summary>Published by UserService when a user requests admin registration.</summary>
public sealed record AdminRegistrationRequested(
    int RequestUserId,
    string Name,
    string Email);

// ── Product contracts ─────────────────────────────────────────────────────────

/// <summary>Published by ProductService when a new product is created and awaits verification.</summary>
public sealed record ProductCreatedForVerification(
    int ProductId,
    int SellerId,
    string ProductName, string description);

public sealed record ProductUpdateForVerification(int ProductId, string? name = null, string? descripiton = null);

/// <summary>Published by ProductService when a product is deleted by its owner or admin.</summary>
public sealed record ProductDeleted(
    int ProductId,
    int DeletedByUserId);

/// <summary>Published by VerifyService when a verifier is assigned to a product.</summary>
public sealed record ProductVerifyRequested(
    int ProductId,
    int VerifierId,
    string Description);

/// <summary>Published by VerifyService when an admin un-assigns the verifier from a product.</summary>
public sealed record ProductUnverifyRequested(
    int ProductId,
    int AdminId,
    string Description);

/// <summary>Published by VerifyService when a product passes verification.</summary>
public sealed record ProductVerified(int ProductId);

/// <summary>Published by VerifyService when a previously verified product is un-verified.</summary>
public sealed record ProductUnverified(
    int ProductId,
    int AdminId);

/// <summary>Published by VerifyService when product un-verification originates from the service layer (audit/internal).</summary>
public sealed record ProductUnverifiedFromService(int ProductId);

/// <summary>Published by CloudinaryService when an image is deleted.</summary>
public sealed record productDeleteImage(
    string PublicId);

// ── Auction contracts ─────────────────────────────────────────────────────────

/// <summary>Published by AuctionService when a new auction is created.</summary>
public sealed record AuctionCreated(
    int AuctionId,
    int ProductId,
    int CreatedByUserId,
    decimal StartingPrice,
    DateTime StartDate,
    DateTime EndDate);

/// <summary>Published by AuctionService (via scheduler) when StartDate is reached and the auction goes live.</summary>
public sealed record AuctionStarted(
    int AuctionId,
    int ProductId,
    DateTime EndDate);

/// <summary>Published by AuctionService every time a valid bid is placed.</summary>
public sealed record AuctionBidPlaced(
    int AuctionId,
    int BidId,
    int BidderId,
    decimal Amount,
    int? PreviousHighestBidderId,
    decimal? PreviousHighestAmount,
    DateTime PlacedAt);

/// <summary>Published by the scheduler 5 minutes before EndDate to notify interested parties.</summary>
public sealed record AuctionEndingSoon(
    int AuctionId,
    DateTime EndDate,
    int MinutesRemaining);

/// <summary>Published by AuctionService when EndDate passes and the auction is formally closed.</summary>
public sealed record AuctionClosed(
    int AuctionId,
    int? WinnerUserId,
    decimal? FinalPrice,
    bool ReserveMet,
    DateTime ClosedAt);

/// <summary>Published after AuctionClosed when there is a valid winner (reserve met, bids exist).</summary>
public sealed record AuctionWinnerDeclared(
    int AuctionId,
    int WinnerUserId,
    decimal FinalPrice,
    int ProductId);

/// <summary>Published when the auction owner cancels an upcoming (not-yet-live) auction.</summary>
public sealed record AuctionCancelled(
    int AuctionId,
    int ProductId,
    string Reason);

// this is used for the creating the auction based on the event calling 

public sealed record AuctionCreatedFromVerifyService(
     int ProductId,
     decimal StartingPrice,
     decimal? ReservePrice,
     decimal MinBidIncrement,
     DateTime StartDate,
     DateTime EndDate,
     int userId,
     int verifierId,
     string ProductName,
     string Description
);

// this below is used for the when the you are trying to do like when auction created in the product table u can show case this thing right 

public sealed record ProductAddAuctionDate(
    int productId,
    DateTime ?StartDate,
    DateTime ?EndDate
);
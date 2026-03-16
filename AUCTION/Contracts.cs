namespace Messaging.Contracts;

// ── Your existing contracts (unchanged) ──────────────────────────────────────

public sealed record AdminRegistrationRequested(
    int RequestUserId,
    string Name,
    string Email);

public sealed record ProductCreatedForVerification(
    int ProductId,
    int SellerId,
    string ProductName);

public sealed record ProductDeleted(
    int ProductId,
    int DeletedByUserId);

public sealed record ProductVerifyRequested(
    int ProductId,
    int VerifierId,
    string Description);

public sealed record ProductUnverifyRequested(
    int ProductId,
    int AdminId,
    string Description);

public sealed record ProductVerified(int ProductId);

public sealed record ProductUnverified(
    int ProductId,
    int AdminId);

public sealed record productDeleteImage(
    string PublicId);

// ── New auction contracts ─────────────────────────────────────────────────────


public sealed record ProductUnverifiedFromService(int productId);

/// <summary>Published by AuctionService when a new auction is created.</summary>
public sealed record AuctionCreated(
    int AuctionId,
    int ProductId,
    int CreatedByUserId,
    decimal StartingPrice,
    DateTime StartDate,
    DateTime EndDate);

/// <summary>Published by AuctionService (via scheduler) when start_date is reached.</summary>
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

/// <summary>Published by scheduler 5 minutes before EndDate.</summary>
public sealed record AuctionEndingSoon(
    int AuctionId,
    DateTime EndDate,
    int MinutesRemaining);

/// <summary>Published by AuctionService when EndDate passes and auction is closed.</summary>
public sealed record AuctionClosed(
    int AuctionId,
    int? WinnerUserId,
    decimal? FinalPrice,
    bool ReserveMet,
    DateTime ClosedAt);

/// <summary>Published after AuctionClosed when there is a valid winner.</summary>
public sealed record AuctionWinnerDeclared(
    int AuctionId,
    int WinnerUserId,
    decimal FinalPrice,
    int ProductId);

/// <summary>Published when owner cancels an upcoming auction.</summary>
public sealed record AuctionCancelled(
    int AuctionId,
    int ProductId,
    string Reason);

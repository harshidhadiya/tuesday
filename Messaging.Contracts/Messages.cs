namespace Messaging.Contracts;

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
    String publicId
);


//  New auction contracts =

public sealed record ProductUnverifiedFromService(int productId);

public sealed record AuctionCreated(
    int AuctionId,
    int ProductId,
    int CreatedByUserId,
    decimal StartingPrice,
    DateTime StartDate,
    DateTime EndDate);

public sealed record AuctionStarted(
    int AuctionId,
    int ProductId,
    DateTime EndDate);

public sealed record AuctionBidPlaced(
    int AuctionId,
    int BidId,
    int BidderId,
    decimal Amount,
    int? PreviousHighestBidderId,
    decimal? PreviousHighestAmount,
    DateTime PlacedAt);

public sealed record AuctionEndingSoon(
    int AuctionId,
    DateTime EndDate,
    int MinutesRemaining);

public sealed record AuctionClosed(
    int AuctionId,
    int? WinnerUserId,
    decimal? FinalPrice,
    bool ReserveMet,
    DateTime ClosedAt);

public sealed record AuctionWinnerDeclared(
    int AuctionId,
    int WinnerUserId,
    decimal FinalPrice,
    int ProductId);

public sealed record AuctionCancelled(
    int AuctionId,
    int ProductId,
    string Reason);


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


namespace MooreHotels.Application.DTOs;

public record OperationLogEntryDto(
    Guid Id,
    DateTime Timestamp,
    string OccupantName,
    string OccupantEmail,
    string Action, // RESERVATION, CHECKED IN, CHECKED OUT, VOIDED, PAYMENT
    string AssetNumber,
    string AssetCategory,
    string VerificationInfo, // Transaction Ref or Authorized User
    string StatusColor); // To help UI rendering (emerald, amber, rose, blue)
namespace MooreHotels.Domain.Enums;

public enum RoomCategory
{
    Standard,
    Business,
    Executive,
    Suite
}

public enum PropertyFloor
{
    GroundFloor,
    FirstFloor,
    SecondFloor,
    ThirdFloor,
    Penthouse
}

public enum RoomStatus
{
    Available,
    Occupied,
    Cleaning,
    Maintenance,
    Reserved
}

public enum BookingStatus
{
    Pending,
    Confirmed,
    CheckedIn,
    CheckedOut,
    Cancelled
}

public enum PaymentStatus
{
    Paid,
    Unpaid,
    AwaitingVerification,
    RefundPending,
    Refunded 
}

public enum PaymentMethod
{
    Paystack,
    DirectTransfer
}

public enum UserRole
{
    Admin,
    Manager,
    Staff,
    Client
}

public enum ProfileStatus
{
    Active,
    Suspended
}
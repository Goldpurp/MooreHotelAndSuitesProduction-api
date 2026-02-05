using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using System.Security.Claims;
using System.Text.Json;

namespace MooreHotels.Application.Services;

public class BookingService : IBookingService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IRoomRepository _roomRepo;
    private readonly IGuestRepository _guestRepo;
    private readonly IAuditLogRepository _auditRepo;
    private readonly IEmailService _emailService;
    private readonly IPaymentService _paymentService;
    private readonly IVisitRecordService _visitService;
    private readonly INotificationService _notificationService;
    private readonly UserManager<ApplicationUser> _userManager;

    private const int CHECK_IN_HOUR = 15; // 3:00 PM
    private const int CHECK_OUT_HOUR = 11; // 11:00 AM

    public BookingService(
        IBookingRepository bookingRepo, 
        IRoomRepository roomRepo, 
        IGuestRepository guestRepo, 
        IAuditLogRepository auditRepo,
        IEmailService emailService,
        IPaymentService paymentService,
        IVisitRecordService visitService,
        INotificationService notificationService,
        UserManager<ApplicationUser> userManager)
    {
        _bookingRepo = bookingRepo;
        _roomRepo = roomRepo;
        _guestRepo = guestRepo;
        _auditRepo = auditRepo;
        _emailService = emailService;
        _paymentService = paymentService;
        _visitService = visitService;
        _notificationService = notificationService;
        _userManager = userManager;
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingRequest request)
    {
        // 1. Rigorous Data Validation
        if (string.IsNullOrWhiteSpace(request.GuestEmail)) throw new Exception("Guest email is required.");
        if (string.IsNullOrWhiteSpace(request.GuestFirstName)) throw new Exception("Guest first name is required.");
        if (string.IsNullOrWhiteSpace(request.GuestLastName)) throw new Exception("Guest last name is required.");
        if (string.IsNullOrWhiteSpace(request.GuestPhone)) throw new Exception("Guest phone number is required.");
        
        var room = await _roomRepo.GetByIdAsync(request.RoomId);
        if (room == null) throw new Exception("Room not found in registry.");
        
        // Ensure dates are treated as UTC for PostgreSQL compatibility
        var checkIn = DateTime.SpecifyKind(request.CheckIn.Date.AddHours(CHECK_IN_HOUR), DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(request.CheckOut.Date.AddHours(CHECK_OUT_HOUR), DateTimeKind.Utc);

        if (checkOut <= checkIn)
            throw new Exception("Check-out must be after check-in date.");

        // New Rule: Do not block booking based on current operational status (Cleaning, etc).
        // Only block if the asset is strictly offline/Maintenance (IsOnline = false).
        if (!room.IsOnline)
            throw new Exception($"Room {room.RoomNumber} is currently offline and unavailable for booking.");

        if (await _bookingRepo.IsRoomBookedAsync(room.Id, checkIn, checkOut))
            throw new Exception("This room is already reserved for the selected dates.");

        // 2. Guest Record Resolution
        var guest = await _guestRepo.GetByEmailAsync(request.GuestEmail.Trim());
        if (guest == null)
        {
            guest = new Guest
            {
                Id = $"GS-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                Email = request.GuestEmail.Trim().ToLower(),
                FirstName = request.GuestFirstName.Trim(),
                LastName = request.GuestLastName.Trim(),
                Phone = request.GuestPhone.Trim()
            };
            await _guestRepo.AddAsync(guest);
        }

        var nights = Math.Max(1, (checkOut.Date - checkIn.Date).Days);
        var totalAmount = room.PricePerNight * nights;

        // 3. Booking Finalization
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingCode = $"MHS-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            RoomId = room.Id,
            GuestId = guest.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = BookingStatus.Pending,
            Amount = totalAmount,
            PaymentStatus = request.PaymentMethod == PaymentMethod.DirectTransfer ? PaymentStatus.AwaitingVerification : PaymentStatus.Unpaid,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes,
            StatusHistoryJson = JsonSerializer.Serialize(new List<object> { new { Status = BookingStatus.Pending, Timestamp = DateTime.UtcNow, Note = "Booking initialized." } }),
            CreatedAt = DateTime.UtcNow
        };

        await _bookingRepo.AddAsync(booking);
        booking.Guest = guest;
        
        string? paymentUrl = (request.PaymentMethod == PaymentMethod.Paystack) ? _paymentService.GeneratePaystackLink(booking.BookingCode, totalAmount, guest.Email) : null;
        string? paymentInstruction = (request.PaymentMethod == PaymentMethod.DirectTransfer) ? _paymentService.GetTransferInstructions() : null;

        _ = _notificationService.NotifyNewBookingAsync(booking, $"{guest.FirstName} {guest.LastName}", room.Name);
        _ = _emailService.SendBookingConfirmationAsync(guest.Email, $"{guest.FirstName} {guest.LastName}", booking.BookingCode, room.Name, booking.CheckIn);

        return MapToDto(booking) with { PaymentUrl = paymentUrl, PaymentInstruction = paymentInstruction };
    }

    public async Task<BookingDto?> GetBookingByCodeAsync(string code)
    {
        var b = await _bookingRepo.GetByCodeAsync(code);
        return b != null ? MapToDto(b) : null;
    }

    public async Task<BookingDto?> GetBookingByCodeAndEmailAsync(string code, string email)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(email)) return null;

        var b = await _bookingRepo.GetByCodeAsync(code.Trim().ToUpper());
        if (b != null && b.Guest?.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase) == true)
        {
            return MapToDto(b);
        }
        return null;
    }

    public async Task<IEnumerable<BookingDto>> GetAllBookingsAsync()
    {
        var bookings = await _bookingRepo.GetAllAsync();
        return bookings.Select(MapToDto);
    }

    public async Task<BookingDto> UpdateStatusAsync(Guid bookingId, BookingStatus status, Guid userId)
    {
        var booking = await _bookingRepo.GetByIdAsync(bookingId);
        if (booking == null) throw new Exception("Booking not found.");

        var actingUser = await _userManager.FindByIdAsync(userId.ToString());
        if (actingUser == null) throw new Exception("User not found.");

        var room = await _roomRepo.GetByIdAsync(booking.RoomId);
        var oldStatus = booking.Status;
        
        if (status == BookingStatus.CheckedIn)
        {
            if (DateTime.UtcNow.Date < booking.CheckIn.Date)
                throw new Exception($"Early check-in not allowed. Reserved start: {booking.CheckIn:MMM dd, yyyy}.");
            
            if (booking.PaymentStatus != PaymentStatus.Paid)
                throw new Exception("Full payment verification required before Check-In.");

            if (room != null) { room.Status = RoomStatus.Occupied; await _roomRepo.UpdateAsync(room); }
            await _visitService.CreateRecordAsync(booking.BookingCode, "CHECK_IN", actingUser.Name);
        }
        else if (status == BookingStatus.CheckedOut)
        {
            if (room != null) { room.Status = RoomStatus.Cleaning; await _roomRepo.UpdateAsync(room); }
            await _visitService.CreateRecordAsync(booking.BookingCode, "CHECK_OUT", actingUser.Name);
        }

        var history = string.IsNullOrEmpty(booking.StatusHistoryJson) 
            ? new List<object>() 
            : JsonSerializer.Deserialize<List<object>>(booking.StatusHistoryJson) ?? new List<object>();
        
        history.Add(new { Status = status, Timestamp = DateTime.UtcNow, Actor = actingUser.Name });
        booking.StatusHistoryJson = JsonSerializer.Serialize(history);
        booking.Status = status;
        
        await _bookingRepo.UpdateAsync(booking);

        await _auditRepo.AddAsync(new AuditLog {
            Id = Guid.NewGuid(), ProfileId = userId, Action = "LIFECYCLE_TRANSITION",
            EntityType = "Booking", EntityId = booking.Id.ToString(),
            OldDataJson = JsonSerializer.Serialize(new { Status = oldStatus }),
            NewDataJson = JsonSerializer.Serialize(new { Status = status })
        });

        return MapToDto(booking);
    }

    public async Task<BookingDto> ProcessPaymentSuccessAsync(string bookingCode, string reference, Guid? actingUserId = null)
    {
        var booking = await _bookingRepo.GetByCodeAsync(bookingCode);
        if (booking == null) throw new Exception("Booking not found.");

        if (booking.PaymentStatus == PaymentStatus.Paid) return MapToDto(booking);

        booking.PaymentStatus = PaymentStatus.Paid;
        booking.TransactionReference = reference;
        booking.Status = BookingStatus.Confirmed; 

        await _bookingRepo.UpdateAsync(booking);
        return MapToDto(booking);
    }

    private static BookingDto MapToDto(Booking b) => new(
        b.Id, b.BookingCode, b.RoomId, b.GuestId, 
        b.Guest?.FirstName ?? "", b.Guest?.LastName ?? "", b.Guest?.Email ?? "", b.Guest?.Phone ?? "",
        b.CheckIn, b.CheckOut,
        b.Status, b.Amount, b.PaymentStatus, b.PaymentMethod, b.TransactionReference, b.Notes, b.CreatedAt);
}
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
        var room = await _roomRepo.GetByIdAsync(request.RoomId);
        if (room == null) throw new Exception("Target asset not found in registry.");
        
        var checkIn = request.CheckIn.Date.AddHours(CHECK_IN_HOUR);
        var checkOut = request.CheckOut.Date.AddHours(CHECK_OUT_HOUR);

        if (checkOut <= checkIn)
            throw new Exception("Policy Violation: Check-out must be at least one day after check-in.");

        if (checkIn.Date == DateTime.UtcNow.Date)
        {
            if (room.Status == RoomStatus.Occupied || room.Status == RoomStatus.Maintenance || room.Status == RoomStatus.Cleaning)
                throw new Exception($"Operational Constraint: Asset unit {room.RoomNumber} is currently {room.Status.ToString().ToLower()}.");
        }

        if (await _bookingRepo.IsRoomBookedAsync(room.Id, checkIn, checkOut))
            throw new Exception("Calendar Conflict: This unit is already secured for the selected period.");

        // Guest logic: Automatically handle users without accounts
        var guest = await _guestRepo.GetByEmailAsync(request.GuestEmail);
        if (guest == null)
        {
            guest = new Guest
            {
                Id = $"GS-{new Random().Next(1000, 9999)}",
                Email = request.GuestEmail,
                FirstName = request.GuestFirstName,
                LastName = request.GuestLastName,
                Phone = request.GuestPhone
            };
            await _guestRepo.AddAsync(guest);
        }

        var nights = Math.Max(1, (checkOut.Date - checkIn.Date).Days);
        var totalAmount = room.PricePerNight * nights;

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            BookingCode = $"MHS-{new Random().Next(100000, 999999)}",
            RoomId = room.Id,
            GuestId = guest.Id,
            CheckIn = checkIn,
            CheckOut = checkOut,
            Status = BookingStatus.Pending,
            Amount = totalAmount,
            PaymentStatus = request.PaymentMethod == PaymentMethod.DirectTransfer ? PaymentStatus.AwaitingVerification : PaymentStatus.Unpaid,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes,
            StatusHistoryJson = JsonSerializer.Serialize(new List<object> { new { Status = BookingStatus.Pending, Timestamp = DateTime.UtcNow, Note = "Initial booking created via public portal." } }),
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
        var b = await _bookingRepo.GetByCodeAsync(code);
        // Security check: Only return if the email matches the guest on the booking
        if (b != null && b.Guest?.Email.Equals(email, StringComparison.OrdinalIgnoreCase) == true)
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
        if (booking == null) throw new Exception("Folio record not found.");

        var actingUser = await _userManager.FindByIdAsync(userId.ToString());
        if (actingUser == null) throw new Exception("Authorization failure: Acting user not found.");

        var room = await _roomRepo.GetByIdAsync(booking.RoomId);
        var oldStatus = booking.Status;
        
        if (status == BookingStatus.CheckedIn)
        {
            if (DateTime.UtcNow.Date < booking.CheckIn.Date)
                throw new Exception($"Policy Violation: Early check-in prohibited before {booking.CheckIn:MMM dd, yyyy}.");
            
            if (booking.PaymentStatus != PaymentStatus.Paid)
                throw new Exception("Policy Violation: Full payment required before key handover.");

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
        if (booking == null) throw new Exception("Booking record not found.");

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
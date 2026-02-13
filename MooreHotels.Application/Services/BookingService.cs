using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private readonly IConfiguration _config;
    private readonly ILogger<BookingService> _logger; // FIX: Added logging for background tasks

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
        UserManager<ApplicationUser> userManager,
        IConfiguration config,
        ILogger<BookingService> logger)
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
        _config = config;
        _logger = logger;
    }

    public async Task<BookingDto> CreateBookingAsync(CreateBookingRequest request)
    {
        // 1. Validation Logic
        if (string.IsNullOrWhiteSpace(request.GuestEmail)) throw new Exception("Guest email is required.");
        if (string.IsNullOrWhiteSpace(request.GuestFirstName)) throw new Exception("Guest first name is required.");
        if (string.IsNullOrWhiteSpace(request.GuestLastName)) throw new Exception("Guest last name is required.");
        if (string.IsNullOrWhiteSpace(request.GuestPhone)) throw new Exception("Guest phone number is required.");
        
        var room = await _roomRepo.GetByIdAsync(request.RoomId);
        if (room == null) throw new Exception("Room not found in registry.");
        
        var checkIn = DateTime.SpecifyKind(request.CheckIn.Date.AddHours(CHECK_IN_HOUR), DateTimeKind.Utc);
        var checkOut = DateTime.SpecifyKind(request.CheckOut.Date.AddHours(CHECK_OUT_HOUR), DateTimeKind.Utc);

        if (checkOut <= checkIn) throw new Exception("Check-out must be after check-in date.");
        if (!room.IsOnline) throw new Exception($"Room {room.RoomNumber} is currently offline.");
        
        // 2. Conflict Check
        if (await _bookingRepo.IsRoomBookedAsync(room.Id, checkIn, checkOut)) 
            throw new Exception("This room is already reserved for the selected dates.");

        // 3. Guest Management
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

        // 4. Persistence
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
            StatusHistoryJson = "[]", // FIX: Initialised as empty JSON array to prevent Deserialization errors
            CreatedAt = DateTime.UtcNow
        };

        await _bookingRepo.AddAsync(booking);
        booking.Guest = guest;
        
        // 5. Critical Communication (Awaited for reliability)
        try 
        {
            // FIX: Awaited this call so the user knows immediately if the confirmation failed to dispatch
            await _emailService.SendBookingConfirmationAsync(guest.Email, $"{guest.FirstName} {guest.LastName}", booking.BookingCode, room.Name, booking.CheckIn);
        }
        catch (Exception ex) 
        {
            _logger.LogError(ex, "Initial booking confirmation failed for {Code}", booking.BookingCode);
        }

        // 6. Notifications & Admin Alerts (Safe to Fire-and-Forget)
        _ = _notificationService.NotifyNewBookingAsync(booking, $"{guest.FirstName} {guest.LastName}", room.Name);
        
        var adminEmail = _config["EmailSettings:AdminNotificationEmail"] ?? _config["EmailSettings:SenderEmail"];
        if (!string.IsNullOrEmpty(adminEmail))
            _ = _emailService.SendAdminNewBookingAlertAsync(adminEmail, $"{guest.FirstName} {guest.LastName}", booking.BookingCode, room.Name, totalAmount);

        string? paymentUrl = (request.PaymentMethod == PaymentMethod.Paystack) ? _paymentService.GeneratePaystackLink(booking.BookingCode, totalAmount, guest.Email) : null;
        string? paymentInstruction = (request.PaymentMethod == PaymentMethod.DirectTransfer) ? _paymentService.GetTransferInstructions() : null;

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
            return MapToDto(b);
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

        // FIX: Ensure Guest navigation property is loaded to prevent NullRef in Emails
        if (booking.Guest == null) throw new Exception("Critical Error: Guest data not loaded. Check Repository Includes.");

        var actingUser = await _userManager.FindByIdAsync(userId.ToString());
        var room = await _roomRepo.GetByIdAsync(booking.RoomId);
        var oldStatus = booking.Status;
        
        if (status == BookingStatus.CheckedIn)
        {
            if (DateTime.UtcNow.Date < booking.CheckIn.Date) throw new Exception("Arrival too early. Official check-in starts at 3:00 PM.");
             if (DateTime.UtcNow > booking.CheckOut) throw new Exception("Access Denied: This booking expired at 11:00 AM today.");
            if (booking.PaymentStatus != PaymentStatus.Paid) throw new Exception("Full payment verification required.");
            if (room != null) { room.Status = RoomStatus.Occupied; await _roomRepo.UpdateAsync(room); }
            await _visitService.CreateRecordAsync(booking.BookingCode, "CHECK_IN", actingUser?.Name ?? "Admin");
        }
        else if (status == BookingStatus.CheckedOut)
        {
            if (room != null) { room.Status = RoomStatus.Cleaning; await _roomRepo.UpdateAsync(room); }
            await _visitService.CreateRecordAsync(booking.BookingCode, "CHECK_OUT", actingUser?.Name ?? "Admin");
            
            // FIX: Wrapped Fire-and-Forget in Task.Run with Try-Catch for background thread safety
            _ = Task.Run(async () => {
                try {
                    await _emailService.SendCheckOutThankYouAsync(booking.Guest.Email, booking.Guest.FirstName, booking.BookingCode);
                } catch (Exception ex) {
                    _logger.LogWarning(ex, "Background Checkout Email failed for {Code}", booking.BookingCode);
                }
            });
        }

        // FIX: Robust JSON handling logic
        var rawHistory = string.IsNullOrWhiteSpace(booking.StatusHistoryJson) ? "[]" : booking.StatusHistoryJson;
        var history = JsonSerializer.Deserialize<List<object>>(rawHistory) ?? new List<object>();
        
        history.Add(new { Status = status, Timestamp = DateTime.UtcNow, Actor = actingUser?.Name ?? "System" });
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

        // Send payment confirmation email
        if (booking.Guest != null)
        {
            _ = _emailService.SendPaymentSuccessAsync(booking.Guest.Email, booking.Guest.FirstName, booking.BookingCode, booking.Amount, reference);
        }

        return MapToDto(booking);
    }
    
public async Task<BookingDto> CancelBookingAsync(Guid bookingId, Guid userId, string? reason = null)
{
    var booking = await _bookingRepo.GetByIdAsync(bookingId);
    if (booking == null) throw new KeyNotFoundException("Booking record not found.");

    if (booking.Status == BookingStatus.Cancelled) return MapToDto(booking);

    if (booking.Status == BookingStatus.CheckedIn || booking.Status == BookingStatus.CheckedOut)
        throw new InvalidOperationException("Active or completed stays cannot be cancelled.");

    var actingUser = await _userManager.FindByIdAsync(userId.ToString());
    var oldStatus = booking.Status;
    var room = await _roomRepo.GetByIdAsync(booking.RoomId);

    if (room != null)
    {
        room.Status = RoomStatus.Available;
        await _roomRepo.UpdateAsync(room);
    }

    booking.Status = BookingStatus.Cancelled;

    // --- TRIGGER REFUND LOGIC ---
    if (booking.PaymentStatus == PaymentStatus.Paid)
    {
        booking.PaymentStatus = PaymentStatus.RefundPending;
    }

    var history = JsonSerializer.Deserialize<List<object>>(booking.StatusHistoryJson ?? "[]") ?? new();
    history.Add(new { 
        Status = BookingStatus.Cancelled, 
        Timestamp = DateTime.UtcNow, 
        Actor = actingUser?.UserName ?? "Staff", 
        Reason = reason ?? "Cancelled by Admin",
        PaymentShift = booking.PaymentStatus.ToString() // Will show 'RefundPending' if it was 'Paid'
    });
    booking.StatusHistoryJson = JsonSerializer.Serialize(history);

    await _bookingRepo.UpdateAsync(booking);

       // 5. Background Notification
    _ = Task.Run(async () => {
        try {
            await _emailService.SendCancellationNoticeAsync(
                booking.Guest!.Email, 
                $"{booking.Guest.FirstName} {booking.Guest.LastName}", 
                booking.BookingCode, 
                room?.Name ?? "Reserved Room", 
                booking.CheckIn, 
                reason);
        } catch (Exception ex) {
            _logger.LogError(ex, "Staff cancellation email failed for {Code}", booking.BookingCode);
        }
    });

     if (booking.PaymentStatus == PaymentStatus.RefundPending)
    {
        var adminEmail = _config["EmailSettings:SenderEmail"];
        _ = Task.Run(() => _emailService.SendAdminRefundAlertAsync(
            adminEmail, 
            $"{booking.Guest?.FirstName} {booking.Guest?.LastName}", 
            booking.BookingCode, 
            room?.Name ?? "Reserved Room", 
            booking.Amount
        ));
    }
    return MapToDto(booking);
}

public async Task<BookingDto> CancelBookingByGuestAsync(string bookingCode, string email, string? reason = null)
{
    var booking = await _bookingRepo.GetByCodeAsync(bookingCode.Trim().ToUpper());
    
    if (booking == null || !booking.Guest!.Email.Equals(email.Trim(), StringComparison.OrdinalIgnoreCase))
        throw new UnauthorizedAccessException("Verification failed.");

    if (booking.Status == BookingStatus.Cancelled) return MapToDto(booking);

    if (booking.Status == BookingStatus.CheckedIn || booking.Status == BookingStatus.CheckedOut)
        throw new InvalidOperationException("Stay in progress.");

    var room = await _roomRepo.GetByIdAsync(booking.RoomId);
    if (room != null)
    {
        room.Status = RoomStatus.Available;
        await _roomRepo.UpdateAsync(room);
    }

    booking.Status = BookingStatus.Cancelled;

    // --- TRIGGER REFUND LOGIC ---
    if (booking.PaymentStatus == PaymentStatus.Paid)
    {
        booking.PaymentStatus = PaymentStatus.RefundPending;
    }

    var history = JsonSerializer.Deserialize<List<object>>(booking.StatusHistoryJson ?? "[]") ?? new();
    history.Add(new { 
        Status = BookingStatus.Cancelled, 
        Timestamp = DateTime.UtcNow, 
        Actor = "Guest", 
        Reason = reason ?? "Self-service cancellation",
        PaymentShift = booking.PaymentStatus.ToString()
    });
    booking.StatusHistoryJson = JsonSerializer.Serialize(history);

    await _bookingRepo.UpdateAsync(booking);

    _ = Task.Run(async () => {
        try {
            await _emailService.SendCancellationNoticeAsync(
                booking.Guest.Email, 
                $"{booking.Guest.FirstName} {booking.Guest.LastName}", 
                booking.BookingCode, 
                room?.Name ?? "Reserved Room", 
                booking.CheckIn, 
                reason);
        } catch (Exception ex) {
            _logger.LogError(ex, "Guest cancellation email failed for {Code}", booking.BookingCode);
        }
    });

      if (booking.PaymentStatus == PaymentStatus.RefundPending)
    {
        var adminEmail = _config["EmailSettings:SenderEmail"];
        _ = Task.Run(() => _emailService.SendAdminRefundAlertAsync(
            adminEmail, 
            $"{booking.Guest!.FirstName} {booking.Guest.LastName}", 
            booking.BookingCode, 
            room?.Name ?? "Reserved Room", 
            booking.Amount
        ));
    }

    return MapToDto(booking);
}

public async Task<BookingDto> CompleteRefundAsync(Guid bookingId, string transactionRef, Guid adminId)
{
    var booking = await _bookingRepo.GetByIdAsync(bookingId);
    if (booking == null) throw new Exception("Booking not found.");
    
    if (booking.PaymentStatus != PaymentStatus.RefundPending)
        throw new Exception("This booking is not flagged for a manual refund.");

    // Update the state
    booking.PaymentStatus = PaymentStatus.Refunded;
    booking.TransactionReference = transactionRef; // Store the Bank/Paystack Refund ID

    // Add to history for tracking
    var history = JsonSerializer.Deserialize<List<object>>(booking.StatusHistoryJson ?? "[]") ?? new();
    history.Add(new { 
        Action = "MANUAL_REFUND_COMPLETED", 
        Timestamp = DateTime.UtcNow, 
        Reference = transactionRef,
        AdminId = adminId 
    });
    booking.StatusHistoryJson = JsonSerializer.Serialize(history);

    await _bookingRepo.UpdateAsync(booking);
        _ = Task.Run(async () => {
        try {
            await _emailService.SendRefundCompletionNoticeAsync(
                booking.Guest!.Email, 
                booking.Guest.FirstName, 
                booking.BookingCode, 
                booking.Amount, 
                transactionRef);
        } catch (Exception ex) {
            _logger.LogError(ex, "Failed to send refund completion email for {Code}", booking.BookingCode);
        }
    });
    
    return MapToDto(booking);
}

public async Task<IEnumerable<BookingDto>> GetPendingRefundsAsync()
{
    var bookings = await _bookingRepo.GetPendingRefundsAsync();
    return bookings.Select(MapToDto);
}


    private static BookingDto MapToDto(Booking b) => new(
        b.Id, b.BookingCode, b.RoomId, b.GuestId, 
        b.Guest?.FirstName ?? "", b.Guest?.LastName ?? "", b.Guest?.Email ?? "", b.Guest?.Phone ?? "",
        b.CheckIn, b.CheckOut,
        b.Status, b.Amount, b.PaymentStatus, b.PaymentMethod, b.TransactionReference, b.Notes, b.CreatedAt);
}
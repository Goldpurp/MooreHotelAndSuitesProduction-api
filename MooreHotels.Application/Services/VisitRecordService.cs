
using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Entities;

namespace MooreHotels.Application.Services;

public class VisitRecordService : IVisitRecordService
{
    private readonly IVisitRecordRepository _visitRepo;
    private readonly IBookingRepository _bookingRepo;

    public VisitRecordService(IVisitRecordRepository visitRepo, IBookingRepository bookingRepo)
    {
        _visitRepo = visitRepo;
        _bookingRepo = bookingRepo;
    }

    public async Task<IEnumerable<VisitRecordDto>> GetAllRecordsAsync()
    {
        var records = await _visitRepo.GetAllAsync();
        return records.Select(v => new VisitRecordDto(
            v.Id, v.GuestId, v.GuestName, v.RoomNumber, v.BookingCode, 
            v.Action, v.Timestamp, v.AuthorizedBy));
    }

    public async Task CreateRecordAsync(string bookingCode, string action, string authorizedBy)
    {
        var booking = await _bookingRepo.GetByCodeAsync(bookingCode);
        if (booking == null) throw new Exception("Invalid booking code");

        var record = new VisitRecord
        {
            Id = Guid.NewGuid(),
            BookingCode = bookingCode,
            GuestId = booking.GuestId,
            GuestName = $"{booking.Guest?.FirstName} {booking.Guest?.LastName}",
            RoomId = booking.RoomId,
            RoomNumber = booking.Room?.RoomNumber ?? "N/A",
            Action = action,
            Timestamp = DateTime.UtcNow,
            AuthorizedBy = authorizedBy
        };

        await _visitRepo.AddAsync(record);
    }
}

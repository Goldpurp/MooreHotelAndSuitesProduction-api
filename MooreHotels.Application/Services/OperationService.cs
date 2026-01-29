using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Services;

public class OperationService : IOperationService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IVisitRecordRepository _visitRepo;

    public OperationService(IBookingRepository bookingRepo, IVisitRecordRepository visitRepo)
    {
        _bookingRepo = bookingRepo;
        _visitRepo = visitRepo;
    }

    public async Task<IEnumerable<OperationLogEntryDto>> GetLedgerAsync(string? filter = null, string? search = null)
    {
        var bookings = await _bookingRepo.GetAllAsync();
        var visits = await _visitRepo.GetAllAsync();
        
        var ledger = new List<OperationLogEntryDto>();

        // 1. Map Booking States (Reservations & Cancellations)
        foreach (var b in bookings)
        {
            // Initial Reservation
            ledger.Add(new OperationLogEntryDto(
                Guid.NewGuid(), b.CreatedAt, $"{b.Guest?.FirstName} {b.Guest?.LastName}", b.Guest?.Email ?? "",
                "RESERVATION", b.Room?.RoomNumber ?? "N/A", b.Room?.Category.ToString() ?? "",
                b.BookingCode, "blue"));

            // If Cancelled
            if (b.Status == BookingStatus.Cancelled)
            {
                ledger.Add(new OperationLogEntryDto(
                    Guid.NewGuid(), DateTime.UtcNow, $"{b.Guest?.FirstName} {b.Guest?.LastName}", b.Guest?.Email ?? "",
                    "VOIDED", b.Room?.RoomNumber ?? "N/A", b.Room?.Category.ToString() ?? "",
                    "Admin Override", "rose"));
            }
        }

        // 2. Map Visit Records (Check-in / Check-out)
        foreach (var v in visits)
        {
            ledger.Add(new OperationLogEntryDto(
                v.Id, v.Timestamp, v.GuestName, "",
                v.Action.Replace("_", " "), v.RoomNumber, "",
                v.AuthorizedBy, v.Action == "CHECK_IN" ? "emerald" : "amber"));
        }

        // 3. Apply Filters & Search
        var query = ledger.AsQueryable();

        if (!string.IsNullOrEmpty(filter) && filter != "SYSTEM FULL")
        {
            query = query.Where(l => l.Action.Equals(filter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrEmpty(search))
        {
            var s = search.ToLower();
            query = query.Where(l => l.OccupantName.ToLower().Contains(s) || 
                                    l.AssetNumber.ToLower().Contains(s) || 
                                    l.VerificationInfo.ToLower().Contains(s));
        }

        return query.OrderByDescending(l => l.Timestamp);
    }

    public async Task<DashboardKpis> GetOperationalKpisAsync()
    {
        var now = DateTime.UtcNow.Date;
        var bookings = await _bookingRepo.GetAllAsync();
        var visits = await _visitRepo.GetAllAsync();

        return new DashboardKpis(
            NetRevenue: bookings.Where(b => b.PaymentStatus == PaymentStatus.Paid).Sum(b => b.Amount),
            OccupancyRate: 0, // Calculated in Analytics
            ActiveGuests: visits.Count(v => v.Timestamp.Date == now && v.Action == "CHECK_IN"),
            AvgNightlyRate: 0,
            RevenueGrowthPercentage: 0,
            OccupancyGrowthPercentage: 0
        );
    }
}
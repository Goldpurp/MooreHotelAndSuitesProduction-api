using MooreHotels.Application.DTOs;
using MooreHotels.Application.Interfaces.Repositories;
using MooreHotels.Application.Interfaces.Services;
using MooreHotels.Domain.Enums;

namespace MooreHotels.Application.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IBookingRepository _bookingRepo;
    private readonly IRoomRepository _roomRepo;

    public AnalyticsService(IBookingRepository bookingRepo, IRoomRepository roomRepo)
    {
        _bookingRepo = bookingRepo;
        _roomRepo = roomRepo;
    }

    public async Task<DashboardOverviewDto> GetOverviewAsync()
    {
        var now = DateTime.UtcNow;
        var bookings = (await _bookingRepo.GetAllAsync()).ToList();
        var rooms = (await _roomRepo.GetAllAsync(onlyOnline: false)).ToList();

        var netRevenue = bookings
            .Where(b => b.Status != BookingStatus.Cancelled && b.PaymentStatus == PaymentStatus.Paid)
            .Sum(b => b.Amount);

        var totalRooms = rooms.Count;
        var occupiedRooms = rooms.Count(r => r.Status == RoomStatus.Occupied);
        var occupancyRate = totalRooms > 0 ? (double)occupiedRooms / totalRooms * 100 : 0;

        var activeGuests = bookings.Count(b => b.Status == BookingStatus.CheckedIn);
        
        var validBookings = bookings.Where(b => b.Status != BookingStatus.Cancelled).ToList();
        var avgNightly = validBookings.Any() ? validBookings.Average(b => b.Amount) : 0;

        var kpis = new DashboardKpis(
            NetRevenue: netRevenue,
            OccupancyRate: occupancyRate,
            ActiveGuests: activeGuests,
            AvgNightlyRate: avgNightly,
            RevenueGrowthPercentage: 12.5m,
            OccupancyGrowthPercentage: 5.2
        );

        var revenueDynamics = Enumerable.Range(0, 7)
            .Select(i => now.AddDays(-i).Date)
            .Reverse()
            .Select(date => new RevenuePoint(
                date.ToString("MMM dd"),
                bookings.Where(b => b.CreatedAt.Date == date && b.Status != BookingStatus.Cancelled).Sum(b => b.Amount)
            ))
            .ToList();

        var assetStatus = new AssetStatusDistribution(
            Occupied: occupiedRooms,
            Available: rooms.Count(r => r.Status == RoomStatus.Available),
            Cleaning: rooms.Count(r => r.Status == RoomStatus.Cleaning),
            Maintenance: rooms.Count(r => r.Status == RoomStatus.Maintenance)
        );

        var activeOps = bookings
            .Where(b => b.Status == BookingStatus.CheckedIn)
            .OrderByDescending(b => b.CreatedAt)
            .Take(5)
            .Select(b => new ActiveOperationDto(
                $"{b.Guest?.FirstName} {b.Guest?.LastName}",
                b.Guest?.AvatarUrl ?? "",
                b.BookingCode,
                b.Room?.Category.ToString() ?? "Unknown",
                b.Room?.RoomNumber ?? "N/A",
                "CHECKED IN",
                b.Amount,
                b.PaymentStatus.ToString()
            ))
            .ToList();

        return new DashboardOverviewDto(kpis, revenueDynamics, assetStatus, activeOps);
    }
}
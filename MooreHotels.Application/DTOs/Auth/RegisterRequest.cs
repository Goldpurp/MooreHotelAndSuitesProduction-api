namespace MooreHotels.Application.DTOs;

public record RegisterRequest(
    string FirstName, 
    string LastName, 
    string Email, 
    string Password,
    string Phone);
## Moore Hotels API Documentation

This document lists the available HTTP APIs exposed by the Moore Hotels backend, including routes, methods, authentication requirements, and request/response body structures inferred from the current codebase.

---

## Conventions

- **Base URL**: `/api`
- **Auth**:
  - `Authorize` attribute on a controller or action means JWT bearer auth is required.
  - `AllowAnonymous` overrides `Authorize` on specific endpoints.
- **Response wrapper**:
  - Many endpoints return either DTOs directly (e.g. `RoomDto`, `BookingDto`) or anonymous objects like `{ Message, Data }`.
  - For anonymous objects, shapes below are indicative.

---

## Auth (`/api/Auth`)

**Controller**: `AuthController` (`[ApiController]`, `[Route("api/[controller]")]`)

### POST `/api/Auth/login`

- **Auth**: None
- **Description**: Authenticate a user and return a JWT token.
- **Request body** (`LoginRequest`):
  - `email` (string)
  - `password` (string)
- **Responses**:
  - `200 OK` → `AuthResponse`:
    - `token` (string)
    - `email` (string)
    - `name` (string)
    - `role` (string; one of `Admin`, `Manager`, `Staff`, `Client`, etc.)
  - `401 Unauthorized` → `{ Message: string }` (invalid credentials, unverified email, or suspended account)

### POST `/api/Auth/register`

- **Auth**: None
- **Description**: Register a new client user and send email verification link.
- **Request body** (`RegisterRequest`):
  - `firstName` (string)
  - `lastName` (string)
  - `email` (string)
  - `password` (string)
  - `phone` (string)
- **Responses**:
  - `200 OK` → `{ Message: string }` (may indicate registration success or email service delay)
  - `400 Bad Request` → `{ Message: string }` or `{ Errors: string[] }`

### GET `/api/Auth/verify-email`

- **Auth**: None
- **Description**: Confirm a user's email using a token.
- **Query parameters**:
  - `userId` (string; GUID)
  - `token` (string; URL-safe base64 encoded)
- **Responses**:
  - `200 OK` → `{ Message: string }`
  - `400 Bad Request` → `{ Message: string }`

### POST `/api/Auth/forgot-password`

- **Auth**: None
- **Description**: Trigger password reset flow for a user.
- **Request body** (`ForgotPasswordRequest`):
  - `email` (string)
- **Responses**:
  - `200 OK` → `{ Message: string }` (generic, does not reveal account existence)

---

## Health (`/api/health`)

**Controller**: `HealthController` (`[Route("api/health")]`)

### GET `/api/health`

- **Auth**: None
- **Description**: Health check endpoint.
- **Responses**:
  - `200 OK` → 
    - `status` (string; `"Healthy"`)
    - `timestamp` (string; ISO date-time)
    - `database` (string; `"Connected"` or `"Disconnected"`)
    - `version` (string; e.g. `"1.0.0-PROD"`)
  - `500 Internal Server Error` → `{ Status: "Unhealthy", Error: string }`

---

## Profile (`/api/Profile`)

**Controller**: `ProfileController` (`[Route("api/[controller]")]`, `[Authorize]`)

### GET `/api/Profile/me`

- **Auth**: JWT (any authenticated user)
- **Description**: Get current user's profile.
- **Responses**:
  - `200 OK` → `UserProfileDto` (shape inferred from DTO, not included here)
  - `401 Unauthorized` → `{ Message: string }`
  - `404 Not Found` → `{ Message: string }`

### PUT `/api/Profile/me`

- **Auth**: JWT
- **Description**: Update current user's profile (partial updates).
- **Request body** (`UpdateProfileRequest`):
  - `fullName` (string, optional)
  - `email` (string, optional)
  - `phone` (string, optional)
  - `avatarUrl` (string, optional)
- **Responses**:
  - `200 OK` → `{ Message: string }`
  - `400 Bad Request` → `{ Message: string }`
  - `401 Unauthorized`

### GET `/api/Profile/bookings`

- **Auth**: JWT
- **Description**: Get booking history for the current user.
- **Responses**:
  - `200 OK` → `BookingDto[]`

### POST `/api/Profile/rotate-security`

- **Auth**: JWT
- **Description**: Change the current user's password.
- **Request body** (`RotateCredentialsRequest`):
  - `oldPassword` (string)
  - `newPassword` (string)
  - `confirmNewPassword` (string)
- **Responses**:
  - `200 OK` → `{ Message: string }`
  - `400 Bad Request` → `{ Message: string }`
  - `401 Unauthorized`

---

## Bookings (`/api/bookings`)

**Controller**: `BookingsController` (`[Route("api/bookings")]`)

### GET `/api/bookings`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Get all bookings.
- **Responses**:
  - `200 OK` → `BookingDto[]`

### GET `/api/bookings/{code}`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Route parameters**:
  - `code` (string; booking code)
- **Responses**:
  - `200 OK` → `BookingDto`
  - `404 Not Found`

### GET `/api/bookings/lookup`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Lookup a booking by code and guest email (public endpoint).
- **Query parameters**:
  - `code` (string)
  - `email` (string)
- **Responses**:
  - `200 OK` → `BookingDto`
  - `400 Bad Request` → `{ Message: string }` (missing query params)
  - `404 Not Found` → `{ Message: string }`

### POST `/api/bookings`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Create a new booking.
- **Request body** (`CreateBookingRequest`):
  - `roomId` (GUID)
  - `guestFirstName` (string, nullable)
  - `guestLastName` (string, nullable)
  - `guestEmail` (string, nullable)
  - `guestPhone` (string, nullable)
  - `checkIn` (DateTime)
  - `checkOut` (DateTime)
  - `paymentMethod` (`PaymentMethod` enum; e.g. card, transfer)
  - `notes` (string, nullable)
- **Responses**:
  - `200 OK` → `BookingDto` (often with payment metadata)
  - `400 Bad Request` → `{ Message: string }`

### POST `/api/bookings/{code}/verify-paystack`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Verify a Paystack payment for a booking, then mark booking as paid.
- **Route parameters**:
  - `code` (string; booking code)
- **Responses**:
  - `200 OK` → `{ Message: string, Data: BookingDto }`
  - `400 Bad Request` → `{ Message: string }`

### POST `/api/bookings/{code}/confirm-transfer`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Confirm a bank transfer for a booking.
- **Route parameters**:
  - `code` (string; booking code)
- **Responses**:
  - `200 OK` → `{ Message: string, Data: BookingDto }`
  - `400 Bad Request` → `{ Message: string }`
  - `401 Unauthorized`

### PUT `/api/bookings/{id}/status`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Update booking status.
- **Route parameters**:
  - `id` (GUID; booking id)
- **Query parameters**:
  - `status` (`BookingStatus` enum)
- **Responses**:
  - `200 OK` → `BookingDto`
  - `400 Bad Request` → `{ Message: string }`
  - `401 Unauthorized`

### POST `/api/bookings/{id}/cancel`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Cancel a booking from staff/admin side.
- **Route parameters**:
  - `id` (GUID; booking id)
- **Query parameters**:
  - `reason` (string, optional)
- **Responses**:
  - `200 OK` → `{ Message: string, Data: BookingDto }`
  - `400 Bad Request` → `{ Message: string }`
  - `401 Unauthorized` / `403 Forbidden`

### POST `/api/bookings/guest/cancel`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Guest-initiated booking cancellation.
- **Request body** (`CancelBookingRequest`):
  - `bookingCode` (string, required)
  - `email` (string, required, email format)
  - `reason` (string, optional, max 500 chars)
- **Responses**:
  - `200 OK` → `{ Message: string, Data: BookingDto }`
  - `400 Bad Request` → model state errors or `{ Message: string }`
  - `401 Unauthorized` (for some error paths)

### POST `/api/bookings/{id}/complete-refund`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Mark a refund as completed.
- **Route parameters**:
  - `id` (GUID; booking id)
- **Query parameters**:
  - `transactionRef` (string)
- **Responses**:
  - `200 OK` → `{ Message: string, Data: BookingDto }`
  - `400 Bad Request` → `{ Message: string }`
  - `401 Unauthorized`

### GET `/api/bookings/pending-refunds`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Get all bookings that are pending refund completion.
- **Responses**:
  - `200 OK` → `BookingDto[]`
  - `400 Bad Request` → `{ Message: string }`

---

## Rooms (`/api/rooms`)

**Controller**: `RoomsController` (`[Route("api/rooms")]`)

### GET `/api/rooms`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Get all rooms, optionally filtered by category.
- **Query parameters**:
  - `category` (`RoomCategory` enum, optional)
- **Responses**:
  - `200 OK` → `RoomDto[]`

### GET `/api/rooms/search`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Search available rooms by date range, category, guest count, room number, and amenities.
- **Query parameters** (wrapped into `RoomSearchRequest` inside service):
  - `checkIn` (DateTime?, optional)
  - `checkOut` (DateTime?, optional)
  - `category` (`RoomCategory` enum, optional)
  - `guest` (int, default 1; must be > 0)
  - `roomNumber` (string, optional)
  - `amenity` (string, optional)
- **Responses**:
  - `200 OK` → `RoomDto[]`
  - `400 Bad Request` → validation messages (e.g., invalid dates, guest count)

### GET `/api/rooms/{id}`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Get a room by ID.
- **Route parameters**:
  - `id` (GUID; room ID)
- **Responses**:
  - `200 OK` → `RoomDto`
  - `404 Not Found` → `{ message: "Room not found." }`

### GET `/api/rooms/{id}/availability`

- **Auth**: None (`[AllowAnonymous]`)
- **Description**: Check availability for a specific room in a date range.
- **Route parameters**:
  - `id` (GUID; room ID)
- **Query parameters**:
  - `checkIn` (DateTime)
  - `checkOut` (DateTime)
- **Responses**:
  - `200 OK` → availability DTO/boolean (from `CheckAvailabilityAsync`)
  - `400 Bad Request` → invalid date range
  - `404 Not Found` → `{ message: "Room not found." }`

### POST `/api/rooms`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Consumes**: `multipart/form-data`
- **Description**: Create a new room along with optional images.
- **Form fields**:
  - `request` (`CreateRoomRequest` bound from form fields):
    - `roomNumber` (string)
    - `name` (string)
    - `category` (`RoomCategory`)
    - `floor` (`PropertyFloor`)
    - `status` (`RoomStatus`)
    - `pricePerNight` (decimal)
    - `guest` (int)
    - `size` (string)
    - `description` (string)
    - `amenities` (`List<string>`)
  - `files` (`List<IFormFile>`; images)
- **Responses**:
  - `201 Created` → `RoomDto` with populated `Images` list
  - `500 Internal Server Error` → `{ message: string, details: string }`

### POST `/api/rooms/{id}/images`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Consumes**: `multipart/form-data`
- **Description**: Upload additional images for an existing room.
- **Route parameters**:
  - `id` (GUID; room ID)
- **Form fields**:
  - `files` (`List<IFormFile>`; required)
- **Responses**:
  - `200 OK` → `{ message: "Images added successfully." }`
  - `400 Bad Request` → `{ message: string }` (e.g. no files)
  - `404 Not Found` → `{ message: "Room not found." }`
  - `500 Internal Server Error` → `{ message: string, details: string }`

### PUT `/api/rooms/{id}`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Update a room's metadata.
- **Route parameters**:
  - `id` (GUID; room ID)
- **Request body** (`UpdateRoomRequest`):
  - `name` (string)
  - `category` (`RoomCategory`)
  - `floor` (`PropertyFloor`)
  - `status` (`RoomStatus`)
  - `pricePerNight` (decimal)
  - `guest` (int)
  - `isOnline` (bool)
  - `description` (string)
  - `amenities` (`List<string>`)
  - `images` (`List<string>`; image URLs)
- **Responses**:
  - `204 No Content`
  - `500 Internal Server Error` → `{ message: string, details: string }`

### DELETE `/api/rooms/{id}`

- **Auth**: JWT (`Roles = Admin, Manager`)
- **Description**: Delete a room and all its associated images.
- **Route parameters**:
  - `id` (GUID; room ID)
- **Responses**:
  - `200 OK` → `{ message: "Room and all associated images deleted successfully." }`
  - `404 Not Found` → `{ message: "Room not found." }`
  - `500 Internal Server Error` → `{ message: string, details: string }`

---

## Staff & User Management (`/api/admin/management`)

**Controller**: `StaffController` (`[Route("api/admin/management")]`)

### GET `/api/admin/management/stats`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Get staff-related statistics for dashboard.
- **Responses**:
  - `200 OK` → `StaffDashboardStatsDto`

### GET `/api/admin/management/employees`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Get list of all staff users.
- **Responses**:
  - `200 OK` → `StaffSummaryDto[]`

### GET `/api/admin/management/clients`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Get list of all client users.
- **Responses**:
  - `200 OK` → filtered list of users where `Role == Client` (DTO type from staff service)

### POST `/api/admin/management/onboard-staff`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Provision a new staff user.
- **Request body** (`OnboardUserRequest`):
  - `fullName` (string)
  - `email` (string)
  - `temporaryPassword` (string)
  - `assignedRole` (`UserRole` enum; e.g. Admin/Manager/Staff)
  - `status` (`ProfileStatus` enum)
  - `department` (string, optional)
  - `phone` (string, optional)
- **Responses**:
  - `200 OK` → `{ Message: string }`
  - `403 Forbidden` → `{ Message: string }` (when unauthorized access)
  - `400 Bad Request` → `{ Message: string }`

### PATCH `/api/admin/management/accounts/{id}/status`

- **Auth**: JWT (`Roles = Admin`)
- **Description**: Change the status of a user account.
- **Route parameters**:
  - `id` (GUID; user id)
- **Request body** (`ChangeStatusRequest`):
  - `status` (`ProfileStatus` enum)
- **Responses**:
  - `200 OK` → `{ Message: "Account status updated successfully." }`
  - `400 Bad Request` → `{ Message: string }`

### DELETE `/api/admin/management/accounts/{id}`

- **Auth**: JWT (`Roles = Admin`)
- **Description**: Delete a user account.
- **Route parameters**:
  - `id` (GUID; user id)
- **Responses**:
  - `204 No Content`
  - `400 Bad Request` → `{ Message: string }`

---

## Guests (`/api/guests`)

**Controller**: `GuestsController` (`[Route("api/guests")]`, `[Authorize(Roles = "Admin,Manager,Staff")]`)

### GET `/api/guests`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Get all guests or search by a query term.
- **Query parameters**:
  - `search` (string, optional)
- **Responses**:
  - `200 OK` → `GuestDto[]` (either all guests or filtered results)

### GET `/api/guests/{id}`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Route parameters**:
  - `id` (string; guest ID, e.g. `"GS-1234"`)
- **Responses**:
  - `200 OK` → `GuestDto`
  - `404 Not Found`

---

## Notifications (`/api/Notifications`)

**Controller**: `NotificationsController` (`[Route("api/[controller]")]`, `[Authorize]`)

### GET `/api/Notifications/staff`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Get notifications relevant to staff.
- **Responses**:
  - `200 OK` → `NotificationDto[]`

### GET `/api/Notifications/my`

- **Auth**: JWT
- **Description**: Get notifications for the current user.
- **Responses**:
  - `200 OK` → `NotificationDto[]`

### PATCH `/api/Notifications/{id}/read`

- **Auth**: JWT
- **Description**: Mark a notification as read.
- **Route parameters**:
  - `id` (GUID; notification id)
- **Responses**:
  - `204 No Content`

---

## Analytics (`/api/analytics`)

**Controller**: `AnalyticsController` (`[Route("api/analytics")]`, `[Authorize(Roles = "Admin,Manager")]`)

### GET `/api/analytics/overview`

- **Auth**: JWT (`Roles = Admin,Manager`)
- **Description**: Get dashboard overview metrics.
- **Responses**:
  - `200 OK` → `DashboardOverviewDto`

---

## Operations (`/api/operations`)

**Controller**: `OperationsController` (`[Route("api/operations")]`, `[Authorize(Roles = "Admin,Manager,Staff")]`)

### GET `/api/operations/ledger`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Get operational ledger entries, optionally filtered.
- **Query parameters**:
  - `filter` (string, optional)
  - `search` (string, optional)
- **Responses**:
  - `200 OK` → `OperationLogEntryDto[]`

### GET `/api/operations/stats/daily`

- **Auth**: JWT (`Roles = Admin,Manager,Staff`)
- **Description**: Get daily operational stats for dashboard cards.
- **Responses**:
  - `200 OK` → 
    - `checkInsToday` (number)
    - `checkOutsToday` (number)
    - `historicalTrace` (number; total ledger count)
    - `auditHealth` (string; e.g. `"100%"`)

---

## Visit Records (`/api/visit-records`)

**Controller**: `VisitRecordsController` (`[Route("api/visit-records")]`, `[Authorize]`)

### GET `/api/visit-records`

- **Auth**: JWT
- **Description**: Get all visit records.
- **Responses**:
  - `200 OK` → `VisitRecordDto[]`

### POST `/api/visit-records`

- **Auth**: JWT
- **Description**: Log a visit record for a booking.
- **Query parameters**:
  - `code` (string; booking code)
  - `action` (string; action taken, e.g. `"CHECK_IN"` / `"CHECK_OUT"`)
- **Responses**:
  - `200 OK` (empty body)

---

## Images (`/api/images`)

**Controller**: `ImagesController` (`[Route("api/images")]`)

### POST `/api/images/upload`

- **Auth**: None
- **Description**: Upload a single image to a specified folder (defaults to `website-assets`).
- **Request**:
  - **Body**: `multipart/form-data`
    - `file` (`IFormFile`; required)
  - **Query parameters**:
    - `folder` (string, optional; default `"website-assets"`)
- **Responses**:
  - `200 OK` → `ImageUploadResult`:
    - `publicId` (string)
    - `url` (string)
  - `400 Bad Request` → `{ message: string }` (e.g. missing/empty file, invalid extension, cloud failure)
  - `500 Internal Server Error` → `{ message: string, details: string }`

### DELETE `/api/images/delete`

- **Auth**: None (note: uses DB context internally; ensure this is protected externally if needed)
- **Description**: Delete an image from Cloudinary and the database by `publicId`.
- **Query parameters**:
  - `publicId` (string; required)
- **Responses**:
  - `200 OK` → `{ message: "Image successfully removed from cloud and database." }`
  - `400 Bad Request` → `{ message: "PublicId is required." }`
  - `404 Not Found` → `{ message: "Image not found in Cloud or Database." }`

---

## Audit Logs (`/api/audit-logs`)

**Controller**: `AuditLogsController` (`[Route("api/audit-logs")]`, `[Authorize(Roles = "Admin")]`)

### GET `/api/audit-logs`

- **Auth**: JWT (`Roles = Admin`)
- **Description**: Get all audit logs.
- **Responses**:
  - `200 OK` → `AuditLogDto[]`

---

## Payment Simulator (`/api/payment-simulator`)

**Controller**: `PaymentSimulatorController` (`[Route("api/payment-simulator")]`)

> **Note**: This endpoint is marked with `[ApiExplorerSettings(IgnoreApi = true)]` and is primarily for local/testing use to simulate Paystack payments.

### GET `/api/payment-simulator`

- **Auth**: None
- **Description**: Render an HTML page that simulates a Paystack payment and then calls `/api/bookings/{code}/verify-paystack`.
- **Query parameters**:
  - `code` (string; booking code)
  - `amount` (decimal)
  - `email` (string)
  - `redirectUrl` (string, optional; base origin to return to)
- **Responses**:
  - `200 OK` → HTML content (not JSON)

---

## DTO Summary (Selected)

For quick reference, here are key request/response body shapes used by the APIs:

- **LoginRequest**
  - `email` (string)
  - `password` (string)

- **RegisterRequest**
  - `firstName` (string)
  - `lastName` (string)
  - `email` (string)
  - `password` (string)
  - `phone` (string)

- **ForgotPasswordRequest**
  - `email` (string)

- **CreateBookingRequest**
  - `roomId` (GUID)
  - `guestFirstName` (string, nullable)
  - `guestLastName` (string, nullable)
  - `guestEmail` (string, nullable)
  - `guestPhone` (string, nullable)
  - `checkIn` (DateTime)
  - `checkOut` (DateTime)
  - `paymentMethod` (`PaymentMethod`)
  - `notes` (string, nullable)

- **CancelBookingRequest**
  - `bookingCode` (string, required)
  - `email` (string, required, email)
  - `reason` (string, optional, max 500)

- **CreateRoomRequest**
  - `roomNumber` (string)
  - `name` (string)
  - `category` (`RoomCategory`)
  - `floor` (`PropertyFloor`)
  - `status` (`RoomStatus`)
  - `pricePerNight` (decimal)
  - `guest` (int)
  - `size` (string)
  - `description` (string)
  - `amenities` (`List<string>`)

- **UpdateRoomRequest**
  - `name` (string)
  - `category` (`RoomCategory`)
  - `floor` (`PropertyFloor`)
  - `status` (`RoomStatus`)
  - `pricePerNight` (decimal)
  - `guest` (int)
  - `isOnline` (bool)
  - `description` (string)
  - `amenities` (`List<string>`)
  - `images` (`List<string>`)

- **UpdateProfileRequest**
  - `fullName` (string, optional)
  - `email` (string, optional)
  - `phone` (string, optional)
  - `avatarUrl` (string, optional)

- **RotateCredentialsRequest**
  - `oldPassword` (string)
  - `newPassword` (string)
  - `confirmNewPassword` (string)

- **OnboardUserRequest**
  - `fullName` (string)
  - `email` (string)
  - `temporaryPassword` (string)
  - `assignedRole` (`UserRole`)
  - `status` (`ProfileStatus`)
  - `department` (string, optional)
  - `phone` (string, optional)

- **ChangeStatusRequest**
  - `status` (`ProfileStatus`)

- **AuthResponse**
  - `token` (string)
  - `email` (string)
  - `name` (string)
  - `role` (string)

- **RoomDto**
  - `id` (GUID)
  - `roomNumber` (string)
  - `name` (string)
  - `category` (`RoomCategory`)
  - `floor` (`PropertyFloor`)
  - `status` (`RoomStatus`)
  - `pricePerNight` (decimal)
  - `guest` (int)
  - `size` (string)
  - `isOnline` (bool)
  - `description` (string)
  - `amenities` (`List<string>`)
  - `images` (`List<string>`)
  - `createdAt` (DateTime)

- **BookingDto**
  - `id` (GUID)
  - `bookingCode` (string)
  - `roomId` (GUID)
  - `guestId` (string)
  - `guestFirstName` (string)
  - `guestLastName` (string)
  - `guestEmail` (string)
  - `guestPhone` (string)
  - `checkIn` (DateTime)
  - `checkOut` (DateTime)
  - `status` (`BookingStatus`)
  - `amount` (decimal)
  - `paymentStatus` (`PaymentStatus`)
  - `paymentMethod` (`PaymentMethod?`)
  - `transactionReference` (string, nullable)
  - `notes` (string, nullable)
  - `createdAt` (DateTime)
  - `paymentUrl` (string, nullable)
  - `paymentInstruction` (string, nullable)

- **ImageUploadResult**
  - `publicId` (string)
  - `url` (string)

- Other DTOs (`UserProfileDto`, `GuestDto`, `NotificationDto`, `DashboardOverviewDto`, `StaffSummaryDto`, `StaffDashboardStatsDto`, `VisitRecordDto`, `AuditLogDto`, `OperationLogEntryDto`) are returned directly by their respective services and controllers; consult their definitions for complete field lists.


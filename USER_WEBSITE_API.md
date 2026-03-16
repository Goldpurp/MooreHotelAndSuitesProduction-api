## User-Facing Website API Guide

This document lists the APIs intended for the **public/user-facing website** (guests & logged-in clients), separate from backoffice/admin endpoints.

Base URL prefix for all routes: `/api`

---

## Authentication & Account

### POST `/api/Auth/login`

- **Used for**: User login on the website.
- **Request body**:
  - `email` (string)
  - `password` (string)
- **Response** `200 OK`:
  - `token` (string; JWT bearer token)
  - `email` (string)
  - `name` (string)
  - `role` (string; typically `"Client"`)

### POST `/api/Auth/register`

- **Used for**: Guest sign up / account creation.
- **Request body**:
  - `firstName` (string)
  - `lastName` (string)
  - `email` (string)
  - `password` (string)
  - `phone` (string)
- **Response** `200 OK`:
  - `{ message: string }` (e.g. “Registration Successful: Check your email for activation instructions.”)

### GET `/api/Auth/verify-email`

- **Used for**: Email verification link from the website.
- **Query params**:
  - `userId` (string; GUID)
  - `token` (string; URL-safe base64)
- **Response** `200 OK`:
  - `{ message: string }`

### POST `/api/Auth/forgot-password`

- **Used for**: “Forgot password” flow.
- **Request body**:
  - `email` (string)
- **Response** `200 OK`:
  - `{ message: string }` (generic)

---

## Rooms & Availability

### GET `/api/rooms`

- **Used for**: Listing rooms (optionally by category).
- **Query params**:
  - `category` (optional; `RoomCategory` enum)
- **Response** `200 OK`:
  - `RoomDto[]`

`RoomDto` (fields):
- `id` (GUID)
- `roomNumber` (string)
- `name` (string)
- `category` (`RoomCategory`)
- `floor` (`PropertyFloor`)
- `status` (`RoomStatus`)
- `pricePerNight` (decimal)
- `guest` (int; capacity)
- `size` (string)
- `isOnline` (bool)
- `description` (string)
- `amenities` (`string[]`)
- `images` (`string[]`; URLs)
- `createdAt` (DateTime)

### GET `/api/rooms/search`

- **Used for**: Search rooms by dates and filters (search page).
- **Query params**:
  - `checkIn` (optional DateTime)
  - `checkOut` (optional DateTime)
  - `category` (optional `RoomCategory`)
  - `guest` (int; default 1; must be > 0)
  - `roomNumber` (optional string)
  - `amenity` (optional string)
- **Response** `200 OK`:
  - `RoomDto[]`

### GET `/api/rooms/{id}`

- **Used for**: Room details page.
- **Route params**:
  - `id` (GUID)
- **Response** `200 OK`:
  - `RoomDto`

### GET `/api/rooms/{id}/availability`

- **Used for**: Check availability for a specific room & date range.
- **Route params**:
  - `id` (GUID)
- **Query params**:
  - `checkIn` (DateTime)
  - `checkOut` (DateTime)
- **Response** `200 OK`:
  - Availability payload (boolean/structure from backend; treat as opaque JSON)

---

## Bookings (Guest-Facing)

### POST `/api/bookings`

- **Used for**: Creating a booking from the website (checkout flow).
- **Request body**:
  - `roomId` (GUID)
  - `guestFirstName` (string, nullable)
  - `guestLastName` (string, nullable)
  - `guestEmail` (string, nullable)
  - `guestPhone` (string, nullable)
  - `checkIn` (DateTime)
  - `checkOut` (DateTime)
  - `paymentMethod` (`PaymentMethod` enum; e.g. card/transfer)
  - `notes` (string, nullable)
- **Response** `200 OK`:
  - `BookingDto` (often with payment URL/instruction populated)

`BookingDto` (fields):
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

### GET `/api/bookings/lookup`

- **Used for**: Public booking lookup page (no login).
- **Query params**:
  - `code` (string; booking code)
  - `email` (string; guest email)
- **Response** `200 OK`:
  - `BookingDto`

### POST `/api/bookings/guest/cancel`

- **Used for**: Guest self-service cancellation.
- **Request body**:
  - `bookingCode` (string; required)
  - `email` (string; required, email)
  - `reason` (string; optional, max 500 chars)
- **Response** `200 OK`:
  - `{ message: string, data: BookingDto }`

### GET `/api/payment-simulator`

- **Used for**: Local/test flow to simulate Paystack redirect (dev-only).
- **Query params**:
  - `code` (string)
  - `amount` (decimal)
  - `email` (string)
  - `redirectUrl` (string, optional)
- **Response**:
  - `200 OK` → HTML page (not JSON) that will call `/api/bookings/{code}/verify-paystack` on the same origin.

> On production, you’ll usually redirect to real Paystack instead; keep this for local testing.

---

## Logged-In Client Area (Profile)

These require the JWT from `/api/Auth/login` sent as `Authorization: Bearer <token>` in headers.

### GET `/api/Profile/me`

- **Used for**: “My profile” page.
- **Response** `200 OK`:
  - `UserProfileDto` (fields defined by backend; name, email, phone, etc.)

### PUT `/api/Profile/me`

- **Used for**: Update profile details.
- **Request body**:
  - `fullName` (string, optional)
  - `email` (string, optional)
  - `phone` (string, optional)
  - `avatarUrl` (string, optional)
- **Response** `200 OK`:
  - `{ message: string }`

### GET `/api/Profile/bookings`

- **Used for**: “My bookings” history (when the user is logged in).
- **Response** `200 OK`:
  - `BookingDto[]`

### POST `/api/Profile/rotate-security`

- **Used for**: Change password from profile/security settings.
- **Request body**:
  - `oldPassword` (string)
  - `newPassword` (string)
  - `confirmNewPassword` (string)
- **Response** `200 OK`:
  - `{ message: string }`

---

## User Notifications

### GET `/api/Notifications/my`

- **Used for**: Notification center for the logged-in user.
- **Auth**: Bearer token.
- **Response** `200 OK`:
  - `NotificationDto[]` (fields defined in backend; includes message, type, read status, timestamps)

### PATCH `/api/Notifications/{id}/read`

- **Used for**: Mark a notification as read.
- **Route params**:
  - `id` (GUID)
- **Response**:
  - `204 No Content`

---

## Health (Optional for Frontend)

### GET `/api/health`

- **Used for**: Optional environment/health pings from the frontend (e.g., diagnostics).
- **Response** `200 OK`:
  - `status` (string; `"Healthy"`)
  - `timestamp` (string)
  - `database` (string)
  - `version` (string)

---

## Quick Reference – Request DTOs

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
  - `bookingCode` (string)
  - `email` (string)
  - `reason` (string, optional)

- **UpdateProfileRequest**
  - `fullName` (string, optional)
  - `email` (string, optional)
  - `phone` (string, optional)
  - `avatarUrl` (string, optional)

- **RotateCredentialsRequest**
  - `oldPassword` (string)
  - `newPassword` (string)
  - `confirmNewPassword` (string)


# Authentication & Authorization Implementation Plan

## Overview
This document outlines the complete plan for implementing authentication and authorization in the Alaska Mountain Runners Grand Prix application using PostgreSQL, ASP.NET Identity, and JWT token-based authentication.

## Implementation Decisions

### Database
- **PostgreSQL 16** (switched from SQL Server)
- Lightweight and suitable for the application scale
- Better compatibility with modern deployment options

### Authentication Method
- **JWT Token-based Authentication**
  - Stateless and scalable
  - Works well with React SPA
  - Access tokens: 15 minutes
  - Refresh tokens: 7 days
  - Easy to extend to mobile apps in the future

### Registration
- **Self-registration enabled**
  - Users can create accounts
  - All new users assigned "ReadOnly" role by default
  - Admins can upgrade user roles later

### Email Verification
- **Email confirmation required**
  - Users must verify email before login
  - Using MailHog for development (SMTP server + web UI)
  - Configuration ready for production SMTP (SendGrid, etc.)

### Role Structure
Three hierarchical permission levels:

1. **ReadOnly**
   - Default role for new registrations
   - Can view race results, standings, and statistics
   - Read-only access to all public data

2. **Manager**
   - Includes all ReadOnly permissions
   - Can create and edit races
   - Can upload and manage race results
   - Can manage race data and configurations

3. **Admin**
   - Includes all Manager permissions
   - Can manage user accounts
   - Can assign/change user roles
   - Full system access and configuration

---

## Implementation Phases

### ✅ Phase 1: Database & Infrastructure (COMPLETED)

#### Tasks Completed:
1. ✅ Switch database from SQL Server to PostgreSQL in Docker
2. ✅ Install Entity Framework Core and Npgsql packages
3. ✅ Install ASP.NET Identity packages
4. ✅ Create custom ApplicationUser model with role support
5. ✅ Create ApplicationDbContext with Identity configuration
6. ✅ Configure Identity services with email confirmation requirements
7. ✅ Add JWT authentication and token generation configuration
8. ✅ Configure email service (SMTP) for confirmation emails
9. ✅ Create database migrations for Identity tables
10. ✅ Create role seeding service (ReadOnly, Manager, Admin)
11. ✅ Create authorization policies for three permission levels

#### What Was Built:

**Docker Configuration** (`docker-compose.yml`):
- PostgreSQL 16 container (port 5432)
- MailHog container for email testing
  - SMTP: localhost:1025
  - Web UI: http://localhost:8025

**NuGet Packages Installed**:
- `Npgsql.EntityFrameworkCore.PostgreSQL` (9.0.4)
- `Microsoft.EntityFrameworkCore.Design` (9.0.10)
- `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (9.0.10)
- `Microsoft.AspNetCore.Authentication.JwtBearer` (9.0.10)

**Models Created**:
- `ApplicationUser.cs` - Extends IdentityUser with:
  - FirstName, LastName (string, nullable)
  - DateOfBirth (DateOnly, nullable)
  - RefreshToken, RefreshTokenExpiryTime
  - CreatedAt, UpdatedAt timestamps
- `JwtSettings.cs` - JWT configuration
- `EmailSettings.cs` - Email configuration

**Data Layer**:
- `ApplicationDbContext.cs` - Identity DbContext with custom table names:
  - Users, Roles, UserRoles, UserClaims, UserLogins, RoleClaims, UserTokens

**Services**:
- `IEmailService` & `EmailService` - SMTP email service
  - SendEmailAsync() - Generic email sending
  - SendEmailConfirmationAsync() - Formatted confirmation emails
- `RoleSeedingService` - Auto-seeds roles on application startup

**Configuration** (`appsettings.json`):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=AmrGrandPrix;Username=postgres;Password=YourStrong@Passw0rd"
  },
  "Jwt": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong!",
    "Issuer": "AmrGrandPrixAPI",
    "Audience": "AmrGrandPrixClient",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  },
  "Email": {
    "SmtpHost": "localhost",
    "SmtpPort": 1025,
    "FromEmail": "noreply@amrgrandprix.com",
    "FromName": "Alaska Mountain Runners Grand Prix",
    "EnableSsl": false
  }
}
```

**Program.cs Configuration**:
- DbContext with PostgreSQL
- ASP.NET Identity with:
  - Password requirements (8+ chars, mixed case, digit, special char)
  - Account lockout (5 attempts, 15 min lockout)
  - Required email confirmation
- JWT Authentication with token validation
- Authorization policies:
  - "ReadOnly" - Requires ReadOnly, Manager, or Admin role
  - "Manager" - Requires Manager or Admin role
  - "Admin" - Requires Admin role only
- Email service registration
- Role seeding on startup

**Database Migrations**:
- `20251111064417_InitialIdentitySetup.cs` - Creates all Identity tables

**Files Created**:
- `/AmrGrandPrix.API/Models/ApplicationUser.cs`
- `/AmrGrandPrix.API/Models/JwtSettings.cs`
- `/AmrGrandPrix.API/Models/EmailSettings.cs`
- `/AmrGrandPrix.API/Data/ApplicationDbContext.cs`
- `/AmrGrandPrix.API/Services/IEmailService.cs`
- `/AmrGrandPrix.API/Services/EmailService.cs`
- `/AmrGrandPrix.API/Services/RoleSeedingService.cs`
- `/AmrGrandPrix.API/Migrations/20251111064417_InitialIdentitySetup.cs`

---

### 🔄 Phase 2: Backend API (NEXT UP - IN PROGRESS)

#### Remaining Tasks:
1. ⏳ Create DTOs for authentication requests/responses
2. ⏳ Create authentication controller (register, login, logout, refresh, confirm-email)
3. ⏳ Add user management endpoints for admins (role assignment)
4. ⏳ Update CORS configuration for authentication

#### What Needs to Be Built:

**DTOs** (`/Models/DTOs/`):
- `RegisterRequest.cs`:
  - Email, Password, ConfirmPassword
  - FirstName, LastName (optional)
  - DateOfBirth (optional)
- `LoginRequest.cs`:
  - Email, Password
- `LoginResponse.cs`:
  - AccessToken, RefreshToken
  - User info (Id, Email, FirstName, LastName, Roles)
  - TokenExpiration
- `RefreshTokenRequest.cs`:
  - RefreshToken
- `ChangePasswordRequest.cs`
- `UpdateUserRequest.cs`
- `UserResponse.cs`
- `AssignRoleRequest.cs`

**Controllers** (`/Controllers/`):
- `AuthController.cs`:
  - `POST /api/auth/register` - Create new account, send confirmation email
  - `POST /api/auth/login` - Authenticate and return JWT tokens
  - `POST /api/auth/logout` - Invalidate refresh token
  - `POST /api/auth/refresh-token` - Get new access token
  - `GET /api/auth/confirm-email?userId={id}&token={token}` - Verify email
  - `POST /api/auth/resend-confirmation` - Resend confirmation email
  - `POST /api/auth/forgot-password` - Send password reset email
  - `POST /api/auth/reset-password` - Reset password with token

- `UserManagementController.cs` (Admin only):
  - `GET /api/users` - List all users (paginated)
  - `GET /api/users/{id}` - Get user details
  - `PUT /api/users/{id}/role` - Assign/change user role
  - `DELETE /api/users/{id}` - Delete user account
  - `PUT /api/users/{id}/lock` - Lock/unlock user account

**Services** (`/Services/`):
- `ITokenService` & `TokenService`:
  - GenerateAccessToken() - Create JWT access token
  - GenerateRefreshToken() - Create refresh token
  - GetPrincipalFromExpiredToken() - Validate expired tokens for refresh
  - ValidateToken() - Validate token structure

**Utilities**:
- JWT token generation logic
- Password validation helpers
- Email template builders

---

### Phase 3: Frontend Implementation

#### Tasks:
1. Create frontend auth context and hooks
2. Build login/register UI components with email confirmation flow
3. Add protected route wrapper component with role checking
4. Implement JWT token storage, refresh, and expiration handling
5. Create email confirmation page component

#### What Needs to Be Built:

**Context** (`/src/contexts/`):
- `AuthContext.jsx`:
  - Current user state
  - Roles and permissions
  - Login/logout functions
  - Token management

**Hooks** (`/src/hooks/`):
- `useAuth.js` - Access auth context
- `useRequireAuth.js` - Redirect if not authenticated
- `useRequireRole.js` - Check role permissions

**Components** (`/src/components/auth/`):
- `LoginForm.jsx` - Email/password login
- `RegisterForm.jsx` - User registration with validation
- `EmailConfirmation.jsx` - Email verification success/error page
- `ForgotPassword.jsx` - Password reset request
- `ResetPassword.jsx` - Password reset form
- `ProtectedRoute.jsx` - HOC for route protection
- `RoleGuard.jsx` - Component that checks roles

**Services** (`/src/services/`):
- `authService.js`:
  - login()
  - register()
  - logout()
  - refreshToken()
  - confirmEmail()
  - getCurrentUser()
- `tokenService.js`:
  - getAccessToken()
  - getRefreshToken()
  - setTokens()
  - clearTokens()
  - isTokenExpired()
  - Auto-refresh logic

**Routing Updates**:
- Add auth routes (/login, /register, /confirm-email, etc.)
- Wrap protected routes with ProtectedRoute component
- Add role-based route guards

---

### Phase 4: Integration & Testing

#### Tasks:
1. Add authorization guards to existing/future API endpoints
2. Test full authentication flow including email confirmation
3. Test role-based authorization
4. Test token refresh logic
5. Test email sending (MailHog)
6. Integration testing

#### Test Scenarios:

**Authentication Flow**:
1. User registers → receives email → confirms → can login
2. User tries to login without email confirmation → denied
3. User logs in → receives access + refresh tokens
4. Access token expires → auto-refresh → continues working
5. User logs out → tokens invalidated

**Authorization Flow**:
1. ReadOnly user tries to access Manager endpoint → denied
2. Manager user accesses Manager endpoint → allowed
3. Manager user tries Admin endpoint → denied
4. Admin user accesses all endpoints → allowed

**Edge Cases**:
- Invalid/expired email confirmation tokens
- Invalid/expired refresh tokens
- Account lockout after failed attempts
- Password reset flow
- Resend confirmation email
- Multiple concurrent sessions

---

## Technical Architecture

### Authentication Flow

```
1. User Registration:
   User → POST /api/auth/register → API creates user (EmailConfirmed=false)
   → Email service sends confirmation link
   → User clicks link → GET /api/auth/confirm-email
   → API confirms email → User can now login

2. Login:
   User → POST /api/auth/login → API validates credentials + email confirmed
   → API generates Access Token (15min) + Refresh Token (7 days)
   → Frontend stores tokens → User authenticated

3. API Requests:
   Frontend → API request with Bearer token in header
   → API validates token → Authorizes based on role → Returns data

4. Token Refresh:
   Access token expires → Frontend → POST /api/auth/refresh-token
   → API validates refresh token → Issues new access token
   → Frontend updates token → Request succeeds

5. Logout:
   User clicks logout → POST /api/auth/logout
   → API invalidates refresh token → Frontend clears tokens
```

### Authorization Flow

```
Endpoint decorated with [Authorize(Policy = "Manager")]
→ User makes request with JWT token
→ Middleware validates token signature + expiration
→ Extracts user claims (including roles)
→ Checks if user has Manager or Admin role
→ If yes: allow access
→ If no: return 403 Forbidden
```

### Database Schema

**Users Table** (ApplicationUser):
- Id, UserName, NormalizedUserName
- Email, NormalizedEmail, EmailConfirmed
- PasswordHash, SecurityStamp, ConcurrencyStamp
- PhoneNumber, PhoneNumberConfirmed
- TwoFactorEnabled, LockoutEnd, LockoutEnabled, AccessFailedCount
- FirstName, LastName, DateOfBirth (custom fields)
- RefreshToken, RefreshTokenExpiryTime (custom fields)
- CreatedAt, UpdatedAt (custom fields)

**Roles Table**:
- Id, Name, NormalizedName, ConcurrencyStamp
- Seeded: ReadOnly, Manager, Admin

**UserRoles Table** (junction):
- UserId, RoleId

**Other Identity Tables**:
- UserClaims, UserLogins, RoleClaims, UserTokens

---

## Security Considerations

### Password Security
- Minimum 8 characters
- Requires uppercase, lowercase, digit, special character
- Hashed using ASP.NET Identity (PBKDF2)

### Token Security
- Access tokens short-lived (15 minutes)
- Refresh tokens longer-lived (7 days) but single-use
- Tokens signed with secret key (min 32 chars)
- Stored in httpOnly cookies or localStorage (frontend decision)

### Email Security
- Email confirmation required before login
- Confirmation tokens expire
- Rate limiting on email sends (future enhancement)

### Account Security
- Account lockout after 5 failed attempts (15 min)
- Password reset via email only
- Audit trail with CreatedAt/UpdatedAt timestamps

### API Security
- CORS configured for specific origins
- HTTPS enforced in production
- JWT validation on all protected endpoints
- Role-based authorization policies

---

## Environment Configuration

### Development (Local - Recommended)
- PostgreSQL: localhost:5432 (running on host, database: amr_grand_prix)
- MailHog SMTP: localhost:1025 (Docker)
- MailHog Web UI: http://localhost:8025 (Docker)
- API: http://localhost:8080 (running on host)
- Client: http://localhost:5173 (running on host)
- Connection String: Configured via .NET user secrets

### Development (Full Docker - Alternative)
- PostgreSQL: db:5432 (Docker container)
- MailHog SMTP: mailhog:1025 (Docker)
- MailHog Web UI: http://localhost:8025 (exposed)
- API: http://localhost:8080 (Docker, exposed)
- Client: http://localhost:5173 (Docker, exposed)

### Production (Future)
- Use environment variables for:
  - Database connection string
  - JWT secret (at least 64 chars)
  - SMTP credentials (SendGrid, AWS SES, etc.)
- Enable HTTPS redirection
- Set RequireHttpsMetadata = true for JWT
- Configure proper CORS origins
- Use secure cookie storage for tokens

---

## API Endpoints Summary

### Public Endpoints (No Auth Required)
- `POST /api/auth/register` - Create account
- `POST /api/auth/login` - Login
- `GET /api/auth/confirm-email` - Confirm email
- `POST /api/auth/forgot-password` - Request password reset
- `POST /api/auth/reset-password` - Reset password
- `GET /health` - Health check

### Authenticated Endpoints (Any Role)
- `POST /api/auth/logout` - Logout
- `POST /api/auth/refresh-token` - Refresh access token
- `GET /api/auth/me` - Get current user info

### ReadOnly Endpoints
- Race results (read)
- Standings (read)
- Statistics (read)
- Runner profiles (read)

### Manager Endpoints
- All ReadOnly permissions
- Races (create, update, delete)
- Results (create, update, delete)
- Race configuration

### Admin Endpoints
- All Manager permissions
- `GET /api/users` - List users
- `GET /api/users/{id}` - Get user
- `PUT /api/users/{id}/role` - Assign role
- `DELETE /api/users/{id}` - Delete user
- `PUT /api/users/{id}/lock` - Lock/unlock account
- System configuration

---

## Where We Left Off

### ✅ Completed:
- **Phase 1: Database & Infrastructure** - 100% complete
- All database configuration done
- All Identity setup complete
- JWT authentication configured
- Email service ready
- Authorization policies defined
- Roles auto-seeding on startup
- Initial migration created

### 🎯 Next Steps (Phase 2):

1. **Create DTOs folder and all authentication DTOs**
   - RegisterRequest, LoginRequest, LoginResponse
   - RefreshTokenRequest, UserResponse, etc.

2. **Create TokenService**
   - Implement JWT token generation
   - Implement refresh token logic
   - Token validation helpers

3. **Create AuthController**
   - Register endpoint
   - Login endpoint (with token generation)
   - Logout endpoint
   - Refresh token endpoint
   - Email confirmation endpoint
   - Password reset endpoints

4. **Create UserManagementController (Admin)**
   - List users
   - Get user details
   - Assign roles
   - Lock/unlock accounts

5. **Test backend with Postman/Thunder Client**
   - Register user
   - Confirm email via MailHog
   - Login and get tokens
   - Test protected endpoints
   - Test role-based authorization

### 📝 Notes:
- Database migration is ready but **NOT YET APPLIED**
  - Run `dotnet ef database update` when PostgreSQL is running
- MailHog will catch all emails sent in development
- Default JWT secret in appsettings.json should be changed for production
- Consider adding appsettings.Development.json to .gitignore if it contains secrets

---

## Quick Start Commands

### Start Development Environment
```bash
# Start Docker containers (PostgreSQL + MailHog)
./docker-dev.sh start

# Apply database migrations
cd AmrGrandPrix.API
dotnet ef database update

# Run API
dotnet run

# View emails
# Open http://localhost:8025 in browser
```

### Useful Commands
```bash
# Create new migration
dotnet ef migrations add MigrationName

# Update database
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName

# Remove last migration (if not applied)
dotnet ef migrations remove

# View logs
./docker-dev.sh logs db
./docker-dev.sh logs mailhog
```

---

## Resources

- [ASP.NET Core Identity Documentation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity)
- [JWT Bearer Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/)
- [Npgsql Entity Framework Core Provider](https://www.npgsql.org/efcore/)
- [MailHog Documentation](https://github.com/mailhog/MailHog)

---

**Last Updated**: November 10, 2025
**Status**: Phase 1 Complete, Ready for Phase 2
**Next Session**: Begin Phase 2 - Create DTOs and TokenService

# Testing the Authentication System

## Prerequisites

Before testing, ensure:

1. **PostgreSQL is running** (via Docker)
2. **Database migrations have been applied**
3. **MailHog is running** for email testing
4. **API is running** on port 8080
5. **Frontend is running** on port 5173

## Starting the Application

### Option 1: Run Locally (Recommended for Development)

```bash
# Terminal 1 - Start PostgreSQL and MailHog
./docker-dev.sh start

# Terminal 2 - Run API
cd AmrGrandPrix.API
dotnet ef database update  # Apply migrations (first time only)
dotnet run

# Terminal 3 - Run Frontend
cd AmrGrandPrix.Client
npm run dev
```

### Option 2: Run in Docker

```bash
./docker-dev.sh start
# Access frontend at http://localhost:5173
```

## Testing Checklist

### 1. User Registration Flow

1. Navigate to http://localhost:5173
   - Should redirect to `/login` (not authenticated)

2. Click "Register" link
   - Should navigate to `/register`

3. Fill out registration form:
   - Email: `test@example.com`
   - Password: `Test@1234`
   - Confirm Password: `Test@1234`
   - First Name: `Test`
   - Last Name: `User`

4. Submit the form
   - Should see success message
   - Should redirect to login page after 3 seconds

5. Check MailHog (http://localhost:8025)
   - Should see confirmation email
   - Click the confirmation link in the email

6. Should see "Email Confirmed!" page
   - Click "Go to Login"

### 2. Login Flow

1. On login page, enter credentials:
   - Email: `test@example.com`
   - Password: `Test@1234`

2. Submit the form
   - Should redirect to home page (`/`)
   - Should see welcome message with user name
   - Should see "Roles: ReadOnly"

3. Verify user is authenticated:
   - Refresh the page
   - Should remain on home page (not redirect to login)

### 3. Protected Routes

1. While logged in, try accessing `/unauthorized`
   - Should see Unauthorized page

2. Try accessing non-existent route
   - Should redirect to home page

### 4. Logout Flow

1. On home page, click "Logout" button
   - Should redirect to login page

2. Try accessing home page after logout
   - Should redirect to `/login`

### 5. Email Not Confirmed Flow

1. Register a new user but DON'T click the confirmation link
   - Email: `test2@example.com`
   - Password: `Test@1234`

2. Try to login immediately
   - Should fail with error message about email confirmation

3. Check MailHog for confirmation email
   - Click the link to confirm
   - Now try logging in again
   - Should succeed

### 6. Invalid Login Attempts

Test various invalid scenarios:

1. **Wrong password**:
   - Email: `test@example.com`
   - Password: `WrongPassword123`
   - Should show error message

2. **Non-existent user**:
   - Email: `doesnotexist@example.com`
   - Password: `Test@1234`
   - Should show error message

3. **Invalid email format**:
   - Email: `notanemail`
   - Should show validation error

4. **Empty fields**:
   - Leave fields blank and submit
   - Should show validation errors

### 7. Password Validation

Try registering with weak passwords:

1. Too short: `Test@1`
   - Should show error: "Password must be at least 8 characters"

2. No uppercase: `test@1234`
   - Should show error about character requirements

3. No special character: `Test1234`
   - Should show error about character requirements

4. Mismatched passwords:
   - Password: `Test@1234`
   - Confirm: `Test@12345`
   - Should show error: "Passwords do not match"

### 8. Token Refresh (Advanced)

1. Login and wait 15 minutes
   - Access token should auto-refresh
   - User should remain logged in

2. Check browser console for refresh activity

### 9. Role-Based Access (Future)

Once role-based routes are added:

1. Login as ReadOnly user
   - Try accessing Manager endpoint
   - Should see Unauthorized page

2. Have admin assign Manager role
   - Try accessing Manager endpoint again
   - Should now have access

## Expected API Endpoints

The following endpoints should be available:

- `POST /api/auth/register` - Create account
- `POST /api/auth/login` - Login
- `POST /api/auth/logout` - Logout
- `POST /api/auth/refresh-token` - Refresh access token
- `GET /api/auth/confirm-email` - Confirm email
- `POST /api/auth/resend-confirmation` - Resend confirmation email (if implemented)

## Troubleshooting

### Frontend can't reach API

**Problem**: Network errors when trying to login/register

**Solutions**:
1. Check API is running on port 8080: `curl http://localhost:8080/health`
2. Check Vite proxy configuration in `vite.config.js`
3. Check browser console for CORS errors
4. Verify API CORS settings in `Program.cs`

### Database errors

**Problem**: API throws database-related errors

**Solutions**:
1. Check PostgreSQL is running: `docker ps`
2. Apply migrations: `cd AmrGrandPrix.API && dotnet ef database update`
3. Check connection string in `appsettings.json`

### Email not being sent

**Problem**: No email in MailHog

**Solutions**:
1. Check MailHog is running: `docker ps`
2. Access MailHog UI: http://localhost:8025
3. Check API email configuration in `appsettings.json`
4. Check API logs for email send errors

### Token issues

**Problem**: User gets logged out unexpectedly

**Solutions**:
1. Check browser localStorage for tokens
2. Check token expiration in JWT settings
3. Verify refresh token logic is working
4. Check browser console for errors

### Styling issues

**Problem**: Forms look unstyled

**Solutions**:
1. Verify `auth.css` is being imported
2. Check browser console for CSS loading errors
3. Clear browser cache

## Success Criteria

Frontend auth implementation is successful when:

- [x] Users can register new accounts
- [x] Confirmation emails are sent and links work
- [x] Users can login with confirmed accounts
- [x] Authenticated users can access protected routes
- [x] Unauthenticated users are redirected to login
- [x] Users can logout successfully
- [x] Tokens are stored and used correctly
- [x] Token refresh works automatically
- [x] Form validation works properly
- [x] Error messages are clear and helpful
- [x] UI is responsive and looks good

## Next Steps

After basic auth is working:

1. Add password reset functionality
2. Add "Remember Me" option
3. Add user profile editing
4. Add admin user management UI
5. Add role-based navigation/features
6. Add loading spinners for better UX
7. Add toast notifications for success/error messages
8. Add form libraries (React Hook Form) for better validation
9. Add UI component library (Tailwind, MUI, etc.)
10. Add comprehensive error handling

## Notes

- All passwords must meet requirements: 8+ chars, uppercase, lowercase, number, special character
- Email confirmation is required before login
- New users are assigned "ReadOnly" role by default
- Admins can upgrade roles via API (UI not yet implemented)
- Tokens are stored in localStorage (consider httpOnly cookies for production)

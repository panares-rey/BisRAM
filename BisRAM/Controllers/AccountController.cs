// ============================================================
// AccountController.cs — Handles all USER ACCOUNT actions:
//   Login, Register, Logout, Profile, Avatar Upload,
//   Forgot Password, and Reset Password
//
// URL prefix: /Account/...
// ============================================================

using Microsoft.AspNetCore.Mvc;
using BisRAM.Data;   // For DbHelper (database access)
using BisRAM.Models; // For User, LoginViewModel, RegisterViewModel

namespace BisRAM.Controllers
{
    public class AccountController : Controller
    {
        private readonly DbHelper _db;          // Database helper for all DB operations
        private readonly IWebHostEnvironment _env; // Provides info about the server's file system (for saving uploads)

        // ── CONSTRUCTOR ──
        // Both DbHelper and IWebHostEnvironment are injected automatically by ASP.NET.
        public AccountController(DbHelper db, IWebHostEnvironment env) { _db = db; _env = env; }

        // ── HELPER PROPERTY ──
        // UserId reads the current logged-in user's ID from the session.
        // Returns null if nobody is logged in (session is empty).
        // This property is used throughout the controller to check login status.
        private int? UserId => HttpContext.Session.GetInt32("UserId");

        // ══════════════════════════════════════════════════════════════
        // ── LOGIN ──
        // ══════════════════════════════════════════════════════════════

        // GET /Account/Login — Shows the login form (empty)
        public IActionResult Login() => View();

        // POST /Account/Login — Processes the submitted login form
        [HttpPost] // This attribute means: this action only runs when the browser sends a POST request
        public IActionResult Login(LoginViewModel model) // 'model' is auto-filled from the form fields
        {
            // Look up the user in the database by their email address
            var user = _db.GetUserByEmail(model.Email);

            // Check if user exists AND if the entered password matches the stored hash.
            // BCrypt.Verify() safely compares a plain password to a hashed one.
            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                // Wrong credentials — show an error message and re-display the login form
                ViewBag.Error = "Invalid email or password.";
                return View(model); // Pass 'model' back so the form keeps their email
            }

            // Check if the account has been suspended by an admin
            if (!user.IsActive)
            {
                ViewBag.Error = "Your account has been suspended. Contact support.";
                return View(model);
            }

            // ── LOGIN SUCCESSFUL — Store user info in the session ──
            // The session acts like the server's memory of who is currently logged in.
            // These values can be read from the session anywhere in the app.
            HttpContext.Session.SetInt32("UserId", user.Id);           // Save user ID
            HttpContext.Session.SetString("UserName", user.FullName);  // Save full name
            // Save username, falling back to FullName if username is empty
            HttpContext.Session.SetString("UserUsername", string.IsNullOrEmpty(user.Username) ? user.FullName : user.Username);
            HttpContext.Session.SetString("UserRole", user.Role);                          // Save role (Customer or Admin)
            HttpContext.Session.SetString("UserAvatar", user.AvatarUrl ?? "/images/avatars/default.svg"); // Save avatar URL

            // Redirect admins to the Admin Dashboard, regular users to the Home page
            if (user.Role == "Admin") return RedirectToAction("Dashboard", "Admin");
            return RedirectToAction("Index", "Home");
        }

        // ══════════════════════════════════════════════════════════════
        // ── REGISTER ──
        // ══════════════════════════════════════════════════════════════

        // GET /Account/Register — Shows the registration form (empty)
        public IActionResult Register() => View();

        // POST /Account/Register — Processes the submitted registration form
        [HttpPost]
        public IActionResult Register(RegisterViewModel model)
        {
            // ── VALIDATION CHECKS (each returns early with an error if it fails) ──

            // Check 1: Passwords must match
            if (model.Password != model.ConfirmPassword)
            { ViewBag.Error = "Passwords do not match."; return View(model); }

            // Check 2: Password must meet security requirements (see IsStrongPassword below)
            if (!IsStrongPassword(model.Password))
            { ViewBag.Error = "Password must be at least 8 characters, include uppercase, lowercase, a number, and a special character (!@#$%^&*)."; return View(model); }

            // Check 3: Email must not already be registered
            if (_db.GetUserByEmail(model.Email) != null)
            { ViewBag.Error = "Email is already registered."; return View(model); }

            // Check 4: Username must not be blank
            if (string.IsNullOrWhiteSpace(model.Username))
            { ViewBag.Error = "Username is required."; return View(model); }

            // Check 5: Username length must be between 3 and 20 characters
            if (model.Username.Length < 3 || model.Username.Length > 20)
            { ViewBag.Error = "Username must be 3–20 characters."; return View(model); }

            // Check 6: Username can only contain letters, numbers, and underscores (regex check)
            // @"^[a-zA-Z0-9_]+$" means: only alphanumeric characters and underscores, nothing else
            if (!System.Text.RegularExpressions.Regex.IsMatch(model.Username, @"^[a-zA-Z0-9_]+$"))
            { ViewBag.Error = "Username can only contain letters, numbers, and underscores."; return View(model); }

            // Check 7: Username must not already be taken by another user
            if (_db.GetUserByUsername(model.Username) != null)
            { ViewBag.Error = "That username is already taken."; return View(model); }

            // ── ALL CHECKS PASSED — Create the new user ──
            var user = new User
            {
                FullName = model.FullName,
                Username = model.Username,
                Email = model.Email,
                // BCrypt.HashPassword() converts the plain password into a secure hash before saving.
                // The original password is never stored in the database.
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Phone = model.Phone,
                Region = model.Region,
                Province = model.Province,
                City = model.City,
                Barangay = model.Barangay,
                ZipCode = model.ZipCode,
                StreetAddress = model.StreetAddress,
                Role = "Customer",                                // New users are always Customers
                AvatarUrl = "/images/avatars/default.svg"         // Start with the default avatar
            };
            _db.CreateUser(user); // Save the new user to the database

            // TempData stores a message that survives ONE redirect (shows up on the next page)
            TempData["Success"] = "Account created! You can now log in.";
            return RedirectToAction("Login"); // Redirect to the login page
        }

        // ══════════════════════════════════════════════════════════════
        // ── LOGOUT ──
        // ══════════════════════════════════════════════════════════════

        // GET /Account/Logout — Clears the session and sends the user to the home page
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Remove ALL data from the session (effectively logs out the user)
            return RedirectToAction("Index", "Home");
        }

        // ══════════════════════════════════════════════════════════════
        // ── PROFILE ──
        // ══════════════════════════════════════════════════════════════

        // GET /Account/Profile — Shows the logged-in user's profile page
        public IActionResult Profile()
        {
            // If not logged in, redirect to login page
            if (UserId == null) return RedirectToAction("Login");

            // Load the user's data from the database
            var user = _db.GetUserById(UserId.Value);

            // Load the user's past orders and marketplace listings for display on the profile
            var orders = _db.GetOrdersByUser(UserId.Value);
            var listings = _db.GetListingsBySeller(UserId.Value);

            // Attach orders and listings to ViewBag so the View can access them
            ViewBag.Orders = orders;
            ViewBag.Listings = listings;

            // Pass the user object as the main model to the View
            return View(user);
        }

        // POST /Account/UpdateProfile — Saves changes to the user's profile info
        [HttpPost]
        public IActionResult UpdateProfile(string fullName, string phone, string region, string province,
            string city, string barangay, string zipCode, string streetAddress)
        {
            if (UserId == null) return RedirectToAction("Login");

            // Update the user's profile in the database
            _db.UpdateUserProfile(UserId.Value, fullName, phone, region, province, city, barangay, zipCode, streetAddress);

            // Update the session with the new name so the navbar shows the correct name
            HttpContext.Session.SetString("UserName", fullName);

            // Reload user to get the username and update that session value too
            var updatedUser = _db.GetUserById(UserId.Value);
            HttpContext.Session.SetString("UserUsername", string.IsNullOrEmpty(updatedUser?.Username) ? fullName : updatedUser.Username);

            TempData["Success"] = "Profile updated!";
            return RedirectToAction("Profile");
        }

        // POST /Account/UploadAvatar — Saves a new profile picture uploaded by the user
        // 'async Task' means this method runs asynchronously (without blocking) while saving the file
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatarFile)
        {
            if (UserId == null) return RedirectToAction("Login");

            // Check that a file was actually uploaded and is not empty
            if (avatarFile != null && avatarFile.Length > 0)
            {
                // Get the file extension (e.g., ".jpg", ".png") in lowercase
                var ext = Path.GetExtension(avatarFile.FileName).ToLower();

                // Only allow image file types for security (prevent uploading .exe, etc.)
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".webp")
                {
                    // Create a unique filename using the user ID + current Unix timestamp
                    // This prevents overwriting other users' avatars and avoids name collisions
                    var fileName = "avatar_" + UserId.Value + "_" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() + ext;

                    // Build the full folder path where the file will be saved on the server
                    // _env.WebRootPath = the wwwroot folder (e.g., C:\project\wwwroot)
                    var uploadPath = Path.Combine(_env.WebRootPath, "uploads", "avatars");

                    // Create the folder if it doesn't already exist
                    Directory.CreateDirectory(uploadPath);

                    // Build the full file path (folder + filename)
                    var filePath = Path.Combine(uploadPath, fileName);

                    // Open a write stream and copy the uploaded file into it
                    // 'using' ensures the stream is closed/disposed when done
                    using var stream = new FileStream(filePath, FileMode.Create);
                    await avatarFile.CopyToAsync(stream); // 'await' waits for the async file copy to complete

                    // Build the URL path that the browser will use to load the image
                    var avatarUrl = "/uploads/avatars/" + fileName;

                    // Save the new avatar URL to the database
                    _db.UpdateAvatar(UserId.Value, avatarUrl);

                    // Update the session so the navbar shows the new avatar immediately
                    HttpContext.Session.SetString("UserAvatar", avatarUrl);
                    TempData["Success"] = "Profile picture updated!";
                }
                else
                {
                    // File is not an image — show an error
                    TempData["Error"] = "Only image files are allowed (jpg, png, gif, webp).";
                }
            }

            return RedirectToAction("Profile"); // Always go back to profile page after upload
        }

        // ══════════════════════════════════════════════════════════════
        // ── FORGOT PASSWORD ──
        // ══════════════════════════════════════════════════════════════

        // GET /Account/ForgotPassword — Shows the forgot password form
        public IActionResult ForgotPassword() => View();

        // POST /Account/ForgotPassword — Processes the forgot password request
        [HttpPost]
        public IActionResult ForgotPassword(string email)
        {
            // Check if an account with that email exists
            var user = _db.GetUserByEmail(email);
            if (user == null)
            {
                ViewBag.Error = "No account found with that email.";
                return View();
            }

            // Generate a random unique token (a GUID without dashes, e.g., "a1b2c3d4e5f6...")
            // This token is used in the reset link so only someone with the link can reset the password
            var token = Guid.NewGuid().ToString("N");

            // Save the token to the user's record in the database, with a 1-hour expiry
            _db.SetResetToken(user.Id, token, DateTime.UtcNow.AddHours(1));

            // In production, this token would be emailed to the user as a link.
            // For now, we pass it through TempData so it shows up on the next page for testing.
            TempData["ResetToken"] = token;
            TempData["Success"] = "Reset link generated. (In production this would be emailed.)";

            // Redirect to the reset password page with the token in the URL
            return RedirectToAction("ResetPassword", new { token });
        }

        // GET /Account/ResetPassword?token=... — Shows the reset password form
        public IActionResult ResetPassword(string token)
        {
            // Look up who owns this token in the database
            var user = _db.GetUserByResetToken(token);

            // If token doesn't exist or has expired, reject it
            if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
            {
                TempData["Error"] = "Reset link is invalid or expired.";
                return RedirectToAction("ForgotPassword");
            }

            // Pass the token to the view so it can be included in the form submission
            ViewBag.Token = token;
            return View();
        }

        // POST /Account/ResetPassword — Saves the new password
        [HttpPost]
        public IActionResult ResetPassword(string token, string newPassword, string confirmPassword)
        {
            // Check passwords match
            if (newPassword != confirmPassword)
            { ViewBag.Error = "Passwords do not match."; ViewBag.Token = token; return View(); }

            // Check password strength
            if (!IsStrongPassword(newPassword))
            { ViewBag.Error = "Password must be at least 8 characters with uppercase, number, and special character."; ViewBag.Token = token; return View(); }

            // Look up the user by their reset token
            var user = _db.GetUserByResetToken(token);
            if (user == null)
            { TempData["Error"] = "Invalid token."; return RedirectToAction("ForgotPassword"); }

            // Hash the new password and save it to the database.
            // The token is also cleared so it cannot be reused.
            _db.UpdatePassword(user.Id, BCrypt.Net.BCrypt.HashPassword(newPassword));

            TempData["Success"] = "Password reset successfully! Please log in.";
            return RedirectToAction("Login");
        }

        // ── PRIVATE HELPER METHOD ──
        // IsStrongPassword() checks if a password meets the security requirements.
        // Returns true if the password passes all checks, false if any check fails.
        // 'private' means only this class can use this method.
        private bool IsStrongPassword(string password)
        {
            if (password.Length < 8) return false;                                              // Must be at least 8 characters long
            if (!password.Any(char.IsUpper)) return false;                                      // Must contain at least one uppercase letter
            if (!password.Any(char.IsLower)) return false;                                      // Must contain at least one lowercase letter
            if (!password.Any(char.IsDigit)) return false;                                      // Must contain at least one number (0-9)
            if (!password.Any(c => "!@#$%^&*()_+-=[]{}|;':\",./<>?".Contains(c))) return false; // Must contain at least one special character
            return true; // All checks passed — password is strong
        }
    }
}

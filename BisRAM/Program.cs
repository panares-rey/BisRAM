// ============================================================
// Program.cs — The ENTRY POINT of the BisRAM web application
// This is the very first file that runs when you start the app.
// It sets up all the services and tells ASP.NET how to handle requests.
// ============================================================

using BisRAM.Data; // Import our DbHelper class so we can use it here

// ── STEP 1: CREATE THE APPLICATION BUILDER ──
// WebApplication.CreateBuilder sets up a blank web app with default settings.
// 'args' are command-line arguments passed when starting the app (usually empty).
var builder = WebApplication.CreateBuilder(args);

// ── STEP 2: REGISTER SERVICES (things the app needs to function) ──

// AddControllersWithViews() tells ASP.NET that this is an MVC app:
//   - Controllers = C# classes that handle incoming web requests
//   - Views       = .cshtml files that generate the HTML pages shown to users
builder.Services.AddControllersWithViews();

// AddSession() enables SESSION SUPPORT — sessions let the app remember who is logged in.
// Think of a session like a temporary "notepad" the server keeps for each user.
builder.Services.AddSession(options => {
    // IdleTimeout: if the user does nothing for 60 minutes, they get logged out automatically
    options.IdleTimeout = TimeSpan.FromMinutes(60);

    // HttpOnly: the session cookie cannot be accessed by JavaScript (protects against hacking)
    options.Cookie.HttpOnly = true;

    // IsEssential: the cookie is required for the app to work (needed for GDPR compliance)
    options.Cookie.IsEssential = true;
});

// AddSingleton<DbHelper>() registers our database helper class.
// Singleton means ONE instance is created and shared across the whole app.
// Any controller that needs the database just asks for DbHelper and gets this shared copy.
builder.Services.AddSingleton<DbHelper>();

// ── STEP 3: BUILD THE APPLICATION ──
// This finalizes all the services registered above and creates the app object.
var app = builder.Build();

// ── STEP 4: INITIALIZE THE DATABASE ──
// Get the DbHelper that was registered as a singleton above
var db = app.Services.GetRequiredService<DbHelper>();

// Call InitializeDatabase() to create the database tables if they don't exist yet
// and seed it with default admin account + sample products.
db.InitializeDatabase();

// ── STEP 5: CONFIGURE THE HTTP REQUEST PIPELINE (Middleware) ──
// Middleware is code that runs on every request before reaching the controllers.
// The ORDER these are added matters — each one passes the request to the next.

// If NOT in development mode (i.e., in production), use a friendly error page
// instead of showing detailed error messages (which could be a security risk).
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Home/Error");

// UseStaticFiles() allows the browser to download files from the wwwroot folder:
// CSS files, JavaScript files, images, etc.
app.UseStaticFiles();

// UseRouting() enables URL routing — matches incoming URLs to the correct controller/action.
// Example: "/Cart/Index" → CartController → Index() method
app.UseRouting();

// UseSession() activates the session system we configured above.
// This MUST come AFTER UseRouting() and BEFORE UseAuthorization().
app.UseSession();

// UseAuthorization() enables the authorization system (checking if a user has permission).
// Even though BisRAM uses manual session checks, this is still required by ASP.NET.
app.UseAuthorization();

// ── STEP 6: DEFINE THE DEFAULT URL ROUTE ──
// This tells the app how to map URLs to controllers and actions.
// Pattern: /{controller}/{action}/{id?}
// Defaults: if no controller given → use "Home", if no action given → use "Index"
// Examples:
//   "/"                        → HomeController.Index()
//   "/Account/Login"           → AccountController.Login()
//   "/Products/Details/5"      → ProductsController.Details(id: 5)
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

// ── STEP 7: START THE APPLICATION ──
// app.Run() starts the web server and begins listening for incoming HTTP requests.
// The application will keep running until it is manually stopped.
app.Run();

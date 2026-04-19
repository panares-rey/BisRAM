// ============================================================
// DbHelper.cs — The DATABASE HELPER for BisRAM
//
// This is the most important backend file. It handles ALL database
// operations: reading, writing, updating, and deleting data.
//
// The database used is SQLite — a file-based database stored in
// the Data/bisram.db file. No separate database server is needed.
//
// This file has two parts:
//   1. DbHelper class — all the methods that controllers call
//   2. SqliteExtensions class — helper methods that make SQL queries easier
// ============================================================

using Microsoft.Data.Sqlite; // The library for talking to SQLite databases
using BisRAM.Models;          // For Product, User, Order, etc.

namespace BisRAM.Data
{
    public class DbHelper
    {
        // _connectionString stores the "address" of the database file.
        // It's read from appsettings.json (key: "ConnectionStrings:DefaultConnection").
        private readonly string _connectionString;

        // ── CONSTRUCTOR ──
        // Called once when the app starts (it's a Singleton in Program.cs).
        // Reads the connection string from configuration and ensures the Data folder exists.
        public DbHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? "Data Source=Data/bisram.db"; // Fallback default path

            // Extract the folder path from the connection string and create it if missing
            // Example: "Data Source=Data/bisram.db" → folder = "Data"
            var dir = Path.GetDirectoryName(_connectionString.Replace("Data Source=", ""));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        // ── GetConnection() ──
        // Creates and returns a new SQLite database connection each time it's called.
        // Using a new connection per method is the recommended pattern for SQLite.
        private SqliteConnection GetConnection() => new SqliteConnection(_connectionString);

        // ════════════════════════════════════════════════════════════════
        // ── INITIALIZE DATABASE ──
        // Creates all the tables and adds default data if they don't exist yet.
        // Called once on startup from Program.cs.
        // ════════════════════════════════════════════════════════════════
        public void InitializeDatabase()
        {
            using var conn = GetConnection();
            conn.Open(); // Open the connection to the database file

            // Execute all CREATE TABLE IF NOT EXISTS statements.
            // "IF NOT EXISTS" means it won't fail if the tables already exist.
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,  -- Auto-incrementing unique ID
                    FullName TEXT NOT NULL,
                    Username TEXT NOT NULL DEFAULT '',
                    Email TEXT NOT NULL UNIQUE,            -- UNIQUE: no two users can have the same email
                    PasswordHash TEXT NOT NULL,
                    Role TEXT NOT NULL DEFAULT 'Customer',
                    Address TEXT,
                    Phone TEXT,
                    AvatarUrl TEXT DEFAULT '/images/avatars/default.svg',
                    Region TEXT,
                    Province TEXT,
                    City TEXT,
                    Barangay TEXT,
                    ZipCode TEXT,
                    StreetAddress TEXT,
                    ResetToken TEXT,                       -- Temporary password reset token
                    ResetTokenExpiry TEXT,                 -- When the reset token expires
                    IsActive INTEGER NOT NULL DEFAULT 1,   -- 1=active, 0=suspended
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    Category TEXT,
                    Brand TEXT,
                    Price REAL NOT NULL,                   -- REAL = decimal number (₱)
                    Stock INTEGER NOT NULL DEFAULT 0,
                    ImageUrl TEXT DEFAULT '/images/products/default.jpg',
                    IsActive INTEGER NOT NULL DEFAULT 1,   -- 1=visible, 0=soft-deleted
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS CartItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,               -- Who owns this cart item
                    ProductId INTEGER,                     -- Set if item is a store product (null for listings)
                    ListingId INTEGER,                     -- Set if item is a marketplace listing (null for products)
                    Quantity INTEGER NOT NULL DEFAULT 1
                );
                CREATE TABLE IF NOT EXISTS Orders (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    FullName TEXT NOT NULL,
                    Address TEXT NOT NULL,                 -- Full address combined into one string
                    Region TEXT,
                    Province TEXT,
                    City TEXT,
                    Barangay TEXT,
                    ZipCode TEXT,
                    Phone TEXT NOT NULL,
                    PaymentMethod TEXT NOT NULL,           -- e.g. 'Cash on Delivery', 'GCash'
                    PaymentReference TEXT,                 -- Reference # for e-wallet payments
                    Status TEXT NOT NULL DEFAULT 'Pending', -- 'Pending','Processing','Shipped','Delivered','Cancelled'
                    Total REAL NOT NULL,
                    EstimatedDelivery TEXT,                -- Stored as ISO date string
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS OrderItems (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    OrderId INTEGER NOT NULL,              -- Which order this belongs to
                    ProductId INTEGER NOT NULL,
                    ProductName TEXT NOT NULL,             -- Saved snapshot of name (in case product is deleted later)
                    Price REAL NOT NULL,                   -- Saved snapshot of price at time of purchase
                    Quantity INTEGER NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Listings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SellerId INTEGER NOT NULL,             -- User ID of the seller
                    SellerName TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    Category TEXT,
                    Condition TEXT DEFAULT 'Used',         -- 'New','Like New','Used','For Parts'
                    Price REAL NOT NULL,
                    Stock INTEGER NOT NULL DEFAULT 1,
                    ImageUrl TEXT DEFAULT '/images/products/default.jpg',
                    Status TEXT DEFAULT 'Active',          -- 'Active' or 'Removed'
                    CreatedAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE IF NOT EXISTS Messages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SenderId INTEGER NOT NULL,
                    ReceiverId INTEGER NOT NULL,
                    SenderName TEXT NOT NULL,
                    ListingId INTEGER,                     -- Optional: which listing this message is about
                    Content TEXT NOT NULL,
                    IsRead INTEGER NOT NULL DEFAULT 0,     -- 0=unread, 1=read
                    SentAt TEXT DEFAULT CURRENT_TIMESTAMP
                );
            ");

            // After creating tables, seed them with initial data
            SeedData(conn);
        }

        // ── SeedData() ──
        // Inserts default admin account and sample products if the tables are empty.
        // Only runs once (checks if data already exists before inserting).
        private void SeedData(SqliteConnection conn)
        {
            // Check if an admin account exists; if not, create one
            var adminExists = conn.QueryScalar<int>("SELECT COUNT(*) FROM Users WHERE Role='Admin'");
            if (adminExists == 0)
            {
                // Hash the default admin password before storing it
                var hash = BCrypt.Net.BCrypt.HashPassword("Admin@123");
                conn.Execute("INSERT INTO Users (FullName,Email,PasswordHash,Role,AvatarUrl) VALUES ('Administrator','admin@bisram.com',@h,'Admin','/images/avatars/default.svg')", new { h = hash });
            }

            // Check if any products exist; if not, insert 20 sample products
            var pc = conn.QueryScalar<int>("SELECT COUNT(*) FROM Products");
            if (pc == 0)
            {
                // Each tuple: (Name, Description, Category, Brand, Price, Stock, ImageUrl)
                var products = new List<(string N, string D, string C, string B, decimal P, int S, string I)>
                {
                    ("Intel Core i9-14900K","24-core processor, up to 6.0GHz Turbo","CPU","Intel",59999m,15,"/images/products/intel-core-i9-14900k.jpg"),
                    ("AMD Ryzen 9 7950X","16-core AM5 processor, 5nm Zen4","CPU","AMD",54999m,12,"/images/products/amd-ryzen-9-7950x.jpg"),
                    ("Intel Core i5-13600K","14-core mid-range gaming CPU","CPU","Intel",28999m,30,"/images/products/intel-core-i5-13600k.jpg"),
                    ("NVIDIA RTX 4090","24GB GDDR6X flagship GPU","GPU","NVIDIA",159999m,8,"/images/products/nvidia-rtx-4090.jpg"),
                    ("NVIDIA RTX 4070 Ti","12GB GDDR6X high-performance GPU","GPU","NVIDIA",79999m,20,"/images/products/nvidia-rtx-4070-ti.jpg"),
                    ("AMD Radeon RX 7900 XTX","24GB GDDR6 flagship AMD GPU","GPU","AMD",99999m,10,"/images/products/amd-radeon-rx-7900-xtx.jpg"),
                    ("Corsair Vengeance DDR5 32GB","32GB DDR5-5600 CL36 RAM Kit","RAM","Corsair",14999m,50,"/images/products/corsair-vengeance-ddr5-32gb.jpg"),
                    ("G.Skill Trident Z5 64GB","64GB DDR5-6400 RGB Memory Kit","RAM","G.Skill",24999m,25,"/images/products/gskill-trident-z5-64gb.jpg"),
                    ("Samsung 990 Pro 2TB","PCIe 4.0 NVMe SSD 7450MB/s","Storage","Samsung",17999m,40,"/images/products/samsung-990-pro-2tb.jpg"),
                    ("WD Black SN850X 1TB","PCIe 4.0 NVMe gaming SSD","Storage","Western Digital",12999m,35,"/images/products/wd-black-sn850x-1tb.jpg"),
                    ("Seagate BarraCuda 4TB","3.5in HDD 5400RPM storage","Storage","Seagate",7999m,60,"/images/products/seagate-barracuda-4tb.jpg"),
                    ("ASUS ROG Maximus Z790","Intel Z790 ATX flagship board","Motherboard","ASUS",59999m,10,"/images/products/asus-rog-maximus-z790.jpg"),
                    ("MSI MAG B650 TOMAHAWK","AMD AM5 ATX gaming motherboard","Motherboard","MSI",24999m,20,"/images/products/msi-mag-b650-tomahawk.jpg"),
                    ("Gigabyte B760M DS3H","Intel LGA1700 Micro-ATX board","Motherboard","Gigabyte",11999m,30,"/images/products/gigabyte-b760m-ds3h.jpg"),
                    ("Corsair RM1000x","1000W 80+ Gold Fully Modular PSU","PSU","Corsair",17999m,20,"/images/products/corsair-rm1000x.jpg"),
                    ("be quiet! Straight Power 850W","850W 80+ Platinum modular PSU","PSU","be quiet!",14999m,18,"/images/products/bequiet-straight-power-850w.jpg"),
                    ("NZXT H510 Flow","Mid-tower ATX mesh front panel","Case","NZXT",8999m,25,"/images/products/nzxt-h510-flow.jpg"),
                    ("Lian Li O11 Dynamic EVO","Premium dual-chamber ATX tower","Case","Lian Li",16999m,15,"/images/products/lian-li-o11-dynamic-evo.jpg"),
                    ("Noctua NH-D15","Premium dual-tower CPU air cooler","Cooling","Noctua",9999m,30,"/images/products/noctua-nh-d15.jpg"),
                    ("ARCTIC Liquid Freezer III 360","360mm AIO liquid CPU cooler","Cooling","ARCTIC",11999m,22,"/images/products/arctic-liquid-freezer-iii-360.jpg"),
                };

                // Insert each product using the SQL INSERT statement
                foreach (var p in products)
                    conn.Execute("INSERT INTO Products (Name,Description,Category,Brand,Price,Stock,ImageUrl) VALUES (@n,@d,@c,@b,@p,@s,@i)",
                        new { n=p.N, d=p.D, c=p.C, b=p.B, p=p.P, s=p.S, i=p.I });
            }
        }

        // ════════════════════════════════════════════════════════════════
        // ── USERS ──
        // ════════════════════════════════════════════════════════════════

        // Find a user by their username (used during registration to check if username is taken)
        public User? GetUserByUsername(string username) {
            using var c = GetConnection(); c.Open();
            return c.QueryOne<User>("SELECT * FROM Users WHERE Username=@u", new { u = username });
        }

        // Find a user by their email address (used during login and forgot password)
        public User? GetUserByEmail(string email) {
            using var c = GetConnection(); c.Open();
            return c.QueryOne<User>("SELECT * FROM Users WHERE Email=@e", new { e = email });
        }

        // Find a user by their numeric ID (used when loading profile, session data, etc.)
        public User? GetUserById(int id) {
            using var c = GetConnection(); c.Open();
            return c.QueryOne<User>("SELECT * FROM Users WHERE Id=@id", new { id });
        }

        // Create a new user account in the database.
        // Returns true if successful, false if there was an error (e.g., duplicate email).
        public bool CreateUser(User user) {
            using var c = GetConnection(); c.Open();
            try {
                c.Execute("INSERT INTO Users (FullName,Username,Email,PasswordHash,Role,Phone,Region,Province,City,Barangay,ZipCode,StreetAddress,AvatarUrl) VALUES (@FullName,@Username,@Email,@PasswordHash,@Role,@Phone,@Region,@Province,@City,@Barangay,@ZipCode,@StreetAddress,@AvatarUrl)", user);
                return true;
            } catch { return false; } // Catch duplicate email errors etc.
        }

        // Get a list of ALL users (used by the admin Users page)
        public List<User> GetAllUsers() {
            using var c = GetConnection(); c.Open();
            return c.QueryList<User>("SELECT * FROM Users ORDER BY CreatedAt DESC");
        }

        // Update a user's profile info (name, phone, and Philippine address fields)
        public void UpdateUserProfile(int id, string fullName, string phone, string region,
            string province, string city, string barangay, string zip, string street)
        {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Users SET FullName=@fn,Phone=@ph,Region=@r,Province=@pv,City=@ci,Barangay=@ba,ZipCode=@zp,StreetAddress=@st WHERE Id=@id",
                new { fn=fullName, ph=phone, r=region, pv=province, ci=city, ba=barangay, zp=zip, st=street, id });
        }

        // Update only the user's avatar URL (called after uploading a new profile picture)
        public void UpdateAvatar(int id, string avatarUrl) {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Users SET AvatarUrl=@a WHERE Id=@id", new { a=avatarUrl, id });
        }

        // Save a password reset token and its expiry time to the user's record
        public void SetResetToken(int id, string token, DateTime expiry) {
            using var c = GetConnection(); c.Open();
            // Store the expiry as an ISO 8601 string (SQLite doesn't have a native DateTime type)
            c.Execute("UPDATE Users SET ResetToken=@t,ResetTokenExpiry=@e WHERE Id=@id", new { t=token, e=expiry.ToString("o"), id });
        }

        // Find a user by their password reset token (used to validate the reset link)
        public User? GetUserByResetToken(string token) {
            using var c = GetConnection(); c.Open();
            return c.QueryOne<User>("SELECT * FROM Users WHERE ResetToken=@t", new { t=token });
        }

        // Update a user's password hash and clear the reset token (so it can't be reused)
        public void UpdatePassword(int id, string hash) {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Users SET PasswordHash=@h,ResetToken=NULL,ResetTokenExpiry=NULL WHERE Id=@id", new { h=hash, id });
        }

        // Enable or disable a user account (admin feature)
        public void ToggleUserActive(int id, bool active) {
            using var c = GetConnection(); c.Open();
            // SQLite stores booleans as integers: 1=active, 0=inactive
            c.Execute("UPDATE Users SET IsActive=@a WHERE Id=@id", new { a=active?1:0, id });
        }

        // ════════════════════════════════════════════════════════════════
        // ── PRODUCTS ──
        // ════════════════════════════════════════════════════════════════

        // Get all products. If includeInactive is true, includes soft-deleted products (for admin).
        public List<Product> GetAllProducts(bool includeInactive = false) {
            using var c = GetConnection(); c.Open();
            // Dynamically include or exclude the WHERE clause for inactive products
            return c.QueryList<Product>($"SELECT * FROM Products {(includeInactive ? "" : "WHERE IsActive=1")} ORDER BY Name");
        }

        // Search and filter products with up to 5 optional filters.
        // This builds the SQL query dynamically based on which filters are provided.
        public List<Product> SearchProducts(string? q, string? cat, string? brand, decimal? min, decimal? max, string sort = "name")
        {
            using var conn = GetConnection(); conn.Open();
            var sql = "SELECT * FROM Products WHERE IsActive=1"; // Start with all active products
            var cmd = conn.CreateCommand();

            // Add each filter to the SQL query only if it was provided (not null/empty)
            if (!string.IsNullOrEmpty(q)) {
                // Search in Name, Description, AND Brand columns with LIKE (% = wildcard)
                sql += " AND (Name LIKE @q OR Description LIKE @q OR Brand LIKE @q)";
                cmd.Parameters.AddWithValue("@q", $"%{q}%"); // %gpu% matches "NVIDIA GPU 4090"
            }
            if (!string.IsNullOrEmpty(cat)) {
                sql += " AND Category=@cat";
                cmd.Parameters.AddWithValue("@cat", cat);
            }
            if (!string.IsNullOrEmpty(brand)) {
                sql += " AND Brand=@brand";
                cmd.Parameters.AddWithValue("@brand", brand);
            }
            if (min.HasValue) {
                sql += " AND Price>=@min"; // Only products at or above the minimum price
                cmd.Parameters.AddWithValue("@min", min.Value);
            }
            if (max.HasValue) {
                sql += " AND Price<=@max"; // Only products at or below the maximum price
                cmd.Parameters.AddWithValue("@max", max.Value);
            }

            // Append the ORDER BY clause based on the chosen sort order
            sql += sort switch {
                "price_asc"  => " ORDER BY Price ASC",   // Cheapest first
                "price_desc" => " ORDER BY Price DESC",  // Most expensive first
                "newest"     => " ORDER BY CreatedAt DESC", // Newest first
                _            => " ORDER BY Name ASC"     // Default: alphabetical
            };

            cmd.CommandText = sql;
            return cmd.ReadList<Product>(); // Execute and map results to Product objects
        }

        // Get a single product by ID (used by Details page and cart validation)
        public Product? GetProductById(int id) {
            using var c = GetConnection(); c.Open();
            return c.QueryOne<Product>("SELECT * FROM Products WHERE Id=@id", new { id });
        }

        // Get a distinct list of all category names (for the filter dropdowns)
        public List<string> GetCategories() {
            using var c = GetConnection(); c.Open();
            return c.QueryScalarList<string>("SELECT DISTINCT Category FROM Products WHERE IsActive=1 ORDER BY Category");
        }

        // Get a distinct list of all brand names (for the filter dropdowns)
        public List<string> GetBrands() {
            using var c = GetConnection(); c.Open();
            return c.QueryScalarList<string>("SELECT DISTINCT Brand FROM Products WHERE IsActive=1 ORDER BY Brand");
        }

        // Get products in the same category (used for "Related Products" on the details page)
        public List<Product> GetProductsByCategory(string cat, int limit = 8) {
            using var c = GetConnection(); c.Open();
            return c.QueryList<Product>("SELECT * FROM Products WHERE Category=@c AND IsActive=1 LIMIT @l", new { c=cat, l=limit });
        }

        // Save a product (INSERT if Id=0, UPDATE if Id>0)
        // This single method handles both creating new products and editing existing ones.
        public void SaveProduct(Product p) {
            using var c = GetConnection(); c.Open();
            if (p.Id == 0)
                // Id=0 means new product — INSERT a new row
                c.Execute("INSERT INTO Products (Name,Description,Category,Brand,Price,Stock,ImageUrl,IsActive) VALUES (@Name,@Description,@Category,@Brand,@Price,@Stock,@ImageUrl,@IsActive)", p);
            else
                // Id>0 means existing product — UPDATE the existing row
                c.Execute("UPDATE Products SET Name=@Name,Description=@Description,Category=@Category,Brand=@Brand,Price=@Price,Stock=@Stock,ImageUrl=@ImageUrl,IsActive=@IsActive WHERE Id=@Id", p);
        }

        // Soft-delete a product: set IsActive=0 so it's hidden but data is preserved
        public void DeleteProduct(int id) {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Products SET IsActive=0 WHERE Id=@id", new { id });
        }

        // Get products with stock at or below the threshold (default 5) — for admin dashboard alerts
        public List<Product> GetLowStockProducts(int t = 5) {
            using var c = GetConnection(); c.Open();
            return c.QueryList<Product>("SELECT * FROM Products WHERE Stock<=@t AND IsActive=1 ORDER BY Stock", new { t });
        }

        // ════════════════════════════════════════════════════════════════
        // ── CART ──
        // ════════════════════════════════════════════════════════════════

        // Get all cart items for a user, with the full Product or Listing data attached.
        // SQLite doesn't do JOINs automatically, so we manually load related data.
        public List<CartItem> GetCartItems(int userId)
        {
            using var c = GetConnection(); c.Open();
            // Get all cart item rows for this user
            var items = c.QueryList<CartItem>("SELECT * FROM CartItems WHERE UserId=@uid", new { uid=userId });

            foreach (var item in items)
            {
                // If it's a store product (ProductId > 0), load the Product object
                if (item.ProductId > 0)
                    item.Product = c.QueryOne<Product>("SELECT * FROM Products WHERE Id=@id", new { id=item.ProductId });

                // If it's a marketplace listing (ListingId is set), load the Listing object
                if (item.ListingId.HasValue)
                    item.Listing = c.QueryOne<Listing>("SELECT * FROM Listings WHERE Id=@id", new { id=item.ListingId.Value });
            }

            return items;
        }

        // Add a store product to the cart.
        // If the product is already in the cart, increase the quantity instead of adding a duplicate.
        public void AddToCart(int userId, int productId, int qty = 1)
        {
            using var c = GetConnection(); c.Open();
            // Check if this product is already in the user's cart
            var ex = c.QueryOne<CartItem>("SELECT * FROM CartItems WHERE UserId=@uid AND ProductId=@pid", new { uid=userId, pid=productId });
            if (ex != null)
                // Already exists — add to quantity
                c.Execute("UPDATE CartItems SET Quantity=Quantity+@q WHERE Id=@id", new { q=qty, id=ex.Id });
            else
                // New cart entry — INSERT a new row
                c.Execute("INSERT INTO CartItems (UserId,ProductId,Quantity) VALUES (@uid,@pid,@q)", new { uid=userId, pid=productId, q=qty });
        }

        // Add a marketplace listing to the cart (same logic as AddToCart but for listings)
        public void AddListingToCart(int userId, int listingId, int qty = 1)
        {
            using var c = GetConnection(); c.Open();
            var ex = c.QueryOne<CartItem>("SELECT * FROM CartItems WHERE UserId=@uid AND ListingId=@lid", new { uid=userId, lid=listingId });
            if (ex != null)
                c.Execute("UPDATE CartItems SET Quantity=Quantity+@q WHERE Id=@id", new { q=qty, id=ex.Id });
            else
                c.Execute("INSERT INTO CartItems (UserId,ListingId,Quantity) VALUES (@uid,@lid,@q)", new { uid=userId, lid=listingId, q=qty });
        }

        // Update the quantity of a specific cart item.
        // If quantity is 0 or less, the item is removed from the cart.
        public void UpdateCartQuantity(int cartItemId, int qty) {
            using var c = GetConnection(); c.Open();
            if (qty <= 0)
                c.Execute("DELETE FROM CartItems WHERE Id=@id", new { id=cartItemId }); // Remove item
            else
                c.Execute("UPDATE CartItems SET Quantity=@q WHERE Id=@id", new { q=qty, id=cartItemId });
        }

        // Remove a single cart item by its ID
        public void RemoveFromCart(int cartItemId) {
            using var c = GetConnection(); c.Open();
            c.Execute("DELETE FROM CartItems WHERE Id=@id", new { id=cartItemId });
        }

        // Remove ALL cart items for a user (called after placing an order)
        public void ClearCart(int userId) {
            using var c = GetConnection(); c.Open();
            c.Execute("DELETE FROM CartItems WHERE UserId=@uid", new { uid=userId });
        }

        // Get the total number of items in a user's cart (sum of all quantities).
        // COALESCE returns 0 if there are no items (avoids null).
        // Displayed as the badge number on the cart icon in the navbar.
        public int GetCartCount(int userId) {
            using var c = GetConnection(); c.Open();
            return c.QueryScalar<int>("SELECT COALESCE(SUM(Quantity),0) FROM CartItems WHERE UserId=@uid", new { uid=userId });
        }

        // ════════════════════════════════════════════════════════════════
        // ── ORDERS ──
        // ════════════════════════════════════════════════════════════════

        // Create a new order + its items in the database, and reduce product stock.
        // Returns the new order's ID so we can redirect to the confirmation page.
        public int CreateOrder(Order order)
        {
            using var c = GetConnection(); c.Open();

            // Insert the order record
            c.Execute("INSERT INTO Orders (UserId,FullName,Address,Region,Province,City,Barangay,ZipCode,Phone,PaymentMethod,PaymentReference,Status,Total,EstimatedDelivery) VALUES (@UserId,@FullName,@Address,@Region,@Province,@City,@Barangay,@ZipCode,@Phone,@PaymentMethod,@PaymentReference,@Status,@Total,@EstimatedDelivery)", order);

            // Get the ID that SQLite auto-generated for the new order
            var orderId = c.QueryScalar<int>("SELECT last_insert_rowid()");

            // Insert each item in the order
            foreach (var item in order.Items)
            {
                item.OrderId = orderId; // Link the item to this order
                c.Execute("INSERT INTO OrderItems (OrderId,ProductId,ProductName,Price,Quantity) VALUES (@OrderId,@ProductId,@ProductName,@Price,@Quantity)", item);
                // Decrease the product's stock by the quantity ordered
                c.Execute("UPDATE Products SET Stock=Stock-@q WHERE Id=@pid", new { q=item.Quantity, pid=item.ProductId });
            }

            return orderId; // Return so the controller can redirect to the confirmation page
        }

        // Get a single order by ID, including all its items
        public Order? GetOrderById(int id) {
            using var c = GetConnection(); c.Open();
            var o = c.QueryOne<Order>("SELECT * FROM Orders WHERE Id=@id", new { id });
            // Load the order items separately and attach them to the order object
            if (o != null) o.Items = c.QueryList<OrderItem>("SELECT * FROM OrderItems WHERE OrderId=@oid", new { oid=id });
            return o;
        }

        // Get all orders placed by a specific user (for the profile page order history)
        public List<Order> GetOrdersByUser(int userId) {
            using var c = GetConnection(); c.Open();
            var orders = c.QueryList<Order>("SELECT * FROM Orders WHERE UserId=@uid ORDER BY CreatedAt DESC", new { uid=userId });
            // Load items for each order
            foreach (var o in orders) o.Items = c.QueryList<OrderItem>("SELECT * FROM OrderItems WHERE OrderId=@oid", new { oid=o.Id });
            return orders;
        }

        // Get ALL orders from all users (for the admin orders page)
        public List<Order> GetAllOrders() {
            using var c = GetConnection(); c.Open();
            var orders = c.QueryList<Order>("SELECT * FROM Orders ORDER BY CreatedAt DESC");
            foreach (var o in orders) o.Items = c.QueryList<OrderItem>("SELECT * FROM OrderItems WHERE OrderId=@oid", new { oid=o.Id });
            return orders;
        }

        // Update the status of an order (admin feature)
        // e.g., "Pending" → "Processing" → "Shipped" → "Delivered"
        public void UpdateOrderStatus(int orderId, string status) {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Orders SET Status=@s WHERE Id=@id", new { s=status, id=orderId });
        }

        // ════════════════════════════════════════════════════════════════
        // ── MARKETPLACE LISTINGS ──
        // ════════════════════════════════════════════════════════════════

        // Get marketplace listings with optional filters (similar to SearchProducts)
        public List<Listing> GetListings(string? q = null, string? cat = null, string? cond = null, string sort = "newest")
        {
            using var conn = GetConnection(); conn.Open();
            var sql = "SELECT * FROM Listings WHERE Status='Active'"; // Only show active listings
            var cmd = conn.CreateCommand();

            // Text search across Title and Description
            if (!string.IsNullOrEmpty(q)) { sql += " AND (Title LIKE @q OR Description LIKE @q)"; cmd.Parameters.AddWithValue("@q", $"%{q}%"); }
            if (!string.IsNullOrEmpty(cat)) { sql += " AND Category=@cat"; cmd.Parameters.AddWithValue("@cat", cat); }
            // Condition filter (e.g., "Used", "Like New")
            if (!string.IsNullOrEmpty(cond)) { sql += " AND Condition=@cond"; cmd.Parameters.AddWithValue("@cond", cond); }

            // Sort by price or newest
            sql += sort == "price_asc" ? " ORDER BY Price ASC" : sort == "price_desc" ? " ORDER BY Price DESC" : " ORDER BY CreatedAt DESC";

            cmd.CommandText = sql;
            return cmd.ReadList<Listing>();
        }

        // Get a single listing by ID
        public Listing? GetListingById(int id) {
            using var c = GetConnection(); c.Open();
            return c.QueryOne<Listing>("SELECT * FROM Listings WHERE Id=@id", new { id });
        }

        // Get all listings posted by a specific seller (for the profile page)
        public List<Listing> GetListingsBySeller(int sellerId) {
            using var c = GetConnection(); c.Open();
            return c.QueryList<Listing>("SELECT * FROM Listings WHERE SellerId=@sid ORDER BY CreatedAt DESC", new { sid=sellerId });
        }

        // Save a listing (INSERT if Id=0, UPDATE if Id>0 — same pattern as SaveProduct)
        public void SaveListing(Listing l) {
            using var c = GetConnection(); c.Open();
            if (l.Id == 0)
                c.Execute("INSERT INTO Listings (SellerId,SellerName,Title,Description,Category,Condition,Price,Stock,ImageUrl,Status) VALUES (@SellerId,@SellerName,@Title,@Description,@Category,@Condition,@Price,@Stock,@ImageUrl,@Status)", l);
            else
                c.Execute("UPDATE Listings SET Title=@Title,Description=@Description,Category=@Category,Condition=@Condition,Price=@Price,Stock=@Stock,ImageUrl=@ImageUrl,Status=@Status WHERE Id=@Id", l);
        }

        // Soft-delete a listing: set Status='Removed' (hidden but data preserved)
        public void DeleteListing(int id) {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Listings SET Status='Removed' WHERE Id=@id", new { id });
        }

        // Get ALL listings regardless of status (for the admin listings page)
        public List<Listing> GetAllListings() {
            using var c = GetConnection(); c.Open();
            return c.QueryList<Listing>("SELECT * FROM Listings ORDER BY CreatedAt DESC");
        }

        // ════════════════════════════════════════════════════════════════
        // ── MESSAGES ──
        // ════════════════════════════════════════════════════════════════

        // Save a new message to the database
        public void SendMessage(Message m) {
            using var c = GetConnection(); c.Open();
            c.Execute("INSERT INTO Messages (SenderId,ReceiverId,SenderName,ListingId,Content) VALUES (@SenderId,@ReceiverId,@SenderName,@ListingId,@Content)", m);
        }

        // Get all messages between two specific users, ordered by time (for the chat view)
        // This includes messages sent in BOTH directions (A→B and B→A)
        public List<Message> GetConversation(int userId, int otherId) {
            using var c = GetConnection(); c.Open();
            return c.QueryList<Message>("SELECT * FROM Messages WHERE (SenderId=@uid AND ReceiverId=@oid) OR (SenderId=@oid AND ReceiverId=@uid) ORDER BY SentAt ASC",
                new { uid=userId, oid=otherId });
        }

        // Build a list of unique conversations for a user (their inbox).
        // Groups messages by the other person and creates a summary for each thread.
        public List<Conversation> GetConversations(int userId)
        {
            using var c = GetConnection(); c.Open();

            // Get all messages involving this user, sorted newest first
            var msgs = c.QueryList<Message>("SELECT * FROM Messages WHERE SenderId=@uid OR ReceiverId=@uid ORDER BY SentAt DESC", new { uid=userId });

            // Use a Dictionary to collect one conversation per unique other user
            // Key = the other user's ID, Value = the Conversation summary
            var convos = new Dictionary<int, Conversation>();

            foreach (var m in msgs)
            {
                // Determine who the "other person" is in this message
                int otherId = m.SenderId == userId ? m.ReceiverId : m.SenderId;

                // Skip if we already have a conversation summary for this person
                // (We only want the most recent message per conversation)
                if (convos.ContainsKey(otherId)) continue;

                // Load the other user's profile info
                var other = c.QueryOne<User>("SELECT * FROM Users WHERE Id=@id", new { id=otherId });

                // Build the conversation summary
                convos[otherId] = new Conversation
                {
                    OtherUserId = otherId,
                    OtherUserName = other?.FullName ?? "Unknown",
                    OtherUserAvatar = other?.AvatarUrl ?? "/images/avatars/default.svg",
                    // Truncate the last message preview to 50 characters (add "..." if longer)
                    LastMessage = m.Content.Length > 50 ? m.Content[..50] + "..." : m.Content,
                    LastMessageAt = m.SentAt,
                    // Count unread messages FROM the other person TO the current user
                    UnreadCount = c.QueryScalar<int>("SELECT COUNT(*) FROM Messages WHERE SenderId=@oid AND ReceiverId=@uid AND IsRead=0",
                        new { oid=otherId, uid=userId }),
                    ListingId = m.ListingId
                };
            }

            return convos.Values.ToList(); // Return the list of conversation summaries
        }

        // Mark messages from a specific sender as read (called when opening a chat)
        public void MarkMessagesRead(int senderId, int receiverId) {
            using var c = GetConnection(); c.Open();
            c.Execute("UPDATE Messages SET IsRead=1 WHERE SenderId=@sid AND ReceiverId=@rid", new { sid=senderId, rid=receiverId });
        }

        // Count how many unread messages a user has (for the notification badge in navbar)
        public int GetUnreadMessageCount(int userId) {
            using var c = GetConnection(); c.Open();
            return c.QueryScalar<int>("SELECT COUNT(*) FROM Messages WHERE ReceiverId=@uid AND IsRead=0", new { uid=userId });
        }

        // ════════════════════════════════════════════════════════════════
        // ── ADMIN DASHBOARD ──
        // ════════════════════════════════════════════════════════════════

        // Collect all statistics needed for the admin dashboard in one call.
        // Returns an AdminDashboardViewModel ready to be passed to the view.
        public AdminDashboardViewModel GetDashboardStats()
        {
            using var c = GetConnection(); c.Open();
            return new AdminDashboardViewModel
            {
                // Count of active products in the store
                TotalProducts = c.QueryScalar<int>("SELECT COUNT(*) FROM Products WHERE IsActive=1"),
                // Count of all orders ever placed
                TotalOrders = c.QueryScalar<int>("SELECT COUNT(*) FROM Orders"),
                // Count of registered customers (not admins)
                TotalUsers = c.QueryScalar<int>("SELECT COUNT(*) FROM Users WHERE Role='Customer'"),
                // Total revenue from non-cancelled orders (COALESCE returns 0 if no orders)
                TotalRevenue = c.QueryScalar<decimal>("SELECT COALESCE(SUM(Total),0) FROM Orders WHERE Status!='Cancelled'"),
                // The 5 most recent orders for the "Recent Orders" table
                RecentOrders = c.QueryList<Order>("SELECT * FROM Orders ORDER BY CreatedAt DESC LIMIT 5"),
                // Products with 5 or fewer units in stock (from a separate method)
                LowStockProducts = GetLowStockProducts(),
                // Count of active marketplace listings
                TotalListings = c.QueryScalar<int>("SELECT COUNT(*) FROM Listings WHERE Status='Active'"),
                // Count of unread messages across ALL users (shows admin workload)
                UnresolvedMessages = c.QueryScalar<int>("SELECT COUNT(*) FROM Messages WHERE IsRead=0")
            };
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // ── SQLITE EXTENSIONS ──
    // These are "extension methods" — they add extra methods to SqliteConnection
    // and SqliteCommand objects so our DbHelper code above is shorter and cleaner.
    //
    // 'internal' means these can only be used within this project (not exposed externally).
    // 'static' means you don't need to create an instance to use them.
    // ════════════════════════════════════════════════════════════════════

    internal static class SqliteExtensions
    {
        // Execute a SQL command that doesn't return data (INSERT, UPDATE, DELETE)
        // 'param' is an anonymous object like new { id = 5, name = "test" }
        // 'this SqliteConnection conn' means: call this as conn.Execute(...)
        public static void Execute(this SqliteConnection conn, string sql, object? param = null) {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (param != null) BindParams(cmd, param); // Attach the parameters to the command
            cmd.ExecuteNonQuery(); // Run the SQL without expecting results back
        }

        // Execute a SQL query that returns ONE single value (e.g., COUNT(*), SUM(), last_insert_rowid())
        // T is the return type (int, decimal, string, etc.)
        public static T QueryScalar<T>(this SqliteConnection conn, string sql, object? param = null) {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (param != null) BindParams(cmd, param);
            var r = cmd.ExecuteScalar(); // Returns a single value
            if (r == null || r == DBNull.Value) return default!; // Return default if null (0 for int)
            return (T)Convert.ChangeType(r, typeof(T)); // Convert the DB value to the expected type
        }

        // Execute a SQL query that returns a list of single values (e.g., SELECT DISTINCT Category)
        public static List<T> QueryScalarList<T>(this SqliteConnection conn, string sql, object? param = null) {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (param != null) BindParams(cmd, param);
            var list = new List<T>();
            using var reader = cmd.ExecuteReader(); // Execute and get a reader to iterate rows
            while (reader.Read()) { // Read one row at a time
                var v = reader.GetValue(0); // Get the first (and only) column
                if (v != null && v != DBNull.Value) list.Add((T)Convert.ChangeType(v, typeof(T)));
            }
            return list;
        }

        // Execute a SQL query that returns ONE row, mapped to an object of type T.
        // Returns null if no row was found.
        // 'where T : new()' means T must have a parameterless constructor (all models do)
        public static T? QueryOne<T>(this SqliteConnection conn, string sql, object? param = null) where T : new() {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (param != null) BindParams(cmd, param);
            using var r = cmd.ExecuteReader();
            return r.Read() ? Map<T>(r) : default; // If a row exists, map it; otherwise return null
        }

        // Execute a SQL query that returns MULTIPLE rows, each mapped to an object of type T.
        public static List<T> QueryList<T>(this SqliteConnection conn, string sql, object? param = null) where T : new() {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (param != null) BindParams(cmd, param);
            return cmd.ReadList<T>(); // Delegate to ReadList below
        }

        // Reads all rows from an already-configured command and maps them to a list of T.
        // Separated so it can also be called directly on a SqliteCommand (used in SearchProducts).
        public static List<T> ReadList<T>(this SqliteCommand cmd) where T : new() {
            var list = new List<T>();
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(Map<T>(r)); // Map each row to a T object
            return list;
        }

        // Maps one row from a SqliteDataReader to a C# object of type T.
        // Uses reflection to match column names to property names (case-insensitive).
        // Reflection means inspecting the class structure at runtime to set property values.
        private static T Map<T>(SqliteDataReader r) where T : new()
        {
            var obj = new T(); // Create an empty instance of T (e.g., new Product())
            var props = typeof(T).GetProperties(); // Get all properties of the class

            for (int i = 0; i < r.FieldCount; i++) // Loop through each column in the result
            {
                var col = r.GetName(i); // Column name from the database (e.g., "FullName")
                // Find the property in T whose name matches this column name (ignoring case)
                var prop = props.FirstOrDefault(p => p.Name.Equals(col, StringComparison.OrdinalIgnoreCase));
                if (prop == null || r.IsDBNull(i)) continue; // Skip if no matching property or value is NULL

                var val = r.GetValue(i); // Get the raw value from the database
                try
                {
                    // Handle special type conversions (SQLite stores everything as text/number)
                    if (prop.PropertyType == typeof(bool) || prop.PropertyType == typeof(bool?))
                        // SQLite booleans are stored as integers: 0=false, 1=true
                        prop.SetValue(obj, Convert.ToInt32(val) != 0);
                    else if (prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(decimal?))
                        prop.SetValue(obj, Convert.ToDecimal(val));
                    else if (prop.PropertyType == typeof(DateTime) || prop.PropertyType == typeof(DateTime?))
                        // SQLite dates are stored as text strings — parse them to DateTime
                        prop.SetValue(obj, DateTime.Parse(val.ToString()!));
                    else if (prop.PropertyType == typeof(int?) || prop.PropertyType == typeof(int))
                        prop.SetValue(obj, Convert.ToInt32(val));
                    else
                        // For all other types (string, etc.), use Convert.ChangeType for automatic conversion
                        prop.SetValue(obj, Convert.ChangeType(val, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType));
                }
                catch { } // Silently skip any columns that fail to convert (e.g., type mismatch)
            }

            return obj; // Return the fully-populated object
        }

        // Binds named parameters from an anonymous object to a SQL command.
        // Example: new { id = 5, name = "test" } adds @id=5 and @name="test" to the command.
        // This PREVENTS SQL injection attacks by never concatenating user input into SQL strings.
        private static void BindParams(SqliteCommand cmd, object param) {
            foreach (var p in param.GetType().GetProperties())
                // Add each property as a named SQL parameter (e.g., @id, @name)
                cmd.Parameters.AddWithValue("@" + p.Name, p.GetValue(param) ?? DBNull.Value);
        }
    }
}

// ============================================================
// Models.cs — DATA MODELS (blueprints) for the BisRAM application
// 
// A "model" is a C# class that represents a real-world thing in the system.
// Each property in a model corresponds to a column in the database table.
// Think of models like forms: each field in the form is a property here.
// ============================================================

namespace BisRAM.Models
{
    // ── PRODUCT ──────────────────────────────────────────────
    // Represents a product sold by the BisRAM store (e.g., CPUs, GPUs, RAM).
    // Maps to the "Products" table in the database.
    public class Product
    {
        public int Id { get; set; }                                         // Unique ID number for the product (auto-assigned by database)
        public string Name { get; set; } = "";                             // Product name, e.g., "Intel Core i9-14900K"
        public string Description { get; set; } = "";                      // Longer text describing the product
        public string Category { get; set; } = "";                         // Product category, e.g., "CPU", "GPU", "RAM"
        public string Brand { get; set; } = "";                            // Brand name, e.g., "Intel", "AMD", "NVIDIA"
        public decimal Price { get; set; }                                  // Price in Philippine Peso (₱)
        public int Stock { get; set; }                                      // How many units are available in inventory
        public string ImageUrl { get; set; } = "/images/products/default.jpg"; // URL path to the product's image
        public DateTime CreatedAt { get; set; } = DateTime.Now;            // Date and time the product was added
        public bool IsActive { get; set; } = true;                         // If false, the product is "soft-deleted" (hidden but not erased)
    }

    // ── USER ─────────────────────────────────────────────────
    // Represents a registered user (customer or admin).
    // Maps to the "Users" table in the database.
    public class User
    {
        public int Id { get; set; }                                         // Unique ID number for the user
        public string FullName { get; set; } = "";                         // User's full name, e.g., "Juan dela Cruz"
        public string Username { get; set; } = "";                         // Unique short username, e.g., "juandc"
        public string Email { get; set; } = "";                            // User's email address (used for login)
        public string PasswordHash { get; set; } = "";                     // Password stored as a BCrypt hash (NOT plain text, for security)
        public string Role { get; set; } = "Customer";                     // Either "Customer" or "Admin"
        public string Address { get; set; } = "";                          // Full combined address string
        public string Phone { get; set; } = "";                            // Phone number
        public string AvatarUrl { get; set; } = "/images/avatars/default.svg"; // URL to the user's profile picture
        public string Region { get; set; } = "";                           // PH Region, e.g., "Western Visayas"
        public string Province { get; set; } = "";                         // PH Province, e.g., "Antique"
        public string City { get; set; } = "";                             // City or municipality
        public string Barangay { get; set; } = "";                         // Barangay (smallest local unit in the Philippines)
        public string ZipCode { get; set; } = "";                          // Postal/ZIP code
        public string StreetAddress { get; set; } = "";                    // House number and street name
        public string? ResetToken { get; set; }                            // Temporary token used for password reset (null if not resetting)
        public DateTime? ResetTokenExpiry { get; set; }                    // When the reset token expires (null if no active reset)
        public bool IsActive { get; set; } = true;                         // If false, the account is suspended by admin
        public DateTime CreatedAt { get; set; } = DateTime.Now;            // Date the account was registered
    }

    // ── CART ITEM ────────────────────────────────────────────
    // Represents one item inside a user's shopping cart.
    // Maps to the "CartItems" table in the database.
    // A cart item can either be a store Product OR a Marketplace Listing.
    public class CartItem
    {
        public int Id { get; set; }             // Unique ID of this cart item row
        public int UserId { get; set; }         // Which user owns this cart item
        public int ProductId { get; set; }      // If this is a store product, its Product ID (0 if it's a listing)
        public int? ListingId { get; set; }     // If this is a marketplace listing, its Listing ID (null if it's a product)
        public int Quantity { get; set; }       // How many of this item the user wants to buy
        public Product? Product { get; set; }   // The actual Product object (loaded separately from DB, not stored in CartItems table)
        public Listing? Listing { get; set; }   // The actual Listing object (loaded separately from DB, not stored in CartItems table)
    }

    // ── ORDER ────────────────────────────────────────────────
    // Represents a completed purchase order placed by a user.
    // Maps to the "Orders" table in the database.
    public class Order
    {
        public int Id { get; set; }                             // Unique order ID number
        public int UserId { get; set; }                         // Which user placed this order
        public string FullName { get; set; } = "";             // Recipient's full name (may differ from account name)
        public string Address { get; set; } = "";              // Full delivery address as a single string
        public string Region { get; set; } = "";               // PH Region for delivery estimation
        public string Province { get; set; } = "";             // Province
        public string City { get; set; } = "";                 // City or municipality
        public string Barangay { get; set; } = "";             // Barangay
        public string ZipCode { get; set; } = "";              // Postal/ZIP code
        public string Phone { get; set; } = "";                // Contact number for delivery
        public string PaymentMethod { get; set; } = "";        // e.g., "Cash on Delivery", "GCash", "PayMaya"
        public string PaymentReference { get; set; } = "";     // Reference number for digital payments (empty for COD)
        public string Status { get; set; } = "Pending";        // Order status: "Pending", "Processing", "Shipped", "Delivered", "Cancelled"
        public decimal Total { get; set; }                      // Total price of the entire order in ₱
        public DateTime CreatedAt { get; set; } = DateTime.Now;    // When the order was placed
        public DateTime? EstimatedDelivery { get; set; }           // Estimated delivery date (calculated based on region)
        public List<OrderItem> Items { get; set; } = new();        // List of individual items in this order
    }

    // ── ORDER ITEM ───────────────────────────────────────────
    // Represents ONE product line inside an order.
    // Example: "2x Intel Core i9-14900K @ ₱59,999" is one OrderItem.
    // Maps to the "OrderItems" table in the database.
    public class OrderItem
    {
        public int Id { get; set; }                 // Unique ID of this order item row
        public int OrderId { get; set; }            // Which order this item belongs to
        public int ProductId { get; set; }          // The product's ID at the time of purchase
        public string ProductName { get; set; } = ""; // Product name saved at purchase (in case product is later deleted)
        public decimal Price { get; set; }          // Price per unit at the time of purchase
        public int Quantity { get; set; }           // How many units were ordered
    }

    // ── MARKETPLACE LISTING ──────────────────────────────────
    // Represents a second-hand or personal item listed for sale by a user.
    // Unlike Products (sold by the store), Listings are user-to-user (like OLX or Facebook Marketplace).
    // Maps to the "Listings" table in the database.
    public class Listing
    {
        public int Id { get; set; }                                         // Unique listing ID
        public int SellerId { get; set; }                                   // User ID of the person selling
        public string SellerName { get; set; } = "";                       // Seller's name (saved for quick display)
        public string Title { get; set; } = "";                            // Title of the listing, e.g., "Used RTX 3080"
        public string Description { get; set; } = "";                      // Detailed description of the item
        public string Category { get; set; } = "";                         // Category, e.g., "GPU", "RAM"
        public string Condition { get; set; } = "Used";                    // Item condition: "New", "Like New", "Used", "For Parts"
        public decimal Price { get; set; }                                  // Asking price in ₱
        public int Stock { get; set; }                                      // How many units the seller has
        public string ImageUrl { get; set; } = "/images/products/default.jpg"; // URL to the item's photo
        public string Status { get; set; } = "Active";                     // "Active" = visible, "Removed" = hidden/deleted
        public DateTime CreatedAt { get; set; } = DateTime.Now;            // When the listing was posted
    }

    // ── MESSAGE ──────────────────────────────────────────────
    // Represents a single chat message between two users.
    // Messages are often sent to ask about a marketplace listing.
    // Maps to the "Messages" table in the database.
    public class Message
    {
        public int Id { get; set; }                         // Unique message ID
        public int SenderId { get; set; }                   // User ID of the person who sent the message
        public int ReceiverId { get; set; }                 // User ID of the person who received the message
        public string SenderName { get; set; } = "";       // Sender's name (saved for display)
        public int? ListingId { get; set; }                 // Optional: the listing this conversation is about (null if general chat)
        public string Content { get; set; } = "";          // The actual text of the message
        public bool IsRead { get; set; } = false;           // Has the recipient read this message? false = unread (shows badge)
        public DateTime SentAt { get; set; } = DateTime.Now; // When the message was sent
    }

    // ── CONVERSATION ─────────────────────────────────────────
    // Represents a summary of a chat thread between the current user and one other user.
    // This is NOT stored in the database — it is built by the DbHelper.GetConversations() method
    // by processing the Messages table.
    public class Conversation
    {
        public int OtherUserId { get; set; }                            // ID of the other person in the chat
        public string OtherUserName { get; set; } = "";                // Name of the other person
        public string OtherUserAvatar { get; set; } = "/images/avatars/default.svg"; // Profile picture of the other person
        public string LastMessage { get; set; } = "";                  // Preview of the most recent message (truncated to 50 chars)
        public DateTime LastMessageAt { get; set; }                     // When the last message was sent
        public int UnreadCount { get; set; }                            // Number of unread messages from the other person
        public int? ListingId { get; set; }                             // The listing this chat is related to (if any)
        public string ListingTitle { get; set; } = "";                 // Title of that listing (for display)
    }

    // ── VIEW MODELS ──────────────────────────────────────────
    // View Models are special classes used to pass data FROM the controller TO the view (HTML page).
    // They are NOT stored in the database. They just carry data needed to render a specific page.

    // Used for the Login page — holds the email and password the user typed in.
    public class LoginViewModel
    {
        public string Email { get; set; } = "";     // Email address entered in the login form
        public string Password { get; set; } = "";  // Password entered in the login form
    }

    // Used for the Register page — holds all the fields the new user fills in.
    public class RegisterViewModel
    {
        public string FullName { get; set; } = "";          // Full name field
        public string Username { get; set; } = "";          // Desired username (3–20 chars, letters/numbers/underscores only)
        public string Email { get; set; } = "";             // Email address
        public string Password { get; set; } = "";          // Desired password
        public string ConfirmPassword { get; set; } = "";   // Re-entered password to confirm they match
        public string Phone { get; set; } = "";             // Contact number
        public string Region { get; set; } = "";            // PH Region
        public string Province { get; set; } = "";          // Province
        public string City { get; set; } = "";              // City/municipality
        public string Barangay { get; set; } = "";          // Barangay
        public string ZipCode { get; set; } = "";           // ZIP code
        public string StreetAddress { get; set; } = "";     // Street address
    }

    // Used for the Checkout page — holds the delivery info + list of items being ordered.
    public class CheckoutViewModel
    {
        public string FullName { get; set; } = "";              // Delivery recipient name
        public string Phone { get; set; } = "";                 // Contact number for delivery
        public string Region { get; set; } = "";                // Delivery region
        public string Province { get; set; } = "";              // Delivery province
        public string City { get; set; } = "";                  // Delivery city
        public string Barangay { get; set; } = "";              // Delivery barangay
        public string ZipCode { get; set; } = "";               // Delivery ZIP code
        public string StreetAddress { get; set; } = "";         // Delivery street address
        public string PaymentMethod { get; set; } = "Cash on Delivery"; // Chosen payment method
        public string PaymentReference { get; set; } = "";      // Reference number for e-wallet payments
        public List<CartItem> CartItems { get; set; } = new();  // List of items in the cart being checked out
        public decimal Total { get; set; }                       // Computed total cost of all cart items
    }

    // Used for the Products search/listing page — holds the search filters and the matching results.
    public class ProductSearchViewModel
    {
        public string? Query { get; set; }              // Text search query typed by the user (null if none)
        public string? Category { get; set; }           // Chosen category filter (null if not filtered)
        public string? Brand { get; set; }              // Chosen brand filter (null if not filtered)
        public decimal? MinPrice { get; set; }          // Minimum price filter (null if no lower limit)
        public decimal? MaxPrice { get; set; }          // Maximum price filter (null if no upper limit)
        public string SortBy { get; set; } = "name";    // How to sort results: "name", "price_asc", "price_desc", "newest"
        public List<Product> Products { get; set; } = new();    // The matching products returned by the search
        public List<string> Categories { get; set; } = new();   // All available categories (for the filter dropdown)
        public List<string> Brands { get; set; } = new();       // All available brands (for the filter dropdown)
    }

    // Used for the Admin Dashboard page — holds statistics shown at the top.
    public class AdminDashboardViewModel
    {
        public int TotalProducts { get; set; }              // Count of active products in the store
        public int TotalOrders { get; set; }                // Count of all orders ever placed
        public int TotalUsers { get; set; }                 // Count of all registered users
        public decimal TotalRevenue { get; set; }           // Sum of all completed order totals in ₱
        public List<Order> RecentOrders { get; set; } = new();      // The 5 most recent orders for quick review
        public List<Product> LowStockProducts { get; set; } = new(); // Products with stock ≤ 5 (need restocking)
        public int TotalListings { get; set; }              // Count of marketplace listings
        public int UnresolvedMessages { get; set; }         // Count of unread messages across all users
    }

    // Used for the Marketplace search page — holds filters and matching listings.
    public class MarketplaceSearchViewModel
    {
        public string? Query { get; set; }              // Text search query (null if none)
        public string? Category { get; set; }           // Category filter (null if not filtered)
        public string? Condition { get; set; }          // Condition filter: "New", "Used", etc. (null if not filtered)
        public string SortBy { get; set; } = "newest";  // Sort order: "newest", "price_asc", "price_desc"
        public List<Listing> Listings { get; set; } = new(); // The matching marketplace listings
    }
}

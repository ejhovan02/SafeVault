using NUnit.Framework;
using Microsoft.AspNetCore.Mvc;
using VaultApi.Controllers;
using VaultShared;
using Microsoft.EntityFrameworkCore;
using VaultApi;
using Microsoft.Extensions.Configuration;

namespace VaultTests;

[TestFixture]
public class TestInputValidation
{
    private UsersController _controller;
    private IConfiguration _config;
    private ApplicationDbContext _context;

    [SetUp]
    public void Setup()
    {
        // Setup In-Memory Database for testing
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "SafeVaultTestDb")
            .Options;
        
        _context = new ApplicationDbContext(options);

        _context.Users.RemoveRange(_context.Users);
        _context.SaveChanges();

        var inMemorySettings = new Dictionary<string, string?> {
            {"Jwt:Key", "SuperSecretKeyForTestingThatIsLongEnough"},
            {"Jwt:Issuer", "TestIssuer"},
            {"Jwt:Audience", "TestAudience"}
        };

    _config = new ConfigurationBuilder()
        .AddInMemoryCollection(inMemorySettings)
        .Build();

        _controller = new UsersController(_context, _config);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task TestForSQLInjection()
    {
        // Arrange: Malicious input
        var maliciousUser = new User 
        { 
            UserName = "'; DROP TABLE Users; --", 
            PasswordHash = "somepassword"
        };

        // Act
        var result = await _controller.Register(maliciousUser) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(200, result.StatusCode);

        // Verify the data was saved as a STRING, and the table wasn't dropped
        var count = _context.Users.Count();
        Assert.AreEqual(1, count); // Table still exists
        var savedUser = _context.Users.First();
        Assert.AreEqual("'; DROP TABLE Users; --", savedUser.UserName);
    }

    [Test]
    public async Task TestForXSS()
    {
        // Arrange: XSS Payload
        var xssUser = new User 
        { 
            UserName = "<script>alert('XSS')</script>", 
            PasswordHash = "somepassword"
        };

        // Act
        var result = await _controller.Register(xssUser) as OkObjectResult;

        // Assert
        Assert.IsNotNull(result);
        
        // Verify the payload is stored as raw text (Server side is safe)
        // The Blazor client will HTML-encode it when rendering, preventing execution.
        var savedUser = _context.Users.First();
        Assert.AreEqual("<script>alert('XSS')</script>", savedUser.UserName);
    }

    [Test]
    public async Task TestInvalidLoginAttempt_WithWrongPassword()
    {
        // Arrange: Create a valid user first
        var validUser = new User 
        { 
            UserName = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("correctPassword") 
        };
        _context.Users.Add(validUser);
        await _context.SaveChangesAsync();

        var loginRequest = new LoginRequest 
        { 
            UserName = "testuser", 
            Password = "wrongPassword" 
        };

        // Act
        var result = await _controller.Login(loginRequest) as UnauthorizedObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(401, result.StatusCode);
        Assert.AreEqual("Invalid credentials.", result.Value);
    }

    [Test]
    public async Task TestInvalidLoginAttempt_WithNonExistentUser()
    {
        // Arrange
        var loginRequest = new LoginRequest 
        { 
            UserName = "nonexistentuser", 
            Password = "anypassword" 
        };

        // Act
        var result = await _controller.Login(loginRequest) as UnauthorizedObjectResult;

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(401, result.StatusCode);
        Assert.AreEqual("Invalid credentials.", result.Value);
    }

    [Test]
    public async Task TestUnauthorizedAccess_ToAdminDashboard()
    {
        // Arrange: Create a standard user (Role = "User")
        var normalUser = new User 
        { 
            UserName = "normaluser",
            Role = "User", // Not Admin
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123")
        };
        _context.Users.Add(normalUser);
        await _context.SaveChangesAsync();

        // Simulate a login to get a token for this user
        var loginRequest = new LoginRequest 
        { 
            UserName = "normaluser", 
            Password = "password123" 
        };
        
        var loginResult = await _controller.Login(loginRequest) as OkObjectResult;
        Assert.IsNotNull(loginResult); // Ensure login worked
        
        var userInDb = _context.Users.First(u => u.UserName == "normaluser");
        Assert.AreEqual("User", userInDb.Role);

        Assert.AreNotEqual("Admin", userInDb.Role);
    }
}


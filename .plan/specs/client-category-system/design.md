# the client Cashbook Category System - Design Document

## Overview

This design document outlines the implementation of a simple category system for the client cashbook entries. The system will provide a clean separation between models, services, controllers, and interfaces, allowing users to create and manage simple Credit/Debit categories without the complexity of traditional accounting classifications.

## Architecture

### Project Structure

```
backend/GarmentsERP.API/
├── Models/Accounting/
│   └── Category.cs                    # Category entity model
├── Interfaces/
│   └── ICategoryService.cs           # Service interface
├── Services/
│   └── CategoryService.cs            # Service implementation (no subfolders)
├── Controllers/
│   └── CategoryController.cs         # API controller
├── DTOs/
│   ├── CategoryDto.cs               # Response DTOs
│   ├── CreateCategoryRequest.cs     # Request DTOs
│   └── UpdateCategoryRequest.cs     # Request DTOs
└── Data/
    └── ApplicationDbContext.cs       # DbContext updates
```

### Component Architecture

```
Frontend Request → CategoryController → ICategoryService → CategoryService → Database
                                    ↑                    ↓
                                Interface            Implementation
```

## Components and Interfaces

### 1. Category Model

**File:** `Models/Accounting/Category.cs`

```csharp
public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; }           // "Fabric Purchase", "Received: Urbo ltd"
    public string? Description { get; set; }   // Optional description
    public CategoryType Type { get; set; }     // Credit or Debit
    public bool IsActive { get; set; }         // Soft delete support
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }     // User tracking
    public string? UpdatedBy { get; set; }     // User tracking
}

public enum CategoryType
{
    Credit = 0,  // Money In (e.g., "Received: Urbo ltd")
    Debit = 1    // Money Out (e.g., "Fabric Purchase")
}
```

**Design Decisions:**
- Simple structure focused on the client needs
- CategoryType enum for Credit/Debit classification
- Audit fields for tracking changes
- Soft delete with IsActive flag
- No complex accounting relationships

### 2. Service Interface

**File:** `Interfaces/ICategoryService.cs`

```csharp
public interface ICategoryService
{
    // Read operations
    Task<IEnumerable<CategoryDto>> GetAllCategoriesAsync();
    Task<IEnumerable<CategoryDto>> GetCategoriesByTypeAsync(CategoryType type);
    Task<CategoryDto?> GetCategoryByIdAsync(Guid id);
    Task<IEnumerable<CategoryDto>> SearchCategoriesAsync(string searchTerm);
    
    // Write operations
    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, string? userId = null);
    Task<CategoryDto> UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, string? userId = null);
    Task<bool> DeleteCategoryAsync(Guid id);
    Task<CategoryDto> ToggleActiveStatusAsync(Guid id, string? userId = null);
    
    // Business logic operations
    Task<bool> IsCategoryNameUniqueAsync(string name, CategoryType type, Guid? excludeId = null);
    Task<bool> IsCategoryUsedInTransactionsAsync(Guid categoryId);
    Task<int> GetCategoryUsageCountAsync(Guid categoryId);
}
```

**Design Decisions:**
- Clear separation of read and write operations
- Support for filtering by CategoryType
- Search functionality for user convenience
- Business logic methods for validation
- User tracking for audit purposes

### 3. Service Implementation

**File:** `Services/CategoryService.cs`

```csharp
public class CategoryService : ICategoryService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CategoryService> _logger;

    // Constructor with dependency injection
    public CategoryService(ApplicationDbContext context, ILogger<CategoryService> logger)

    // Implementation of all interface methods with business logic
    // - Category name uniqueness validation
    // - Transaction usage checking
    // - Soft delete handling
    // - Audit trail maintenance
}
```

**Business Logic:**
- **Name Uniqueness:** Category names must be unique within their type (Credit/Debit)
- **Usage Validation:** Check if category is used before deletion
- **Soft Delete:** Mark as inactive instead of hard delete
- **Audit Trail:** Track creation and modification timestamps and users
- **Search Logic:** Case-insensitive search across name and description

### 4. API Controller

**File:** `Controllers/CategoryController.cs`

```csharp
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CategoryController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly ILogger<CategoryController> _logger;

    // Constructor injects interface only
    public CategoryController(ICategoryService categoryService, ILogger<CategoryController> logger)

    // HTTP endpoints that delegate to service
    [HttpGet] GetCategories()
    [HttpGet("{id}")] GetCategory(Guid id)
    [HttpGet("type/{type}")] GetCategoriesByType(CategoryType type)
    [HttpGet("search")] SearchCategories(string searchTerm)
    [HttpPost] CreateCategory(CreateCategoryRequest request)
    [HttpPut("{id}")] UpdateCategory(Guid id, UpdateCategoryRequest request)
    [HttpDelete("{id}")] DeleteCategory(Guid id)
    [HttpPatch("{id}/toggle-status")] ToggleActiveStatus(Guid id)
}
```

**Controller Responsibilities:**
- HTTP request/response handling only
- Input validation and model binding
- Authentication/authorization enforcement
- Delegate all business logic to ICategoryService
- Error handling and appropriate HTTP status codes

## Data Models

### Request DTOs

**File:** `DTOs/CreateCategoryRequest.cs`
```csharp
public class CreateCategoryRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    [Required]
    public CategoryType Type { get; set; }
}
```

**File:** `DTOs/UpdateCategoryRequest.cs`
```csharp
public class UpdateCategoryRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    
    [MaxLength(500)]
    public string? Description { get; set; }
    
    public bool IsActive { get; set; } = true;
}
```

### Response DTOs

**File:** `DTOs/CategoryDto.cs`
```csharp
public class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CategoryType Type { get; set; }
    public string TypeName { get; set; } = string.Empty; // "Credit" or "Debit"
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    public int UsageCount { get; set; } // Number of transactions using this category
}
```

## Error Handling

### Service Layer Error Handling

```csharp
public class CategoryValidationException : Exception
{
    public CategoryValidationException(string message) : base(message) { }
}

public class CategoryNotFoundException : Exception
{
    public CategoryNotFoundException(string message) : base(message) { }
}

public class CategoryInUseException : Exception
{
    public CategoryInUseException(string message) : base(message) { }
}
```

### Controller Error Responses

```csharp
// 400 Bad Request - Validation errors
// 404 Not Found - Category not found
// 409 Conflict - Category name already exists
// 422 Unprocessable Entity - Category in use, cannot delete
// 500 Internal Server Error - Unexpected errors
```

## Testing Strategy

### Unit Tests

1. **Service Tests:**
   - Test business logic methods
   - Test validation rules
   - Test error conditions
   - Mock database context

2. **Controller Tests:**
   - Test HTTP endpoints
   - Test request/response mapping
   - Test error handling
   - Mock service interface

### Integration Tests

1. **Database Tests:**
   - Test CRUD operations
   - Test data persistence
   - Test constraint validation

2. **API Tests:**
   - Test complete request/response flow
   - Test authentication/authorization
   - Test error scenarios

## Frontend Integration

### API Endpoints

```typescript
// Frontend service calls
GET    /api/category                    // Get all categories
GET    /api/category/{id}              // Get specific category
GET    /api/category/type/{type}       // Get categories by type (Credit/Debit)
GET    /api/category/search?q={term}   // Search categories
POST   /api/category                   // Create new category
PUT    /api/category/{id}              // Update category
DELETE /api/category/{id}              // Delete category
PATCH  /api/category/{id}/toggle-status // Toggle active status
```

### Frontend Components

```typescript
// Component structure
CategoryListPage
├── CategoryList (displays categories)
├── CategoryCreateModal (create new category)
├── CategoryEditModal (edit existing category)
├── CategoryDeleteDialog (confirm deletion)
└── CategorySearchBar (search functionality)
```

## Database Schema

### Category Table

```sql
CREATE TABLE Categories (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500) NULL,
    Type INT NOT NULL, -- 0=Credit, 1=Debit
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NULL,
    CreatedBy NVARCHAR(100) NULL,
    UpdatedBy NVARCHAR(100) NULL,
    
    CONSTRAINT UK_Categories_Name_Type UNIQUE (Name, Type, IsActive)
);

CREATE INDEX IX_Categories_Type ON Categories (Type);
CREATE INDEX IX_Categories_IsActive ON Categories (IsActive);
CREATE INDEX IX_Categories_Name ON Categories (Name);
```

## Implementation Considerations

### Performance Optimizations

1. **Database Indexing:**
   - Index on Type for filtering
   - Index on Name for searching
   - Composite index on Name + Type for uniqueness

2. **Caching Strategy:**
   - Cache frequently accessed categories
   - Invalidate cache on category updates
   - Use memory cache for category lists

### Security Considerations

1. **Authorization:**
   - Require authentication for all endpoints
   - Role-based access for category management
   - Audit trail for all changes

2. **Input Validation:**
   - Validate category names for SQL injection
   - Sanitize description fields
   - Enforce length limits

### Migration Strategy

1. **Database Migration:**
   - Create Categories table
   - Add indexes and constraints
   - Seed with common the client categories

2. **Coexistence:**
   - New Category system works alongside ChartOfAccount
   - No impact on existing functionality
   - Clear separation of concerns

This design provides a clean, simple category system that meets the the client cashbook requirements while maintaining proper architectural separation and following established patterns in the codebase.
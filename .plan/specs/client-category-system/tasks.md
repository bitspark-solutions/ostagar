# Implementation Plan

- [x] 1. Create Category Model and Database Setup





  - Create Category.cs model in Models/Accounting folder with CategoryType enum
  - Add Category DbSet to ApplicationDbContext
  - Create database migration for Categories table with proper indexes
  - Add unique constraint on Name + Type combination
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Create Category Service Interface





  - [x] 2.1 Create ICategoryService interface in Interfaces folder


    - Define GetAllCategoriesAsync() method signature
    - Define GetCategoriesByTypeAsync(CategoryType type) method signature
    - Define GetCategoryByIdAsync(Guid id) method signature
    - Define SearchCategoriesAsync(string searchTerm) method signature
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

  - [x] 2.2 Add CRUD operation method signatures to interface


    - Define CreateCategoryAsync(CreateCategoryRequest request, string? userId) method signature
    - Define UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, string? userId) method signature
    - Define DeleteCategoryAsync(Guid id) method signature
    - Define ToggleActiveStatusAsync(Guid id, string? userId) method signature
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 2.3 Add business logic method signatures to interface


    - Define IsCategoryNameUniqueAsync(string name, CategoryType type, Guid? excludeId) method signature
    - Define IsCategoryUsedInTransactionsAsync(Guid categoryId) method signature
    - Define GetCategoryUsageCountAsync(Guid categoryId) method signature
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

- [x] 3. Create Category DTOs





  - [x] 3.1 Create request DTOs in DTOs folder


    - Create CreateCategoryRequest.cs with Name, Description, Type properties and validation attributes
    - Create UpdateCategoryRequest.cs with Name, Description, IsActive properties and validation attributes
    - Add proper data annotations for validation (Required, MaxLength)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 3.2 Create response DTOs in DTOs folder

    - Create CategoryDto.cs with all category properties including TypeName and UsageCount
    - Add computed properties for display purposes (TypeName as string)
    - Include audit fields (CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

- [x] 4. Implement Category Service Business Logic





  - [x] 4.1 Create CategoryService class in Services folder (no subfolders)


    - Implement constructor with ApplicationDbContext and ILogger dependency injection
    - Implement ICategoryService interface
    - Add private helper methods for common operations
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 6.8, 6.9, 6.10_

  - [x] 4.2 Implement read operations in CategoryService

    - Implement GetAllCategoriesAsync() with proper mapping to CategoryDto
    - Implement GetCategoriesByTypeAsync(CategoryType type) with filtering
    - Implement GetCategoryByIdAsync(Guid id) with null handling
    - Implement SearchCategoriesAsync(string searchTerm) with case-insensitive search
    - _Requirements: 6.1, 6.2, 6.3, 6.4_

  - [x] 4.3 Implement write operations in CategoryService

    - Implement CreateCategoryAsync() with name uniqueness validation and audit trail
    - Implement UpdateCategoryAsync() with validation and audit trail
    - Implement DeleteCategoryAsync() with usage checking and soft delete
    - Implement ToggleActiveStatusAsync() with audit trail
    - _Requirements: 6.1, 6.2, 6.5_

  - [x] 4.4 Implement business logic methods in CategoryService


    - Implement IsCategoryNameUniqueAsync() with proper uniqueness checking within type
    - Implement IsCategoryUsedInTransactionsAsync() to check transaction references
    - Implement GetCategoryUsageCountAsync() to count transaction usage
    - Add proper error handling and logging throughout service
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5_

- [x] 5. Create Category API Controller





  - [x] 5.1 Create CategoryController class in Controllers folder


    - Set up controller with proper routing, authorization, and API controller attributes
    - Implement constructor with ICategoryService interface injection (not concrete service)
    - Add ILogger dependency injection for error logging
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

  - [x] 5.2 Implement GET endpoints in CategoryController


    - Implement GET /api/category endpoint that calls service.GetAllCategoriesAsync()
    - Implement GET /api/category/{id} endpoint that calls service.GetCategoryByIdAsync()
    - Implement GET /api/category/type/{type} endpoint that calls service.GetCategoriesByTypeAsync()
    - Implement GET /api/category/search endpoint that calls service.SearchCategoriesAsync()
    - _Requirements: 5.1, 5.2, 5.3, 5.4_

  - [x] 5.3 Implement write endpoints in CategoryController


    - Implement POST /api/category endpoint that calls service.CreateCategoryAsync()
    - Implement PUT /api/category/{id} endpoint that calls service.UpdateCategoryAsync()
    - Implement DELETE /api/category/{id} endpoint that calls service.DeleteCategoryAsync()
    - Implement PATCH /api/category/{id}/toggle-status endpoint that calls service.ToggleActiveStatusAsync()
    - _Requirements: 5.1, 5.2, 5.3, 5.5_

  - [x] 5.4 Add proper error handling and HTTP status codes to controller


    - Handle validation errors and return 400 Bad Request
    - Handle not found scenarios and return 404 Not Found
    - Handle uniqueness conflicts and return 409 Conflict
    - Handle category in use scenarios and return 422 Unprocessable Entity
    - Add try-catch blocks and return 500 Internal Server Error for unexpected errors
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5_

- [x] 6. Register Services in Dependency Injection





  - Add ICategoryService and CategoryService registration in Program.cs
  - Ensure proper scoped lifetime for service registration
  - Verify dependency injection works correctly with interface injection
  - _Requirements: 4.4, 5.1_

- [x] 7. Create Frontend Category Management Interface





  - [x] 7.1 Create category management page structure


    - Create /admin/accounting/categories/page.tsx for category list
    - Design clean layout with category list table showing Name, Type, Description, Status
    - Add Create Category button that opens modal or navigates to create page
    - Add search bar for filtering categories by name or description
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 7.2 Implement category CRUD operations in frontend


    - Create category creation form/modal with Name, Description, Type fields
    - Create category edit form/modal with pre-filled data
    - Add delete confirmation dialog with category usage warning
    - Implement search functionality with real-time filtering
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_



  - [x] 7.3 Create category service for frontend API calls





    - Create categoryService.ts in services folder with all API endpoint calls
    - Implement proper error handling and TypeScript types
    - Add loading states and error message handling
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 8. Integrate Categories with Existing Cashbook System





  - [x] 8.1 Update cashbook entry forms to use new categories


    - Modify credit transaction form to show only Credit type categories
    - Modify debit transaction form to show only Debit type categories
    - Replace existing category selection with new category system
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

  - [x] 8.2 Update cashbook data models to reference categories


    - Update transaction models to reference Category by ID instead of string names
    - Ensure backward compatibility with existing transaction data
    - Add migration to handle existing transaction category references
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 9. Testing and Quality Assurance





  - [x] 9.1 Write unit tests for CategoryService


    - Test all CRUD operations with various scenarios
    - Test business logic methods (uniqueness, usage checking)
    - Test error conditions and exception handling
    - Mock ApplicationDbContext for isolated testing
    - _Requirements: All requirements_

  - [x] 9.2 Write unit tests for CategoryController


    - Test all HTTP endpoints with valid and invalid inputs
    - Test error handling and proper HTTP status codes
    - Mock ICategoryService interface for isolated testing
    - Test authentication and authorization requirements
    - _Requirements: All requirements_

  - [x] 9.3 Write integration tests


    - Test complete API workflow from request to database
    - Test database constraints and data persistence
    - Test category integration with cashbook entries
    - Test concurrent access and data consistency
    - _Requirements: All requirements_

- [x] 10. Database Migration and Deployment




  - [x] 10.1 Create and test database migration





    - Generate migration for Categories table creation
    - Test migration on development database
    - Verify indexes and constraints are created correctly
    - Test rollback scenario if needed
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_

  - [x] 10.2 Seed initial the client categories


    - Create seed data with common the client categories from CSV
    - Add both Credit categories (Received: Urbo ltd, etc.) and Debit categories (Fabric Purchase, etc.)
    - Ensure seeded categories are properly typed and active
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [ ] 11. Documentation and Final Integration
  - Update API documentation with new category endpoints
  - Create user guide for category management interface
  - Document integration points with existing cashbook system
  - Verify coexistence with existing ChartOfAccount system
  - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5_
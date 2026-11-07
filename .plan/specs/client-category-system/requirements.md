# the client Cashbook Category System - Requirements Document

## Introduction

The current category system uses the complex ChartOfAccount model designed for traditional accounting, but the the client cashbook needs a simpler category system. Based on the CSV file structure, we need separate category management that supports both credit (money in) and debit (money out) categories that users can create, edit, and manage through CRUD operations.

## Requirements

### Requirement 1: Simple Category Model

**User Story:** As a user, I want to create simple categories for my cashbook entries (like "Fabric Purchase", "Electric Bill", "Received: Urbo ltd") without complex accounting classifications, so that I can quickly categorize my transactions.

#### Acceptance Criteria

1. WHEN I create a category THEN I SHALL specify a name, description, and whether it's for Credit (money in) or Debit (money out)
2. WHEN I create a category THEN the system SHALL store it with a unique ID and timestamps
3. WHEN I view categories THEN I SHALL see separate lists for Credit and Debit categories
4. WHEN I create a category THEN it SHALL be immediately available for use in cashbook entries
5. WHEN I create a category THEN it SHALL be marked as active by default

### Requirement 2: Category CRUD Operations

**User Story:** As a user, I want to create, read, update, and delete categories through a simple interface, so that I can manage my cashbook categories dynamically.

#### Acceptance Criteria

1. WHEN I access the category management page THEN I SHALL see a list of all categories with their type (Credit/Debit)
2. WHEN I click "Add Category" THEN I SHALL be able to create a new category with name, description, and type
3. WHEN I click edit on a category THEN I SHALL be able to modify its name, description, and active status
4. WHEN I click delete on a category THEN I SHALL be able to remove it (with confirmation)
5. WHEN I search categories THEN I SHALL be able to find categories by name or description

### Requirement 3: Category Type Management

**User Story:** As a user, I want to specify whether a category is for Credit (money in) or Debit (money out) transactions, so that the system can organize and suggest appropriate categories.

#### Acceptance Criteria

1. WHEN I create a category THEN I SHALL select either "Credit" or "Debit" type
2. WHEN I view the category list THEN I SHALL see categories grouped by type (Credit/Debit)
3. WHEN I use categories in cashbook entries THEN the system SHALL suggest appropriate categories based on transaction type
4. WHEN I filter categories THEN I SHALL be able to view only Credit or only Debit categories
5. WHEN I change a category type THEN the system SHALL update it and reflect the change in cashbook entries

### Requirement 4: Category Service Interface and Implementation

**User Story:** As a developer, I want a well-structured service layer with interface and implementation, so that the system follows proper separation of concerns and is maintainable.

#### Acceptance Criteria

1. WHEN I create the service THEN I SHALL define an ICategoryService interface with all service method signatures
2. WHEN I implement the service THEN I SHALL create a CategoryService class that implements ICategoryService
3. WHEN I write service methods THEN they SHALL contain all business logic for category operations
4. WHEN I register services THEN I SHALL register the interface and implementation in dependency injection
5. WHEN controllers need category operations THEN they SHALL only call interface methods, not direct service methods

### Requirement 5: Category API Controller

**User Story:** As a developer, I want API controllers that only handle HTTP concerns and delegate business logic to services, so that the code is clean and follows best practices.

#### Acceptance Criteria

1. WHEN I create the controller THEN it SHALL only inject ICategoryService interface (not concrete service)
2. WHEN controller methods are called THEN they SHALL only handle HTTP request/response and call service methods
3. WHEN business logic is needed THEN the controller SHALL delegate to service methods
4. WHEN I call GET /api/categories THEN the controller SHALL call service and return formatted response
5. WHEN I call POST/PUT/DELETE endpoints THEN the controller SHALL validate input and call appropriate service methods

### Requirement 6: Category Service Business Logic and Project Organization

**User Story:** As a developer, I want service methods that contain all business logic for category operations and follow proper project organization, so that controllers remain thin and the codebase stays organized.

#### Acceptance Criteria

1. WHEN the service creates a category THEN it SHALL validate the category name is unique within its type
2. WHEN the service deletes a category THEN it SHALL check if the category is used in existing transactions
3. WHEN the service retrieves categories THEN it SHALL support filtering by type (Credit/Debit)
4. WHEN the service searches categories THEN it SHALL perform case-insensitive search on name and description
5. WHEN the service updates a category THEN it SHALL maintain audit trail with timestamps and user tracking
6. WHEN I organize the project THEN I SHALL place CategoryService.cs directly in Services folder (no subfolders)
7. WHEN I create DTOs THEN they SHALL be placed in separate DTOs folder, not inside Services
8. WHEN I create models THEN they SHALL be placed in Models folder, not inside Services
9. WHEN I create interfaces THEN they SHALL be placed in Interfaces folder, not inside Services
10. WHEN I organize files THEN I SHALL maintain clean separation: Models/, Services/, Controllers/, DTOs/, Interfaces/

### Requirement 7: Frontend Category Management Interface

**User Story:** As a user, I want a simple web interface to manage my categories, so that I can easily add, edit, and organize my cashbook categories.

#### Acceptance Criteria

1. WHEN I access the category page THEN I SHALL see a clean list of categories with Create, Edit, Delete buttons
2. WHEN I click "Create Category" THEN I SHALL see a form with fields for name, description, and type
3. WHEN I click edit on a category THEN I SHALL see a pre-filled form to modify the category
4. WHEN I search categories THEN I SHALL see filtered results in real-time
5. WHEN I delete a category THEN I SHALL see a confirmation dialog before deletion

### Requirement 8: Integration with Existing Cashbook

**User Story:** As a user, I want the new categories to work seamlessly with the existing cashbook entry system, so that I can use them immediately in my transactions.

#### Acceptance Criteria

1. WHEN I create a cashbook entry THEN I SHALL see categories from the new system in dropdown/autocomplete
2. WHEN I select "Credit" transaction THEN I SHALL see only Credit type categories
3. WHEN I select "Debit" transaction THEN I SHALL see only Debit type categories
4. WHEN I save a cashbook entry THEN it SHALL reference the category by ID
5. WHEN I view transaction history THEN I SHALL see the category names displayed correctly

### Requirement 9: Data Migration and Compatibility

**User Story:** As a system administrator, I want the new category system to coexist with the existing ChartOfAccount system, so that existing functionality is not broken.

#### Acceptance Criteria

1. WHEN the new category system is deployed THEN existing ChartOfAccount functionality SHALL continue to work
2. WHEN I create new categories THEN they SHALL be stored separately from ChartOfAccount data
3. WHEN I view reports THEN both old and new category systems SHALL be supported
4. WHEN I export data THEN the system SHALL clearly distinguish between category types
5. WHEN I migrate existing data THEN the system SHALL provide tools to convert ChartOfAccount entries to simple categories if needed

## Success Criteria

- Users can create and manage simple categories without accounting knowledge
- Category CRUD operations work smoothly through both API and UI
- Categories integrate seamlessly with cashbook entry system
- System supports the the client CSV category structure
- New system coexists with existing ChartOfAccount functionality
- Category management is intuitive and fast for daily use
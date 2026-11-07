# Cashbook Functionality Plan - Client CSV Format

## Overview
Based on the sample CSV files, the cashbook system needs to support the the client format with categories, suppliers, buyers, and trial balance functionality.

## Sample CSV Analysis

### 1. Categories Mapping (Cash-Book-Client-categories.csv)
```csv
Credit Categories,Debit Categories,Supplier,Buyer
Sales,Purchase,Supplier A,Buyer A
Commission,Rent,Supplier B,Buyer B
Interest,Salary,Supplier C,Buyer C
```

**Purpose**: Defines the chart of accounts and trading partners
**Structure**: 
- Credit Categories: Revenue accounts
- Debit Categories: Expense accounts  
- Supplier: Trading partners for purchases
- Buyer: Trading partners for sales

### 2. Main Transaction Format (Cash-Book-Client-main.csv)
```csv
Date,Credit Categories,Particulars,Amount,Date,Debit Categories,Particulars,Amount
2024-01-01,Sales,Sale to Buyer A,5000,2024-01-01,Purchase,Purchase from Supplier A,3000
2024-01-02,Commission,Commission earned,500,2024-01-02,Rent,Office rent,1000
```

**Purpose**: Double-entry transaction recording
**Structure**:
- **Left Side (Credit)**: Date, Credit Categories, Particulars, Amount
- **Right Side (Debit)**: Date, Debit Categories, Particulars, Amount
- **Double Entry**: Each row represents balanced credit and debit entries

### 3. Trial Balance (Cash-Book-Client-trial-balance.csv)
```csv
Account,Dr.,Cr.,Balance
Purchase,3000,,3000
Sales,,5000,5000
Commission,,500,500
Rent,1000,,1000
Net Balance,,1500,1500
```

**Purpose**: Financial position summary
**Structure**:
- Account: Chart of accounts
- Dr.: Debit balances
- Cr.: Credit balances
- Balance: Net position

## Database Schema Design

### 1. Chart of Accounts
```sql
CREATE TABLE chart_of_accounts (
    id SERIAL PRIMARY KEY,
    account_code VARCHAR(20) UNIQUE NOT NULL,
    account_name VARCHAR(100) NOT NULL,
    account_type VARCHAR(20) NOT NULL, -- 'ASSET', 'LIABILITY', 'EQUITY', 'REVENUE', 'EXPENSE'
    parent_account_id INTEGER REFERENCES chart_of_accounts(id),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### 2. Trading Partners
```sql
CREATE TABLE trading_partners (
    id SERIAL PRIMARY KEY,
    partner_code VARCHAR(20) UNIQUE NOT NULL,
    partner_name VARCHAR(100) NOT NULL,
    partner_type VARCHAR(20) NOT NULL, -- 'SUPPLIER', 'BUYER', 'BOTH'
    contact_details JSONB,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### 3. Cash Book Transactions
```sql
CREATE TABLE cash_book_transactions (
    id SERIAL PRIMARY KEY,
    transaction_date DATE NOT NULL,
    reference_number VARCHAR(50),
    description TEXT,
    total_amount DECIMAL(15,2) NOT NULL,
    created_by INTEGER REFERENCES users(id),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### 4. Transaction Lines (Double Entry)
```sql
CREATE TABLE transaction_lines (
    id SERIAL PRIMARY KEY,
    transaction_id INTEGER REFERENCES cash_book_transactions(id),
    account_id INTEGER REFERENCES chart_of_accounts(id),
    trading_partner_id INTEGER REFERENCES trading_partners(id),
    particulars TEXT,
    debit_amount DECIMAL(15,2) DEFAULT 0,
    credit_amount DECIMAL(15,2) DEFAULT 0,
    line_type VARCHAR(10) NOT NULL, -- 'DEBIT', 'CREDIT'
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### 5. the client Categories Mapping
```sql
CREATE TABLE client_categories (
    id SERIAL PRIMARY KEY,
    credit_category VARCHAR(100),
    debit_category VARCHAR(100),
    supplier_name VARCHAR(100),
    buyer_name VARCHAR(100),
    account_id INTEGER REFERENCES chart_of_accounts(id),
    trading_partner_id INTEGER REFERENCES trading_partners(id),
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

## API Endpoints Design

### 1. Chart of Accounts Management
```csharp
// GET /api/accounting/chart-of-accounts
// POST /api/accounting/chart-of-accounts
// PUT /api/accounting/chart-of-accounts/{id}
// DELETE /api/accounting/chart-of-accounts/{id}
```

### 2. Trading Partners Management  
```csharp
// GET /api/accounting/trading-partners
// POST /api/accounting/trading-partners
// PUT /api/accounting/trading-partners/{id}
// DELETE /api/accounting/trading-partners/{id}
```

### 3. Cash Book Transactions
```csharp
// GET /api/accounting/cash-book-transactions
// POST /api/accounting/cash-book-transactions
// PUT /api/accounting/cash-book-transactions/{id}
// DELETE /api/accounting/cash-book-transactions/{id}
// GET /api/accounting/cash-book-transactions/{id}/lines
```

### 4. CSV Import/Export
```csharp
// POST /api/accounting/cash-book/import/client-csv
// GET /api/accounting/cash-book/export/client-csv
// POST /api/accounting/cash-book/import/categories
// GET /api/accounting/cash-book/export/trial-balance
```

### 5. Reports
```csharp
// GET /api/accounting/reports/trial-balance
// GET /api/accounting/reports/cash-book-summary
// GET /api/accounting/reports/trading-partners-summary
```

## Frontend Components Design

### 1. Chart of Accounts Management
```
/admin/accounting/chart-of-accounts
├── ChartOfAccountsList.tsx
├── ChartOfAccountsForm.tsx
├── AccountHierarchy.tsx
└── AccountBalance.tsx
```

### 2. Trading Partners Management
```
/admin/accounting/trading-partners
├── TradingPartnersList.tsx
├── TradingPartnersForm.tsx
├── PartnerTransactionHistory.tsx
└── PartnerBalance.tsx
```

### 3. Cash Book Entry (Enhanced)
```
/admin/accounting/cash-book-entry
├── CashBookEntryForm.tsx (existing - enhanced)
├── TransactionLines.tsx
├── DoubleEntryValidator.tsx
└── QuickEntryTemplates.tsx
```

### 4. CSV Import/Export
```
/admin/accounting/csv-operations
├── CashBookCsvImport.tsx
├── CategoriesImport.tsx
├── TrialBalanceExport.tsx
└── TransactionExport.tsx
```

### 5. Reports
```
/admin/accounting/reports
├── TrialBalanceReport.tsx
├── CashBookSummary.tsx
├── TradingPartnersSummary.tsx
└── AccountBalanceReport.tsx
```

## Implementation Phases

### Phase 1: Core Setup (Week 1-2)
- [ ] Create database schema
- [ ] Implement Chart of Accounts API
- [ ] Create Trading Partners API
- [ ] Build Chart of Accounts frontend
- [ ] Build Trading Partners frontend

### Phase 2: Transaction Management (Week 3-4)
- [ ] Enhance Cash Book Transaction API
- [ ] Implement double-entry validation
- [ ] Update Cash Book Entry form
- [ ] Add transaction lines management
- [ ] Implement balance validation

### Phase 3: Client CSV Format Support (Week 5-6)
- [ ] Create the client categories mapping
- [ ] Implement CSV import for categories
- [ ] Implement CSV import for transactions
- [ ] Build the client import interface
- [ ] Add validation for the client format

### Phase 4: Reporting (Week 7-8)
- [ ] Implement trial balance generation
- [ ] Create cash book summary reports
- [ ] Build trading partners summary
- [ ] Implement CSV export functionality
- [ ] Add report filtering and pagination

## Business Rules

### 1. Double-Entry Validation
- Every transaction must have equal debits and credits
- Minimum one debit line and one credit line per transaction
- Maximum 10 lines per transaction for performance

### 2. Account Types
- **Assets**: Normal debit balance
- **Liabilities**: Normal credit balance
- **Equity**: Normal credit balance
- **Revenue**: Normal credit balance
- **Expenses**: Normal debit balance

### 3. Client CSV Format Rules
- Credit Categories map to Revenue accounts
- Debit Categories map to Expense accounts
- Suppliers linked to Purchase transactions
- Buyers linked to Sales transactions
- Transaction date must be valid business date

### 4. Data Validation
- Account codes must be unique
- Trading partner codes must be unique
- Transaction amounts must be positive
- Transaction dates cannot be future dates
- All required fields must be populated

## Error Handling

### 1. Import Validation
- CSV format validation
- Data type validation
- Business rule validation
- Duplicate detection
- Missing reference validation

### 2. Transaction Validation
- Balance validation (debits = credits)
- Account existence validation
- Trading partner existence validation
- Date range validation
- Amount validation

### 3. User Feedback
- Clear error messages
- Progress indicators for imports
- Success confirmations
- Validation warnings
- Rollback capabilities

## Performance Considerations

### 1. Database Optimization
- Indexes on frequently queried columns
- Partitioning for large transaction tables
- Archived data management
- Query optimization

### 2. Frontend Performance
- Pagination for large data sets
- Lazy loading for components
- Debounced search inputs
- Cached dropdown data

### 3. Import Performance
- Batch processing for large files
- Progress tracking
- Background processing
- Error recovery mechanisms

## Security Considerations

### 1. Data Access
- Role-based access control
- Audit trail for all changes
- Data encryption at rest
- Secure API endpoints

### 2. Financial Data
- Transaction immutability after approval
- Approval workflow for large amounts
- Backup and recovery procedures
- Compliance with accounting standards

This comprehensive plan provides a roadmap for implementing the cashbook functionality that supports the the client CSV format while maintaining proper accounting principles and user experience.

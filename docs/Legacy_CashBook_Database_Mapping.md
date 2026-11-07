# 🗃️ Client Cash Book to Database Models Mapping

## 📋 **CSV Column Structure Analysis**

### **Original the client CSV Format:**
```csv
Date,Catagories,Particulars,Amount Tk.,Date,Catagories,Supplier,Buyer,Particulars,Amount Tk.
```

### **Left Side (Credit Transactions):**
- `Date` → Credit transaction date  
- `Catagories` → Account category (Loan A/C, Machine Purchase, etc.)
- `Particulars` → Transaction description
- `Amount Tk.` → Credit amount

### **Right Side (Debit Transactions):**
- `Date` → Debit transaction date
- `Catagories` → Account category (Fabric Purchase, Salary, etc.)
- `Supplier` → Supplier/Contact name
- `Buyer` → Customer/Contact name  
- `Particulars` → Transaction description
- `Amount Tk.` → Debit amount

## 🎯 **Database Model Mapping**

### **1. Categories → ChartOfAccount**
```csharp
// Table: ChartOfAccounts
the client Category          Database Field               Account Type    Account Code
─────────────────────────────────────────────────────────────────────────────────────
"Loan A/C Chairman"      →   AccountName                 Liability       2001
"Machine Purchase"       →   AccountName                 Asset           1001  
"Fabric Purchase"        →   AccountName                 Expense         5001
"Salary A/C"            →   AccountName                 Expense         6001
"Electric Bill"         →   AccountName                 Expense         7001
"Received: Urbo ltd"    →   AccountName                 Revenue         4001
"Received: Brooklyn BD" →   AccountName                 Revenue         4002
"Cash on Hand"          →   AccountName (Default)       Asset           1100
```

### **2. Suppliers/Buyers → Contact**
```csharp
// Table: Contacts  
the client Field            Database Field               Contact Type
────────────────────────────────────────────────────────────────────
Supplier                →   Name, CompanyName           Supplier
Buyer                   →   Name, CompanyName           Customer  
"Urbo ltd"             →   CompanyName                 Customer
"Brooklyn: Joggers"     →   CompanyName                 Customer
```

### **3. Transactions → JournalEntry + JournalEntryLine**
```csharp
// Table: JournalEntries
the client Field            Database Field               Sample Value
────────────────────────────────────────────────────────────────────
Date                    →   TransactionDate             2025-02-01
Auto Generated          →   JournalNumber               JE-2025-0001
"Cash Book Entry"       →   Description                 Cash Book Entry  
Manual Entry            →   JournalType                 General
Auto Calculated         →   TotalDebit                  2400.00
Auto Calculated         →   TotalCredit                 2400.00

// Table: JournalEntryLines (Double Entry)
the client Data             Database Mapping             Debit/Credit
────────────────────────────────────────────────────────────────────
Credit Side Transaction →   
  Amount Tk.            →   Credit                      261080.00
  Categories            →   AccountId (via lookup)      Loan A/C Chairman
  Particulars           →   Description                 "Loan received"

Debit Side Transaction  →
  Amount Tk.            →   Debit                       2400.00  
  Categories            →   AccountId (via lookup)      Cash on Hand
  Particulars           →   Description                 "Cash received"

Cash Account (Balance)  →
  Auto Entry            →   Credit/Debit                Balance entry
  Cash Account          →   AccountId (1100)            Cash on Hand
```

## 🏗️ **Data Entry Form Structure**

### **Transaction Entry Form Fields:**

#### **1. Header Information:**
```typescript
interface CashBookEntry {
  transactionDate: Date;           // the client "Date"
  referenceNumber: string;         // Manual entry
  description: string;             // Optional overall description
  journalType: JournalType;        // Default: General
}
```

#### **2. Credit Transaction (Money In):**
```typescript
interface CreditTransaction {
  date: Date;                      // the client left "Date"
  categoryId: string;              // ChartOfAccount.Id via category lookup
  categoryName: string;            // For display/search
  particulars: string;             // the client "Particulars" 
  amount: number;                  // the client "Amount Tk."
  contactId?: string;              // Optional supplier/customer
}
```

#### **3. Debit Transaction (Money Out):**
```typescript
interface DebitTransaction {
  date: Date;                      // the client right "Date"  
  categoryId: string;              // ChartOfAccount.Id via category lookup
  categoryName: string;            // For display/search
  supplierName?: string;           // the client "Supplier"
  buyerName?: string;              // the client "Buyer"
  particulars: string;             // the client "Particulars"
  amount: number;                  // the client "Amount Tk."
  contactId?: string;              // Contact.Id after creation
}
```

## ⚙️ **Automatic Processing Logic**

### **1. Account Creation:**
```csharp
// Auto-create ChartOfAccount entries
if (!accountExists(categoryName)) {
    var account = new ChartOfAccount {
        AccountCode = GenerateAccountCode(categoryType),
        AccountName = categoryName,
        AccountType = DetermineAccountType(categoryName),
        IsActive = true
    };
}
```

### **2. Contact Creation:**
```csharp
// Auto-create Contact entries  
if (!string.IsNullOrEmpty(supplierName) && !contactExists(supplierName)) {
    var contact = new Contact {
        Name = supplierName,
        CompanyName = supplierName,
        ContactType = ContactType.Supplier,
        Email = $"{supplierName.ToLower()}@supplier.com",
        IsActive = true
    };
}
```

### **3. Double-Entry Journal:**
```csharp
// Create balanced journal entry
var journalEntry = new JournalEntry {
    JournalNumber = GenerateJournalNumber(),
    TransactionDate = transactionDate,
    JournalType = JournalType.General,
    Description = "Cash Book Entry",
    TotalDebit = totalDebits,
    TotalCredit = totalCredits
};

// Credit line (Money received)
var creditLine = new JournalEntryLine {
    AccountId = creditAccountId,
    Credit = creditAmount,
    Debit = 0,
    Description = creditParticulars
};

// Debit line (Cash account)  
var debitLine = new JournalEntryLine {
    AccountId = cashAccountId,
    Debit = creditAmount,
    Credit = 0,
    Description = "Cash received"
};
```

## 🎯 **Account Type Classification Rules**

### **Automatic Account Type Assignment:**
```csharp
public AccountType DetermineAccountType(string categoryName) {
    // Liability patterns
    if (categoryName.Contains("Loan") || categoryName.Contains("Payable")) 
        return AccountType.Liability;
        
    // Asset patterns  
    if (categoryName.Contains("Machine") || categoryName.Contains("Cash") || 
        categoryName.Contains("Purchase") && !categoryName.Contains("Expense"))
        return AccountType.Asset;
        
    // Revenue patterns
    if (categoryName.Contains("Received") || categoryName.Contains("Sale") ||
        categoryName.Contains("Income"))
        return AccountType.Revenue;
        
    // Expense patterns (default for most operational categories)
    return AccountType.Expense;
}
```

### **Account Code Generation:**
```csharp
public string GenerateAccountCode(AccountType accountType) {
    return accountType switch {
        AccountType.Asset => $"1{GetNextSequence("1")}",      // 1001, 1002...
        AccountType.Liability => $"2{GetNextSequence("2")}",  // 2001, 2002...
        AccountType.Equity => $"3{GetNextSequence("3")}",     // 3001, 3002...
        AccountType.Revenue => $"4{GetNextSequence("4")}",    // 4001, 4002...
        AccountType.Expense => $"5{GetNextSequence("5")}",    // 5001, 5002...
        _ => $"9{GetNextSequence("9")}"                       // 9001, 9002...
    };
}
```

## 🔄 **Cash Flow Logic**

### **Cash Account Management:**
```csharp
// Default Cash Account (Always exists)
var cashAccount = new ChartOfAccount {
    AccountCode = "1100",
    AccountName = "Cash on Hand", 
    AccountType = AccountType.Asset,
    IsActive = true
};

// Every transaction affects cash
foreach (var transaction in transactions) {
    // Credit transaction: Cash increases (Debit Cash, Credit Source)
    if (transaction.Type == TransactionType.Credit) {
        journal.Lines.Add(new JournalEntryLine {
            AccountId = cashAccountId,
            Debit = transaction.Amount,
            Description = $"Cash from {transaction.CategoryName}"
        });
        journal.Lines.Add(new JournalEntryLine {
            AccountId = transaction.CategoryAccountId,
            Credit = transaction.Amount,
            Description = transaction.Particulars
        });
    }
    
    // Debit transaction: Cash decreases (Credit Cash, Debit Expense)
    else {
        journal.Lines.Add(new JournalEntryLine {
            AccountId = cashAccountId,
            Credit = transaction.Amount,
            Description = $"Cash paid for {transaction.CategoryName}"
        });
        journal.Lines.Add(new JournalEntryLine {
            AccountId = transaction.CategoryAccountId,
            Debit = transaction.Amount,
            Description = transaction.Particulars
        });
    }
}
```

## ✅ **Data Validation Rules**

### **Business Logic Validation:**
```csharp
public bool ValidateCashBookEntry(CashBookEntryDto entry) {
    // Date validation
    if (entry.TransactionDate > DateTime.Today)
        throw new ValidationException("Transaction date cannot be in future");
        
    // Amount validation  
    if (entry.CreditAmount <= 0 && entry.DebitAmount <= 0)
        throw new ValidationException("At least one amount must be greater than zero");
        
    // Category validation
    if (string.IsNullOrWhiteSpace(entry.CategoryName))
        throw new ValidationException("Category is required");
        
    // Balance validation (Total Debits = Total Credits)
    if (Math.Abs(entry.TotalDebits - entry.TotalCredits) > 0.01m)
        throw new ValidationException("Journal entry must be balanced");
        
    return true;
}
```

## 🎯 **Summary**

This mapping allows you to:

1. **Manual Data Entry**: Create transactions mimicking the client cash book structure
2. **Automatic Account Creation**: System creates ChartOfAccount entries from categories  
3. **Contact Management**: Auto-creates suppliers/customers from names
4. **Double-Entry Bookkeeping**: Maintains accounting principles automatically
5. **Cash Flow Tracking**: All transactions flow through central cash account
6. **Audit Trail**: Complete transaction history with references

The UI will provide a familiar cash book interface while ensuring proper accounting standards in the backend. 🚀

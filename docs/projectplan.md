# Ostagar Software

This repository is for a comprehensive Ostagar system targeted at small manufacturing and garments businesses. The project uses an **ASP.NET Core Web API backend** and a **Next.js frontend**. This document serves as a detailed prompt and guide outlining our architecture, modules, data models, and user interface designs.

---

## Table of Contents
1. [Overview](#overview)
2. [Technology Stack](#technology-stack)
3. [Modules & Features](#modules--features)
   - [User Registration and Authentication](#user-registration-and-authentication)
   - [Store & Inventory Management](#store--inventory-management)
   - [Sales Management](#sales-management)
   - [Production Management](#production-management)
   - [Order Management](#order-management)
   - [Invoicing](#invoicing)
   - [HR and Payroll](#hr-and-payroll)
   - [Accounting (100% Balanced Sheet)](#accounting-100-balanced-sheet)
4. [Detailed Prompt for GitHub Copilot Agent](#detailed-prompt-for-github-copilot-agent)
5. [Conclusion](#conclusion)

---

## Overview

This project aims to build a modular ERP system containing:

- **User Registration/Login**: with role-based authentication.
- **Store and Inventory**: managing product catalogues and inventory levels.
- **Sales Management**: tracking orders through the sales pipeline.
- **Production Management**: organizing work orders and production schedules.
- **Order Management**: overseeing the full order cycle.
- **Invoicing**: generating billing documents.
- **HR and Payroll**: maintaining employee records and payroll processing.
- **Accounting Module**: a full general ledger and chart of accounts ensuring a 100% balanced sheet.

Each module is designed to seamlessly interact with the others for complete business process integration.

---

## Technology Stack

- **Backend**: ASP.NET Core Web API
- **Frontend**: Next.js with TypeScript and Material-UI (MUI) for UI components
- **Database**: PostgreSQL
- **Cache**: Redis
- **State Management**: Zustand
- **Authentication**: JWT with ASP.NET Identity
- **Containerization**: Docker with Alpine Linux images

---

## Modules & Features

### User Registration and Authentication
- **Features:**
  - User registration with email verification.
  - Secure login using ASP.NET Identity.
  - Role-based authorization (e.g., Admin, Manager, Employee, Vendor, Customer).
- **Data Entities / Form Fields:**
  - **Users Table:**
    - `UserId` (GUID)
    - `Username` (string)
    - `Email` (string)
    - `PasswordHash` (string)
    - `RoleId` (Foreign Key to Roles)
    - `CreatedAt` (DateTime)
    - `IsActive` (Boolean)
  - **Roles Table:**
    - `RoleId` (GUID)
    - `RoleName` (enum: Admin, Manager, staff, etc.)
- **Registration Form:**
  - Fields: Username, Email, Password, Confirm Password, Full Name, Contact Number.
- **Login Form:**
  - Fields: Email/Username, Password.

---

### Store & Inventory Management
- **Features:**
  - Manage products, SKUs, and categories.
  - Track inventory levels, reorder thresholds, and stock adjustments.
- **Data Entities:**
  - **Products Table:**
    - `ProductId` (GUID)
    - `ProductName` (string)
    - `SKU` (string)
    - `Category` (string)
    - `Price` (decimal)
    - `Description` (text)
  - **Inventory Table:**
    - `InventoryId` (GUID)
    - `ProductId` (Foreign Key)
    - `QuantityInStock` (integer)
    - `ReorderLevel` (integer)

---

### Sales Management
- **Features:**
  - Enable tracking and processing of customer orders.
  - Integration with inventory and invoicing.
- **Data Entities:**
  - **Sales Orders Table:**
    - `OrderId` (GUID)
    - `CustomerId` (Foreign Key)
    - `OrderDate` (Date)
    - `Status` (enum: Pending, Confirmed, Delivered, Cancelled)
    - `TotalAmount` (decimal)
  - **Order Items Table:**
    - `OrderItemId` (GUID)
    - `OrderId` (Foreign Key)
    - `ProductId` (Foreign Key)
    - `Quantity` (integer)
    - `UnitPrice` (decimal)

---

### Production Management
- **Features:**
  - Create and track work orders for production.
  - Schedule production runs and monitor progress.
- **Data Entities:**
  - **WorkOrders Table:**
    - `WorkOrderId` (GUID)
    - `ProductionLineId` (Foreign Key)
    - `ProductId` (Foreign Key)
    - `Quantity` (integer)
    - `ScheduledStartDate` (Date)
    - `ScheduledEndDate` (Date)
    - `Status` (enum: Scheduled, In-Progress, Completed)
  - **Production Schedule Table:**
    - `ScheduleId` (GUID)
    - `WorkOrderId` (Foreign Key)
    - `ProductionDate` (Date)
    - `Shift` (enum: Morning, Evening, Night)

---

### Order Management
- **Features:**
  - Consolidate sales and production orders for overall tracking.
  - Provide status updates and order histories.
- **Data Entities:**
  - **Orders Table:**
    - `OrderId` (GUID)
    - `CustomerId` (Foreign Key)
    - `SalesOrderId` (Foreign Key, if applicable)
    - `ProductionOrderId` (Foreign Key, if linked)
    - `CreatedDate` (Date)
    - `DeliveryDate` (Date)
    - `Status` (enum: New, Processing, Shipped, Delivered)
  - **Order Details Table:**
    - `OrderDetailId` (GUID)
    - `OrderId` (Foreign Key)
    - `ProductId` (Foreign Key)
    - `Quantity` (integer)
    - `Price` (decimal)

---

### Invoicing
- **Features:**
  - Generate detailed invoices based on orders.
  - Integrate invoicing with sales and accounting.
- **Data Entities:**
  - **Invoices Table:**
    - `InvoiceId` (GUID)
    - `OrderId` (Foreign Key)
    - `InvoiceDate` (Date)
    - `DueDate` (Date)
    - `Subtotal` (decimal)
    - `Taxes` (decimal)
    - `TotalAmount` (decimal)
  - **Invoice Items Table:**
    - `InvoiceItemId` (GUID)
    - `InvoiceId` (Foreign Key)
    - `Description` (text)
    - `Quantity` (integer)
    - `UnitPrice` (decimal)
    - `LineTotal` (decimal)

---

### HR and Payroll
- **Features:**
  - Manage employee information and maintain HR records.
  - Automate payroll processing.
- **Data Entities:**
  - **Employee Table:**
    - `EmployeeId` (GUID)
    - `FirstName` (string)
    - `LastName` (string)
    - `Email` (string)
    - `Role` (Foreign Key; may also relate to User Role)
    - `HireDate` (Date)
    - `Salary` (decimal)
  - **Payroll Table:**
    - `PayrollId` (GUID)
    - `EmployeeId` (Foreign Key)
    - `PayPeriodStart` (Date)
    - `PayPeriodEnd` (Date)
    - `GrossSalary` (decimal)
    - `Deductions` (decimal)
    - `NetSalary` (decimal)

---

### Accounting (100% Balanced Sheet)
- **Features:**
  - Full integration of accounting records ensuring the balance sheet remains 100% balanced.
  - Real-time posting of transactions across modules.
- **Data Entities:**
  - **Chart of Accounts:**
    - `AccountId` (GUID)
    - `AccountName` (string)
    - `AccountType` (enum: Asset, Liability, Equity, Revenue, Expense)
    - `ParentAccountId` (nullable)
    - `Description` (text)
  - **General Ledger (GL) Entries:**
    - `GLEntryId` (GUID)
    - `VoucherType` (enum: Journal, Payment, Receipt)
    - `Date` (Date)
    - `ReferenceNumber` (string)
    - `Description` (text)
  - **GL Entry Lines:**
    - `LineId` (GUID)
    - `GLEntryId` (Foreign Key)
    - `AccountId` (Foreign Key)
    - `Debit` (decimal)
    - `Credit` (decimal)
    - `Remarks` (text)
  - Additional tables for Accounts Payable/Receivable and Fixed Assets as needed.

---


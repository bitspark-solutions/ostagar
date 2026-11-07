'use client'

import React, { useState, useEffect } from 'react';
import {
  Box,
  Typography,
  Card,
  CardContent,
  Button,
  Alert,
  Paper,
  Chip,
  Stack,
  Breadcrumbs,
  Link
} from '@mui/material';
import {
  Cancel as CancelIcon,
  AccountBalance as AccountBalanceIcon,
  ArrowBack as ArrowBackIcon,
  Home as HomeIcon,
  AccountBalanceWallet as AccountBalanceWalletIcon
} from '@mui/icons-material';
import { useRouter } from 'next/navigation';
import { LocalizationProvider } from '@mui/x-date-pickers/LocalizationProvider';
import { AdapterDateFns } from '@mui/x-date-pickers/AdapterDateFns';
import { categoryService, CategoryType, Category as CategoryModel } from '@/services/categoryService';
import { TransactionList } from '@/components/accounting/TransactionList';
import { AddTransactionButtons } from '@/components/accounting/AddTransactionButtons';
import { CreditTransactionModal } from '@/components/accounting/CreditTransactionModal';
import { DebitTransactionModal } from '@/components/accounting/DebitTransactionModal';
import ServiceStatusBanner from '@/components/ui/ServiceStatusBanner';
import { cashBookService } from '@/services/cashBookService';
import { transactionService, SavedTransaction } from '@/services/transactionService';

interface CashBookEntry {
  id: string;
  transactionDate: Date;
  referenceNumber: string;
  description: string;
  creditTransactions: CreditTransaction[];
  debitTransactions: DebitTransaction[];
}

interface CreditTransaction {
  id: string;
  date: Date;
  categoryName: string;
  particulars: string;
  amount: number;
  contactName?: string;
}

interface DebitTransaction {
  id: string;
  date: Date;
  categoryName: string;
  supplierName?: string;
  buyerName?: string;
  particulars: string;
  amount: number;
}


interface Contact {
  id: string;
  name: string;
  contactType: 'Customer' | 'Supplier' | 'Both';
}



const SAMPLE_CONTACTS: Contact[] = [
  { id: '1', name: 'Urbo ltd', contactType: 'Customer' },
  { id: '2', name: 'Brooklyn', contactType: 'Customer' },
  { id: '3', name: 'Fabric Supplier Ltd', contactType: 'Supplier' },
  { id: '4', name: 'Machine Parts Co', contactType: 'Supplier' },
];

export default function CashBookEntryPage() {
  const router = useRouter();
  const [entry, setEntry] = useState<CashBookEntry>({
    id: '',
    transactionDate: new Date(),
    referenceNumber: '',
    description: '',
    creditTransactions: [],
    debitTransactions: []
  });


  const [creditCategories, setCreditCategories] = useState<CategoryModel[]>([]);
  const [debitCategories, setDebitCategories] = useState<CategoryModel[]>([]);
  const [contacts] = useState<Contact[]>(SAMPLE_CONTACTS);
  const [loading, setLoading] = useState(false);
  const [categoriesLoading, setCategoriesLoading] = useState(true);
  const [errors, setErrors] = useState<string[]>([]);
  const [successMessage, setSuccessMessage] = useState<string>('');
  const [savedTransactions, setSavedTransactions] = useState<SavedTransaction[]>([]);
  const [loadingSavedTransactions, setLoadingSavedTransactions] = useState(true);

  // Modal state management
  const [modals, setModals] = useState({
    creditModal: {
      isOpen: false,
      editingTransaction: undefined as CreditTransaction | undefined
    },
    debitModal: {
      isOpen: false,
      editingTransaction: undefined as DebitTransaction | undefined
    }
  });

  // Auto-generate reference number
  useEffect(() => {
    const today = new Date();
    const refNumber = `CB-${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}-${Date.now().toString().slice(-4)}`;
    setEntry(prev => ({ ...prev, referenceNumber: refNumber }));
  }, []);

  // Load categories from API
  useEffect(() => {
    const loadCategories = async () => {
      try {
        setCategoriesLoading(true);
        const categories = await categoryService.getAll();

        // Separate categories by type
        const credits = categories.filter(c => c.type === CategoryType.Credit && c.isActive);
        const debits = categories.filter(c => c.type === CategoryType.Debit && c.isActive);

        setCreditCategories(credits);
        setDebitCategories(debits);
      } catch (error) {
        console.error('Failed to load categories:', error);
        setErrors(prev => [...prev, 'Failed to load categories. Please refresh the page.']);
      } finally {
        setCategoriesLoading(false);
      }
    };

    loadCategories();
  }, []);

  // Load saved transactions from API
  useEffect(() => {
    const loadSavedTransactions = async () => {
      try {
        setLoadingSavedTransactions(true);
        const response = await transactionService.getRecentTransactions(20);
        if (response.success) {
          setSavedTransactions(response.transactions);
        }
      } catch (error) {
        console.error('Failed to load saved transactions:', error);
      } finally {
        setLoadingSavedTransactions(false);
      }
    };

    loadSavedTransactions();
  }, []);



  const removeCreditTransaction = (id: string) => {
    setEntry(prev => ({
      ...prev,
      creditTransactions: prev.creditTransactions.filter(t => t.id !== id)
    }));
  };

  const removeDebitTransaction = (id: string) => {
    setEntry(prev => ({
      ...prev,
      debitTransactions: prev.debitTransactions.filter(t => t.id !== id)
    }));
  };

  // Modal management functions
  const openCreditModal = (transaction?: CreditTransaction) => {
    setModals(prev => ({
      ...prev,
      creditModal: {
        isOpen: true,
        editingTransaction: transaction
      }
    }));
  };

  const closeCreditModal = () => {
    setModals(prev => ({
      ...prev,
      creditModal: {
        isOpen: false,
        editingTransaction: undefined
      }
    }));
  };

  const openDebitModal = (transaction?: DebitTransaction) => {
    setModals(prev => ({
      ...prev,
      debitModal: {
        isOpen: true,
        editingTransaction: transaction
      }
    }));
  };

  const closeDebitModal = () => {
    setModals(prev => ({
      ...prev,
      debitModal: {
        isOpen: false,
        editingTransaction: undefined
      }
    }));
  };

  const handleSaveCreditTransaction = async (transaction: CreditTransaction) => {
    setLoading(true);
    setErrors([]);
    setSuccessMessage('');

    try {
      // Save the credit transaction to the database
      const result = await cashBookService.saveCreditTransaction(transaction);

      if (!result.success) {
        setErrors([result.message]);
        return;
      }

      // Show success message
      setSuccessMessage(`✅ Credit transaction saved successfully! Amount: $${transaction.amount}`);

      if (modals.creditModal.editingTransaction) {
        // Update existing transaction in local state
        setEntry(prev => ({
          ...prev,
          creditTransactions: prev.creditTransactions.map(t =>
            t.id === transaction.id ? transaction : t
          )
        }));
      } else {
        // Add new transaction to local state
        const newTransaction = {
          ...transaction,
          id: Date.now().toString()
        };
        setEntry(prev => ({
          ...prev,
          creditTransactions: [...prev.creditTransactions, newTransaction]
        }));
      }

      closeCreditModal();
      
      // Refresh the saved transactions list
      await refreshSavedTransactions();
    } catch {
      setErrors(['Failed to save credit transaction. Please try again.']);
    } finally {
      setLoading(false);
    }
  };

  const handleSaveDebitTransaction = async (transaction: DebitTransaction) => {
    setLoading(true);
    setErrors([]);
    setSuccessMessage('');

    try {
      // Save the debit transaction to the database
      const result = await cashBookService.saveDebitTransaction(transaction);

      if (!result.success) {
        setErrors([result.message]);
        return;
      }

      // Show success message
      setSuccessMessage(`✅ Debit transaction saved successfully! Amount: $${transaction.amount}`);

      if (modals.debitModal.editingTransaction) {
        // Update existing transaction in local state
        setEntry(prev => ({
          ...prev,
          debitTransactions: prev.debitTransactions.map(t =>
            t.id === transaction.id ? transaction : t
          )
        }));
      } else {
        // Add new transaction to local state
        const newTransaction = {
          ...transaction,
          id: Date.now().toString()
        };
        setEntry(prev => ({
          ...prev,
          debitTransactions: [...prev.debitTransactions, newTransaction]
        }));
      }

      closeDebitModal();
      
      // Refresh the saved transactions list
      await refreshSavedTransactions();
    } catch {
      setErrors(['Failed to save debit transaction. Please try again.']);
    } finally {
      setLoading(false);
    }
  };


  const refreshSavedTransactions = async () => {
    try {
      const response = await transactionService.getRecentTransactions(20);
      if (response.success) {
        setSavedTransactions(response.transactions);
      }
    } catch (error) {
      console.error('Failed to refresh saved transactions:', error);
    }
  };

  // Removed validateEntry function as transactions are now saved independently without validation

  return (
    <LocalizationProvider dateAdapter={AdapterDateFns}>
      {/* Service Status Banner */}
      <ServiceStatusBanner
        showWhenHealthy={false}
        dismissible={true}
        showDetails={true}
        position="top"
      />

      <Box sx={{ p: 3, mt: 6 }}>
        {/* Navigation Breadcrumbs */}
        <Breadcrumbs sx={{ mb: 3 }}>
          <Link
            color="inherit"
            href="/"
            sx={{ display: 'flex', alignItems: 'center', textDecoration: 'none' }}
          >
            <HomeIcon sx={{ mr: 0.5 }} fontSize="inherit" />
            Home
          </Link>
          <Link
            color="inherit"
            href="/admin/accounting"
            sx={{ display: 'flex', alignItems: 'center', textDecoration: 'none' }}
          >
            <AccountBalanceIcon sx={{ mr: 0.5 }} fontSize="inherit" />
            Accounting
          </Link>
          <Typography color="text.primary" sx={{ display: 'flex', alignItems: 'center' }}>
            <AccountBalanceWalletIcon sx={{ mr: 0.5 }} fontSize="inherit" />
            Cash Book Entry
          </Typography>
        </Breadcrumbs>

        {/* Header with Navigation */}
        <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
          <Box>
            <Typography variant="h4" fontWeight="bold" gutterBottom>
              📚 Manual Cash Book Entry
            </Typography>
            <Typography variant="body1" color="text.secondary">
              Enter accounting transactions manually in the client cash book format
            </Typography>
          </Box>
          <Button
            variant="outlined"
            startIcon={<ArrowBackIcon />}
            onClick={() => router.push('/admin/accounting')}
            sx={{ minWidth: 120 }}
          >
            Back to Accounting
          </Button>
        </Box>

        {/* Success Message */}
        {successMessage && (
          <Alert severity="success" sx={{ mb: 3 }}>
            <Typography variant="body2">
              {successMessage}
            </Typography>
          </Alert>
        )}



        {/* Categories Loading State */}
        {categoriesLoading && (
          <Alert severity="info" sx={{ mb: 3 }}>
            <Typography variant="body2">
              Loading categories from the new category system...
            </Typography>
          </Alert>
        )}

        {/* Quick Help - Transaction Guide */}
        <Card sx={{ mb: 3, backgroundColor: 'info.light', color: 'info.dark' }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              💡 Cash Book Transaction Guide
            </Typography>
            <Typography variant="body2" sx={{ mb: 2 }}>
              <strong>Credit Transactions (Money In):</strong> Money received, income, sales revenue
            </Typography>
            <Typography variant="body2" sx={{ mb: 2 }}>
              <strong>Debit Transactions (Money Out):</strong> Money paid, expenses, purchases
            </Typography>
            <Typography variant="body2" sx={{ mb: 2, fontStyle: 'italic' }}>
              📝 Categories are managed through the dedicated Category Management system.
              Only active categories will appear in their respective transaction types.
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 'bold', color: 'success.main' }}>
              💾 Each transaction is saved independently - no balance validation required.
            </Typography>
          </CardContent>
        </Card>

        {/* Add Transaction Buttons */}
        <Box sx={{ mb: 3 }}>
          <AddTransactionButtons
            onAddCredit={() => openCreditModal()}
            onAddDebit={() => openDebitModal()}
            disabled={modals.creditModal.isOpen || modals.debitModal.isOpen}
          />
        </Box>

        {/* Transaction List */}
        <Box sx={{ mb: 3 }}>
          <TransactionList
            creditTransactions={entry.creditTransactions}
            debitTransactions={entry.debitTransactions}
            onEditCredit={openCreditModal}
            onEditDebit={openDebitModal}
            onDeleteCredit={removeCreditTransaction}
            onDeleteDebit={removeDebitTransaction}
            loading={false}
            error={undefined}
          />
        </Box>

        {/* Saved Transactions List */}
        <Card sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="h6" gutterBottom>
              💾 Recently Saved Transactions
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              These are the transactions you&apos;ve saved to the database. They persist across sessions.
            </Typography>
            
            {loadingSavedTransactions ? (
              <Typography variant="body2" color="text.secondary">
                Loading saved transactions...
              </Typography>
            ) : savedTransactions.length === 0 ? (
              <Typography variant="body2" color="text.secondary">
                No saved transactions yet. Add and save some transactions to see them here.
              </Typography>
            ) : (
              <Box sx={{ maxHeight: 400, overflowY: 'auto' }}>
                {savedTransactions.map((transaction) => (
                  <Paper 
                    key={transaction.id} 
                    sx={{ 
                      p: 2, 
                      mb: 1, 
                      backgroundColor: transaction.type === 'Credit' ? 'success.50' : 'error.50',
                      border: `1px solid ${transaction.type === 'Credit' ? 'success.200' : 'error.200'}`
                    }}
                  >
                    <Stack direction="row" justifyContent="space-between" alignItems="center">
                      <Box sx={{ flex: 1 }}>
                        <Stack direction="row" spacing={2} alignItems="center">
                          <Chip 
                            label={transaction.type}
                            color={transaction.type === 'Credit' ? 'success' : 'error'}
                            size="small"
                          />
                          <Typography variant="body2" fontWeight="bold">
                            {transaction.categoryName}
                          </Typography>
                          <Typography variant="body2">
                            {transaction.particulars}
                          </Typography>
                        </Stack>
                        <Stack direction="row" spacing={2} sx={{ mt: 1 }}>
                          <Typography variant="caption" color="text.secondary">
                            {new Date(transaction.date).toLocaleDateString()}
                          </Typography>
                          <Typography variant="caption" color="text.secondary">
                            Ref: {transaction.referenceNumber}
                          </Typography>
                          {transaction.contactName && (
                            <Typography variant="caption" color="text.secondary">
                              Contact: {transaction.contactName}
                            </Typography>
                          )}
                        </Stack>
                      </Box>
                      <Typography variant="h6" color={transaction.type === 'Credit' ? 'success.main' : 'error.main'}>
                        ৳{transaction.amount.toFixed(2)}
                      </Typography>
                    </Stack>
                  </Paper>
                ))}
              </Box>
            )}
          </CardContent>
        </Card>

        {/* Error Messages */}
        {errors.length > 0 && (
          <Alert severity="error" sx={{ mt: 2, mb: 3 }}>
            <Typography variant="subtitle2">Please fix the following errors:</Typography>
            <ul style={{ margin: '8px 0', paddingLeft: '20px' }}>
              {errors.map((error, index) => (
                <li key={index}>{error}</li>
              ))}
            </ul>
          </Alert>
        )}

        {/* Action Buttons */}
        <Box sx={{ display: 'flex', gap: 2, mt: 3, mb: 3 }}>
          <Button
            variant="outlined"
            startIcon={<CancelIcon />}
            onClick={() => window.location.reload()}
            disabled={loading}
            size="large"
          >
            Reset Form
          </Button>
          <Button
            variant="text"
            startIcon={<ArrowBackIcon />}
            onClick={() => router.push('/admin/accounting')}
            disabled={loading}
            size="large"
          >
            Back to Accounting
          </Button>
        </Box>

        {/* Credit Transaction Modal */}
        <CreditTransactionModal
          isOpen={modals.creditModal.isOpen}
          transaction={modals.creditModal.editingTransaction}
          categories={creditCategories}
          contacts={contacts}
          onSave={handleSaveCreditTransaction}
          onCancel={closeCreditModal}
        />

        {/* Debit Transaction Modal */}
        <DebitTransactionModal
          isOpen={modals.debitModal.isOpen}
          transaction={modals.debitModal.editingTransaction}
          categories={debitCategories}
          contacts={contacts}
          onSave={handleSaveDebitTransaction}
          onCancel={closeDebitModal}
        />
      </Box>
    </LocalizationProvider>
  );
}

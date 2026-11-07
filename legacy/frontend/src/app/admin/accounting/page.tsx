'use client'

import React from 'react';
import {
  Box,
  Typography,
  Card,
  CardContent,
  Button,
  Stack,
  Paper
} from '@mui/material';
import {
  AccountBalance,
  Upload,
  Assessment,
  Receipt,
  PieChart,
  TrendingUp,
  Category
} from '@mui/icons-material';
import { useRouter } from 'next/navigation';

export default function AccountingPage() {
  const router = useRouter();

  const accountingModules = [
    {
      title: 'Cash Book Entry',
      description: 'Manual data entry in the client cash book format',
      icon: <Upload />,
      path: '/admin/accounting/cash-book-entry',
      color: 'primary.main',
      bgColor: 'primary.light'
    },
    {
      title: 'Cash Book Import',
      description: 'Import the client cash book data from CSV files',
      icon: <Upload />,
      path: '/admin/accounting/cash-book-import',
      color: 'secondary.main',
      bgColor: 'secondary.light'
    },
    {
      title: 'Categories',
      description: 'Manage credit and debit categories for transactions',
      icon: <Category />,
      path: '/admin/accounting/categories',
      color: 'warning.main',
      bgColor: 'warning.light'
    },
    {
      title: 'Chart of Accounts',
      description: 'Create, edit, and manage your accounting categories',
      icon: <AccountBalance />,
      path: '/admin/accounting/chart-of-accounts',
      color: 'success.main',
      bgColor: 'success.light'
    },
    {
      title: 'Balance Overview',
      description: 'Real-time bank balance, cash on hand, and account balances',
      icon: <TrendingUp />,
      path: '/admin/accounting/balance',
      color: 'info.main',
      bgColor: 'info.light'
    },
    {
      title: 'Journal Entries',
      description: 'View and manage journal entries',
      icon: <Receipt />,
      path: '/admin/accounting/journal-entries',
      color: 'success.main',
      bgColor: 'success.light'
    },
    {
      title: 'Trial Balance',
      description: 'Generate trial balance reports to verify accounting accuracy',
      icon: <Assessment />,
      path: '/admin/accounting/trial-balance',
      color: 'error.main',
      bgColor: 'error.light'
    },
    {
      title: 'Financial Reports',
      description: 'Generate trial balance, P&L, and balance sheet',
      icon: <Assessment />,
      path: '/admin/accounting/reports',
      color: 'warning.main',
      bgColor: 'warning.light'
    },
    {
      title: 'Cash Flow',
      description: 'Monitor cash inflows and outflows',
      icon: <TrendingUp />,
      path: '/admin/accounting/cash-flow',
      color: 'info.main',
      bgColor: 'info.light'
    },
    {
      title: 'Account Analysis',
      description: 'Analyze account balances and trends',
      icon: <PieChart />,
      path: '/admin/accounting/analysis',
      color: 'error.main',
      bgColor: 'error.light'
    }
  ];

  return (
    <Box sx={{ p: 3 }}>
      <Typography variant="h4" fontWeight="bold" gutterBottom>
        Accounting Module
      </Typography>
      <Typography variant="body1" color="text.secondary" sx={{ mb: 4 }}>
        Manage your financial data, import transactions, and generate reports
      </Typography>

      {/* Quick Stats */}
      <Paper sx={{ p: 3, mb: 4, background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)', color: 'white' }}>
        <Typography variant="h6" gutterBottom>
          💼 Client Cash Book Integration
        </Typography>
        <Typography variant="body1">
          Create and manage accounting transactions manually in the familiar the client cash book format.
          Enter categories, transactions, suppliers, and buyers with automatic double-entry bookkeeping.
        </Typography>
      </Paper>

      {/* Accounting Modules Grid */}
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', md: '1fr 1fr 1fr' }, gap: 3 }}>
        {accountingModules.map((module, index) => (
          <Card
            key={index}
            sx={{
              height: '100%',
              cursor: 'pointer',
              transition: 'all 0.3s ease',
              '&:hover': {
                transform: 'translateY(-4px)',
                boxShadow: 3
              }
            }}
            onClick={() => router.push(module.path)}
          >
            <CardContent>
              <Box sx={{ display: 'flex', alignItems: 'center', mb: 2 }}>
                <Box
                  sx={{
                    p: 1.5,
                    borderRadius: 2,
                    backgroundColor: module.bgColor,
                    color: module.color,
                    mr: 2
                  }}
                >
                  {module.icon}
                </Box>
                <Typography variant="h6" fontWeight="600">
                  {module.title}
                </Typography>
              </Box>
              <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                {module.description}
              </Typography>
              <Button
                variant="outlined"
                size="small"
                sx={{ mt: 'auto' }}
                onClick={(e) => {
                  e.stopPropagation();
                  router.push(module.path);
                }}
              >
                Access Module
              </Button>
            </CardContent>
          </Card>
        ))}
      </Box>

      {/* Quick Actions */}
      <Card sx={{ mt: 4 }}>
        <CardContent>
          <Typography variant="h6" gutterBottom>
            Quick Actions
          </Typography>
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
            <Button
              variant="contained"
              startIcon={<Upload />}
              onClick={() => router.push('/admin/accounting/cash-book-entry')}
            >
              Manual Cash Book Entry
            </Button>
            <Button
              variant="outlined"
              startIcon={<Upload />}
              onClick={() => router.push('/admin/accounting/cash-book-import')}
            >
              Import CSV Data
            </Button>
            <Button
              variant="outlined"
              startIcon={<Assessment />}
              onClick={() => router.push('/admin/accounting/trial-balance')}
            >
              Trial Balance
            </Button>
            <Button
              variant="outlined"
              startIcon={<Assessment />}
              onClick={() => router.push('/admin/accounting/reports')}
            >
              Generate Reports
            </Button>
            <Button
              variant="outlined"
              startIcon={<Category />}
              onClick={() => router.push('/admin/accounting/categories')}
            >
              Manage Categories
            </Button>
            <Button
              variant="outlined"
              startIcon={<Receipt />}
              onClick={() => router.push('/admin/accounting/journal-entries')}
            >
              View Transactions
            </Button>
          </Stack>
        </CardContent>
      </Card>
    </Box>
  );
}

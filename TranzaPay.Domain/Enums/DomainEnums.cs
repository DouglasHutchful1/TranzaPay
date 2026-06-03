namespace TranzaPay.Domain.Enums;

public enum UserRole
{
    User,
    Admin
}

public enum WalletStatus
{
    Active,
    Frozen,
    Closed
}

public enum TransactionType
{
    Deposit,
    Withdrawal,
    Transfer
}

public enum TransactionStatus
{
    Pending,
    Completed,
    Failed,
    Rejected
}

public enum LedgerEntryType
{
    Debit,
    Credit
}

public enum WithdrawalStatus
{
    Pending,
    Approved,
    Rejected
}

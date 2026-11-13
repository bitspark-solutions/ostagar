namespace Ostagar.Data;

public static class BankingEnforcement
{
    public const string Up = """
        DO $$ DECLARE tab text; BEGIN
            FOREACH tab IN ARRAY ARRAY['bank_accounts', 'bank_reconciliations', 'bank_reconciliation_matches'] LOOP
                EXECUTE format('ALTER TABLE public.%I OWNER TO garments_owner', tab);
                EXECUTE format('GRANT SELECT, INSERT ON public.%I TO garments_app', tab);
                EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', tab);
                EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', tab);
                EXECUTE format('CREATE POLICY tenant_isolation ON public.%I FOR ALL TO garments_app USING (tenant_id = current_setting(''app.tenant_id'', true)::uuid) WITH CHECK (tenant_id = current_setting(''app.tenant_id'', true)::uuid)', tab);
            END LOOP;
        END $$;

        CREATE FUNCTION reject_bank_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'Bank history is immutable';
        END $$;
        CREATE TRIGGER bank_accounts_immutable BEFORE UPDATE OR DELETE ON bank_accounts
            FOR EACH ROW EXECUTE FUNCTION reject_bank_history_mutation();
        CREATE TRIGGER bank_reconciliations_immutable BEFORE UPDATE OR DELETE ON bank_reconciliations
            FOR EACH ROW EXECUTE FUNCTION reject_bank_history_mutation();
        CREATE TRIGGER bank_matches_immutable BEFORE UPDATE OR DELETE ON bank_reconciliation_matches
            FOR EACH ROW EXECUTE FUNCTION reject_bank_history_mutation();

        CREATE FUNCTION assert_bank_account() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM accounts WHERE id = NEW.ledger_account_id
                AND tenant_id = NEW.tenant_id AND sub_type = 'Bank' AND is_postable AND is_active FOR SHARE) THEN
                RAISE EXCEPTION 'Bank account requires an active postable bank GL account in its tenant';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER bank_account_valid BEFORE INSERT ON bank_accounts
            FOR EACH ROW EXECUTE FUNCTION assert_bank_account();

        CREATE FUNCTION protect_bank_classification() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF (NEW.sub_type IS DISTINCT FROM OLD.sub_type OR NEW.is_postable IS DISTINCT FROM OLD.is_postable
                OR NEW.tenant_id IS DISTINCT FROM OLD.tenant_id)
                AND EXISTS (SELECT 1 FROM bank_accounts WHERE ledger_account_id = OLD.id) THEN
                RAISE EXCEPTION 'Registered bank GL classification is immutable';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER bank_classification_fixed BEFORE UPDATE ON accounts
            FOR EACH ROW EXECUTE FUNCTION protect_bank_classification();

        CREATE FUNCTION assert_bank_statement_sequence() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE previous bank_reconciliations%ROWTYPE;
        BEGIN
            PERFORM pg_advisory_xact_lock(hashtextextended(NEW.bank_account_id::text, 0));
            PERFORM 1 FROM bank_accounts WHERE id = NEW.bank_account_id AND tenant_id = NEW.tenant_id;
            IF NOT FOUND THEN RAISE EXCEPTION 'Bank account not found'; END IF;
            IF NEW.actor_user_id = '00000000-0000-0000-0000-000000000000'::uuid THEN
                RAISE EXCEPTION 'Reconciliation requires an actor';
            END IF;
            SELECT * INTO previous FROM bank_reconciliations
                WHERE bank_account_id = NEW.bank_account_id AND tenant_id = NEW.tenant_id ORDER BY to_date DESC LIMIT 1;
            IF FOUND AND (NEW.from_date <> previous.to_date + 1 OR NEW.opening_balance <> previous.closing_balance) THEN
                RAISE EXCEPTION 'Statement must continue the previous date and closing balance';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER bank_statement_sequence BEFORE INSERT ON bank_reconciliations
            FOR EACH ROW EXECUTE FUNCTION assert_bank_statement_sequence();

        CREATE FUNCTION assert_bank_match() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM bank_reconciliations s
                JOIN bank_accounts b ON b.id = s.bank_account_id AND b.tenant_id = s.tenant_id
                JOIN gl_lines l ON l.id = NEW.gl_line_id AND l.tenant_id = s.tenant_id
                JOIN gl_entries e ON e.id = l.entry_id AND e.tenant_id = s.tenant_id
                WHERE s.id = NEW.reconciliation_id AND s.tenant_id = NEW.tenant_id
                  AND s.xmin = pg_current_xact_id()::xid
                  AND l.account_id = b.ledger_account_id AND l.currency = b.currency
                  AND e.status = 'Posted' AND e.posting_date <= s.to_date) THEN
                RAISE EXCEPTION 'Match requires a posted bank line and a statement created in this transaction';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER bank_match_valid BEFORE INSERT ON bank_reconciliation_matches
            FOR EACH ROW EXECUTE FUNCTION assert_bank_match();

        CREATE FUNCTION assert_bank_statement_balanced() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE movement numeric; statement bank_reconciliations%ROWTYPE;
        BEGIN
            IF TG_TABLE_NAME = 'bank_reconciliation_matches' THEN
                SELECT * INTO STRICT statement FROM bank_reconciliations
                    WHERE id = NEW.reconciliation_id AND tenant_id = NEW.tenant_id;
            ELSE
                statement := NEW;
            END IF;
            SELECT COALESCE(SUM(CASE WHEN l.debit IS NOT NULL THEN l.amount_in_currency ELSE -l.amount_in_currency END), 0)
                INTO movement FROM bank_reconciliation_matches m JOIN gl_lines l ON l.id = m.gl_line_id
                WHERE m.reconciliation_id = statement.id AND m.tenant_id = statement.tenant_id;
            IF statement.opening_balance + movement <> statement.closing_balance THEN
                RAISE EXCEPTION 'Bank statement does not tie exactly to matched GL lines';
            END IF;
            RETURN NEW;
        END $$;
        CREATE CONSTRAINT TRIGGER bank_statement_balanced AFTER INSERT ON bank_reconciliations
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION assert_bank_statement_balanced();
        CREATE CONSTRAINT TRIGGER bank_match_balanced AFTER INSERT ON bank_reconciliation_matches
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION assert_bank_statement_balanced();

        CREATE FUNCTION assert_bank_transfer() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW.source_type = 'bank.transfer' AND NEW.status = 'Posted' THEN
                IF NEW.journal_code <> 'BANK' OR NEW.source_id IS NULL
                    OR (SELECT COUNT(*) FROM gl_lines WHERE entry_id = NEW.id) <> 2
                    OR (SELECT COUNT(DISTINCT b.id) FROM gl_lines l JOIN bank_accounts b
                        ON b.ledger_account_id = l.account_id AND b.tenant_id = l.tenant_id AND b.currency = l.currency
                        WHERE l.entry_id = NEW.id AND l.tenant_id = NEW.tenant_id) <> 2
                    OR (SELECT COUNT(DISTINCT currency) FROM gl_lines WHERE entry_id = NEW.id) <> 1 THEN
                    RAISE EXCEPTION 'Bank transfer requires two distinct registered banks in one currency on BANK';
                END IF;
            END IF;
            RETURN NEW;
        END $$;
        CREATE CONSTRAINT TRIGGER bank_transfer_valid AFTER INSERT OR UPDATE ON gl_entries
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION assert_bank_transfer();

        CREATE FUNCTION assert_bank_line_currency() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF EXISTS (SELECT 1 FROM bank_accounts b
                WHERE b.ledger_account_id = NEW.account_id AND b.tenant_id = NEW.tenant_id
                  AND b.currency <> NEW.currency) THEN
                RAISE EXCEPTION 'Bank movements must be denominated in the registered bank currency';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER bank_line_currency BEFORE INSERT OR UPDATE ON gl_lines
            FOR EACH ROW EXECUTE FUNCTION assert_bank_line_currency();
        """;

    public const string Down = """
        DROP TRIGGER bank_line_currency ON gl_lines;
        DROP FUNCTION assert_bank_line_currency();
        DROP TRIGGER bank_transfer_valid ON gl_entries;
        DROP TRIGGER bank_classification_fixed ON accounts;
        DROP FUNCTION assert_bank_transfer();
        DROP FUNCTION protect_bank_classification();
        DROP TRIGGER bank_match_balanced ON bank_reconciliation_matches;
        DROP TRIGGER bank_statement_balanced ON bank_reconciliations;
        DROP TRIGGER bank_match_valid ON bank_reconciliation_matches;
        DROP TRIGGER bank_statement_sequence ON bank_reconciliations;
        DROP TRIGGER bank_account_valid ON bank_accounts;
        DROP TRIGGER bank_accounts_immutable ON bank_accounts;
        DROP TRIGGER bank_reconciliations_immutable ON bank_reconciliations;
        DROP TRIGGER bank_matches_immutable ON bank_reconciliation_matches;
        DROP FUNCTION assert_bank_statement_balanced();
        DROP FUNCTION assert_bank_match();
        DROP FUNCTION assert_bank_statement_sequence();
        DROP FUNCTION assert_bank_account();
        DROP FUNCTION reject_bank_history_mutation();
        """;
}

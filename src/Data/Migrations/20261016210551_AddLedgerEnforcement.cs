using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ostagar.Data.Migrations;

/// <summary>
/// Puts the ledger's accounting rules inside the database, and puts the five new tables behind the
/// same row-level security the rest of the schema already uses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the invariants are triggers and not service code.</b> A grep across the legacy project's 15
/// migration files found no check constraint and no trigger of any kind. Debits-equals-credits was a C#
/// comparison with a 0.01 tolerance, and the cash-book path wrote single-sided <c>Posted</c> rows that
/// bypassed even that — which is why its trial balance could never balance and why nobody could tell
/// which report was wrong. Anything the application is solely responsible for is something the next
/// code path forgets.
/// </para>
///
/// <para>
/// <b>The order a posting must be written in.</b> <c>reject_posted_line_mutation</c> covers insert,
/// update and delete, and reads the entry's status to decide whether a line may change, so posting is
/// necessarily two steps: write the entry and all of its lines while it is <c>Draft</c>, then flip the
/// status. A caller that saved an entry already marked <c>Posted</c> would be refused on its own first
/// line — loudly, in the same transaction, with the entry id in the message.
///
/// That ordering is not an inconvenience worked around by leaving inserts unprotected. Adding line
/// coverage was the point: without it, a balanced pair of lines smuggled into an existing posted entry
/// passed every other rule here and silently rewrote history with a plausible-looking audit trail.
/// </para>
///
/// <para>
/// <b>Why there is no <c>REVOKE UPDATE</c> here.</b> The plan for this phase assumed posted rows would be
/// protected by revoking update privileges. It cannot work, and pretending otherwise would be decoration:
/// Draft to Posted <i>is</i> an update on <c>gl_entries</c>, so revoking update revokes posting. Editing a
/// draft also updates and deletes <c>gl_lines</c>. The protection is therefore the BEFORE triggers below,
/// which distinguish the one permitted status transition from every other mutation.
/// </para>
///
/// <para>
/// <b>These functions are deliberately not <c>SECURITY DEFINER</c>.</b> They read <c>gl_entries</c> and
/// <c>gl_lines</c> while running as whichever role is posting, so row-level security still applies to
/// what they can see. Making them definer-owned would hand a postgres-owned bypass to every path that
/// writes a line — a way to read another company's ledger discovered by whoever added the clause, not by
/// a test.
/// </para>
/// </remarks>
public partial class AddLedgerEnforcement : Migration
{
    /// <summary>
    /// The ledger tables added by <c>AddLedgerSchema</c>. Every one carries <c>tenant_id</c>, so every one
    /// needs a policy: the assertion script in <c>infra/sql</c> counts the two and fails on a difference,
    /// which is how this list was found missing rather than noticed in production.
    /// </summary>
    private static readonly string[] LedgerTables =
    [
        "accounts", "fiscal_periods", "gl_entries", "gl_lines", "journals",
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tenancy first: an invariant that runs over rows it should never have seen is a way to leak them.
        foreach (var table in LedgerTables)
        {
            migrationBuilder.Sql($"""
                ALTER TABLE public."{table}" OWNER TO garments_owner;
                GRANT SELECT, INSERT, UPDATE, DELETE ON public."{table}" TO garments_app;
                ALTER TABLE public."{table}" ENABLE ROW LEVEL SECURITY;
                ALTER TABLE public."{table}" FORCE ROW LEVEL SECURITY;
                DROP POLICY IF EXISTS "tenant_isolation" ON public."{table}";
                CREATE POLICY "tenant_isolation" ON public."{table}"
                    AS PERMISSIVE
                    FOR ALL
                    TO garments_app
                    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
                    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);
                """);
        }

        // I1 — a Posted entry's lines balance, exactly, with no tolerance.
        //
        // Deferred to commit because a double-entry is not balanced until its last line exists: an
        // immediate row trigger would refuse the debit while it waited for a credit that was already in
        // the same statement. DEFERRABLE also lets a caller set the constraint immediate mid-transaction
        // to check its own work before saving.
        //
        // Both tables carry a trigger. A line changing can unbalance an entry, and so can an entry
        // becoming Posted around lines that were never touched — the draft-edited-then-posted path.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION assert_entry_balanced() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                target_entry_id uuid;
                total_debit numeric;
                total_credit numeric;
                entry_status text;
            BEGIN
                -- CASE, not COALESCE(NEW.col, OLD.col): in an AFTER DELETE row trigger NEW is not
                -- assigned at all, and naming one of its fields is an error rather than a null.
                IF TG_TABLE_NAME = 'gl_lines' THEN
                    target_entry_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.entry_id ELSE NEW.entry_id END;
                ELSE
                    target_entry_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.id ELSE NEW.id END;
                END IF;

                SELECT status INTO entry_status FROM gl_entries WHERE id = target_entry_id;

                -- A draft is allowed to be unfinished; that is the whole reason it is a draft.
                IF entry_status IS DISTINCT FROM 'Posted' THEN
                    RETURN NULL;
                END IF;

                SELECT COALESCE(SUM(debit), 0), COALESCE(SUM(credit), 0)
                    INTO total_debit, total_credit
                    FROM gl_lines
                    WHERE entry_id = target_entry_id;

                IF total_debit <> total_credit THEN
                    RAISE EXCEPTION
                        'Invariant I1: entry % is Posted but does not balance (% debit, % credit)',
                        target_entry_id, total_debit, total_credit;
                END IF;

                RETURN NULL;
            END
            $$;
            """);

        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS CK_gl_entry_balanced_on_line ON gl_lines;
            CREATE CONSTRAINT TRIGGER CK_gl_entry_balanced_on_line
                AFTER INSERT OR UPDATE OR DELETE ON gl_lines
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION assert_entry_balanced();

            DROP TRIGGER IF EXISTS CK_gl_entry_balanced_on_entry ON gl_entries;
            CREATE CONSTRAINT TRIGGER CK_gl_entry_balanced_on_entry
                AFTER INSERT OR UPDATE OF status ON gl_entries
                DEFERRABLE INITIALLY DEFERRED
                FOR EACH ROW EXECUTE FUNCTION assert_entry_balanced();
            """);

        // I3 — a Posted entry is immutable, and a correction is a new entry that points back.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION reject_posted_entry_mutation() RETURNS trigger
            LANGUAGE plpgsql AS $$
            BEGIN
                IF TG_OP = 'DELETE' THEN
                    IF OLD.status = 'Posted' THEN
                        RAISE EXCEPTION
                            'Invariant I3: entry % is Posted and cannot be deleted; reverse it instead',
                            OLD.id;
                    END IF;
                    RETURN OLD;
                END IF;

                IF OLD.status = 'Posted' THEN
                    RAISE EXCEPTION
                        'Invariant I3: entry % is Posted and cannot be changed; reverse it with a new entry',
                        OLD.id;
                END IF;

                RETURN NEW;
            END
            $$;

            CREATE OR REPLACE FUNCTION reject_posted_line_mutation() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                owning_entry_id uuid;
                entry_status text;
            BEGIN
                owning_entry_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.entry_id ELSE NEW.entry_id END;

                SELECT status INTO entry_status FROM gl_entries WHERE id = owning_entry_id;

                IF entry_status = 'Posted' THEN
                    RAISE EXCEPTION
                        'Invariant I3: line % belongs to Posted entry % and cannot be %',
                        CASE WHEN TG_OP = 'DELETE' THEN OLD.id ELSE NEW.id END,
                        owning_entry_id,
                        lower(TG_OP);
                END IF;

                RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
            END
            $$;
            """);

        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS CK_gl_entry_immutable_when_posted ON gl_entries;
            CREATE TRIGGER CK_gl_entry_immutable_when_posted
                BEFORE UPDATE OR DELETE ON gl_entries
                FOR EACH ROW EXECUTE FUNCTION reject_posted_entry_mutation();

            DROP TRIGGER IF EXISTS CK_gl_line_immutable_when_posted ON gl_lines;
            CREATE TRIGGER CK_gl_line_immutable_when_posted
                BEFORE INSERT OR UPDATE OR DELETE ON gl_lines
                FOR EACH ROW EXECUTE FUNCTION reject_posted_line_mutation();
            """);

        // I3, second half: a posted entry is corrected by a new entry that names it, and only once.
        //
        // The partial unique index is what makes "only once" a fact rather than a convention. Without
        // it, two clicks on a reversal button post two mirrors of one mistake, and the period then
        // reports a correction twice — while every entry involved is individually valid, balanced, and
        // would pass every other check in this file.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION assert_reversed_entry_is_posted() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                reversed_status text;
            BEGIN
                IF NEW.reverses_entry_id IS NULL THEN
                    RETURN NEW;
                END IF;

                SELECT status INTO reversed_status FROM gl_entries WHERE id = NEW.reverses_entry_id;

                IF NOT FOUND THEN
                    RAISE EXCEPTION
                        'Invariant I3: entry % claims to reverse entry %, which does not exist',
                        NEW.id, NEW.reverses_entry_id;
                END IF;

                IF reversed_status <> 'Posted' THEN
                    RAISE EXCEPTION
                        'Invariant I3: entry % reverses entry %, which is not posted. A draft is edited, not reversed.',
                        NEW.id, NEW.reverses_entry_id;
                END IF;

                RETURN NEW;
            END
            $$;

            DROP TRIGGER IF EXISTS CK_gl_entry_reversal_target_posted ON gl_entries;
            CREATE TRIGGER CK_gl_entry_reversal_target_posted
                BEFORE INSERT OR UPDATE OF reverses_entry_id ON gl_entries
                FOR EACH ROW EXECUTE FUNCTION assert_reversed_entry_is_posted();

            CREATE UNIQUE INDEX IF NOT EXISTS UQ_gl_entries_one_reversal
                ON gl_entries (tenant_id, reverses_entry_id)
                WHERE reverses_entry_id IS NOT NULL;
            """);

        // I6 — a locked period refuses a posting date, so closing a year cannot be reopened by a
        // back-dated entry typed in later. Three locks because they expire differently in practice:
        // management may lift a soft lock, an auditor's tax lock is not theirs to lift, and a hard lock
        // is the archive. All three are checked here rather than in a service that could be skipped.
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION reject_locked_period() RETURNS trigger
            LANGUAGE plpgsql AS $$
            DECLARE
                period_number integer;
                soft_locked date;
                hard_locked date;
                tax_locked date;
            BEGIN
                SELECT number, soft_locked_on, hard_locked_on, tax_locked_on
                    INTO period_number, soft_locked, hard_locked, tax_locked
                    FROM fiscal_periods
                    WHERE id = NEW.period_id;

                IF NOT FOUND THEN
                    RAISE EXCEPTION 'Invariant I6: entry references period % that does not exist', NEW.period_id;
                END IF;

                IF hard_locked IS NOT NULL AND NEW.posting_date <= hard_locked THEN
                    RAISE EXCEPTION
                        'Invariant I6: period % is hard locked through %, so % cannot be posted',
                        period_number, hard_locked, NEW.posting_date;
                END IF;

                IF tax_locked IS NOT NULL AND NEW.posting_date <= tax_locked THEN
                    RAISE EXCEPTION
                        'Invariant I6: period % is tax locked through %, so % cannot be posted',
                        period_number, tax_locked, NEW.posting_date;
                END IF;

                IF soft_locked IS NOT NULL AND NEW.posting_date <= soft_locked THEN
                    RAISE EXCEPTION
                        'Invariant I6: period % is soft locked through %, so % needs the lock lifted first',
                        period_number, soft_locked, NEW.posting_date;
                END IF;

                RETURN NEW;
            END
            $$;

            DROP TRIGGER IF EXISTS CK_gl_entry_period_open ON gl_entries;
            CREATE TRIGGER CK_gl_entry_period_open
                BEFORE INSERT OR UPDATE OF period_id, posting_date ON gl_entries
                FOR EACH ROW EXECUTE FUNCTION reject_locked_period();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS CK_gl_entry_period_open ON gl_entries;
            DROP TRIGGER IF EXISTS CK_gl_entry_reversal_target_posted ON gl_entries;
            DROP TRIGGER IF EXISTS CK_gl_line_immutable_when_posted ON gl_lines;
            DROP TRIGGER IF EXISTS CK_gl_entry_immutable_when_posted ON gl_entries;
            DROP TRIGGER IF EXISTS CK_gl_entry_balanced_on_entry ON gl_entries;
            DROP TRIGGER IF EXISTS CK_gl_entry_balanced_on_line ON gl_lines;

            DROP INDEX IF EXISTS "UQ_gl_entries_one_reversal";

            DROP FUNCTION IF EXISTS reject_locked_period();
            DROP FUNCTION IF EXISTS assert_reversed_entry_is_posted();
            DROP FUNCTION IF EXISTS reject_posted_line_mutation();
            DROP FUNCTION IF EXISTS reject_posted_entry_mutation();
            DROP FUNCTION IF EXISTS assert_entry_balanced();
            """);

        foreach (var table in LedgerTables)
        {
            migrationBuilder.Sql($"""
                DROP POLICY IF EXISTS "tenant_isolation" ON public."{table}";
                ALTER TABLE public."{table}" NO FORCE ROW LEVEL SECURITY;
                ALTER TABLE public."{table}" DISABLE ROW LEVEL SECURITY;
                """);
        }
    }
}

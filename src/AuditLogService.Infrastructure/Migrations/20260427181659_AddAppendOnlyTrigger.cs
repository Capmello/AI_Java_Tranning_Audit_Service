using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AuditLogService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAppendOnlyTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION audit_events_reject_update()
RETURNS trigger AS $$
BEGIN
    RAISE EXCEPTION 'audit_events is append-only; UPDATE is not permitted';
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER audit_events_no_update
BEFORE UPDATE ON audit_events
FOR EACH ROW
EXECUTE FUNCTION audit_events_reject_update();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_events_no_update ON audit_events;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit_events_reject_update();");
        }
    }
}

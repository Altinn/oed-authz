using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace oed_authz.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class Add_justification_column : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "justification",
                schema: "oedauthz",
                table: "roleassignments_log",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "justification",
                schema: "oedauthz",
                table: "roleassignments",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql("DO $$\r\n    BEGIN\r\n        IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'roleassignments_insert_log') THEN\r\n            DROP TRIGGER roleassignments_insert_log ON oedauthz.roleassignments;\r\n        END IF;\r\n\r\n        IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'roleassignments_delete_log') THEN\r\n            DROP TRIGGER roleassignments_delete_log ON oedauthz.roleassignments;\r\n        END IF;\r\n    END $$;\r\n\r\nCREATE OR REPLACE FUNCTION oedauthz.log_roleassignments_changes()\r\n    RETURNS TRIGGER AS $$\r\nBEGIN\r\n    IF TG_OP = 'INSERT' THEN\r\n        INSERT INTO oedauthz.roleassignments_log (\"estateSsn\", \"recipientSsn\", \"roleCode\", \"heirSsn\", \"action\", \"timestamp\", \"justification\")\r\n        VALUES (NEW.\"estateSsn\", NEW.\"recipientSsn\", NEW.\"roleCode\", NEW.\"heirSsn\", 'GRANT', NOW(), NEW.\"justification\");\r\n        RETURN NEW;\r\n    ELSIF TG_OP = 'DELETE' THEN\r\n        INSERT INTO oedauthz.roleassignments_log (\"estateSsn\", \"recipientSsn\", \"roleCode\", \"heirSsn\", \"action\", \"timestamp\", \"justification\")\r\n        VALUES (OLD.\"estateSsn\", OLD.\"recipientSsn\", OLD.\"roleCode\", OLD.\"heirSsn\", 'REVOKE', NOW(), OLD.\"justification\");\r\n        RETURN OLD;\r\n    END IF;\r\n    RETURN NULL;\r\nEND;\r\n$$ LANGUAGE plpgsql;\r\n\r\nCREATE TRIGGER roleassignments_insert_log\r\n    AFTER INSERT ON oedauthz.roleassignments\r\n    FOR EACH ROW\r\nEXECUTE FUNCTION oedauthz.log_roleassignments_changes();\r\n\r\nCREATE TRIGGER roleassignments_delete_log\r\n    AFTER DELETE ON oedauthz.roleassignments\r\n    FOR EACH ROW\r\nEXECUTE FUNCTION oedauthz.log_roleassignments_changes();");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$\r\n    BEGIN\r\n        IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'roleassignments_insert_log') THEN\r\n            DROP TRIGGER roleassignments_insert_log ON oedauthz.roleassignments;\r\n        END IF;\r\n\r\n        IF EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'roleassignments_delete_log') THEN\r\n            DROP TRIGGER roleassignments_delete_log ON oedauthz.roleassignments;\r\n        END IF;\r\n    END $$;\r\n\r\nCREATE OR REPLACE FUNCTION oedauthz.log_roleassignments_changes()\r\n    RETURNS TRIGGER AS $$\r\nBEGIN\r\n    IF TG_OP = 'INSERT' THEN\r\n        INSERT INTO oedauthz.roleassignments_log (\"estateSsn\", \"recipientSsn\", \"roleCode\", \"heirSsn\", \"action\", \"timestamp\")\r\n        VALUES (NEW.\"estateSsn\", NEW.\"recipientSsn\", NEW.\"roleCode\", NEW.\"heirSsn\", 'GRANT', NOW());\r\n        RETURN NEW;\r\n    ELSIF TG_OP = 'DELETE' THEN\r\n        INSERT INTO oedauthz.roleassignments_log (\"estateSsn\", \"recipientSsn\", \"roleCode\", \"heirSsn\", \"action\", \"timestamp\")\r\n        VALUES (OLD.\"estateSsn\", OLD.\"recipientSsn\", OLD.\"roleCode\", OLD.\"heirSsn\", 'REVOKE', NOW());\r\n        RETURN OLD;\r\n    END IF;\r\n    RETURN NULL;\r\nEND;\r\n$$ LANGUAGE plpgsql;\r\n\r\nCREATE TRIGGER roleassignments_insert_log\r\n    AFTER INSERT ON oedauthz.roleassignments\r\n    FOR EACH ROW\r\nEXECUTE FUNCTION oedauthz.log_roleassignments_changes();\r\n\r\nCREATE TRIGGER roleassignments_delete_log\r\n    AFTER DELETE ON oedauthz.roleassignments\r\n    FOR EACH ROW\r\nEXECUTE FUNCTION oedauthz.log_roleassignments_changes();");

            migrationBuilder.DropColumn(
                name: "justification",
                schema: "oedauthz",
                table: "roleassignments_log");

            migrationBuilder.DropColumn(
                name: "justification",
                schema: "oedauthz",
                table: "roleassignments");
        }
    }
}

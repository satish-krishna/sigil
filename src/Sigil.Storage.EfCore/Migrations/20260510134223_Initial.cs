using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sigil.Storage.EfCore.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_registrations",
                columns: table => new
                {
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    domain = table.Column<string>(type: "text", nullable: false),
                    endpoint_url = table.Column<string>(type: "text", nullable: false),
                    semantic_version = table.Column<string>(type: "text", nullable: false),
                    routing_weight = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    model_provider = table.Column<string>(type: "text", nullable: false),
                    model_name = table.Column<string>(type: "text", nullable: false),
                    model_temperature = table.Column<double>(type: "double precision", nullable: true),
                    model_top_p = table.Column<double>(type: "double precision", nullable: true),
                    model_max_output_tokens = table.Column<int>(type: "integer", nullable: true),
                    skills = table.Column<string>(type: "jsonb", nullable: false),
                    tools = table.Column<string>(type: "jsonb", nullable: false),
                    max_token_budget = table.Column<int>(type: "integer", nullable: true),
                    security_cert_thumbprint = table.Column<string>(type: "text", nullable: true),
                    security_sigil_key = table.Column<string>(type: "text", nullable: true),
                    security_is_pii_cleared = table.Column<bool>(type: "boolean", nullable: false),
                    security_allowed_tools = table.Column<string>(type: "jsonb", nullable: false),
                    security_tier = table.Column<string>(type: "text", nullable: false),
                    metadata_tags = table.Column<string>(type: "jsonb", nullable: false),
                    registered_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    last_heartbeat = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_registrations", x => x.agent_id);
                });

            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    audit_id = table.Column<string>(type: "text", nullable: false),
                    job_id = table.Column<string>(type: "text", nullable: false),
                    agent_id = table.Column<string>(type: "text", nullable: false),
                    step_id = table.Column<string>(type: "text", nullable: false),
                    delta_updates = table.Column<string>(type: "jsonb", nullable: false),
                    delta_removals = table.Column<string>(type: "jsonb", nullable: false),
                    metrics_prompt_tokens = table.Column<long>(type: "bigint", nullable: false),
                    metrics_completion_tokens = table.Column<long>(type: "bigint", nullable: false),
                    metrics_duration_ticks = table.Column<long>(type: "bigint", nullable: false),
                    metrics_custom = table.Column<string>(type: "jsonb", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.audit_id);
                });

            migrationBuilder.CreateTable(
                name: "checkpoints",
                columns: table => new
                {
                    checkpoint_id = table.Column<string>(type: "text", nullable: false),
                    job_id = table.Column<string>(type: "text", nullable: false),
                    step_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    resolved_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamptz", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.checkpoint_id);
                });

            migrationBuilder.CreateTable(
                name: "context_states",
                columns: table => new
                {
                    job_id = table.Column<string>(type: "text", nullable: false),
                    etag = table.Column<string>(type: "text", nullable: false),
                    state = table.Column<string>(type: "jsonb", nullable: false),
                    log = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_context_states", x => x.job_id);
                });

            migrationBuilder.CreateTable(
                name: "jobs",
                columns: table => new
                {
                    job_id = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_jobs", x => x.job_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agent_registrations_domain",
                table: "agent_registrations",
                column: "domain");

            migrationBuilder.CreateIndex(
                name: "ix_agent_registrations_skills_gin",
                table: "agent_registrations",
                column: "skills")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_agent_id",
                table: "audit_entries",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_job_id",
                table: "audit_entries",
                column: "job_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_timestamp",
                table: "audit_entries",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_checkpoints_job_id",
                table: "checkpoints",
                column: "job_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_registrations");

            migrationBuilder.DropTable(
                name: "audit_entries");

            migrationBuilder.DropTable(
                name: "checkpoints");

            migrationBuilder.DropTable(
                name: "context_states");

            migrationBuilder.DropTable(
                name: "jobs");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repositories.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class RestrictInternalRlsHelper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DO $security$
DECLARE role_name text;
BEGIN
  IF to_regprocedure('public.rls_auto_enable()') IS NOT NULL THEN
    REVOKE EXECUTE ON FUNCTION public.rls_auto_enable() FROM PUBLIC;
    FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
      IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
        EXECUTE format('REVOKE EXECUTE ON FUNCTION public.rls_auto_enable() FROM %I', role_name);
      END IF;
    END LOOP;
  END IF;
END $security$;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}

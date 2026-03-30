using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DAL.Migrations
{
    /// <summary>
    /// Align ChatMessages schema with the production database naming (snake_case)
    /// and ensure columns used by the app exist.
    /// </summary>
    public partial class AlignChatMessagesSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Supabase schema: message_id, sender_user_id, receiver_user_id, content, created_at, is_deleted
            // (không có is_read). Chỉ bổ sung cột nếu DB cũ thiếu.
            migrationBuilder.Sql("""
                ALTER TABLE "ChatMessages"
                ADD COLUMN IF NOT EXISTS "is_deleted" boolean NOT NULL DEFAULT false;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE "ChatMessages"
                ADD COLUMN IF NOT EXISTS "created_at" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Non-destructive: don't drop columns in Down.
        }
    }
}


using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DnDiscordAPI.Games.Database.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert Race from string to integer using enum mapping
            migrationBuilder.Sql(@"
                ALTER TABLE ""Characters""
                ALTER COLUMN ""Race"" TYPE integer
                USING CASE ""Race""
                    WHEN 'Humain' THEN 0
                    WHEN 'Elfe' THEN 1
                    WHEN 'Nain' THEN 2
                    WHEN 'Halfelin' THEN 3
                    WHEN 'DemiOrc' THEN 4
                    WHEN 'Tieffelin' THEN 5
                    WHEN 'Gnome' THEN 6
                    ELSE 0
                END;
            ");

            // Convert Class from string to integer using enum mapping
            migrationBuilder.Sql(@"
                ALTER TABLE ""Characters""
                ALTER COLUMN ""Class"" TYPE integer
                USING CASE ""Class""
                    WHEN 'Barbare' THEN 0
                    WHEN 'Barde' THEN 1
                    WHEN 'Clerc' THEN 2
                    WHEN 'Druide' THEN 3
                    WHEN 'Guerrier' THEN 4
                    WHEN 'Moine' THEN 5
                    WHEN 'Paladin' THEN 6
                    WHEN 'Rodeur' THEN 7
                    WHEN 'Voleur' THEN 8
                    WHEN 'Ensorceleur' THEN 9
                    WHEN 'Sorcier' THEN 10
                    WHEN 'Magicien' THEN 11
                    ELSE 0
                END;
            ");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert Class from integer back to string using enum mapping
            migrationBuilder.Sql(@"
                ALTER TABLE ""Characters""
                ALTER COLUMN ""Class"" TYPE character varying(50)
                USING CASE ""Class""
                    WHEN 0 THEN 'Barbare'
                    WHEN 1 THEN 'Barde'
                    WHEN 2 THEN 'Clerc'
                    WHEN 3 THEN 'Druide'
                    WHEN 4 THEN 'Guerrier'
                    WHEN 5 THEN 'Moine'
                    WHEN 6 THEN 'Paladin'
                    WHEN 7 THEN 'Rodeur'
                    WHEN 8 THEN 'Voleur'
                    WHEN 9 THEN 'Ensorceleur'
                    WHEN 10 THEN 'Sorcier'
                    WHEN 11 THEN 'Magicien'
                    ELSE 'Guerrier'
                END;
            ");

            // Convert Race from integer back to string using enum mapping
            migrationBuilder.Sql(@"
                ALTER TABLE ""Characters""
                ALTER COLUMN ""Race"" TYPE character varying(50)
                USING CASE ""Race""
                    WHEN 0 THEN 'Humain'
                    WHEN 1 THEN 'Elfe'
                    WHEN 2 THEN 'Nain'
                    WHEN 3 THEN 'Halfelin'
                    WHEN 4 THEN 'DemiOrc'
                    WHEN 5 THEN 'Tieffelin'
                    WHEN 6 THEN 'Gnome'
                    ELSE 'Humain'
                END;
            ");
        }
    }
}
